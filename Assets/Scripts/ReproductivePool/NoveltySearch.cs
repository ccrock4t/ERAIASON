using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class NoveltySearch
{
    public const bool LIMIT_SIZE = false;
    public const int SIZE_LIMIT = 100000;
    public float behavior_characterization_timer = 0;
    public float BEHAVIOR_CHARACTERIZATION_RECORD_PERIOD = 1.5f;
    public const int k_for_kNN = 15; // more neighbors is less strict, similar behaviors to enter the archive; less neighbors is more strict, only very novel behaviors can enter

    public float initial_novelty = 1.0f;
    public const float chance_to_add_to_archive = 0.1f;

    public NoveltySearch()
    {

    }

    public abstract void AddToArchive(object behavior);
    public abstract (bool, float) GetBehaviorNoveltyScoreAndTryAddToArchive(object behavior);
    public abstract float GetAnimatNoveltyScore(object behavior);
    public abstract bool NeedsMoreEntries();

    public void RecordBehaviorCharacterizationSnapshot(Animat a)
    {
    
        BehaviorCharacterizationCPUDatapoint datapoint = new();
  
        float3 birthplace = a.birthplace;
        float3 current_position = a.GetCenterOfMass();
        float x = current_position.x - birthplace.x;
        float y = current_position.y - birthplace.y;
        float z = current_position.z - birthplace.z;
        var offset_from_birthplace = new float3(x, y, z);

        float food_eaten_since_last_datapoint = a.body.food_eaten_since_last_novelty_datapoint;

        datapoint.position = current_position;
        datapoint.offset_from_birthplace = offset_from_birthplace;
        datapoint.food_eaten_so_far = a.body.number_of_food_eaten;
        datapoint.food_interaction = food_eaten_since_last_datapoint; // activation of eating motor neuron which was successful during the most recent timestep
       
        datapoint.food_seen = a.body.food_was_seen; // activation of food vision sensor neuron

        a.behavior_characterization_list.Add(datapoint);

        if (GlobalConfig.novelty_search_processing_method == GlobalConfig.ProcessingMethod.GPU)
        {
            Color gpu_datapoint = new();
            gpu_datapoint.r = 0;// a.body.food_was_seen;
            gpu_datapoint.g = 0;
            //gpu_datapoint.g = a.body.food_was_seen;
            float nz = a.GetCenterOfMass().z;
            if (nz < 0) nz = 0;
            gpu_datapoint.b = nz;


            a.behavior_characterization_list_GPU.Add(gpu_datapoint);
        }

        a.body.food_eaten_since_last_novelty_datapoint = 0;

        float delta = 0;

        a.got_closer_to_food += delta;
    }

    /// <summary>
    /// 
    /// STRUCTS
    /// 
    /// </summary>
    /// 


    public struct BehaviorCharacterizationGPU
    {
        public Color[] datapoints;

        public BehaviorCharacterizationGPU(List<Color> gpu_datas)
        {
            datapoints = gpu_datas.ToArray();
        }

    }

    public struct BehaviorCharacterizationGPUMetadata
    {
        public int index;
        public int start_offset;
        public int behavior_length;
      

        public BehaviorCharacterizationGPUMetadata(int index, int start_offset, int behavior_length)
        {
            this.index = index;
            this.start_offset = start_offset;
            this.behavior_length = behavior_length;
        }

    }

    public struct BehaviorCharacterizationCPU
    {
  
        public BehaviorCharacterizationCPUDatapoint[] datapoints;

        public BehaviorCharacterizationCPU(BehaviorCharacterizationCPUDatapoint[] datas)
        {
            datapoints = datas;
        }

    }


    public struct BehaviorCharacterizationCPUDatapoint
    {
        public float3 offset_from_birthplace;
        public float3 position;
        public float food_interaction;
        public float food_eaten_so_far;
        public float food_seen;
        public float closest_food;
    }


}
