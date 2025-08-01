using System;
using System.Collections.Generic;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;
using static ArticulatedRobotBodyGenome;
using static BodyGenome;
using static Brain;
using static Brain.Neuron;
using static SoftVoxelRobotBodyGenome;

public class InitialNEATGenomes : MonoBehaviour
{
    // setup parameters
    const bool ADD_RANDOM_STARTING_CONNECTIONS = true;
    const bool ADD_RANDOM_STARTING_NODES = true;
    const bool FULLY_CONNECT_WITH_HIDDEN_LAYER = false;
    const int NUM_OF_RANDOM_STARTING_CONNECTIONS = 10;
    const int NUM_OF_RANDOM_STARTING_NODES = 3;
    const int NUM_OF_UNIVERSAL_HIDDEN_NEURONS = 40;
    const bool DROPOUT = true;
    const float DROPOUT_RATE = 0.8f;

    public const Neuron.ActivationFunction neuron_activation_function = ActivationFunction.Tanh;

    // universal sensory-motor neuron indexs
    //motor
    public const int EATING_MOTOR_NEURON_INDEX = -10000_1;
    public const int MATING_MOTOR_NEURON_INDEX = -10000_2;
    public const int FIGHTING_MOTOR_NEURON_INDEX = -10000_3;
    public const int ASEXUAL_MOTOR_NEURON = -10000_4;
    public const int PICKUP_VOXEL_MOTOR_NEURON = -10000_5;
    public const int PLACE_VOXEL_MOTOR_NEURON = -10000_6;
  

    //sensor
    public const int INTERNAL_ENERGY_SENSOR = -1;
    public const int INTERNAL_HEALTH_SENSOR = -2;
    public const int MOUTH_SENSOR = -3; // 1 when eating food, 0 otherwise
    public const int SINEWAVE_SENSOR = -4; // generates a periodic signal (sinewave)
    public const int PAIN_SENSOR = -5; // detects being attacked by other animat
    public const int RAYCAST_VISION_SENSOR_INTERACTABLE_VOXEL = -6; 
    public const int INTERNAL_VOXEL_HELD = -7; 


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
    public static NEATGenome CreateTestGenome(BodyGenome body_genome)
    {
        NEATGenome brain_genome;
        if (GlobalConfig.BODY_METHOD == GlobalConfig.BodyMethod.WheeledRobot)
        {
            brain_genome = CreateTestGenomeWheeledRobot();
        }
        else if (GlobalConfig.BODY_METHOD == GlobalConfig.BodyMethod.ArticulatedRobot)
        {
            brain_genome = CreateTestGenomeArticulatedRobot((ArticulatedRobotBodyGenome)body_genome);
        }
        else if (GlobalConfig.BODY_METHOD == GlobalConfig.BodyMethod.SoftVoxelRobot)
        {
            brain_genome = CreateTestGenomeSoftVoxelRobot((SoftVoxelRobotBodyGenome)body_genome);
        }
        else
        {
            return null;
        }

        AddUniversalSensorNeurons(body_genome, brain_genome);
        AddUniversalMotorNeurons(brain_genome);
        ConnectVisionSensorsToMotors(brain_genome, body_genome);


        brain_genome.sensorymotor_end_idx = brain_genome.nodes.Count;

        //int global_connection_start_id = FullyConnect(brain_genome);
        int global_connection_start_id = brain_genome.connections.Count;
        if (NEATConnection.NEXT_GLOBAL_CONNECTION_ID == -1) NEATConnection.NEXT_GLOBAL_CONNECTION_ID = global_connection_start_id;
        if (NEATNode.NEXT_GLOBAL_HIDDENNODE_ID == -1) NEATNode.NEXT_GLOBAL_HIDDENNODE_ID = brain_genome.nodes.Count;

        if (ADD_RANDOM_STARTING_CONNECTIONS)
        {
            int num_connections = NUM_OF_RANDOM_STARTING_CONNECTIONS;
            for (int j = 0; j < num_connections; j++)
            {

                NEATConnection connection = brain_genome.AddNewRandomConnection();
            }
        }


        if (ADD_RANDOM_STARTING_NODES)
        {
            int num_nodes = NUM_OF_RANDOM_STARTING_NODES;
            for (int j = 0; j < num_nodes; j++)
            {
                NEATNode node = brain_genome.AddNewHiddenNodeAtRandomConnection();
            }
        }

        


        return brain_genome;
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
            node = new(neuronID, neuron_activation_function, NeuronRole.Motor);
            genome.AddNode(node);
        }

