
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static GlobalConfig;
using static NoveltySearch;
using static WorldAutomaton.Elemental;
using Debug = UnityEngine.Debug;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;


public class AnimatArena : MonoBehaviour
{

    public CPPNGenome high_score_genome;

    public List<Animat> current_generation;

    // reproductive pool
    public AnimatTable objectiveFitnessTable = new(AnimatTable.SortingRule.sorted, AnimatTable.ScoreType.objective_fitness);  // score, genomes
    public AnimatTable noveltyTable = new(AnimatTable.SortingRule.sorted, AnimatTable.ScoreType.novelty);  // score, genomes
    public AnimatTable recentPopulationTable = new(AnimatTable.SortingRule.unsorted, AnimatTable.ScoreType.objective_fitness);  // score, genomes
    private DataAnalyzer data_analyzer;
    private SimulationUserInterface user_interface;
    public NoveltySearch novelty_search;

    public List<GameObject> food_blocks;
    public List<GameObject> obstacle_blocks;

    // prefabs
    [SerializeField]
    GameObject food_prefab;

    [SerializeField]
    GameObject obstacle_prefab;

    [SerializeField]
    TMPro.TextMeshProUGUI user_points_textUI;

    float user_points = 0;

    // dynamic vars
    float high_score;

    // data
    const int MAX_PLACEMENT_ATTEMPTS = 1000;

    // constants

    [System.NonSerialized]
    public int MINIMUM_POPULATION_QUANTITY = 30;
    public const int MAXIMUM_POPULATION_QUANTITY = 100;

    const int FOOD_QUANTITY = 150;

    public const int ANIMAT_GAMEOBJECT_LAYER = 9; // for raycast detectionsS
    public const int FOOD_GAMEOBJECT_LAYER = 10; // for raycast detections
    public const int OBSTACLE_GAMEOBJECT_LAYER = 11; // for raycast detections
    public const int INTERACTABLE_VOXEL_GAMEOBJECT_LAYER = 12; // for raycast detections
    bool LOAD_MODE = false; // Load mode means to load the brain from disk, for testing purposes, rather than to evolve

    // singleton
    static AnimatArena _instance;
    private float behavior_characterization_timer;

    public const float BEHAVIOR_CHARACTERIZATION_RECORD_PERIOD = 1.0f;

    GameObject worldUIcanvas;

    private void Awake()
    {
        _instance = this;
        this.current_generation = new();
    }

    // Start is called before the first frame update
    void Start()
    {
        Time.captureDeltaTime = 1f / GlobalConfig.TARGET_FRAMERATE; // set a target framerate
        JobsUtility.JobWorkerCount = SystemInfo.processorCount - 1; // number of CPU cores minus 1. This prevents Unity from going overthrottling the CPU (https://thegamedev.guru/unity-performance/job-system-excessive-multithreading/)
        Debug.Log("Running with " + JobsUtility.JobWorkerCount + " threads");
        Physics.IgnoreLayerCollision(AnimatArena.ANIMAT_GAMEOBJECT_LAYER, AnimatArena.ANIMAT_GAMEOBJECT_LAYER);

        worldUIcanvas = GameObject.Find("WorldSpaceCanvas");

        // scale the arena
        var arena_container = GameObject.Find("FlatPlaneArena").transform;
        arena_container.position = new Vector3(GlobalConfig.WORLD_DIMENSIONS.x / 2f, 0, GlobalConfig.WORLD_DIMENSIONS.z / 2f);
        var ground = arena_container.Find("Ground");
        var left_wall = arena_container.Find("LeftWall");
        var right_wall = arena_container.Find("RightWall");
        var front_wall = arena_container.Find("FrontWall");
        var back_wall = arena_container.Find("BackWall");

        ground.localScale = new Vector3(GlobalConfig.WORLD_DIMENSIONS.x / 10f, 1, GlobalConfig.WORLD_DIMENSIONS.z / 10f);

        left_wall.localScale = new Vector3(left_wall.localScale.x, left_wall.localScale.y, GlobalConfig.WORLD_DIMENSIONS.z / 10f);
        left_wall.localPosition = new Vector3(-GlobalConfig.WORLD_DIMENSIONS.x /2f, left_wall.localPosition.y, left_wall.localPosition.z);

        right_wall.localScale = new Vector3(right_wall.localScale.x, right_wall.localScale.y, GlobalConfig.WORLD_DIMENSIONS.z / 10f);
        right_wall.localPosition = new Vector3(GlobalConfig.WORLD_DIMENSIONS.x / 2f, right_wall.localPosition.y, right_wall.localPosition.z);

        front_wall.localScale = new Vector3(front_wall.localScale.x, front_wall.localScale.y, GlobalConfig.WORLD_DIMENSIONS.z / 10f);
        front_wall.localPosition = new Vector3(front_wall.localPosition.x, front_wall.localPosition.y, GlobalConfig.WORLD_DIMENSIONS.z / 2f);

        back_wall.localScale = new Vector3(back_wall.localScale.x, back_wall.localScale.y, GlobalConfig.WORLD_DIMENSIONS.z / 10f);
        back_wall.localPosition = new Vector3(back_wall.localPosition.x, back_wall.localPosition.y, -GlobalConfig.WORLD_DIMENSIONS.z / 2f);
        // set vars
        this.high_score = 0;
        this.user_points = 0;

        if (GlobalConfig.novelty_search_processing_method == ProcessingMethod.CPU)
        {
            this.novelty_search = new NoveltySearchCPU();
        }
        else if (GlobalConfig.novelty_search_processing_method == ProcessingMethod.GPU)
        {
            this.novelty_search = new NoveltySearchGPU();
        }


        // spawn food
        this.food_blocks = new();
        for (int i = 0; i < FOOD_QUANTITY; i++)
        {
            SpawnFoodBlockInRandomPosition();
        }



        // spawn animat population
        if (LOAD_MODE)
        {
            // load the brain from disk and place it in animats
            // evolve animats
            for (int i = 0; i < MINIMUM_POPULATION_QUANTITY; i++)
            {
                LoadGenomeAndSpawn();
            }
        }

        var offsprings = GetTestGenomeAnimat();

        foreach (var offspring in offsprings)
        {
            SpawnGenomeInRandomSpot(offspring);
        }

        //initialize UI

        this.data_analyzer = GetComponent<DataAnalyzer>();
        this.user_interface = GetComponent<SimulationUserInterface>();
    }



