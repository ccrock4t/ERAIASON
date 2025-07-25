using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static ArticulatedRobotBodyGenome;
using static BodyGenome;
using static Brain;
using static RayPreview;
using static WorldAutomaton.Elemental;
using CVX_Voxel = System.IntPtr;

public class VisionSensor
{

    const bool draw_sensor_raycasts = true;

    public static Vector2Int eye_dimensions = new (3, 3);


    public static int NUM_OF_RAYCASTS = (VisionSensor.eye_dimensions.x * VisionSensor.eye_dimensions.y);

    public const float MAX_VISION_DISTANCE = 40f;
    const float EAT_RATE = 2;
    public const float ACTION_RANGE = 2f;
    const float EAT_THRESHOLD = 0.1f;
    const float MATE_THRESHOLD = 0.1f;
    const float FIGHT_THRESHOLD = 0.1f;
    const float PICKUP_VOXEL_THRESHOLD = 0.1f;
    public const float PLACE_VOXEL_THRESHOLD = 0.1f;
    public const float ASEXUAL_THRESHOLD = 0.05f;

    public RayPreview ray_preview;
    List<RayPreviewCast> ray_preview_casts = new();
    public void DoRaycastVisionAndMotorHandling(Animat animat)
    {
        float seconds_to_draw_debug_ray = Time.fixedDeltaTime * GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD;

        AnimatBody body = animat.body;
        int old_count = animat.body.number_of_voxels_held;
        //
        // get animat motor activations
        //
        double eat_motor_activation;
        double fight_motor_activation;
        double mate_motor_activation;
        double pickup_voxel_motor_activation;
        double place_voxel_motor_activation;

        if (animat.mind is Brain)
        {
            Brain brain = animat.mind as Brain;
            // get neuron info
            int motor_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.EATING_MOTOR_NEURON_INDEX];
            Neuron motor_neuron = brain.GetNeuronCurrentState(motor_neuron_idx);
            if (motor_neuron.neuron_role != Neuron.NeuronRole.Motor) Debug.LogError("error");
            eat_motor_activation = motor_neuron.activation;


            motor_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.FIGHTING_MOTOR_NEURON_INDEX];
            motor_neuron = brain.GetNeuronCurrentState(motor_neuron_idx);
            if (motor_neuron.neuron_role != Neuron.NeuronRole.Motor) Debug.LogError("error");
            fight_motor_activation = motor_neuron.activation;


