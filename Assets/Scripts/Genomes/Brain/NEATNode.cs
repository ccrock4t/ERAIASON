using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using static Brain;
using static NEATGenome;

public class NEATNode
{
    public int ID;
    public double bias;
    // sum and squash activation functions
    public double sigmoid_alpha;
    public double sigmoid_alpha2;

    // CTRNN
    public double time_constant;
    public double gain;

    //CPG
    public double r; // mix ratio
    public double w; // wave frequency
    public double r_gain;
    public double p_gain;
    public double mu;
    public double K;
    public double max_input;
    public double osc_inject_gain;
    public double theta;
    public double phase_offset;

    public double blend;

    public float4 brainviewer_coords;
    public Neuron.ActivationFunction activation_function;
    public static int NEXT_GLOBAL_HIDDENNODE_ID = 1_000_000;
    internal Neuron.NeuronRole neuron_role;
    internal double w_gain;
    internal double epsilon;
    internal double tau;

    public NEATNode(int ID, 
        Neuron.ActivationFunction activation_function,
        Neuron.NeuronRole neuron_role,
        float4? override_brainviewer_coords=null)
    {
        if (ID == int.MinValue) ID = NEXT_GLOBAL_HIDDENNODE_ID++;
        this.ID = ID;
        this.brainviewer_coords = override_brainviewer_coords ?? new float4(ID, 0, 0, 0);
        this.neuron_role = neuron_role;
        this.bias = NEATConnection.GetRandomInitialWeight();
        this.time_constant = 1;
        this.gain = 1;
        this.sigmoid_alpha = 1;

        var r_range = CPGRanges.GetRRange();
        var w_range = CPGRanges.GetWRange();
        var thetaRange = CPGRanges.GetThetaRange();
        var rGainRange = CPGRanges.GetRGainRange();
        var pGainRange = CPGRanges.GetPGainRange();
        var wGainRange = CPGRanges.GetWGainRange();
        var epsilonRange = CPGRanges.GetEpsilonRange();
        var tauRange = CPGRanges.GetTauRange();
        var muRange = CPGRanges.GetMuRange();
        var kRange = CPGRanges.GetKRange();
        var miRange = CPGRanges.GetMaxInputRange();
        var giRange = CPGRanges.GetOscInjectGainRange();
        var phaseOffsetRange = CPGRanges.GetPhaseOffsetRange();
        var blendRange = CPGRanges.GetBlendRange();

        // Assign random values from ranges
        this.r = UnityEngine.Random.Range(r_range.x, r_range.y);
        this.w = UnityEngine.Random.Range(w_range.x, w_range.y);
        this.theta = UnityEngine.Random.Range(thetaRange.x, thetaRange.y); // assuming 'p' is phase offset
        this.r_gain = UnityEngine.Random.Range(rGainRange.x, rGainRange.y);
        this.p_gain = UnityEngine.Random.Range(pGainRange.x, pGainRange.y);
        this.w_gain = UnityEngine.Random.Range(wGainRange.x, wGainRange.y);
        this.epsilon = UnityEngine.Random.Range(epsilonRange.x, epsilonRange.y);
        this.tau = UnityEngine.Random.Range(tauRange.x, tauRange.y);
        this.mu = UnityEngine.Random.Range(muRange.x, muRange.y);
        this.K = UnityEngine.Random.Range(kRange.x, kRange.y);         
        this.max_input = UnityEngine.Random.Range(miRange.x, miRange.y);
        this.osc_inject_gain = UnityEngine.Random.Range(giRange.x, giRange.y);
        this.phase_offset = UnityEngine.Random.Range(phaseOffsetRange.x, phaseOffsetRange.y);
        this.blend = UnityEngine.Random.Range(blendRange.x, blendRange.y);
        this.activation_function = activation_function;
    }

    public static double GetRandomTimeContant()
    {
        return UnityEngine.Random.Range(-3f, 3f);
    }

    public NEATNode Clone()
    {
        NEATNode new_node = new(this.ID,
            this.activation_function,
            this.neuron_role);

        new_node.brainviewer_coords = this.brainviewer_coords;
        new_node.bias = this.bias;
        new_node.sigmoid_alpha = this.sigmoid_alpha;
        new_node.sigmoid_alpha2 = this.sigmoid_alpha2;
        new_node.activation_function = this.activation_function;

        // ctrnn
        new_node.time_constant = this.time_constant;
        new_node.gain = this.gain;

        //cpg
        new_node.r = this.r;
        new_node.w = this.w;
        new_node.r_gain = this.r_gain;
        new_node.p_gain = this.p_gain;
        new_node.w_gain = this.w_gain;
        new_node.epsilon = this.epsilon;
        new_node.tau = this.tau;
        new_node.mu = this.mu;
        new_node.K = this.K;
        new_node.max_input = this.max_input;
        new_node.osc_inject_gain = this.osc_inject_gain;
        new_node.theta = this.theta;
        new_node.phase_offset = this.phase_offset;
        new_node.blend = this.blend;

        // update this whenever a new field is added
        return new_node; // shallow copy of the object, its ok because tthe fields are primitives
    }
}