    public void SpawnFoodBlock(Vector3 position, float nutrition = 1)
    {
        GameObject new_food_block = Instantiate(food_prefab, position, Quaternion.identity);
        new_food_block.layer = FOOD_GAMEOBJECT_LAYER;
        food_blocks.Add(new_food_block);
        new_food_block.GetComponent<Food>().arena = this;
        new_food_block.GetComponent<Food>().ResetFood(nutrition);
        new_food_block.GetComponent<Food>().original_position = position;
    }

    public void SpawnFoodBlockInRandomPosition()
    {
        SpawnFoodBlock(GetRandomPositionForBlock());

    }


    public void ChangeBlockPosition(GameObject block)
    {
        Vector3 position = GetRandomPositionForBlock();
        block.transform.position = position;
        block.GetComponent<Food>().original_position = position;
    }

    public Vector3 GetRandomPositionForBlock()
    {
        Vector3 best_position;
        Vector3 position;
        float best_distance = 0;

        int attempts = 0;

        do
        {
            position = new Vector3(NEATConnection.ThreadSafeSystemRandomRange(10, WORLD_DIMENSIONS.x - 10) + 0.5f,
                0,
                NEATConnection.ThreadSafeSystemRandomRange(10, WORLD_DIMENSIONS.x - 10) + 0.5f
            );
            bool good_position = true;
            float minimum_distance = float.MaxValue;
            foreach (Animat animat in this.current_generation)
            {
                if (animat.body == null) continue;
                float distance = Vector3.Distance(position, animat.GetCenterOfMass());
                if (distance < VisionSensor.ACTION_RANGE*2.5)
                {
                    // bad position, try again
                    good_position = false;

                    minimum_distance = math.min(minimum_distance, distance);

                }
            }
            foreach (GameObject foodblock in this.food_blocks)
            {
                float distance = Vector3.Distance(position, foodblock.transform.position);
                if (distance < VisionSensor.ACTION_RANGE * 5)
                {
                    // bad position, try again
                    good_position = false;
                }
            }


            if (good_position)
            {
                break;
            }
            else
            {

                if (minimum_distance > best_distance)
                {
                    best_distance = minimum_distance;
                    best_position = position;
                }
            }
            attempts++;
        } while (attempts < MAX_PLACEMENT_ATTEMPTS);


        int last_good_y = GlobalConfig.WORLD_DIMENSIONS.y - 1;
        for (int y = GlobalConfig.WORLD_DIMENSIONS.y - 2; y >= 0; y--)
        {
            Vector3Int voxel_position = new((int)position.x, y, (int)position.z);

            Element element = GlobalConfig.world_automaton.GetCellCurrentState(voxel_position);
            if (element != Element.Empty)
            {
                // solid block, place it above here.
                break;
            }
            last_good_y = y;
        }
        position.y = last_good_y;


        return position;
    }

