struct int5
{
    int x;
    int y;
    int z;
    int w;
    int v;
};
struct float5
{
    float x;
    float y;
    float z;
    float w;
    float v;
};
    
struct NeuronID
{
    int4 coords;
    int neuron_role;
};
    
// keep this up to date with Brain.Neuron
struct Neuron
{
     // === static
    int activation_function;
    int neuron_role; // 0 no, 1 sensor, 2 motor
    int neuron_class; // what kind of neuron?
    
    float learning_rate_r_bias; // the learning rate
    int excitatory;
    
    // === dynamic
    
    // perceptron
    float activation;
    
    // CTRNN
    float voltage;
    float gain;  
    float tau_time_constant;

    //CPG
    float r;
    float w;
    float p;

     // === misc parameters
    float bias;
    float sigmoid_alpha;
    
    // metadata
    int synapse_start_idx;
    int synapse_count;
    int5 position_idxs;
    
    int real_num_of_synapses;
    float5 position_normalized;
    
    NeuronID ID;
    int idx;
    
    
    float Linear(float sum)
    {
        return sum;
    }
    
    float SigmoidSquashSum(float sum)
    {
        return 1.0f / (1.0f + exp(-sigmoid_alpha * sum));
    }

    float TanhSquashSum(float sum)
    {
        return tanh(sigmoid_alpha*sum);
    }

    float ReLUSum(float sum)
    {
        return max(0, sigmoid_alpha * sum);
    }

    float LeakyReLUSum(float sum)
    {
        if(sum < 0)
        {
            return sigmoid_alpha * sum;
        }
        else
        {
            return sigmoid_alpha * sum;
        }
            
    }
            
    float Step(float sum)
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
};

// keep this up to date with Brain.Synapse
struct Synapse
{
    float weight;
    float learning_rate_r; 
    float coefficient_A;
    float coefficient_B;
    float coefficient_C;
    float coefficient_D;
    int from_neuron_idx;
    int to_neuron_idx;
    int enabled;
};

bool IsNaN(float x)
{
  return !(x < 0.0f || x > 0.0f || x == 0.0f);
}

// keep this up to date
#define NEURON_CLASS_SUMANDSQUASH 0
#define NEURON_CLASS_CTRNN 1

// keep this up to date
#define NEURON_ROLE_HIDDEN 0
#define NEURON_ROLE_SENSOR 1 
#define NEURON_ROLE_MOTOR 2

// keep this up to date
#define NEURON_ACTIVATIONFUNCTION_LINEAR 0
#define NEURON_ACTIVATIONFUNCTION_SIGMOID 1
#define NEURON_ACTIVATIONFUNCTION_TANH 2
#define NEURON_ACTIVATIONFUNCTION_LEAKYRELU 3
#define NEURON_ACTIVATIONFUNCTION_RELU 4
#define NEURON_ACTIVATIONFUNCTION_STEP 5
