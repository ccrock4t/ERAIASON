using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Brain;
using static GlobalConfig;
using static SoftVoxelRobot;
using Debug = UnityEngine.Debug;
using System;
using Task = System.Threading.Tasks.Task;
using static WorldAutomaton.Elemental;

/// <summary>
///     Script attached to neural network agent. Coordinates neural network and the body.
/// </summary>
public class Animat : MonoBehaviour
{
    public AnimatGenome genome;
    public Mind mind;
    public AnimatBody body;
    Color? override_color = null;

    public Transform animat_creator_food_block;

    public bool initialized = false;

    public float3 birthplace;
    public float3 birthplace_forward_vector;
    public float original_distance_from_food;

    public float was_hit = 0;
    public bool dead = false;
    public bool was_born = false;

    //score

    public bool birthed = false;

    public object behavior_characterization;
    public NoveltySearch.BehaviorCharacterizationCPU behavior_characterization_CPU;
    public List<NoveltySearch.BehaviorCharacterizationCPUDatapoint> behavior_characterization_list = new();
    public List<Color> behavior_characterization_list_GPU = new();

    public Dictionary<int, float> motor_idx_to_activation;


    NativeArray<RobotVoxel> robot_voxels;
    bool body_created = false;

    public Task brain_develop_task = null;

    public static int animatNameGenerator = 0;

    public void Initialize(AnimatGenome genome)
    {
        this.genome = genome;

        this.genome.uniqueName = IntToCustomString(animatNameGenerator++);

        // set vars

        this.motor_idx_to_activation = new();


        // read the genome
        // body
        // brain
        if (GlobalConfig.BRAIN_PROCESSING_METHOD == BrainProcessingMethod.NARSCPU)
        {
            InitializeNARS(genome);
        }
        else if (GlobalConfig.BRAIN_PROCESSING_METHOD == BrainProcessingMethod.NeuralNetworkCPU
            || GlobalConfig.BRAIN_PROCESSING_METHOD == BrainProcessingMethod.NeuralNetworkGPU)
        {
            InitializeBrain(genome);
        }
        else if (GlobalConfig.BRAIN_PROCESSING_METHOD == BrainProcessingMethod.Random)
        {
            this.brain_develop_task = Task.Run(() =>
            {
            });
        }



    }

    public void InitializeNARS(AnimatGenome genome)
    {
        this.brain_develop_task = Task.Run(() =>
        {
            NARSGenome nars_genome = (NARSGenome)genome.brain_genome;
            NARS nar = new NARS(nars_genome);
            this.mind = nar;
        });
    }

