using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Brain;

// Parallel compute the neural activations for the next time step

[BurstCompile]
public struct ParallelNeuralUpdateCPU : IJobParallelFor
{

    [ReadOnly]
    // buffers holding current state
    public NativeArray<Neuron> current_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    [ReadOnly]
    public NativeArray<Synapse> current_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    // buffers holding next state
    [WriteOnly]
    [NativeDisableParallelForRestriction]
    public NativeArray<Neuron> next_state_neurons; // 1-to-1 mapping NeuronID --> neuron

    [NativeDisableParallelForRestriction]
    public NativeArray<Synapse> next_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    const bool CONSTRAIN_WEIGHT = false;
    //static double2 CONSTRAIN_WEIGHT_RANGE = (GlobalConfig.USE_HEBBIAN && GlobalConfig.HEBBIAN_METHOD == GlobalConfig.NeuralLearningMethod.HebbYaeger) ? new double2(0, 50) : new double2(-50, 50);

    const bool CONSTRAIN_BIAS = false;
    static double2 CONSTRAIN_BIAS_RANGE = new double2(-10, 10);

    public bool use_hebb;
    public GlobalConfig.NeuralLearningMethod hebb_rule;
    public double brain_update_period;

    public double time;

    public void Execute(int i)
    {
        // set the neuron data
        this.next_state_neurons[i] = this.CalculateNeuronActivation(i,
            this.current_state_neurons, 
            this.current_state_synapses,
            this.next_state_synapses);
    
    }

    public Neuron CalculateNeuronActivation(int i,
        NativeArray<Neuron> current_state_neurons,
        NativeArray<Synapse> current_state_synapses,
        NativeArray<Synapse> next_state_synapses)
    {
        bool update_synapses = use_hebb;

        if (current_state_synapses == next_state_synapses && update_synapses)
        {
            Debug.LogError("Error: can't update synapses at the same timestep as using them.");
        }

        Neuron to_neuron = current_state_neurons[i];
        bool is_sensory_neuron = to_neuron.IsSensory();
        if (is_sensory_neuron)
        {
            return to_neuron;
        }


        to_neuron.real_num_of_synapses = 0; //for metadata

        // sum inputs to the neuron
        
        int start_idx = to_neuron.synapse_start_idx;
        int end_idx = (to_neuron.synapse_start_idx + to_neuron.synapse_count);
        double sum = 0; // only bias motor and hidden units (todo - is this right? or include sensors?)

        for (int j = start_idx; j < end_idx; j++)
        {
            Synapse connection = current_state_synapses[j];
            if (!connection.IsEnabled()) continue;
            int from_idx = connection.from_neuron_idx;
            Neuron from_neuron = current_state_neurons[from_idx];

            double input = connection.weight * from_neuron.activation;
 
            sum += input;
            
            to_neuron.real_num_of_synapses++;
        }

        double to_neuron_new_activation;


        if (to_neuron.neuron_class == Neuron.NeuronClass.CTRNN)
        {
            double voltage_change = -to_neuron.voltage;
            voltage_change += sum;
            var delta = (voltage_change * brain_update_period / to_neuron.tau_time_constant);
            to_neuron.voltage += delta;
            to_neuron.voltage = math.clamp(to_neuron.voltage, -1000000, 1000000);
            sum = to_neuron.voltage;
        }


        sum += to_neuron.bias;

        if (to_neuron.neuron_class == Neuron.NeuronClass.CTRNN) sum *= to_neuron.gain;

        double net_input = sum;
        if (to_neuron.neuron_role == Neuron.NeuronRole.Hidden)
        {
            // 1. Unpack state & parameters
            double r = to_neuron.r;
            double theta = to_neuron.theta;
            double mu = to_neuron.mu;
            double omega = to_neuron.w;
            double K = to_neuron.K;
            double pGain = to_neuron.p_gain;
            double blend_alpha = to_neuron.r_gain;
            double oscGain = to_neuron.osc_inject_gain;
            double maxInp = to_neuron.max_input;
            double phaseOffset = to_neuron.phase_offset;

            // 2. Normalize sensor drive once (or move inside loop if 'sum' changes)
            double safeMaxInp = math.max(1e-6, maxInp);
            double u = math.clamp(sum / safeMaxInp, -1.0, 1.0);


            // 3. Determine micro‑step size
            const double maxDt = 0.02;  // 20 ms “safe” sub‐step
            int nSteps = math.max(1, (int)math.ceil(brain_update_period / maxDt));
            double dt = brain_update_period / nSteps;

            for (int substep = 0; substep < nSteps; substep++)
            {
                // 1. Compute derivatives at current state
                double dr1 = mu * (1.0 - r * r) * r + K * u;
                double dth1 = omega + pGain * u;

                // 2. Estimate midpoint state (RK2)
                double rMid = r + dr1 * dt * 0.5;
                double thetaMid = theta + dth1 * dt * 0.5;

                // 3. Compute derivatives at midpoint
                double dr2 = mu * (1.0 - rMid * rMid) * rMid + K * u;
                double dth2 = omega + pGain * u;

                // 4. Update r and theta with RK2 step
                r += dr2 * dt;
                r = math.max(0.0, r); // clamp non-negative as before

                theta += dth2 * dt;

                // 5. Wrap theta to [0, 2π)
                theta = math.fmod(theta, math.PI * 2);
                if (theta < 0.0) theta += math.PI * 2;

                // (Optional) If u changes during substeps, recalc here before next iteration
            }

            // 5. Compute oscillator output & mix
            double oscillator = r * math.sin(theta + phaseOffset);
            net_input = blend_alpha * sum
                              + (1.0 - blend_alpha) * (oscGain * oscillator);

            // 6. Save state
            to_neuron.r = r;
            to_neuron.theta = theta;
        }

        // 9. Activation
        double activation = to_neuron.RunActivationFunction(net_input);
        to_neuron_new_activation = activation;
        to_neuron.activation = activation;

        if (update_synapses)
        {
            // update synapse
            for (int j = start_idx; j < end_idx; j++)
            {
                Synapse connection = current_state_synapses[j];
                if (!connection.IsEnabled()) continue;
                Synapse new_connection = HebbianUpdateSynapse(connection, sum, current_state_neurons, to_neuron_new_activation); // set the new synapse
                next_state_synapses[j] = new_connection;

            }


            if (use_hebb && hebb_rule == GlobalConfig.NeuralLearningMethod.HebbYaeger)
            {
                //update bias
                to_neuron.bias += to_neuron.learning_rate_r_bias * (to_neuron_new_activation - 0.5f) * (1 - 0.5f); // bias activation is always considered "1"
     

                if (CONSTRAIN_BIAS)
                {
                    // constrain weight in [-range,range]
                    if (to_neuron.bias > CONSTRAIN_BIAS_RANGE.y)
                    {
                        to_neuron.bias = CONSTRAIN_BIAS_RANGE.y;
                    }
                    else if (to_neuron.bias < CONSTRAIN_BIAS_RANGE.x)
                    {
                        to_neuron.bias = CONSTRAIN_BIAS_RANGE.x;
                    }
                }

            }
        }

        return to_neuron;
    }


