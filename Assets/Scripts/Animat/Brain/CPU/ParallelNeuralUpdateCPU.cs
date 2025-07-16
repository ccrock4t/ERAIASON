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

        // 1. Unpack state & parameters
        double r = to_neuron.r;       // radius
        double theta = to_neuron.p;       // phase

        double mu = to_neuron.mu;         // amplitude convergence rate
        double omega = to_neuron.w;          // base angular frequency
        double K = to_neuron.K;          // sensor → amplitude coupling
        double pGain = to_neuron.p_gain;     // sensor → phase coupling
        double alpha = to_neuron.r_gain;     // static/osc mix (0→osc, 1→static)
        double oscGain = to_neuron.osc_inject_gain;
        double maxInp = to_neuron.max_input;

        // 2. Normalize sensor drive
        double u = math.clamp(sum / maxInp, -1.0, 1.0);

        // 3. Compute derivatives at current state
        double dr1 = mu * (1.0 - r * r) * r + K * u;
        double dth1 = omega + pGain * u;

        // 4. Semi‑implicit update for r
        //    r_new = (r + dr1*dt) / (1 + mu*r*r*dt);
        r = (r + dr1 * brain_update_period)
            / (1.0 + mu * r * r * brain_update_period);

        // 5. Explicit Euler for theta (with wrapping)
        theta += dth1 * brain_update_period;
        if (theta < 0.0) theta += math.PI2;
        else if (theta >= math.PI2) theta -= math.PI2;

        // 6. Compute oscillator output
        double oscillator = r * math.sin(theta);

        // 7. Mix static vs. oscillatory contributions
        //    alpha=1 → pure static(sum), alpha=0 → pure osc
        double net_input = alpha * sum
                         + (1.0 - alpha) * (oscGain * oscillator);

        // 8. Save state
        to_neuron.r = r;
        to_neuron.p = theta;

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
