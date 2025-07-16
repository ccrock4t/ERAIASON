using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Brain;
using static Brain.Neuron;

public class InitialNEATGenomes : MonoBehaviour
{
    // setup parameters
    const bool ADD_RANDOM_STARTING_CONNECTIONS = true;
    const bool ADD_RANDOM_STARTING_NODES = true;
    const bool FULLY_CONNECT_WITH_HIDDEN_LAYER = false;
    const int NUM_OF_RANDOM_STARTING_CONNECTIONS = 5;
    const int NUM_OF_RANDOM_STARTING_NODES = 2;
    const bool DROPOUT = true;
    const float DROPOUT_RATE = 0.5f;

    public const Neuron.ActivationFunction neuron_activation_function = ActivationFunction.Sigmoid;

    // universal sensory-motor neuron indexs
    //motor
    public const int EATING_MOTOR_NEURON_INDEX = -1;
    public const int MATING_MOTOR_NEURON_INDEX = -2;
    public const int FIGHTING_MOTOR_NEURON_INDEX = -3;
    public const int ASEXUAL_MOTOR_NEURON = -4;
    public const int PICKUP_VOXEL_MOTOR_NEURON = -5;
    public const int PLACE_VOXEL_MOTOR_NEURON = -6;
  

    //sensor
    public const int RAYCAST_VISION_SENSOR_FOOD_NEURON_INDEX = -1;
    public const int RAYCAST_VISION_SENSOR_ANIMAT_NEURON_INDEX = -2;
    public const int RAYCAST_VISION_SENSOR_OBSTACLE_NEURON_INDEX = -3;
    public const int INTERNAL_ENERGY_SENSOR = -4;
    public const int INTERNAL_HEALTH_SENSOR = -5;
    public const int MOUTH_SENSOR = -6; // 1 when eating food, 0 otherwise
    public const int SINEWAVE_SENSOR = -7; // generates a periodic signal (sinewave)
    public const int PAIN_SENSOR = -8; // detects being attacked by other animat
    public const int RAYCAST_VISION_SENSOR_INTERACTABLE_VOXEL = -9; 
    public const int INTERNAL_VOXEL_HELD = -10; 

    static readonly int[] vision_neurons_IDs = new int[]
    {
            RAYCAST_VISION_SENSOR_FOOD_NEURON_INDEX,
            RAYCAST_VISION_SENSOR_ANIMAT_NEURON_INDEX,
            RAYCAST_VISION_SENSOR_OBSTACLE_NEURON_INDEX,
    };

    static readonly int[] vision_neurons_IDs_voxel_world = new int[]
    {
        RAYCAST_VISION_SENSOR_INTERACTABLE_VOXEL
    };

    static readonly int[] misc_sensory_neuron_IDs = new int[]
    {
            INTERNAL_ENERGY_SENSOR,
            INTERNAL_HEALTH_SENSOR,
            MOUTH_SENSOR,
            SINEWAVE_SENSOR,
            PAIN_SENSOR
    };

    static readonly int[] misc_sensory_neuron_IDs_voxel_world = new int[]
    {
        INTERNAL_VOXEL_HELD
    };

    static readonly int[] universal_motor_neurons_IDs_to_add = new int[]
    {
        EATING_MOTOR_NEURON_INDEX,
        MATING_MOTOR_NEURON_INDEX,
        FIGHTING_MOTOR_NEURON_INDEX,
        ASEXUAL_MOTOR_NEURON
    };