    public Synapse HebbianUpdateSynapse(Synapse connection,
        double sum,
        NativeArray<Neuron> current_state_neurons,
        double to_neuron_next_activation)
    {
        if (connection.IsEnabled())
        {
            int to_idx = connection.to_neuron_idx;
            int from_idx = connection.from_neuron_idx;
            Neuron to_neuron_current_state = current_state_neurons[to_idx];
            Neuron from_neuron_current_state = current_state_neurons[from_idx];

            double presynaptic_firing = from_neuron_current_state.activation;
            
            double delta_weight;

            if (hebb_rule == GlobalConfig.NeuralLearningMethod.HebbABCD)
            {
                double postsynaptic_firing = to_neuron_current_state.activation;
                delta_weight = connection.coefficient_LR * (connection.coefficient_A * presynaptic_firing * postsynaptic_firing
                    + connection.coefficient_B * presynaptic_firing
                    + connection.coefficient_C * postsynaptic_firing
                    + connection.coefficient_D);
            }
            else if (hebb_rule == GlobalConfig.NeuralLearningMethod.HebbYaeger)
            {
                Debug.LogError("");
                delta_weight = 0;
            }
            else
            {
                delta_weight = 0;
                Debug.LogError("error");
                return connection;
            }

            connection.weight += delta_weight;
            //if (GlobalConfig.HEBBIAN_METHOD == GlobalConfig.HebbianMethod.Yaeger) connection.weight *= connection.decay_rate;

            /*     if (!double.IsFinite(connection.weight))
                 {
                     connection.weight = 0;
                 }*/



        }

        if (CONSTRAIN_WEIGHT)
        {
            // constrain weight in [-range,range]
        //    if (connection.weight > CONSTRAIN_WEIGHT_RANGE.y)
        //    {
        //        connection.weight = CONSTRAIN_WEIGHT_RANGE.y;
        //    }
        //    else if (connection.weight < CONSTRAIN_WEIGHT_RANGE.x)
        //    {
        //        connection.weight = CONSTRAIN_WEIGHT_RANGE.x;
        //    }
        }

        return connection;
    }


}
