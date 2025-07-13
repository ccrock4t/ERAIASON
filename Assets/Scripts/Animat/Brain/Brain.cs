using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class Brain : Mind
{
    public const string save_file_extension = ".Brain";
    public Dictionary<NeuronID, int> nodeID_to_idx;
    public List<int> motor_neuron_indices = new(); // 1-to-1 mapping NeuronID --> neuron

    public struct MultiLayerNetworkInfo
    {
        public int input_layer_size;
        public int hidden_layer_size;
        public int output_layer_size;
        public int num_of_hidden_layers;

        public MultiLayerNetworkInfo(int input_layer_size, int hidden_layer_size, int output_layer_size, int num_of_hidden_layers)
        {
            this.input_layer_size = input_layer_size;
            this.hidden_layer_size = hidden_layer_size;
            this.output_layer_size = output_layer_size;
            this.num_of_hidden_layers = num_of_hidden_layers;
        }

        // number of neurons in the whole network
        public int GetNumOfNeurons()
        {
            return this.input_layer_size + this.hidden_layer_size * this.num_of_hidden_layers + this.output_layer_size;
        }

        public int GetNumOfSynapses()
        {
            int num_synapses = 0;
            num_synapses += (this.input_layer_size * this.hidden_layer_size); // between input and first hidden layer
            num_synapses += (this.hidden_layer_size * this.hidden_layer_size) * (this.num_of_hidden_layers - 1); // between hidden layers
            num_synapses += (this.output_layer_size * this.hidden_layer_size); // between last hidden layer and the output
            return num_synapses;
        }

        public int GetNumOfLayers()
        {
            return this.num_of_hidden_layers + 2;
        }

        public int GetFirstInputNeuronIdx()
        {
            return 0;
        }
        public int GetFirstOutputNeuronIdx()
        {
            return this.input_layer_size;
        }

        public int GetFirstHiddenNeuronIdx()
        {
            return this.input_layer_size + this.output_layer_size;
        }

        public int GetNumOfInputToHiddenSynapses()
        {
            return this.input_layer_size * this.hidden_layer_size;
        }

        public int GetNumOfHiddenToOutputSynapses()
        {
            return this.hidden_layer_size * this.output_layer_size;
        }
    }

    public Brain()
    {
    }



    public abstract void DisposeOfNativeCollections();

    public abstract Neuron GetNeuronCurrentState(int index);
    public abstract void SetNeuronCurrentState(int index, Neuron neuron);

    public abstract int GetNumberOfNeurons();

    public abstract int GetNumberOfSynapses();




    [System.Serializable]
    public struct Synapse
    {

        public double weight; // the activation value multiplier


        // evolvable parameters

        // Hebbian ABCD
      
        public double coefficient_LR;  // Learning rate
        public double coefficient_A;  // correlated activation coefficient
        public double coefficient_B;  // pre-synaptic activation coefficient
        public double coefficient_C; // post-synaptic activation coefficient
        public double coefficient_D; // a connection bias

        // Yaeger
        public double learning_rate_r_e_e;  // the learning rate
        public double learning_rate_r_i_i;  // the learning rate
        public double learning_rate_r_e_i;  // the learning rate
        public double learning_rate_r_i_e;  // the learning rate


        //decay
        public double decay_rate; 

        // info
        public int from_neuron_idx; // neuron index this connection is coming from
        public int to_neuron_idx; // neuron index this connection is coming from
        public int enabled;

        public Synapse(int enabled)
        {
            this.learning_rate_r_e_e = 0;
            this.learning_rate_r_e_i = 0;
            this.learning_rate_r_i_e = 0;
            this.learning_rate_r_i_i = 0;
            this.coefficient_A = 0;
            this.coefficient_B = 0;
            this.coefficient_C = 0;
            this.coefficient_D =0;
            this.coefficient_LR = 0;
            this.weight = 0;
            this.enabled = enabled;
            this.from_neuron_idx = 0;
            this.to_neuron_idx = 0;
            this.decay_rate = 1;
        }

        public static Synapse GetDefault()
        {
            return new Synapse(1);
        }

        public bool IsEnabled()
        {
            return this.enabled != 0;
        }
    }

    [System.Serializable]
    public struct NeuronID
    {
        public int4 coords;
        public Neuron.NeuronRole neuron_role;

        public NeuronID(int4 coords, Neuron.NeuronRole neuron_role)
        {
            this.coords = coords;
            this.neuron_role = neuron_role;
        }

        public static bool operator ==(NeuronID obj1, NeuronID obj2)
        {
            return math.all(obj1.coords == obj2.coords)
                && obj1.neuron_role == obj2.neuron_role;
        }

        public static bool operator !=(NeuronID obj1, NeuronID obj2)
        {
            return !(obj1 == obj2);
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType()) return false;
            return this == (NeuronID)obj;
        }
    }

    [System.Serializable]
    public struct Neuron
    {

        public enum NeuronClass : int
        {
            SumAndSquash,
            CTRNN,
            CPG
        }

        public enum NeuronRole : int
        {
            Hidden,
            Sensor,
            Motor
        }

        public enum ActivationFunction : int
        {
            Linear,
            Sigmoid,
            Tanh,
            LeakyReLU,
            ReLU,
            Step,
            Swish,
            ELU
        }


        // === static
        public ActivationFunction activation_function;
        public NeuronRole neuron_role; // 0 no, 1 sensor, 2 motor
        public NeuronClass neuron_class;

        public double learning_rate_r_bias;  // the learning rate
        public int excitatory; 

        // === dynamic

        // perceptron
        public double activation; //sigmoid output

        // CTRNN
        public double voltage;
        public double gain;
        public double tau_time_constant;

        //CPG
        public double r; // mix ratio
        public double w; // wave frequency
        public double p; // wave phase offset

        public double r_gain; // mix ratio
        public double w_gain; // wave frequency
        public double p_gain; // wave phase offset

        // === misc parameters
        public double bias;  // bias
        public double sigmoid_alpha; // larger alpha = steeper slope, easier to activate --- smaller alpha = gradual slope, harder to activate.
        public double sigmoid_alpha2; // larger alpha = steeper slope, easier to activate --- smaller alpha = gradual slope, harder to activate.

        // metadata
        public int synapse_start_idx;
        public int synapse_count;
        public int5 position_idxs;

        public int real_num_of_synapses;
        public float5 position_normalized;

        public NeuronID ID;
        public int idx;

       

        public static Neuron GetNewNeuron()
        {
            Neuron neuron = new();
            neuron.activation_function = ActivationFunction.Sigmoid;
            neuron.neuron_class = GlobalConfig.NEURAL_NETWORK_METHOD;

            neuron.bias = 0;
            neuron.voltage = 0;
            neuron.gain = 0;
            neuron.tau_time_constant = 0;
            neuron.activation = 0.0f;
            neuron.neuron_role = NeuronRole.Hidden;

            neuron.synapse_count = 0;
            neuron.synapse_start_idx = -1;
            neuron.position_idxs = new(int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue);
            neuron.position_normalized = new(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);

            neuron.real_num_of_synapses = 0;
            neuron.excitatory = 1;
            neuron.sigmoid_alpha = 1;
            neuron.sigmoid_alpha2 = 1;
            neuron.learning_rate_r_bias = 1;  // the bias learning rate 
            return neuron;
        }

        public static ActivationFunction GetRandomActivationFunction()
        {
            Array values = Enum.GetValues(typeof(ActivationFunction));
            return (ActivationFunction)values.GetValue(UnityEngine.Random.Range(0,values.Length));
        }

        public bool IsSensory()
        {
            return this.neuron_role == NeuronRole.Sensor;
        }

        public double RunActivationFunction(double sum)
        {
            double result;
            if (this.activation_function == Neuron.ActivationFunction.Linear)
            {
                result = this.LinearSum(sum);
            }
            else if (this.activation_function == Neuron.ActivationFunction.Sigmoid)
            {
                result = this.SigmoidSquash(sum);
            }
            else if (this.activation_function == Neuron.ActivationFunction.Tanh)
            {
                result = this.TanhSquash(sum);
            }
            else if (this.activation_function == Neuron.ActivationFunction.LeakyReLU)
            {
                result = this.LeakyReLU(sum);
            }
            else if (this.activation_function == Neuron.ActivationFunction.ReLU)
            {
                result = this.ReLU(sum);
            }
            else if (this.activation_function == Neuron.ActivationFunction.Step)
            {
                result = this.Step(sum);
            }
            else if (this.activation_function == Neuron.ActivationFunction.ELU)
            {
                result = this.ELU(sum);
            }
            else if (this.activation_function == Neuron.ActivationFunction.Swish)
            {
                result = this.Swish(sum);
            }
            else
            {
                Debug.LogError("error didn't recognize activation function");
                return double.NaN;
            }
            return result;
        }

        public double LinearSum(double sum)
        {
            return this.sigmoid_alpha*sum;
        }

        /// <summary>
        ///         Perceptron squash with a sigmoid function
        /// </summary>
        /// <param name="sum"></param>
        /// <returns></returns>
        public double SigmoidSquash(double sum)
        {
            return 1.0f / (1.0f + math.exp(-sigmoid_alpha * sum));
        }

        public double TanhSquash(double sum)
        {
            return math.tanh(sigmoid_alpha * sum);
        }

        public double ReLU(double sum)
        {
            return math.max(0, sigmoid_alpha * sum);
        }

        public double LeakyReLU(double sum)
        {
            if(sum < 0)
            {
                return this.sigmoid_alpha2 * sum;
            }
            else
            {
                return this.sigmoid_alpha*sum;
            }
            
        }

        public double Step(double sum)
        {
            if (sum <= 0)
            {
                return 0;
            }
            else
            {
                return 1;
            }

        }

        public double Swish(double sum)
        {
            double sigmoid = 1.0f / (1.0f + math.exp(-this.sigmoid_alpha*sum));
            return sum * sigmoid;

        }

        public double ELU(double sum)
        {
            if (sum > 0)
            {
                return sum;
            }
            else
            {
                return this.sigmoid_alpha*(math.exp(sum)-1);
            }

        }


    }