    public Vector3 GetRandomPositionForAnimat()
    {

        Vector3 position;
        bool position_found = false;
        int attempts = 0;
        do
        {
            position = new Vector3(NEATConnection.ThreadSafeSystemRandomRange(10, WORLD_DIMENSIONS.x - 10) + 0.5f,
                0,
                NEATConnection.ThreadSafeSystemRandomRange(10, WORLD_DIMENSIONS.x - 10) + 0.5f
            );



            bool good_position = true;
            foreach (GameObject foodblock in this.food_blocks)
            {
                float distance = Vector3.Distance(position, foodblock.transform.position);
                if (distance < VisionSensor.ACTION_RANGE*4)
                {
                    // bad position, try again
                    good_position = false;
                    break;
                }
            }
            /*   foreach (Animat animat in this.current_generation)
               {
                   float distance = Vector3.Distance(position, animat.GetCenterOfMass());
                   if (distance < VisionSensor.ACTION_RANGE)
                   {
                       // bad position, try again
                       good_position = false;
                       break;
                   }
               }*/

            if (good_position) position_found = true;
            attempts++;
        } while (!position_found && attempts < MAX_PLACEMENT_ATTEMPTS);


        int last_good_y = GlobalConfig.WORLD_DIMENSIONS.y - 1;
        for (int y = GlobalConfig.WORLD_DIMENSIONS.y - 2; y >= 0; y--)
        {
            Vector3Int voxel_position = new((int)position.x, y, (int)position.z);

            Element element = GlobalConfig.world_automaton.GetCellCurrentState(voxel_position);
            if (element != Element.Empty)
            {
                // solid block, place it above here.
                break;
            }
            last_good_y = y;
        }
        position.y = last_good_y;

        if(GlobalConfig.BODY_METHOD != BodyMethod.SoftVoxelRobot)
        {
            position += new Vector3(0, 0.5f, 0);
        }

        return position;
    }

    public static AnimatArena GetInstance()
    {
        return _instance;
    }

    public bool IsOutOfArenaBounds(Vector3 position)
    {
        return
            (position.x < 0 || position.x > WORLD_DIMENSIONS.x)
            || (position.y < -10)
            || (position.z < 0 || position.z > WORLD_DIMENSIONS.z);
    }




    public void FixedUpdate()
    {
        DataAnalyzer.TrackPopulationSnapshot();
        if (this.current_generation.Count < MINIMUM_POPULATION_QUANTITY)
        {
            //Parallel.For(0, MINIMUM_POPULATION_QUANTITY - this.current_generation.Count, i =>
            //{ 
                GenerateNewAnimat();
            //});
            
        }


        for (int animat_idx = 0; animat_idx < this.current_generation.Count; animat_idx++)
        {
            Animat animat = this.current_generation[animat_idx];
            if (!animat.initialized) continue;
            var animat_position = animat.body._GetCenterOfMass();
            bool out_of_bounds = IsOutOfArenaBounds(animat_position);
            // check for death conditions
            if (animat.body.energy <= 0 ||
                animat.body.health <= 0 ||
                animat.body.age > AnimatBody.MAX_AGE ||
                animat.body.Crashed() ||
                out_of_bounds)
            {
                bool ignore_for_reproduction = animat.body.Crashed();
                if (out_of_bounds){
                    animat.body.energy = 0; // dont leave food behind if fell thru mesh
                    ignore_for_reproduction = true;
                    Debug.LogError("animat fell outs of bounds");
                }
                if (animat.body.health > 0) animat.body.energy = 0; // dont leave food behind if didnt die from predation, it spams too much food in the world
                if (animat.body is SoftVoxelRobot soft_voxel_robot)
                {
                    if (soft_voxel_robot.soft_voxel_object.crashed) Debug.LogWarning("Voxelyze crashed, killing Animat");
                    if (!soft_voxel_robot.soft_voxel_object.contains_solid_voxels) Debug.LogWarning("Killing animat, it contained no voxels.");
                }


                KillAnimat(animat_idx, ignore_for_reproduction);
                animat_idx--;
            }
            else
            {
                animat.DoFixedUpdate();


                behavior_characterization_timer -= Time.fixedDeltaTime;
                if (animat.birthed && behavior_characterization_timer < 0)
                {
                    this.novelty_search.RecordBehaviorCharacterizationSnapshot(animat);
                    behavior_characterization_timer = BEHAVIOR_CHARACTERIZATION_RECORD_PERIOD;
                }



                float3 pos = animat.GetCenterOfMass();
                Vector3Int voxel_position = new((int)pos.x, (int)pos.y, (int)pos.z);

                if (!GlobalConfig.world_automaton.IsOutOfBounds(voxel_position))
                {
                    Element element = GlobalConfig.world_automaton.GetCellCurrentState(voxel_position);
                    if (element != Element.Empty)
                    {
                        // animt is stuck underground, move it up
                        if (animat.body is WheeledRobot)
                        {
                            ((WheeledRobot)animat.body).controller.MovePosition(new Vector3(((WheeledRobot)animat.body).controller.position.x,
                                ((WheeledRobot)animat.body).controller.position.y + 1,
                                ((WheeledRobot)animat.body).controller.position.z));
                        }
                        else
                        {
                            Debug.LogError("todo -- teleport other robots up when underground");
                        }
                    }
                }

            }

        }

        if (food_blocks.Count < FOOD_QUANTITY)
        {
            SpawnFoodBlockInRandomPosition();
        }
    }

