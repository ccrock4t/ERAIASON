using Unity.Mathematics;
using static Brain;

public class NEATNode
{
    public NeuronID ID;
    public float bias;
    // sum and squash activation functions
    public float sigmoid_alpha;
    public float sigmoid_alpha2;

    // CTRNN
    public float time_constant;
    public float gain;

    //CPG
    public float r; // mix ratio
    public float w; // wave frequency
    public float p; // wave phase offset

    public float4 brainviewer_coords;
    public Neuron.ActivationFunction activation_function;
    public static int NEXT_GLOBAL_HIDDENNODE_ID = -1; 

    public NEATNode(NeuronID ID, 
        Neuron.ActivationFunction activation_function, 
        float4? override_brainviewer_coords=null)
    {
        if (ID.coords.w == int.MinValue) ID.coords.w = NEXT_GLOBAL_HIDDENNODE_ID++;
        this.ID = ID;
        this.brainviewer_coords = override_brainviewer_coords == null ? ID.coords : (float4)override_brainviewer_coords;
        this.bias = NEATConnection.GetRandomInitialWeight();
        this.time_constant = 1;
        this.gain = 1;
        this.sigmoid_alpha = 1;
        this.r = 1;
        this.w = 1;
        this.p = 1;

        if(ID.neuron_role == Neuron.NeuronRole.Hidden && NEATGenome.EVOLVE_ACTIVATION_FUNCTIONS)
        {
            this.activation_function = Brain.Neuron.GetRandomActivationFunction();
        }
        else
        {
            this.activation_function = activation_function;
        }

    
    }

    public static float GetRandomTimeContant()
    {
        return UnityEngine.Random.Range(-3f, 3f);
    }

    public NEATNode Clone()
    {
        NEATNode new_node = new(this.ID,
            this.activation_function);

        new_node.brainviewer_coords = this.brainviewer_coords;
        new_node.bias = this.bias;
        new_node.time_constant = this.time_constant;
        new_node.r = this.r;
        new_node.w = this.w;
        new_node.p = this.p;
        new_node.gain = this.gain;
        new_node.sigmoid_alpha = this.sigmoid_alpha;
        new_node.sigmoid_alpha2 = this.sigmoid_alpha2;
        new_node.activation_function = this.activation_function;

        // update this whenever a new field is added
        return new_node; // shallow copy of the object, its ok because tthe fields are primitives
    }
}