    public void InitializeBrain(AnimatGenome genome)
    {

        var syncContext = System.Threading.SynchronizationContext.Current;
        int num_of_neurons = -1;
        int num_of_synapses = -1;
        if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.CPPN)
        {
            Debug.LogError("error");
            /*   int neurons_per_voxel = CPPNGenome.USE_MULTILAYER_PERCEPTRONS_IN_CELL ? ((CPPNGenome)this.genome.brain_genome).network_info.GetNumOfNeurons() : 1;
               num_of_neurons = this.unified_CPPN_genome.substrate_size_3D * neurons_per_voxel;
               num_of_synapses = num_of_neurons * num_of_neurons;*/
        }
        else if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.NEAT)
        {
            num_of_neurons = ((NEATGenome)this.genome.brain_genome).nodes.Count;
            num_of_synapses = ((NEATGenome)this.genome.brain_genome).connections.Count;
        }
        else
        {
            Debug.LogError("error");
        }

        NativeArray<Neuron> neurons = new(length: num_of_neurons, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        NativeArray<Synapse> synapses = new(length: num_of_synapses, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);


        //develop the brain and body in a separate thread
        this.brain_develop_task = Task.Run(() =>
        {
            Dictionary<NeuronID, int> nodeID_to_idx = new();
            List<int> motor_neuron_indices = new();
            if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.CPPN)
            {
                Debug.LogError("error. CPPN temporarily disabled.");
                /*                ParallelHyperNEATCPU job = new()
                                {
                                    network_info = ((CPPNGenome)this.genome.brain_genome).network_info,
                                    dimensions = this.unified_CPPN_genome.dimensions,
                                    dimensions3D = this.unified_CPPN_genome.dimensions3D,
                                    neurons = neurons,
                                    synapses = synapses,
                                    robot_voxels = robot_voxels,

                                    CPPN_nodes = this.unified_CPPN_genome.CPPN_nodes,
                                    CPPN_connections = this.unified_CPPN_genome.CPPN_connections,

                                    CPPN_IO_NODES_INDEXES = this.unified_CPPN_genome.CPPN_IO_IDXS
                                };
                                job.Run(synapses.Length);*/
            }
            else if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.NEAT)
            {
                // ===== Transcribe brain genome to a brain
                // neurons. get neuron ID to idx (for brainviewer, allows checking for correctness)
                NEATGenome brain_genome = ((NEATGenome)this.genome.brain_genome);

                int i = 0;
                foreach (NEATNode node in brain_genome.nodes)
                {
                    nodeID_to_idx[node.ID] = i;
                    if (node.ID.neuron_role == Neuron.NeuronRole.Motor) motor_neuron_indices.Add(i);
                    i++;
                }

                // connections. First group them together
                Dictionary<NeuronID, List<NEATConnection>> nodeID_to_connections = new();
                foreach (NEATConnection connection in brain_genome.connections)
                {
                    if (!nodeID_to_connections.ContainsKey(connection.toID))
                    {
                        nodeID_to_connections[connection.toID] = new();
                    }
                    nodeID_to_connections[connection.toID].Add(connection);
                }

                // connections. Next place them in the array
                Dictionary<NeuronID, int> nodeID_to_synapse_startIdx = new();
                i = 0;
                foreach (KeyValuePair<NeuronID, List<NEATConnection>> node_connections in nodeID_to_connections)
                {
                    NeuronID nodeID = node_connections.Key;
                    nodeID_to_synapse_startIdx[nodeID] = i;
                    foreach (NEATConnection connection in node_connections.Value)
                    {
                        Synapse synapse = Synapse.GetDefault();
                        synapse.from_neuron_idx = nodeID_to_idx[connection.fromID];
                        synapse.to_neuron_idx = nodeID_to_idx[connection.toID];

                        if (nodeID != connection.toID) Debug.LogError("Error connections dont match");

                        synapse.enabled = connection.enabled ? 1 : 0;
                        if (connection.toID.coords.x != nodeID.coords.x
                        || connection.toID.coords.y != nodeID.coords.y
                        || connection.toID.coords.z != nodeID.coords.z
                        || connection.toID.coords.w != nodeID.coords.w
                        || connection.toID.neuron_role != nodeID.neuron_role)
                        {
                            Debug.LogError("error");
                        }
                        synapse.weight = connection.weight;
                        synapse.coefficient_A = connection.hebb_ABCDLR[0];
                        synapse.coefficient_B = connection.hebb_ABCDLR[1];
                        synapse.coefficient_C = connection.hebb_ABCDLR[2];
                        synapse.coefficient_D = connection.hebb_ABCDLR[3];
                        synapse.coefficient_LR = connection.hebb_ABCDLR[4];

                        synapses[i] = synapse;
                        i++;
                    }

                }

                // neurons. Set their synapse start idx and count
                i = 0;
                foreach (NEATNode node in brain_genome.nodes)
                {
                    Neuron neuron = Neuron.GetNewNeuron();
                    neuron.activation_function = node.activation_function;
                    neuron.bias = node.bias;
                    neuron.tau_time_constant = node.time_constant;
                    if(neuron.tau_time_constant < 0)
                    {
                        Debug.LogError("o");
                    }
                    neuron.gain = node.gain;
                    neuron.neuron_role = node.ID.neuron_role;
                    neuron.sigmoid_alpha = node.sigmoid_alpha;
                    neuron.r = node.r;
                    neuron.w = node.w;
                    neuron.p = node.p;
                    neuron.r_gain = node.r_gain;
                    neuron.w_gain = node.w_gain;
                    neuron.p_gain = node.p_gain;
                    neuron.ID = node.ID;
                    neuron.idx = nodeID_to_idx[node.ID];
                    int layer_num;
                    if (neuron.neuron_role == Neuron.NeuronRole.Sensor)
                    {
                        layer_num = 0;
                    }
                    else if (neuron.neuron_role == Neuron.NeuronRole.Motor)
                    {
                        layer_num = 2;
                    }
                    else
                    { //hidden
                        layer_num = 1;
                    }


                    if (neuron.neuron_role != Neuron.NeuronRole.Hidden) neuron.position_idxs = new((int)node.brainviewer_coords.x, (int)node.brainviewer_coords.y, (int)node.brainviewer_coords.z, (int)node.brainviewer_coords.w, layer_num);
                    neuron.position_normalized = new((float)node.brainviewer_coords.x,
                        (float)node.brainviewer_coords.y,
                        (float)node.brainviewer_coords.z,
                        node.brainviewer_coords.w / 2f,
                        (layer_num - 1) / 2f);

                    int synapse_start_idx;
                    int synapse_count;
                    if (nodeID_to_synapse_startIdx.ContainsKey(node.ID))
                    {
                        synapse_start_idx = nodeID_to_synapse_startIdx[node.ID];
                        synapse_count = nodeID_to_connections[node.ID].Count;
                    }
                    else
                    {
                        synapse_start_idx = 0;
                        synapse_count = 0;
                    }
                    neuron.synapse_start_idx = synapse_start_idx;
                    neuron.synapse_count = synapse_count;
                    neurons[nodeID_to_idx[node.ID]] = neuron;
                    i++;
                }

            }
            else
            {
                Debug.LogError("error not implemented");
            }

            // === create brain object

            Brain brain;
            if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.NeuralNetworkCPU)
            {
                brain = new BrainCPU(neurons, synapses, motor_neuron_indices);
                brain.nodeID_to_idx = nodeID_to_idx;
                this.mind = brain;
            }
            else if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.NeuralNetworkGPU)
            {
                // On main thread, during initialization:

                brain = null;
                // On your worker thread
                syncContext.Send(_ =>
                {
                    brain = new BrainGPU(
                        neurons,
                        synapses,
                        ((NEATGenome)this.genome.brain_genome).sensorymotor_end_idx,
                        motor_neuron_indices);
                    brain.nodeID_to_idx = nodeID_to_idx;
                    this.mind = brain;
                }, null);
            }
            else
            {
                GlobalUtils.LogErrorEnumNotRecognized("NOT SUPPORTED");
            }


            // body will be created on the main thread later, since it requires creating a Unity GameObject
        });
    }

    public void Update()
    {
        if (!body_created)
        {
            if (this.IsGenomeTranscribed())
            {
                // === create body
                GameObject body_GO = new GameObject("Body");
                body_GO.transform.parent = this.transform;
                body_GO.transform.localPosition = Vector3.zero;

                if (GlobalConfig.BODY_METHOD == BodyMethod.WheeledRobot)
                {
                    this.body = body_GO.AddComponent<WheeledRobot>();
                    ((WheeledRobot)this.body).Initialize();
                }
                else if (GlobalConfig.BODY_METHOD == BodyMethod.ArticulatedRobot)
                {
                    this.body = body_GO.AddComponent<ArticulatedRobot>();

                    ((ArticulatedRobot)this.body).Initialize((ArticulatedRobotBodyGenome)this.genome.body_genome);

                    ((ArticulatedRobot)this.body).root_ab.TeleportRoot(this.transform.position, Quaternion.Euler(new Vector3(-90, 0, 0)));
                }
                else if (GlobalConfig.BODY_METHOD == BodyMethod.SoftVoxelRobot)
                {
                    this.body = body_GO.AddComponent<SoftVoxelRobot>();
                    ((SoftVoxelRobot)this.body).Initialize((SoftVoxelRobotBodyGenome)this.genome.body_genome);

                    // add voxel world
                    IntPtr cvoxelyze = ((SoftVoxelRobot)this.body).soft_voxel_object.cpp_voxel_object;
                    for (int x = 0; x < GlobalConfig.WORLD_DIMENSIONS.x; x++)
                    {
                        for (int y = 0; y < GlobalConfig.WORLD_DIMENSIONS.y; y++)
                        {
                            for (int z = 0; z < GlobalConfig.WORLD_DIMENSIONS.z; z++)
                            {
                                Vector3Int voxel_position = new(x, y, z);

                                Element element = GlobalConfig.world_automaton.GetCellCurrentState(voxel_position);

                                VoxelyzeEngine.AddVoxelToWorld(cvoxelyze, x, y, z, (int)element);
                            }
                        }
                    }




                }

                this.body.transform.parent = this.transform;
                // flag as fully initialized
                this.initialized = true;
                body_created = true;

            }
        }

    }


    void OnApplicationQuit()
    {
        // manually dispose of all unmanaged memory
        DiposeOfAllocatedMemory();
    }


    public void DiposeOfAllocatedMemory()
    {
        if (this.brain_develop_task != null)
        {
            this.brain_develop_task.Wait();
            this.brain_develop_task.Dispose();
        }

        if (this.mind != null && this.mind is Brain)
        {
            ((Brain)this.mind).DisposeOfNativeCollections();
        }
    }

    public GameObject closest_food;
    public float last_datapoint_distance_to_food = 0;



    uint frame = 0;
    public void DoFixedUpdate()
    {
        if (!this.initialized) return;

        if (!this.birthed)
        {
            this.birthplace = GetCenterOfMass();
            this.birthplace_forward_vector = Vector3.forward;
            float min_dist = float.MaxValue;
            (closest_food, last_datapoint_distance_to_food) = AnimatArena.GetInstance().GetClosestFoodAndDistance(this.GetCenterOfMass());
            this.birthed = true;
        }

        if(this.body is SoftVoxelRobot)
        {
            this.body.transform.localPosition = new Vector3(0, 0.25f, 0) * this.body.scale / 40.0f;
        }


        if (frame % GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD == 0)
        {
            this.body.UpdateScale();
            this.OperateAnimatOneCycle();
            this.body.UpdateColor(this);

        }




        this.body.age += Time.fixedDeltaTime;

        frame++;
    }

    public void AsexualReproduce()
    {
        if (AnimatArena.GetInstance().current_generation.Count > AnimatArena.MAXIMUM_POPULATION_QUANTITY) return;
        if (this.body.energy < 2 * AnimatBody.OFFSPRING_COST) return;
        Debug.Log("ANIMAT REPRODUCED ASEXUALLY");

        AnimatGenome parent1 = this.genome;
        AnimatGenome offspring_genome = parent1.Clone();
        offspring_genome.brain_genome.Mutate();

        offspring_genome.reproduction_chain = parent1.reproduction_chain + 1;

        Vector3 position = this.GetCenterOfMass();
        position.y = 0;
        Animat offspring = AnimatArena.GetInstance().SpawnGenomeInPosition(offspring_genome, position);
        offspring.was_born = true;
        this.body.energy -= AnimatBody.OFFSPRING_COST;
        this.body.times_reproduced++;
        this.body.times_reproduced_asexually++;
        offspring.genome.momName = this.genome.uniqueName;
    }


    public static void SexualReproduce(Animat mate1, Animat mate2)
    {
        if (AnimatArena.GetInstance().current_generation.Count > AnimatArena.MAXIMUM_POPULATION_QUANTITY) return;
        if (mate1.body.energy < AnimatBody.OFFSPRING_COST
            || mate2.body.energy < AnimatBody.OFFSPRING_COST) return;
        Debug.Log("ANIMATS MATED REPRODUCING SEXUALLY");

        AnimatGenome parent1 = mate1.genome;
        AnimatGenome parent2 = mate2.genome;

        int chain = math.max(parent1.reproduction_chain, parent2.reproduction_chain);

        (AnimatGenome offspring1, AnimatGenome offspring2) = parent1.Reproduce(parent2);
        offspring1.reproduction_chain = chain + 1;
        offspring2.reproduction_chain = chain + 1;
        // offspring1.brain_genome.Mutate();
        // offspring2.brain_genome.Mutate();

        Vector3 position = (mate1.GetCenterOfMass() + mate2.GetCenterOfMass()) / 2;
        position.y = 0;
        Animat offspring;
        if (UnityEngine.Random.Range(0, 2) == 0)
        {
            offspring = AnimatArena.GetInstance().SpawnGenomeInPosition(offspring1, position);
        }
        else
        {
            offspring = AnimatArena.GetInstance().SpawnGenomeInPosition(offspring2, position);
        }
        offspring.was_born = true;



        mate1.body.energy -= AnimatBody.OFFSPRING_COST / 2;
        mate2.body.energy -= AnimatBody.OFFSPRING_COST / 2;

        mate1.body.times_reproduced++;
        mate2.body.times_reproduced++;
        mate1.body.times_reproduced_sexually++;
        mate2.body.times_reproduced_sexually++;

        offspring.genome.momName = mate1.genome.uniqueName;
        offspring.genome.dadName = mate2.genome.uniqueName;
    }

    public void OperateAnimatOneCycle()
    {
        if (!this.initialized) return;

        if (this.mind is BrainCPU braincpu)
        {
            if (!braincpu.update_job_handle.Equals(null))
            {
                braincpu.update_job_handle.Complete();
                braincpu.SwapCurrentAndNextStates();
            }

        }else if (this.mind is BrainGPU braingpu)
        {
            braingpu.next_state_neurons_buffer.GetData(braingpu.current_neurons,0,0, braingpu.sensorymotor_end_idx);
            braingpu.SwapCurrentAndNextStateBuffers();
        }else if (this.mind is NARS nar){
            if (nar.task != null)
            {
                nar.task.Wait();
            }
            nar.SetStoredActivation(NARSGenome.eat_op);
            nar.SetStoredActivation(NARSGenome.mate_op);
            nar.SetStoredActivation(NARSGenome.fight_op);
        }


        this.SpendEnergy();

        this.body.Sense(this);
        this.body.MotorEffect(this);


        if(GlobalConfig.BRAIN_PROCESSING_METHOD != BrainProcessingMethod.Random)
        {
            this.mind.ScheduleWorkingCycle();
        }

    }




    public bool IsGenomeTranscribed()
    {
        if (this.brain_develop_task == null) return false;
        if (this.brain_develop_task.IsFaulted)
        {
            Debug.LogError("ERROR TASK FAULTED " + this.brain_develop_task.Exception);
            return false;
        }
        return this.brain_develop_task.IsCompleted; // return if task is completed
    }




    public const float DRAIN = 0.1f;
    public const float SVR_VOXEL_ENERGY_SCALE_FACTOR = 0.1f;
    public const float ARTICULATED_ROBOT_ENERGY_SCALE_FACTOR = 0.1f;
    public const float GENERAL_MOTOR_ENERGY_SPEND_SCALE_FACTOR = 0.05f;
    public const float FIGHT_ENERGY_SPEND_SCALE_FACTOR = 0.5f;

    internal float got_closer_to_food;

    public void SpendEnergy()
    {
        float energy_spent = 0;
        energy_spent = DRAIN;
        int i = 0;

        //if(this.mind is Brain brain)
        //{
        //    foreach (int motor_idx in brain.motor_neuron_indices)
        //    {
        //        float last_activation = 0;
        //        if (motor_idx_to_last_activation.ContainsKey(motor_idx)) last_activation = motor_idx_to_last_activation[motor_idx];

        //        Neuron neuron = brain.GetNeuronCurrentState(motor_idx);
        //        float current_activation = neuron.activation;
        //        motor_idx_to_last_activation[motor_idx] = current_activation;




        //        float energy_spent_neuron = math.abs(current_activation - last_activation);
        //        //  body-specific motor neurons
        //        if (this.body is SoftVoxelRobot svr)
        //        {
        //            energy_spent_neuron *= (SVR_VOXEL_ENERGY_SCALE_FACTOR / (svr.soft_voxel_object.num_of_voxels * SoftVoxelRobot.NUM_OF_MOTOR_NEURONS));
        //        }
        //        else if (this.body is WheeledRobot robot)
        //        {
        //            energy_spent_neuron *= GENERAL_MOTOR_ENERGY_SPEND_SCALE_FACTOR;
        //        }
        //        else if (this.body is ArticulatedRobot art_robot)
        //        {
        //            energy_spent_neuron *= ARTICULATED_ROBOT_ENERGY_SCALE_FACTOR / (art_robot.body_segments_Drive_abs.Count * ArticulatedRobot.NUM_OF_MOTOR_NEURONS_PER_JOINT);
        //        }


        //        //if (NEATGenome.IsFightingNeuron(neuron.ID))
        //        //    {
        //        //        energy_spent *= FIGHT_ENERGY_SPEND_SCALE_FACTOR;
        //        //    }
        //        //    else
        //        //    {
        //        //        energy_spent *= GENERAL_MOTOR_ENERGY_SPEND_SCALE_FACTOR;
        //        //    }

        //        //}


        //        //energy_spent += energy_spent_neuron;

        //        i++;

        //    }
        //}else if(this.mind is NARS nar)
        //{
        //   // Debug.LogError("error");
        //}

         this.body.energy -= energy_spent;
    }






    public float3 GetCenterOfMass()
    {
        if(this.body == null)
        {
            Debug.LogError("body was null when getting center of mass. returning zero.");
            return float3.zero;
        }
        if(GlobalConfig.BODY_METHOD == BodyMethod.WheeledRobot)
        {
            return this.body._GetCenterOfMass();
        }
        else if (GlobalConfig.BODY_METHOD == BodyMethod.SoftVoxelRobot)
        {
            return (float3)this.transform.position + this.body._GetCenterOfMass();
        }
        else if (GlobalConfig.BODY_METHOD == BodyMethod.ArticulatedRobot)
        {
            return this.body._GetCenterOfMass();
        }
        return float3.zero;
    }

    public Quaternion GetRotation()
    {
        return this.body.GetRotation();
    }



    public void Kill()
    {
        this.DiposeOfAllocatedMemory();
        Destroy(this.gameObject);
    }

    public float GetDisplacementFromBirthplace()
    {
        Vector2 position = this.GetCenterOfMass().xz;
        float distance = Vector2.Distance(this.birthplace.xz, position);

        if(distance > 10)
        {
            int test = 1;
        }
        return distance;

        //Vector2 D = this.GetCenterOfMass().xz - this.birthplace.xz;
    }
    public float GetDistanceTowardsClosestFood()
    {
        float distance = Vector3.Distance(closest_food.transform.position, this.GetCenterOfMass());

        float difference = last_datapoint_distance_to_food - distance; // if it went from larger distance to smaller, give a larger score.
        return difference;

        //Vector2 D = this.GetCenterOfMass().xz - this.birthplace.xz;
    }

    public float GetDisplacementAlongBirthplaceForwardVector()
    {
        Vector3 travelled_vector = this.GetCenterOfMass() - this.birthplace;
        float result = Vector3.Dot(travelled_vector, this.birthplace_forward_vector);
        if (result < 0) result = 0;
        return result;
    }


    internal void ReduceHealth(float activation)
    {
        this.body.health -= activation;
        was_hit += activation;
    }

    static string IntToCustomString(int num)
    {
        // Define the mapping dictionary
        Dictionary<int, char> mapping = new Dictionary<int, char>
        {
            { 0, 'A' }, { 1, 'B' }, { 2, 'C' }, { 3, 'D' }, { 4, 'E' },
            { 5, 'F' }, { 6, 'G' }, { 7, 'H' }, { 8, 'I' }, { 9, 'J' }
        };

        // Create a result string
        string result = "";

        // Map each digit to its corresponding character
        while (num > 0)
        {
            int digit = num % 10; // Get the last digit
            result = mapping[digit] + result; // Prepend the mapped character
            num /= 10; // Remove the last digit
        }

        return result;
    }

    internal float GetDistanceTravelled()
    {
        float distance = 0;
        for (int i = 1; i < this.behavior_characterization_CPU.datapoints.Length; i++)
        {
            var datapoint0 = this.behavior_characterization_CPU.datapoints[i-1];
            var datapoint1 = this.behavior_characterization_CPU.datapoints[i];

            distance += Vector3.Distance(datapoint0.position, datapoint1.position);
        }
        return distance;
    }
}