    public (GameObject, float) GetClosestFoodAndDistance(Vector3 position)
    {
        float min_dist = float.MaxValue;
        GameObject closest_food = null;
        foreach (var food in AnimatArena.GetInstance().food_blocks)
        {
            float distance = Vector3.Distance(food.transform.position, position);

            if (distance < min_dist)
            {
                min_dist = distance;
                closest_food = food;
            }
        }
        return (closest_food, min_dist);
    }

    public float GetAnimatObjectiveFitnessScore(Animat animat)
    {
        //float food_was_seen = animat.body.food_was_seen
        //float displacement = animat.GetDisplacementFromBirthplace();
        //float food_eaten = animat.body.number_of_food_eaten;
        //float times_reproduced = animat.body.times_reproduced;
        //float ratio_of_life_spent_looking_at_food = animat.body.frames_food_detected / animat.body.total_frames_alive; //[0,1]
        //float distance_score = math.min(1.0f, displacement / 20);

        //distance_score *= ratio_of_life_spent_looking_at_food;

        //return distance_score + ((food_eaten / AnimatBody.ENERGY_IN_A_FOOD) * (1 + times_reproduced));


        // Total food eaten
        float food_score = animat.body.number_of_food_eaten / AnimatBody.ENERGY_IN_A_FOOD;
        float reproduction_bonus = math.pow(1f + animat.body.times_reproduced,2);
        float look_score = animat.body.frames_food_detected / animat.body.total_frames_alive;
        float displacement = animat.GetDisplacementFromBirthplace();
        float distance_score = math.min(1.0f, displacement / 20f);
 
        return (distance_score + distance_score * look_score + look_score * food_score + food_score) * reproduction_bonus;
     

        // Reproduction bonus




        //// Reward *progress* toward food based on sensor activation increases
        //// You need to compute this inside the animat update loop:
        ////   if (activation > lastActivation)
        ////       animat.body.approach_reward += activation - lastActivation;
        //

        //// Combine components
        //return
        //    0.2f * distance_score +        // encourages general movement
        //    0.4f * look_score +           // encourages facing food
        //    0.4f * approach_score +       // encourages moving toward food
        //    3.0f * food_score * reproduction_bonus; // strong reward for eating/reproducing
    }