/*        public class DevelopmentSynapse
    {
        public float learning_rate;
        public float[] coefficients;

        public DevelopmentSynapse(float[] coefficients = null,
            float learning_rate = 1.0f)
        {
            if (coefficients == null)
            {
                this.coefficients = new float[]
                {
                    0,
                    0,
                    0,
                    0
                };
            }
            else
            {
                this.coefficients = coefficients;
            }
            
            this.learning_rate = learning_rate;
        }


    }


    public class DevelopmentNeuron : DataElement
    {

        public int threshold;
        public float bias;
        public bool sign; // sign of outputs
        public float adaptation_delta;
        public float decay;
        public float sigmoid_alpha; // larger alpha = steeper slope, easier to activate --- smaller alpha = gradual slope, harder to activate.

        public string extradata = "";

        public DevelopmentNeuron(int threshold,
            float bias,
            bool sign,
            float adaptation_delta,
            float decay,
            float sigmoid_alpha)
        {
            this.threshold = threshold;
            this.bias = bias;
            this.adaptation_delta = adaptation_delta;
            this.decay = decay;
            this.sign = sign;
            this.sigmoid_alpha = sigmoid_alpha;
        }

        public virtual DevelopmentNeuron Clone()
        {
            DevelopmentNeuron clone = new(this.threshold,
                this.bias,
                this.sign,
                this.adaptation_delta,
                this.decay,
                this.sigmoid_alpha);

            clone.extradata = extradata;
            return clone;
        }


    }*/

}
