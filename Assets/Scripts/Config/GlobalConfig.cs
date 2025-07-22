using Unity.Mathematics;
using UnityEngine;

public class GlobalConfig : MonoBehaviour
{
    // === Enums ===

    // used to configure something to run on CPU or GPU
    public enum ProcessingMethod
    {
        CPU,
        GPU
    }

    public enum BrainProcessingMethod
    {
        NeuralNetworkCPU,
        NeuralNetworkGPU,
        NARSCPU,
        Random
    }

    public enum NeuralLearningMethod
    {
        None, // static weights
        HebbABCD,
        HebbLMS,
        HebbYaeger
    }

    public enum BrainMethod
    {
        NeuralNetwork,
        NARS
    }

    public enum BodyMethod
    {
        WheeledRobot,
        ArticulatedRobot,
        SoftVoxelRobot,
        Custom
    }

    public enum BrainGenomeMethod
    {
        CPPN,
        NEAT,
    }

    public enum BodyGenomeMethod
    {
        None, // for wheeled robots
        Linear, // for soft voxel robots
        Sims, // for articulated robots only
    }

    public enum VoxelWorldSmoothingMethod
    {
        None,
        MarchingCubes
    }


    public enum WorldType
    {
        FlatPlane,
        VoxelWorld
    }


    // ========================================================================


    // === User Preferences ===
    public const int TARGET_FRAMERATE = 30;

    // ============

    // === GPU ===
    // To maximize usage of the GPU, request the max number of threads per thread group (brand-dependent), and request the min number of thread groups per dispatch
    public const int MAX_NUM_OF_THREAD_GROUPS = 65535;  // can only have 65535 thread groups per Dispatch
    public const int NUM_OF_GPU_THREADS_PER_THREADGROUP = 32; // AMD uses 64 threads per GPU thread group. NVIDIA uses 32.

    // ============

    // === Genome ===
    public const BrainGenomeMethod BRAIN_GENOME_METHOD = BrainGenomeMethod.NEAT;


    // === Evolutionary Algorithm ===
    public const ProcessingMethod novelty_search_processing_method = ProcessingMethod.GPU;

    // === Animat ===

    // === Animat brain ===
    public static BrainProcessingMethod BRAIN_PROCESSING_METHOD = BrainProcessingMethod.NARSCPU;
    public static bool USE_HEBBIAN = true;
    public const NeuralLearningMethod HEBBIAN_METHOD = NeuralLearningMethod.HebbABCD;
    public static Brain.Neuron.NeuronClass NEURAL_NETWORK_METHOD = Brain.Neuron.NeuronClass.SumAndSquash;


    public static int ANIMAT_BRAIN_UPDATE_PERIOD = 2; // runs every X FixedUpdates. Lower number = runs more freqeuntlly
    public static int BRAIN_VIEWER_UPDATE_PERIOD = 8;

    // ============


    // === Animat body ===

    public static BodyMethod BODY_METHOD = BodyMethod.WheeledRobot;
    public static BodyGenome custom_genome = null;

    // wheeled robot config

    // soft voxel robot config
    public const int MAX_VOXELYZE_ITERATIONS = 200000;


    // articulated robot config


    // ============

    // === World voxel automaton === 
    public const bool RUN_WORLD_AUTOMATA = true;

    public const ProcessingMethod voxel_processing_method = ProcessingMethod.CPU;
    public const VoxelWorldSmoothingMethod voxel_mesh_smoothing_method = VoxelWorldSmoothingMethod.None;
    public GameObject world_automaton_game_object;
    public GameObject flat_plane_arena;
    public static WorldAutomaton world_automaton;
    public static int3 WORLD_DIMENSIONS = new int3(256, 8, 256); // number cells in each dimension. Must be a multiple of 2
    public static int WORLD_AUTOMATA_UPDATE_PERIOD = 6; // runs every X FixedUpdates
    public const string TERRAIN_TAG = "Terrain";
    public static WorldType WORLD_TYPE = WorldType.FlatPlane;
    // ============

    //EA settings
    public static bool USE_NOVELTY_SEARCH = false;
    internal static bool show_lines;

    // === Saving and loading ===
    public const string save_file_path = "SaveFiles/";
    public const string save_file_base_name = "myfile";
    public const string open_string = "[";
    public const string close_string = "]";




    public const bool RECORD_DATA = false;

    // ============





    private void Awake()
    {
 
        // setup automaton
        WorldAutomaton world_automaton;

        switch (voxel_processing_method)
        {
            case ProcessingMethod.CPU:
                world_automaton = world_automaton_game_object.GetComponent<WorldAutomatonCPU>();
                break;
            case ProcessingMethod.GPU:
                world_automaton = world_automaton_game_object.GetComponent<WorldAutomatonGPU>();
                break;
            default:
                Debug.LogError("No voxel automaton component for the given processing method.");
                return;
        }

        world_automaton.enabled = true;

        GlobalConfig.world_automaton = world_automaton;
        GlobalConfig.world_automaton.Start();
    
    }
}