        //sensor
        List<int> sensor_neurons_IDs_to_add = new()
        {
            WheeledRobot.TOUCH_SENSOR_NEURON_ID
        };

        foreach (int neuronID in sensor_neurons_IDs_to_add)
        {
            node = new(neuronID, neuron_activation_function, NeuronRole.Sensor);
            genome.AddNode(node);
        }

        return genome;
    }

    public static NEATGenome CreateTestGenomeArticulatedRobot(ArticulatedRobotBodyGenome body_genome)
    {
        NEATGenome brain_genome = new();

        int num_of_segments = body_genome.CountNumberOfSegments(body_genome.node_array[0]);

        for (int i = 0; i < num_of_segments; i++)
        {
            // add sensor neurons for each segment
            for (int k = 0; k < ArticulatedRobot.NUM_OF_SENSOR_NEURONS_PER_SEGMENT; k++)
            {
                NEATNode sensor_node = new(ID: brain_genome.nodes.Count, neuron_activation_function, NeuronRole.Sensor);
                brain_genome.AddNode(sensor_node);
                var sensorKey = new ArticulatedSensorKey(i, (ArticulatedSensorType)k);
                body_genome.articulatedSensorKeyToNodeID.Add(sensorKey, sensor_node.ID);
            }

            // add motor neurons for each joint
            for (int k = 0; k < ArticulatedRobot.NUM_OF_MOTOR_NEURONS_PER_JOINT; k++)
            {
                NEATNode motor_node = new(ID: brain_genome.nodes.Count, neuron_activation_function, NeuronRole.Motor);
                brain_genome.AddNode(motor_node);
                var motorKey = new ArticulatedMotorKey(i, (dof)k);
                body_genome.articulatedMotorKeyToNodeID.Add(motorKey, motor_node.ID);
            }
        }
 


        return brain_genome;
    }

    public static NEATGenome CreateTestGenomeSoftVoxelRobot(SoftVoxelRobotBodyGenome body_genome)
    {
        NEATGenome brain_genome = new();

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
                NEATNode sensor_node = new(ID: brain_genome.nodes.Count, neuron_activation_function, NeuronRole.Sensor);
                brain_genome.AddNode(sensor_node);
                var sensorKey = new SoftVoxelSensorKey(coords, (SoftVoxelSensorType)k);
                body_genome.svrSensorKeyToNodeID.Add(sensorKey, sensor_node.ID);
            }
        }



        // add motor neurons
        for (int i = 0; i < num_of_voxels; i++)
        {
            int3 coords = GlobalUtils.Index_int3FromFlat(i, dims);
            if (body_genome.voxel_array[i] == SoftVoxelRobot.RobotVoxel.Empty) continue;
            for (int j = 0; j < NUM_OF_MOTOR_NEURONS; j++)
            {
                NEATNode motor_node = new(ID: brain_genome.nodes.Count, neuron_activation_function, NeuronRole.Motor);
                var motor_node_ID = motor_node.ID;
                brain_genome.AddNode(motor_node);
                var motorKey = new SoftVoxelMotorKey(coords, (dof)j);
                body_genome.svrMotorKeyToNodeID.Add(motorKey, motor_node.ID);

                // add a connection from sensor to motor in the voxel as well
                for (int k = 0; k < NUM_OF_SENSOR_NEURONS; k++)
                {
                    var sensorKey = new SoftVoxelSensorKey(coords, (SoftVoxelSensorType)k);
                    var sense_node_ID = body_genome.svrSensorKeyToNodeID[sensorKey];
                    var sense_node = brain_genome.GetNode(sense_node_ID);

                    NEATConnection sr_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                        fromNodeID: sense_node_ID,
                        toNodeID: motor_node.ID,
                        ID: brain_genome.connections.Count);
                    brain_genome.AddConnection(sr_connection);


                }

            }
        }

        return brain_genome;
    }

    public static int FullyConnect(NEATGenome genome)
    {
        int conn_ID = genome.connections.Count;
        const int num_hidden_neurons = 10;
        List<NEATNode> hiddenLayer1 = new();
        for (int i = 0; i < num_hidden_neurons; i++)
        {
            NEATNode hidden_node = genome.AddDisconnectedHiddenNode(genome.nodes.Count);
            hiddenLayer1.Add(hidden_node);
        }

        foreach (var sensor_node in genome.sensor_nodes)
        {
            foreach (var hidden_node in hiddenLayer1)
            {
                conn_ID++;
                if (DROPOUT && NEATConnection.GetRandomFloat() < DROPOUT_RATE) continue;
                NEATConnection sr_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                    fromNodeID: sensor_node.ID,
                     toNodeID: hidden_node.ID,
                    ID: conn_ID - 1);
                genome.AddConnection(sr_connection);

            }
        }

        // recurrent
        foreach (var hidden_node1 in genome.hidden_nodes)
        {
            foreach (var hidden_node2 in genome.hidden_nodes)
            {
                conn_ID++;
                if (DROPOUT && NEATConnection.GetRandomFloat() < DROPOUT_RATE) continue;
                NEATConnection sr_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                    fromNodeID: hidden_node1.ID,
                     toNodeID: hidden_node2.ID,
                    ID: conn_ID - 1);
                genome.AddConnection(sr_connection);

            }
        }

        // hidden to motor
        foreach (var hidden_node in hiddenLayer1)
        {
            foreach (var motor_node in genome.motor_nodes)
            {
                conn_ID++;
                if (DROPOUT && NEATConnection.GetRandomFloat() < DROPOUT_RATE) continue;
                NEATConnection sr_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                    fromNodeID: hidden_node.ID,
                     toNodeID: motor_node.ID,
                    ID: conn_ID - 1);
                genome.AddConnection(sr_connection);

            }
        }



        //foreach (var sensor_node in genome.sensor_nodes)
        //{
        //    foreach (var motor_node in genome.motor_nodes)
        //    {
        //        conn_ID++;
        //        if (DROPOUT && NEATConnection.GetRandomFloat() < DROPOUT_RATE) continue;
        //        NEATConnection sr_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
        //            fromNodeID: sensor_node.ID,
        //            toNodeID: motor_node.ID,
        //            ID: conn_ID - 1);
        //        genome.AddConnection(sr_connection);

        //    }
        //}
        return conn_ID;
    }

    public static void ConnectVisionSensorsToMotors(NEATGenome brain_genome, BodyGenome body_genome)
    {


        int vision_sensor_types_count = Enum.GetValues(typeof(VisionSensorType)).Length;
        for (int r = 0; r < VisionSensor.NUM_OF_RAYCASTS; r++)
        {
            for (int i = 0; i < vision_sensor_types_count - 1; i++)
            {
                var sensorKey = new VisionSensorKey(r, (VisionSensorType)i);

                var vision_node_id =  body_genome.visionSensorKeyToNodeID[sensorKey];
                foreach (var motor_node in brain_genome.motor_nodes)
                {
                    NEATConnection vision_to_motor_connection = new(weight: NEATConnection.GetRandomInitialWeight(),
                    fromNodeID: vision_node_id,
                    toNodeID: motor_node.ID,
                    ID: brain_genome.connections.Count);
                    if (DROPOUT && NEATConnection.GetRandomFloat() < DROPOUT_RATE) vision_to_motor_connection.enabled = false;
                    brain_genome.AddConnection(vision_to_motor_connection);
                }
            }
        }

    }

    //public static void AddUniversalHiddenNeurons(NEATGenome genome)
    //{


    //    for (int i = 0; i < NUM_OF_UNIVERSAL_HIDDEN_NEURONS; i++)
    //    {
    //        NeuronID hidden_node_ID = NEATGenome.GetTupleIDFromInt(genome.nodes.Count, NeuronRole.Hidden);
    //        NEATNode hidden_node = genome.AddDisconnectedHiddenNode(hidden_node_ID);

    //        hidden_node.brainviewer_coords = float4.zero;
    //        foreach (var sensor_node in genome.sensor_nodes)
    //        {
    //            NEATConnection new_connection = new(
    //                weight: NEATConnection.GetRandomInitialWeight(),
    //                fromNodeID: sensor_node.ID,
    //                toNodeID: hidden_node.ID,
    //                ID: genome.connections.Count
    //            );
    //            if (DROPOUT && NEATConnection.GetRandomFloat() < DROPOUT_RATE) new_connection.enabled = false;
    //            genome.AddConnection(new_connection);
    //        }

    //        foreach (var motor_node in genome.motor_nodes)
    //        {
    //            NEATConnection new_connection = new(
    //                weight: NEATConnection.GetRandomInitialWeight(),
    //                fromNodeID: hidden_node.ID,
    //                toNodeID: motor_node.ID,
    //                ID: genome.connections.Count
    //            );
    //            if (DROPOUT && NEATConnection.GetRandomFloat() < DROPOUT_RATE) new_connection.enabled = false;
    //            genome.AddConnection(new_connection);
    //        }
    //    }


    //}

    // add universal sensor neurons (for all robots)
    public static void AddUniversalSensorNeurons(BodyGenome body_genome, NEATGenome brain_genome)
    {
     
        int vision_sensor_types_count = Enum.GetValues(typeof(VisionSensorType)).Length;
        for (int r = 0; r < VisionSensor.NUM_OF_RAYCASTS; r++)
        {
            for (int i =0; i < vision_sensor_types_count - 1; i++)
            {
                NEATNode sensor_node = new(brain_genome.nodes.Count, neuron_activation_function, NeuronRole.Sensor);
                brain_genome.AddNode(sensor_node);
                var sensorKey = new VisionSensorKey(r, (VisionSensorType)i);
                body_genome.visionSensorKeyToNodeID.Add(sensorKey, sensor_node.ID);
            }

            if (GlobalConfig.RUN_WORLD_AUTOMATA)
            {
                // for voxel world, add motors
             
                NEATNode sensor_node = new(brain_genome.nodes.Count, neuron_activation_function, NeuronRole.Sensor);
                brain_genome.AddNode(sensor_node);
                var sensorKey = new VisionSensorKey(r, (VisionSensorType)vision_sensor_types_count - 1);
                body_genome.visionSensorKeyToNodeID.Add(sensorKey, sensor_node.ID);
                
            }

            
        }

        foreach (int neuronID in misc_sensory_neuron_IDs)
        {
            NEATNode sensor_node = new(neuronID, neuron_activation_function, NeuronRole.Sensor);
            brain_genome.AddNode(sensor_node);
        }

        if (GlobalConfig.RUN_WORLD_AUTOMATA)
        {
            // for voxel world
            foreach (int neuronID in misc_sensory_neuron_IDs_voxel_world)
            {
                NEATNode sensor_node = new(neuronID, neuron_activation_function, NeuronRole.Sensor);
                brain_genome.AddNode(sensor_node);
            }
        }
        
    }


    // add universal sensor neurons (for all robots)
    public static void AddUniversalMotorNeurons(NEATGenome genome)
    {
        NEATNode node;

        foreach (int neuronID in universal_motor_neurons_IDs_to_add)
        {
            node = new(neuronID, neuron_activation_function, NeuronRole.Motor);
            genome.AddNode(node);
        }

     
        // for voxel world, add motors
        foreach (int neuronID in universal_motor_neurons_IDs_to_add_voxel_world)
        {
            node = new(neuronID, neuron_activation_function, NeuronRole.Motor);
            genome.AddNode(node);
        }
        
        
    }
}