            motor_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.MATING_MOTOR_NEURON_INDEX];
            motor_neuron = brain.GetNeuronCurrentState(motor_neuron_idx);
            if (motor_neuron.neuron_role != Neuron.NeuronRole.Motor) Debug.LogError("error");
            mate_motor_activation = motor_neuron.activation;

            motor_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.PICKUP_VOXEL_MOTOR_NEURON];
            motor_neuron = brain.GetNeuronCurrentState(motor_neuron_idx);
            if (motor_neuron.neuron_role != Neuron.NeuronRole.Motor) Debug.LogError("error");
            pickup_voxel_motor_activation = motor_neuron.activation;

            motor_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.PLACE_VOXEL_MOTOR_NEURON];
            motor_neuron = brain.GetNeuronCurrentState(motor_neuron_idx);
            if (motor_neuron.neuron_role != Neuron.NeuronRole.Motor) Debug.LogError("error");
            place_voxel_motor_activation = motor_neuron.activation;
        

        }
        else if (animat.mind is NARS)
        {
            NARS nar = animat.mind as NARS;
            eat_motor_activation = nar.GetGoalActivation(NARSGenome.eat_op);
            fight_motor_activation = nar.GetGoalActivation(NARSGenome.fight_op);
            mate_motor_activation = nar.GetGoalActivation(NARSGenome.mate_op);
            pickup_voxel_motor_activation = 0;
            place_voxel_motor_activation = 0;
            //Debug.LogError("todo");
        }
        else if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.Random)
        {

            eat_motor_activation = UnityEngine.Random.value;
            fight_motor_activation = UnityEngine.Random.value;
            mate_motor_activation = UnityEngine.Random.value;
            pickup_voxel_motor_activation = UnityEngine.Random.value;
            place_voxel_motor_activation = UnityEngine.Random.value; 
            //Debug.LogError("todo");
        }
        else
        {
            Debug.LogError("error");
            return;
        }

        if (eat_motor_activation < EAT_THRESHOLD) eat_motor_activation = 0;
        if (mate_motor_activation < MATE_THRESHOLD) mate_motor_activation = 0;
        if (fight_motor_activation < FIGHT_THRESHOLD) fight_motor_activation = 0;
        if (pickup_voxel_motor_activation < PICKUP_VOXEL_THRESHOLD) pickup_voxel_motor_activation = 0;
        if (place_voxel_motor_activation < PLACE_VOXEL_THRESHOLD) place_voxel_motor_activation = 0;

        //
        //now do raycasting sensors
        //

        Dictionary<GameObject, float> detected_food_to_distance = new();


        NativeArray<RaycastCommand> raycast_commands = new(NUM_OF_RAYCASTS, Allocator.TempJob);
        NativeArray<RaycastHit> raycast_results = new(raycast_commands.Length, Allocator.TempJob);


        int all_layer_mask = (1 << AnimatArena.FOOD_GAMEOBJECT_LAYER)
                | (1 << AnimatArena.OBSTACLE_GAMEOBJECT_LAYER)
                | (1 << AnimatArena.ANIMAT_GAMEOBJECT_LAYER)
                | (1 << AnimatArena.INTERACTABLE_VOXEL_GAMEOBJECT_LAYER);
        //
        // queue up the raycasts
        //
        (Vector3 raycast_position, Vector3 raycast_direction) = body.GetVisionSensorPositionAndDirection();
        
        Vector3 up = body.GetVisionSensorUpDirection();

        Physics.queriesHitBackfaces = true;
        QueryParameters query_params = QueryParameters.Default;
        query_params.layerMask = all_layer_mask;
        query_params.hitTriggers = QueryTriggerInteraction.Collide;


        SetupRaycasts(up, raycast_direction, raycast_position, query_params, raycast_commands);

        //if (ray_preview != null)
        //{ 
        //    ray_preview.UpdateRays(raycast_commands.ToArray());
        //}

        //
        //Execute the batch of raycasts in parallel
        // 
        JobHandle handle = RaycastCommand.ScheduleBatch(raycast_commands, raycast_results, 1, 1, default(JobHandle));
        handle.Complete();

        

        //
        //handle raycast results
        // 
        ConcurrentQueue<(RaycastHit, int, double)> objects_to_update = new(); // raycast object, behavior index, activation

        bool voxel_was_picked = false;
        float max_food_activation = 0;
        float max_animat_activation = 0;
        float max_obstacle_activation = 0;
        ray_preview_casts.Clear();
        for (int r = 0; r < raycast_commands.Length; r++)
        {
            RaycastHit? food_hit = null;
            RaycastHit? animat_hit = null;
            RaycastHit? obstacle_hit = null;
            RaycastHit? pickable_voxel_hit = null;

            float food_activation = 0;
            float animat_activation = 0;
            float obstacle_activation = 0;
            float pickable_voxel_activation = 0;

            RaycastHit raycast_hit = raycast_results[r];
            RaycastCommand raycast_cmd = raycast_commands[r];

            if (raycast_hit.distance != 0)
            {
                // activation = math.pow(((raycast_hit.distance - MAX_VISION_DISTANCE) / MAX_VISION_DISTANCE), 2);
                float closeness = (MAX_VISION_DISTANCE - raycast_hit.distance) / MAX_VISION_DISTANCE;
                //activation += closeness;

                int layer_hit = raycast_hit.collider.gameObject.layer;

                switch (layer_hit)
                {
                    case AnimatArena.FOOD_GAMEOBJECT_LAYER:
                        food_activation = closeness;
                        food_hit = raycast_hit;

                        break;
                    case AnimatArena.ANIMAT_GAMEOBJECT_LAYER:

                        animat_activation = closeness;
                        animat_hit = raycast_hit;

                        break;
                    case AnimatArena.OBSTACLE_GAMEOBJECT_LAYER:

                        obstacle_activation = closeness;
                        obstacle_hit = raycast_hit;

                        if (draw_sensor_raycasts)
                        {
                            ray_preview_casts.Add(new RayPreviewCast(raycast_cmd.from, raycast_cmd.direction,raycast_hit.distance, new Color(closeness, closeness, closeness)));
                        }
                        break;
                    case AnimatArena.INTERACTABLE_VOXEL_GAMEOBJECT_LAYER:

                        pickable_voxel_activation = closeness;
                        pickable_voxel_hit = raycast_hit;

                        if (draw_sensor_raycasts)
                        {
                            ray_preview_casts.Add(new RayPreviewCast(raycast_cmd.from, raycast_cmd.direction,raycast_hit.distance, new Color(closeness, closeness, closeness)));
                        }
                        break;
                    default:
                        Debug.LogError("error " + layer_hit);
                        break;
                }




            }
            else
            {
                // no hit
                if (draw_sensor_raycasts)
                {
                    ray_preview_casts.Add(new RayPreviewCast(raycast_cmd.from, raycast_cmd.direction,MAX_VISION_DISTANCE, Color.black));
                }
                //activation += 0;
            }



            //food

            if (food_hit != null)
            {
                GameObject food = ((RaycastHit)food_hit).transform.gameObject;
                float current_frame_distance = Vector3.Distance(food.transform.position, animat.GetCenterOfMass());
                detected_food_to_distance[food] = current_frame_distance;

                if (((RaycastHit)food_hit).distance < ACTION_RANGE)
                {
                    // close enough to interact
                    if (eat_motor_activation > 0)
                    {
                        if (food_hit == null)
                        {
                            Debug.LogError("error");
                            return;
                        }  
                        objects_to_update.Enqueue(((RaycastHit)food_hit, InitialNEATGenomes.EATING_MOTOR_NEURON_INDEX, food_activation));
                    }

                    if (draw_sensor_raycasts)
                    {
                        var dir = ((RaycastHit)food_hit).transform.position - raycast_position;
                        ray_preview_casts.Add(new RayPreviewCast(raycast_position, dir.normalized, dir.magnitude, new Color(0, (float)eat_motor_activation, 0)));
                    }
                    
                }
                else
                {
                    if (draw_sensor_raycasts)
                    {
                        var dir = ((RaycastHit)food_hit).transform.position - raycast_position;
                        ray_preview_casts.Add(new RayPreviewCast(raycast_position, dir.normalized, dir.magnitude, new Color(food_activation, food_activation, food_activation)));
                    }
                }

            }


            bool enough_energy_to_mate = body.energy > AnimatBody.OFFSPRING_COST / 2;

            // animat mating
            if (animat_hit != null)
            {
                Transform top_level_parent = ((RaycastHit)animat_hit).transform;
                while (top_level_parent.parent != null)
                {
                    top_level_parent = top_level_parent.parent;
                }
                if (((RaycastHit)animat_hit).distance < ACTION_RANGE)
                {
                    if (mate_motor_activation > 0)
                    {
                        if (enough_energy_to_mate)
                        {
                            if (animat_hit == null)
                            {
                                Debug.LogError("error");
                                return;
                            }
                            objects_to_update.Enqueue(((RaycastHit)animat_hit, InitialNEATGenomes.MATING_MOTOR_NEURON_INDEX, mate_motor_activation));
                            if (draw_sensor_raycasts)
                            {
                                Animat other_animat = top_level_parent.GetComponent<Animat>();
                                var dir = (Vector3)other_animat.GetCenterOfMass() - raycast_position;
                                ray_preview_casts.Add(new RayPreviewCast(raycast_position, dir.normalized, dir.magnitude, new Color(0, 0, 1)));
                            }
                        }
                    }

                }
                else
                {
                    if (draw_sensor_raycasts)
                    {
                        Animat other_animat = top_level_parent.GetComponent<Animat>();
                        var dir = (Vector3)other_animat.GetCenterOfMass() - raycast_position;
                        ray_preview_casts.Add(new RayPreviewCast(raycast_position, dir.normalized, dir.magnitude, new Color(animat_activation, animat_activation, animat_activation)));
                    }
                }


                // animat fighting

                if (((RaycastHit)animat_hit).distance < ACTION_RANGE)
                {
                    if (fight_motor_activation > 0)
                    {
                        if (animat_hit == null)
                        {
                            Debug.LogError("error");
                            return;
                        }
                        objects_to_update.Enqueue(((RaycastHit)animat_hit, InitialNEATGenomes.FIGHTING_MOTOR_NEURON_INDEX, fight_motor_activation));
                        if (draw_sensor_raycasts)
                        {
                            Animat other_animat = top_level_parent.GetComponent<Animat>();
                            var dir = (Vector3)other_animat.GetCenterOfMass() - raycast_position;
                            ray_preview_casts.Add(new RayPreviewCast(raycast_position, dir.normalized, dir.magnitude, new Color(1, 0, 0)));
                        }
                    }


                }
                else
                {
                    if (draw_sensor_raycasts)
                    {
                        Animat other_animat = top_level_parent.GetComponent<Animat>();
                        var dir = (Vector3)other_animat.GetCenterOfMass() - raycast_position;
                        ray_preview_casts.Add(new RayPreviewCast(raycast_position, dir.normalized, dir.magnitude, new Color(animat_activation, animat_activation, animat_activation)));
                    }
                }


            }

            if (!voxel_was_picked 
                && pickable_voxel_hit != null 
                && animat.body.number_of_voxels_held < AnimatBody.MAX_VOXELS_HELD)
            {
                RaycastHit voxel_hit = (RaycastHit)pickable_voxel_hit;
                if(voxel_hit.distance < ACTION_RANGE)
                {
                    if (pickup_voxel_motor_activation > 0)
                    {
                        Vector3 hit_point = voxel_hit.point;
                        Vector3 normal = voxel_hit.normal;

                        Vector3 internal_position = hit_point - 0.1f * normal;

                        Vector3Int voxel_position = new((int)internal_position.x, (int)internal_position.y, (int)internal_position.z);

                        Element element = GlobalConfig.world_automaton.GetCellNextState(voxel_position);

                        if (element == Element.Sand) { 
                            GlobalConfig.world_automaton.SetCellNextState(voxel_position, Element.Empty);
                            animat.body.number_of_voxels_held++;
                            voxel_was_picked = true;
                        }

                    }
                }

            }

            /*            obstacle_neuron.activation = max_obstacle_activation;
                        animat_neuron.activation = max_animat_activation;


                        brain.SetNeuronCurrentState(obstacle_neuron_idx, obstacle_neuron);
                        brain.SetNeuronCurrentState(animat_neuron_idx, animat_neuron);
            */

            // Send vision sensory inputs
            if (animat.mind is Brain)
            {
                Brain brain = animat.mind as Brain;
                // food
                var sensorKey = new VisionSensorKey(r, VisionSensorType.Food);
                var neuronID = animat.genome.body_genome.visionSensorKeyToNodeID[sensorKey];
                int sensory_neuron_idx = brain.nodeID_to_idx[neuronID];
                Neuron sensor_neuron = brain.GetNeuronCurrentState(sensory_neuron_idx);
                if (sensor_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
                sensor_neuron.activation = food_activation;
                brain.SetNeuronCurrentState(sensory_neuron_idx, sensor_neuron);

                // animat
                sensorKey = new VisionSensorKey(r, VisionSensorType.Animat);
                neuronID = animat.genome.body_genome.visionSensorKeyToNodeID[sensorKey];
                sensory_neuron_idx = brain.nodeID_to_idx[neuronID];
                sensor_neuron = brain.GetNeuronCurrentState(sensory_neuron_idx);
                if (sensor_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
                sensor_neuron.activation = animat_activation;
                brain.SetNeuronCurrentState(sensory_neuron_idx, sensor_neuron);

                //obstacle
                sensorKey = new VisionSensorKey(r, VisionSensorType.Obstacle);
                neuronID = animat.genome.body_genome.visionSensorKeyToNodeID[sensorKey];
                sensory_neuron_idx = brain.nodeID_to_idx[neuronID];
                sensor_neuron = brain.GetNeuronCurrentState(sensory_neuron_idx);
                if (sensor_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
                sensor_neuron.activation = obstacle_activation;
                brain.SetNeuronCurrentState(sensory_neuron_idx, sensor_neuron);

                if (GlobalConfig.WORLD_TYPE == GlobalConfig.WorldType.VoxelWorld)
                {
                    sensorKey = new VisionSensorKey(r, VisionSensorType.PickableVoxel);
                    neuronID = animat.genome.body_genome.visionSensorKeyToNodeID[sensorKey];
                    sensory_neuron_idx = brain.nodeID_to_idx[neuronID];
                    sensor_neuron = brain.GetNeuronCurrentState(sensory_neuron_idx);
                    if (sensor_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
                    sensor_neuron.activation = pickable_voxel_activation;
                    brain.SetNeuronCurrentState(sensory_neuron_idx, sensor_neuron);
                }
                
            }
            else
            {
                // for NARS average them
            }

            max_food_activation =  math.max(max_food_activation, food_activation);
            max_animat_activation = math.max(max_animat_activation, animat_activation);
            max_obstacle_activation = math.max(max_obstacle_activation, obstacle_activation);

        }



        double food_was_eaten = 0;

        Dictionary<object, bool> object_already_interacted = new();
        foreach (var hit_tuple in objects_to_update)
        {

            RaycastHit hit = hit_tuple.Item1;
            int behavior = hit_tuple.Item2;
            double activation = hit_tuple.Item3;
            if (hit.transform.gameObject.layer == AnimatArena.FOOD_GAMEOBJECT_LAYER)
            {
                // eating food


                double amount_of_food_to_eat = math.abs(eat_motor_activation);
                var food = hit.transform.gameObject.GetComponent<Food>();

         

                if (!object_already_interacted.ContainsKey(food) && amount_of_food_to_eat > 0)
                {
                    amount_of_food_to_eat *= EAT_RATE;
                    food_was_eaten = amount_of_food_to_eat;

                    object_already_interacted[food] = true;

                    if (food.nutrition_remaining > amount_of_food_to_eat)
                    {
                        food.RemoveNutrition((float)amount_of_food_to_eat);
                        body.FoodEaten((float)amount_of_food_to_eat);
                    }
                    else
                    {
                        body.FoodEaten(food.nutrition_remaining);
                        AnimatArena.GetInstance().food_blocks.Remove(hit.transform.gameObject);
                        GameObject.Destroy(hit.transform.gameObject);
                    }
                }


            }
            else if (hit.transform.gameObject.layer == AnimatArena.ANIMAT_GAMEOBJECT_LAYER)
            {
                Transform top_level_parent = ((RaycastHit)hit).transform;
                while (top_level_parent.parent != null)
                {
                    top_level_parent = top_level_parent.parent;
                }
                Animat other_animat = top_level_parent.GetComponent<Animat>();
                if (behavior == InitialNEATGenomes.MATING_MOTOR_NEURON_INDEX)
                {
                    if (!object_already_interacted.ContainsKey(other_animat))
                    {
                        object_already_interacted[other_animat] = true;
                        if (!other_animat.dead)
                        {
                            double other_animat_mate_motor_activation;
                            if(other_animat.mind is NARS)
                            {
                                NARS other_NARS = (NARS)other_animat.mind;
                                other_animat_mate_motor_activation = other_NARS.GetGoalActivation(NARSGenome.mate_op);
                            }
                            else if(other_animat.mind is Brain)
                            {
                                int other_mate_motor_neuron_idx = ((Brain)other_animat.mind).nodeID_to_idx[InitialNEATGenomes.MATING_MOTOR_NEURON_INDEX];
                                Neuron other_mate_motor_neuron = ((Brain)other_animat.mind).GetNeuronCurrentState(other_mate_motor_neuron_idx);
                                if (other_mate_motor_neuron.neuron_role != Neuron.NeuronRole.Motor) Debug.LogError("error");
                                other_animat_mate_motor_activation = other_mate_motor_neuron.activation;
                            }
                            else if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.Random)
                            {
                                other_animat_mate_motor_activation = UnityEngine.Random.value;
                            }
                            else
                            {
                                Debug.LogError("error");
                                return;
                            }


                            if (other_animat_mate_motor_activation > MATE_THRESHOLD)
                            {
                                Animat.SexualReproduce(animat, other_animat);
                            }
                        }
                    }


                }
                else if (behavior == InitialNEATGenomes.FIGHTING_MOTOR_NEURON_INDEX)
                {
                    if (!object_already_interacted.ContainsKey(other_animat))
                    {
                        object_already_interacted[other_animat] = true;
                        other_animat.ReduceHealth((float)math.abs(activation));
                    }
                }
            }




        }


        //  send NARS vision sensations
        if (animat.mind is NARS)
        {
            NARS nar = (NARS)animat.mind;

            //food
            Term food_statement;
            if (max_food_activation == 0)
            {
                food_statement = NARSGenome.food_unseen;
            }
            else if (max_food_activation < ACTION_RANGE)
            {
                food_statement = NARSGenome.food_far;
            }
            else// if (food_activation < CLOSENESS_ACTION_RANGE)
            {
                food_statement = NARSGenome.food_near;
            }

            var judgment = new Judgment(food_statement, new EvidentialValue(), occurrence_time: nar.current_cycle_number);
            nar.SendInput(judgment);

            // animat
            Term animat_statement;
            if (max_animat_activation == 0)
            {
                animat_statement = NARSGenome.animat_unseen;
            }
            else if (max_animat_activation < ACTION_RANGE)
            {
                animat_statement = NARSGenome.animat_far;
            }
            else// if (food_activation < CLOSENESS_ACTION_RANGE)
            {
                animat_statement = NARSGenome.animat_near;
            }

            judgment = new Judgment(animat_statement, new EvidentialValue(), occurrence_time: nar.current_cycle_number);
            nar.SendInput(judgment);

            //if(food_was_eaten > 0)
            //{
            //    judgment = new Judgment(NARSGenome.energy_increase, new EvidentialValue(), occurrence_time: nar.current_cycle_number);
            //    nar.SendInput(judgment);
            //}
          
        }

 
        raycast_results.Dispose();
        raycast_commands.Dispose();

        // now check if the animat placing a voxel

        if (animat.body.number_of_voxels_held > 0)
        {
      
            if (place_voxel_motor_activation > 0)
            {
                Vector3Int next_voxel_position = GetNextVoxel(raycast_position, raycast_direction);
                next_voxel_position = GetNextVoxel(next_voxel_position, raycast_direction);

                if (!GlobalConfig.world_automaton.IsOutOfBounds(next_voxel_position))
                {
                    Element next_voxel_element = GlobalConfig.world_automaton.GetCellNextState(next_voxel_position);

                    if (next_voxel_element == Element.Empty)
                    {
                        GlobalConfig.world_automaton.SetCellNextState(next_voxel_position, Element.Sand);
                        animat.body.number_of_voxels_held--;
                    }
                }

   
             

            }
            

        }

        // update mouth sensor
        if (animat.mind is Brain)
        {
            Brain brain = animat.mind as Brain;
            int mouth_sensory_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.MOUTH_SENSOR];
            Neuron mouth_sensory_neuron = brain.GetNeuronCurrentState(mouth_sensory_neuron_idx);
            if (mouth_sensory_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
            mouth_sensory_neuron.activation = food_was_eaten;
            brain.SetNeuronCurrentState(mouth_sensory_neuron_idx, mouth_sensory_neuron);
        }

        // data stuff
        body.food_eaten_since_last_novelty_datapoint += (float)food_was_eaten;
        body.food_was_seen = max_food_activation;
        animat.body.frames_food_detected += max_food_activation;
        animat.body.total_frames_alive++;

        ray_preview.UpdateRays(ray_preview_casts);
    }

    public static void SetupRaycasts(
        Vector3 up,
        Vector3 raycast_direction,
        Vector3 raycast_position,
        QueryParameters query_params,
        NativeArray<RaycastCommand> raycast_commands)
    {
        int idx = 0;

        float horizontalFOV = 5f; // total width of view in degrees
        float verticalFOV = 5f;   // total height of view in degrees

        int xCount = eye_dimensions.x;
        int yCount = eye_dimensions.y;

        // Build local basis
        Vector3 forward = raycast_direction.normalized;
        Vector3 right = Vector3.Cross(up.normalized, forward).normalized;
        Vector3 adjusted_up = Vector3.Cross(forward, right).normalized;

        for (int y = 0; y < yCount; y++)
        {
            float yPercent = (y / (float)(yCount - 1)) - 0.5f; // from -0.5 to 0.5
            float verticalAngle = yPercent * verticalFOV;

            for (int x = 0; x < xCount; x++)
            {
                float xPercent = (x / (float)(xCount - 1)) - 0.5f; // from -0.5 to 0.5
                float horizontalAngle = xPercent * horizontalFOV;

                // First rotate around local X (right), then Y (up)
                Quaternion rot = Quaternion.AngleAxis(-verticalAngle, right) *
                                 Quaternion.AngleAxis(horizontalAngle, adjusted_up);

                Vector3 rotated_direction = rot * forward;

                // Slightly offset origin for better spacing (optional)
                Vector3 origin = raycast_position
                    + xPercent * 0.1f * right
                    + yPercent * 0.1f * adjusted_up;

                raycast_commands[idx++] = new RaycastCommand(origin, rotated_direction, query_params, MAX_VISION_DISTANCE);
            }
        }

        if (idx != raycast_commands.Length)
        {
            Debug.LogError("Mismatch in raycast count");
        }
    }

    Vector3Int GetNextVoxel(Vector3 ray_position, Vector3 ray_direction)
    {
        // Current voxel
        Vector3Int voxel = new Vector3Int(Mathf.FloorToInt(ray_position.x), Mathf.FloorToInt(ray_position.y), Mathf.FloorToInt(ray_position.z));

        // Step in each direction (+1 or -1)
        Vector3Int step = new Vector3Int(
            ray_direction.x > 0 ? 1 : -1,
            ray_direction.y > 0 ? 1 : -1,
            ray_direction.z > 0 ? 1 : -1
        );

        // Compute tMax (distance to the next voxel boundary in each axis)
        Vector3 tMax = new Vector3(
            ((voxel.x + (ray_direction.x >= 0 ? 1 : 0)) - ray_position.x) / ray_direction.x,
            ((voxel.y + (ray_direction.y >= 0 ? 1 : 0)) - ray_position.y) / ray_direction.y,
            ((voxel.z + (ray_direction.z >= 0 ? 1 : 0)) - ray_position.z) / ray_direction.z
        );

        // Find the smallest tMax to determine which axis the ray crosses first
        if (tMax.x < tMax.y && tMax.x < tMax.z)
            voxel.x += step.x;
        else if (tMax.y < tMax.z)
            voxel.y += step.y;
        else
            voxel.z += step.z;

        return voxel;
    }

}