    static readonly int[] universal_motor_neurons_IDs_to_add_voxel_world = new int[]
    {
        PICKUP_VOXEL_MOTOR_NEURON,
        PLACE_VOXEL_MOTOR_NEURON
    };
    public static NEATGenome CreateTestGenome(BodyGenome bodygenome)
    {
        NEATGenome genome;
        if (GlobalConfig.BODY_METHOD == GlobalConfig.BodyMethod.WheeledRobot)
        {
            genome = CreateTestGenomeWheeledRobot();
        }
        else if (GlobalConfig.BODY_METHOD == GlobalConfig.BodyMethod.ArticulatedRobot)
        {
            genome = CreateTestGenomeArticulatedRobot((ArticulatedRobotBodyGenome)bodygenome);
        }
        else if (GlobalConfig.BODY_METHOD == GlobalConfig.BodyMethod.SoftVoxelRobot)
        {
            genome = CreateTestGenomeSoftVoxelRobot((SoftVoxelRobotBodyGenome)bodygenome);
        }
        else
        {
            return null;
        }

        AddUniversalSensorNeurons(genome);
        AddUniversalMotorNeurons(genome);
        ConnectVisionSensorsToMotors(genome);

        genome.sensorymotor_end_idx = genome.nodes.Count;

       // AddUniversalHiddenNeurons(genome);



        if (FULLY_CONNECT_WITH_HIDDEN_LAYER)
        {
            int num_of_hidden_nodes_to_add = (genome.sensor_nodes.Count + genome.motor_nodes.Count)/2;
            for (int i = 0; i < num_of_hidden_nodes_to_add; i++)
            {

                NEATNode node = new(NEATGenome.GetTupleIDFromInt(genome.nodes.Count, NeuronRole.Hidden), neuron_activation_function);
                genome.AddNode(node);
            }

            // connect hidden layers
            int ID = genome.connections.Count - 1;
            foreach (var sensor_node in genome.sensor_nodes)
            {

                foreach (var hidden_node in genome.hidden_nodes)
                {
                    ID++;
                    NEATConnection sr_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                    fromID: sensor_node.ID,
                     toID: hidden_node.ID,
                    ID: ID);
                    if (UnityEngine.Random.Range(0, 4) != 0) sr_connection.enabled = false;
                    genome.AddConnection(sr_connection);
                }
            }
            foreach (var motor_node in genome.motor_nodes)
            {

                foreach (var hidden_node in genome.hidden_nodes)
                {
                    ID++;
                    NEATConnection sr_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                    fromID: hidden_node.ID,
                    toID: motor_node.ID,
                    ID: ID);
                    if (UnityEngine.Random.Range(0, 4) != 0) sr_connection.enabled = false;
                    genome.AddConnection(sr_connection);
                    
                }
            }
        }

        if (NEATConnection.NEXT_GLOBAL_CONNECTION_ID == -1) NEATConnection.NEXT_GLOBAL_CONNECTION_ID = genome.connections.Count;
        if (NEATNode.NEXT_GLOBAL_HIDDENNODE_ID == -1) NEATNode.NEXT_GLOBAL_HIDDENNODE_ID = genome.nodes.Count;

        if (ADD_RANDOM_STARTING_CONNECTIONS)
        {
            int num_connections = UnityEngine.Random.Range(0, NUM_OF_RANDOM_STARTING_CONNECTIONS);
            for (int j = 0; j < num_connections; j++)
            {

                NEATConnection connection = genome.AddNewRandomConnection();
            }
        }


        if (ADD_RANDOM_STARTING_NODES)
        {
            int num_nodes = UnityEngine.Random.Range(0, NUM_OF_RANDOM_STARTING_NODES);
            for (int j = 0; j < num_nodes; j++)
            {
                NEATNode node = genome.AddNewHiddenNodeAtRandomConnection();
            }
        }

        


        return genome;
    }

    public static NEATGenome CreateTestGenomeWheeledRobot()
    {
        NEATGenome genome = new();

        NEATNode node;

        // motor
        List<int> motor_neurons_IDs_to_add = new()
        {   WheeledRobot.MOVE_FORWARD_NEURON_ID,
            WheeledRobot.ROTATE_RIGHT_NEURON_ID,
            WheeledRobot.ROTATE_LEFT_NEURON_ID,
            WheeledRobot.JUMP_MOTOR_NEURON_ID
        }; 


        foreach (int neuronID in motor_neurons_IDs_to_add)
        {
            var ID = NEATGenome.GetTupleIDFromInt(neuronID, Brain.Neuron.NeuronRole.Motor);
            node = new(ID, neuron_activation_function);
            genome.AddNode(node);
        }

        //sensor
        List<int> sensor_neurons_IDs_to_add = new()
        {
            WheeledRobot.TOUCH_SENSOR_NEURON_ID
        };

        foreach (int neuronID in sensor_neurons_IDs_to_add)
        {
            var ID = NEATGenome.GetTupleIDFromInt(neuronID, Brain.Neuron.NeuronRole.Sensor);
            node = new(ID, neuron_activation_function);
            genome.AddNode(node);
        }

        return genome;
    }

    public static NEATGenome CreateTestGenomeArticulatedRobot(ArticulatedRobotBodyGenome body_genome)
    {
        NEATGenome genome = new();

        int num_of_segments = body_genome.CountNumberOfSegments(body_genome.node_array[0]);

        for (int i = 0; i < num_of_segments; i++)
        {
            // add sensor neurons for each segment
            for (int k = 0; k < ArticulatedRobot.NUM_OF_SENSOR_NEURONS_PER_SEGMENT; k++)
            {
                NEATNode sensor_node = new(ID: NEATGenome.GetTupleIDFrom2Ints(i, k, Brain.Neuron.NeuronRole.Sensor), neuron_activation_function);
                genome.AddNode(sensor_node);
            }

            // add motor neurons for each joint
            for (int k = 0; k < ArticulatedRobot.NUM_OF_MOTOR_NEURONS_PER_JOINT; k++)
            {
                NEATNode motor_node = new(ID: NEATGenome.GetTupleIDFrom2Ints(i, k, Brain.Neuron.NeuronRole.Motor), neuron_activation_function);
                genome.AddNode(motor_node);

                ////add connections and hidden layer
                for (int j = 0; j < ArticulatedRobot.NUM_OF_SENSOR_NEURONS_PER_SEGMENT; j++)
                {
                    var sense_node_ID = NEATGenome.GetTupleIDFrom2Ints(i, j, Brain.Neuron.NeuronRole.Sensor);

                    var sense_node = genome.GetNode(sense_node_ID);


                    NEATConnection sr_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                       fromID: sense_node_ID,
                       toID: motor_node.ID,
                       ID: genome.connections.Count);
                    if (DROPOUT && UnityEngine.Random.value < DROPOUT_RATE) sr_connection.enabled = false;
                    genome.AddConnection(sr_connection);
                }

            }
        }
 


        return genome;
    }

    public static NEATGenome CreateTestGenomeSoftVoxelRobot(SoftVoxelRobotBodyGenome body_genome)
    {
        NEATGenome genome = new();

        int3 dims = body_genome.dimensions3D;
        int num_of_voxels = dims.x * dims.y * dims.z;
        int NUM_OF_SENSOR_NEURONS = SoftVoxelRobot.NUM_OF_SENSOR_NEURONS;
        int NUM_OF_MOTOR_NEURONS = SoftVoxelRobot.NUM_OF_MOTOR_NEURONS;

        // add sensor neurons for each voxel
        for (int i = 0; i < num_of_voxels; i++)
        {
            int3 coords = GlobalUtils.Index_int3FromFlat(i, dims);
            if (body_genome.voxel_array[i] == SoftVoxelRobot.RobotVoxel.Empty) continue; 
            for (int k = 0; k < NUM_OF_SENSOR_NEURONS; k++)
            {
                NEATNode sensor_node = new(ID:NEATGenome.GetTupleIDFromInt3(coords, k, Brain.Neuron.NeuronRole.Sensor), neuron_activation_function);
                genome.AddNode(sensor_node);

            }
        }



        // add motor neurons
        for (int i = 0; i < num_of_voxels; i++)
        {
            int3 coords = GlobalUtils.Index_int3FromFlat(i, dims);
            if (body_genome.voxel_array[i] == SoftVoxelRobot.RobotVoxel.Empty) continue;
            for (int j = 0; j < NUM_OF_MOTOR_NEURONS; j++)
            {
                NEATNode motor_node = new(ID: NEATGenome.GetTupleIDFromInt3(coords,j,NeuronRole.Motor), neuron_activation_function);
                var motor_node_ID = motor_node.ID;
                genome.AddNode(motor_node);


                // add a connection from sensor to motor in the voxel as well
                for (int k = 0; k < NUM_OF_SENSOR_NEURONS; k++)
                {

                    var sense_node_ID = new NeuronID(new int4(coords, k), Neuron.NeuronRole.Sensor);
                    var sense_node = genome.GetNode(sense_node_ID);

                    NEATConnection sr_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                        fromID: sense_node_ID,
                        toID: motor_node.ID,
                        ID: genome.connections.Count);
                    genome.AddConnection(sr_connection);


                }

            }
        }

        return genome;
    }

    public static void ConnectVisionSensorsToMotors(NEATGenome genome)
    {
        // connect all the vision sensory neurons for a given raycast to a hidden neuron
        for (int i = 0; i < VisionSensor.NUM_OF_RAYCASTS; i++)
        {
            foreach (int neuronID in vision_neurons_IDs)
            {
                var vision_neuron_ID = NEATGenome.GetTupleIDFrom2Ints(neuronID, i, Brain.Neuron.NeuronRole.Sensor);
                var vision_neuron = genome.GetNode(vision_neuron_ID);
                foreach (var motor_node in genome.motor_nodes)
                {
                    NEATConnection vision_to_motor_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                    fromID: vision_neuron.ID,
                    toID: motor_node.ID,
                    ID: genome.connections.Count);
                    if (DROPOUT && UnityEngine.Random.value < DROPOUT_RATE) vision_to_motor_connection.enabled = false;
                    genome.AddConnection(vision_to_motor_connection);
                }
            }



        }

    }

    public static void AddUniversalHiddenNeurons(NEATGenome genome)
    {
        // get all vision sensors and connect them to hidden neurons
        NEATNode node;


        // connect all the vision sensory neurons for a given raycast to a hidden neuron
        for (int i = 0; i < VisionSensor.NUM_OF_RAYCASTS; i++)
        {
            NeuronID hidden_node_ID = NEATGenome.GetTupleIDFromInt(genome.nodes.Count, NeuronRole.Hidden);
            NEATNode hidden_node = genome.AddDisconnectedHiddenNode(hidden_node_ID);
            float4 average_coords = float4.zero;
            foreach (int neuronID in vision_neurons_IDs)
            {
                var vision_neuron_ID = NEATGenome.GetTupleIDFrom2Ints(neuronID, i, Brain.Neuron.NeuronRole.Sensor);
                var vision_neuron = genome.GetNode(vision_neuron_ID);
                NEATConnection new_connection = new(weight: NEATConnection.GetRandomInitialWeight(), fromID: vision_neuron_ID, toID: hidden_node_ID, ID: genome.connections.Count);
                genome.AddConnection(new_connection);
                average_coords += vision_neuron.brainviewer_coords;
            }
            average_coords /= vision_neurons_IDs.Length;
            hidden_node.brainviewer_coords = average_coords;
        }

        // connect all the vision sensory neurons for a given type of raycast to a hidden neuron
        foreach (int neuronID in vision_neurons_IDs)
        {
            NeuronID hidden_node_ID = NEATGenome.GetTupleIDFromInt(genome.nodes.Count, NeuronRole.Hidden);
            NEATNode hidden_node = genome.AddDisconnectedHiddenNode(hidden_node_ID);
            float4 average_coords = float4.zero;
            for (int i = 0; i < VisionSensor.NUM_OF_RAYCASTS; i++)
            {
                var vision_neuron_ID = NEATGenome.GetTupleIDFrom2Ints(neuronID, i, Brain.Neuron.NeuronRole.Sensor);
                var vision_neuron = genome.GetNode(vision_neuron_ID);
                NEATConnection new_connection = new(weight: NEATConnection.GetRandomInitialWeight(), fromID: vision_neuron_ID, toID: hidden_node_ID, ID: genome.connections.Count);
                genome.AddConnection(new_connection);
                average_coords += vision_neuron.brainviewer_coords;
            }
            average_coords /= VisionSensor.NUM_OF_RAYCASTS;
            hidden_node.brainviewer_coords = average_coords;
        }


        List<NEATNode> misc_hidden_nodes = new();
        for (int i = 0; i < misc_sensory_neuron_IDs.Length; i++)
        {
            NeuronID hidden_node_ID = NEATGenome.GetTupleIDFromInt(genome.nodes.Count, NeuronRole.Hidden);
            NEATNode hidden_node = genome.AddDisconnectedHiddenNode(hidden_node_ID);
            misc_hidden_nodes.Add(hidden_node);
            hidden_node.brainviewer_coords = float4.zero;
        }

        foreach (int neuronID in misc_sensory_neuron_IDs)
        {
            var misc_sensory_ID = NEATGenome.GetTupleIDFromInt(neuronID, Brain.Neuron.NeuronRole.Sensor);
            var misc_sensory_node = genome.GetNode(misc_sensory_ID);
            float4 average_coords = float4.zero;
            float i = 0;
            foreach(NEATNode hidden_node in misc_hidden_nodes) 
            {
                NEATConnection new_connection = new(weight: NEATConnection.GetRandomInitialWeight(), fromID: misc_sensory_ID, toID: hidden_node.ID, ID: genome.connections.Count);
                genome.AddConnection(new_connection);
                hidden_node.brainviewer_coords += (misc_sensory_node.brainviewer_coords+ new float4(0f,0f,0f,i)) / misc_sensory_neuron_IDs.Length;
                i++;
            }
        }
    }

    // add universal sensor neurons (for all robots)
    public static void AddUniversalSensorNeurons(NEATGenome genome)
    {
        NEATNode node;

        for (int i = 0; i < VisionSensor.NUM_OF_RAYCASTS; i++)
        {
            foreach (int neuronID in vision_neurons_IDs)
            {
                var ID = NEATGenome.GetTupleIDFrom2Ints(neuronID, i, Brain.Neuron.NeuronRole.Sensor);
                node = new(ID, neuron_activation_function);
                genome.AddNode(node);
            }
         
            // for voxel world, add motors
            foreach (int neuronID in vision_neurons_IDs_voxel_world)
            {
                var ID = NEATGenome.GetTupleIDFrom2Ints(neuronID, i, Brain.Neuron.NeuronRole.Sensor);
                node = new(ID, neuron_activation_function);
                genome.AddNode(node);
            }
            
        }

        foreach (int neuronID in misc_sensory_neuron_IDs)
        {
            var ID = NEATGenome.GetTupleIDFromInt(neuronID, Brain.Neuron.NeuronRole.Sensor);
            node = new(ID, neuron_activation_function);
            genome.AddNode(node);
        }

        // for voxel world, add motors
        foreach (int neuronID in misc_sensory_neuron_IDs_voxel_world)
        {
            var ID = NEATGenome.GetTupleIDFromInt(neuronID, Brain.Neuron.NeuronRole.Sensor);
            node = new(ID, neuron_activation_function);
            genome.AddNode(node);
        }
        
    }


    // add universal sensor neurons (for all robots)
    public static void AddUniversalMotorNeurons(NEATGenome genome)
    {
        NEATNode node;



        foreach (int neuronID in universal_motor_neurons_IDs_to_add)
        {
            var ID = NEATGenome.GetTupleIDFromInt(neuronID, Brain.Neuron.NeuronRole.Motor);
            node = new(ID, neuron_activation_function);
            genome.AddNode(node);
        }

     
        // for voxel world, add motors
        foreach (int neuronID in universal_motor_neurons_IDs_to_add_voxel_world)
        {
            var ID = NEATGenome.GetTupleIDFromInt(neuronID, Brain.Neuron.NeuronRole.Motor);
            node = new(ID, neuron_activation_function);
            genome.AddNode(node);
        }
        
        
    }
}