    public void KillAnimat(int i, bool ignore_for_reproduction)
    {
        Animat animat = this.current_generation[i];
        animat.dead = true;

        Vector3 animat_death_position = animat.GetCenterOfMass();

        // animat must die
        this.current_generation.RemoveAt(i);

        float food_eaten = animat.body.number_of_food_eaten;

        if (!ignore_for_reproduction)
        {


            animat.behavior_characterization_CPU = new BehaviorCharacterizationCPU(animat.behavior_characterization_list.ToArray());
            if (GlobalConfig.novelty_search_processing_method == ProcessingMethod.CPU)
            {
                animat.behavior_characterization = animat.behavior_characterization_CPU;
            }
            else if (GlobalConfig.novelty_search_processing_method == ProcessingMethod.GPU)
            {
                animat.behavior_characterization = new BehaviorCharacterizationGPU(animat.behavior_characterization_list_GPU);
            }
            // float distance = animat.GetDistanceTravelled();
            //float displacement = animat.GetDisplacementFromBirthplace();

            if (USE_NOVELTY_SEARCH)
            {
                (bool added_to_archive, float animat_novelty_score) = this.novelty_search.GetBehaviorNoveltyScoreAndTryAddToArchive(animat.behavior_characterization);
                if (added_to_archive) this.noveltyTable.UpdateAllNovelties();
                this.noveltyTable.TryAdd(animat_novelty_score, animat);
            }

            float animat_objective_fitness_score = GetAnimatObjectiveFitnessScore(animat);
            this.objectiveFitnessTable.TryAdd(animat_objective_fitness_score, animat);
            this.recentPopulationTable.TryAdd(animat_objective_fitness_score, animat);


            float high_score_contender = animat_objective_fitness_score;

            if (high_score_contender > this.high_score && !LOAD_MODE)
            {
                Debug.Log("NEW HIGH SCORE: " + high_score_contender + "! Saving its genome to disk.");
                this.high_score = high_score_contender;
                if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.CPPN)
                {
                    Debug.LogError("Error");
                    //((CPPNGenome)animat.genome.brain_genome).SaveToDisk();
                }
                else if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.NEAT)
                {
                    Debug.LogWarning("todo save linear genome to disk");
                }
                else
                {
                    Debug.LogError("error");
                }
                this.user_interface.UpdateHighScoreText(this.high_score);
            }
        }

        //if (animat.body.energy > 0) SpawnFoodBlock(animat_death_position, nutrition: animat.body.energy);

        // if it was carrying voxels,drop them

        int voxels_remaining = animat.body.number_of_voxels_held;
        Vector3Int voxel_position = new((int)animat_death_position.x, 0, (int)animat_death_position.z);
        int attempts = 0;
        while (voxels_remaining > 0 && attempts < 100) {

            voxel_position.x = voxel_position.x % GlobalConfig.WORLD_DIMENSIONS.x;
            voxel_position.z = voxel_position.z % GlobalConfig.WORLD_DIMENSIONS.z;
            for (int y = 0; y < GlobalConfig.WORLD_DIMENSIONS.y; y++)
            {
                if (voxels_remaining == 0) break;
                voxel_position.y = y;

                Element element = GlobalConfig.world_automaton.GetCellCurrentState(voxel_position);
                if (element == Element.Empty)
                {
                    voxels_remaining--;
                    GlobalConfig.world_automaton.SetCellNextState(voxel_position, Element.Sand);
                }
            }

            if (voxels_remaining > 0)
            {
                if(NEATConnection.ThreadSafeSystemRandomRange(0, 2) == 0)
                {
                    voxel_position.x++;
                }
                else
                {
                    voxel_position.z++;
                }


            }
            attempts++;
        }

        if(attempts >= 100)
        {
            int x = 1;
        }



        // clean it up
        animat.Kill();

        // move camera to next animat
        this.user_interface.OnAfterAnimatDied(i);
    }


    void LoadGenomeAndSpawn()
    {
        if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.CPPN)
        {
            Debug.LogError("todo");
            // CPPNGenome genome = CPPNGenome.LoadFromDisk();
            //Animat animat = SpawnGenomeInRandomSpot(genome);
        }
        else
        {
            Debug.LogError("Error");
        }

    }

    void GenerateNewAnimat()
    {
        if (LOAD_MODE)
        {
            LoadGenomeAndSpawn();
            return;
        }

        AnimatGenome[] offsprings;

        int rnd = NEATConnection.ThreadSafeSystemRandomRange(0, 20);

        if (rnd == 0 || this.objectiveFitnessTable.Count() == 0)
        {
            offsprings = GetTestGenomeAnimat(); // generate brand new animat
        }
        else if (rnd >= 1 && rnd < 10) // 1-10
        {
            offsprings = GetExplicitFitnessAnimat(false); // asexual
        }
        else //if (rnd >= 10) // 10-219
        {
            offsprings = GetExplicitFitnessAnimat(true); // sexual
        }

        foreach (var offspring in offsprings)
        {
            //MainThreadDispatcher.RunOnMainThread(() =>
           // {
                SpawnGenomeInRandomSpot(offspring);
           // });
        }
    }

    public AnimatTable GetRandomTable()
    {
        int rnd;
        if (USE_NOVELTY_SEARCH)
        {
            rnd = NEATConnection.ThreadSafeSystemRandomRange(0, 3);
        }
        else
        {
            rnd = NEATConnection.ThreadSafeSystemRandomRange(0, 2);
        }

        if (rnd == 0)
        {
            return objectiveFitnessTable;
        }
        else if (rnd == 1)
        {
            return recentPopulationTable;
        }
        else if (rnd == 2)
        {
            return noveltyTable;
        }
        else
        {
            Debug.LogError("rnd");
            return null;
        }
    }




    AnimatGenome[] GetExplicitFitnessAnimat(bool sexual)
    {
        var table1 = this.GetRandomTable();
        (AnimatGenome parent1, int parent1_idx) = table1.PeekProbabilistic();

        AnimatGenome[] results;
        if (sexual)
        {
            results = new AnimatGenome[2];
            // sexual
            var table2 = this.GetRandomTable();
            int ignore_idx = -1;
            if (table1 == table2) ignore_idx = parent1_idx; // same table, so dont pick the same animat
            (AnimatGenome parent2, int parent2_idx) = table2.PeekProbabilistic(ignore_idx: ignore_idx);

            AnimatGenome offspring1_genome;
            AnimatGenome offspring2_genome;
            if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.CPPN)
            {
                /*generation = math.max(((CPPNGenome)parent1).generation, ((CPPNGenome)parent2).generation);
                (offspring1_genome, offspring2_genomem) = ((CPPNGenome)parent1).Reproduce(((CPPNGenome)parent2));*/
            }
            else if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.NEAT)
            {
                (offspring1_genome, offspring2_genome) = parent1.Reproduce(parent2);
                //offspring1_genome.brain_genome.Mutate();
                //offspring2_genome.brain_genome.Mutate();
            }
            else
            {
                Debug.LogError("error not implemented");
                return null;
            }
            offspring1_genome.momName = parent1.uniqueName;
            offspring1_genome.dadName = parent2.uniqueName;
            offspring2_genome.momName = parent1.uniqueName;
            offspring2_genome.dadName = parent2.uniqueName;
            results[0] = offspring1_genome;
            results[1] = offspring2_genome;
        }
        else
        {
            results = new AnimatGenome[1];
            // asexual
            AnimatGenome cloned_genome = parent1.Clone();
            cloned_genome.momName = parent1.uniqueName;

            cloned_genome.brain_genome.Mutate();
            results[0] = cloned_genome;
        }
        return results;
    }

    AnimatGenome[] GetTestGenomeAnimat()
    {
        BodyGenome body_genome = BodyGenome.CreateTestGenome();
        BrainGenome brain_genome = null;
        try
        {
            if (GlobalConfig.BRAIN_PROCESSING_METHOD == BrainProcessingMethod.NARSCPU)
            {
                brain_genome = new NARSGenome((WheeledRobotBodyGenome)body_genome);
            }
            else
            {
                brain_genome = InitialNEATGenomes.CreateTestGenome(body_genome);
            }

       
        }
        catch (TypeInitializationException ex)
        {
            UnityEngine.Debug.LogError("TypeInitializationException: " + ex.Message);
            if (ex.InnerException != null)
            {
                UnityEngine.Debug.LogError("Inner Exception: " + ex.InnerException.GetType().Name);
                UnityEngine.Debug.LogError("Inner Message: " + ex.InnerException.Message);
                UnityEngine.Debug.LogError("StackTrace: " + ex.InnerException.StackTrace);
            }
        }

        AnimatGenome genome = new(
           brain_genome,
           body_genome,
           0);
        return new AnimatGenome[] { genome };

    }


    public Animat SpawnGenomeInRandomSpot(AnimatGenome genome)
    {
        Vector3 position = GetRandomPositionForAnimat();
        return SpawnGenomeInPosition(genome, position);
    }

    public Animat SpawnGenomeInPosition(AnimatGenome genome, Vector3 position)
    {
        GameObject new_animat_GO = new GameObject("agent");

        position.y += genome.body_genome.GetSpawnHeightOffset();

        new_animat_GO.transform.position = position;
        Animat animat = new_animat_GO.AddComponent<Animat>();

        animat.Initialize(genome);
        this.current_generation.Add(animat);

        return animat;
    }


    private void OnApplicationQuit()
    {
        this.data_analyzer.OnAppQuit();
    }

}
