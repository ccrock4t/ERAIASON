using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Unity.Collections;
using Color = UnityEngine.Color;

public class NoveltySearchGPU : NoveltySearch
{

    private ComputeShader novelty_search_compute_shader;
    private ComputeShader novelty_search_sort_compute_shader;
    public int novelty_search_main_kernel;
    public int novelty_search_sort_main_kernel;

    //public NativeArray<BehaviorCharacterizationGPU> novelty_archive_array;
    int num_of_entries_in_archive = 0;
    List<BehaviorCharacterizationGPU> novelty_archive_list = new();
    List<BehaviorCharacterizationGPUMetadata> novelty_archive_metadata_list = new();
    List<Color> novelty_archive_datapoints_list = new();

    public ComputeBuffer novelty_archive_datapoints_buffer;
    public ComputeBuffer novelty_archive_metadata;

    public NoveltySearchGPU() : base()
    {
        this.novelty_search_compute_shader = GameObject.Instantiate((ComputeShader)Resources.Load("ParallelNoveltySearchGPU"));
        this.novelty_search_sort_compute_shader = GameObject.Instantiate((ComputeShader)Resources.Load("ParallelNoveltySearchBitonicSort"));
        this.novelty_search_compute_shader.SetFloat("FLOAT_MAX_VALUE", float.MaxValue);
        this.novelty_search_sort_compute_shader.SetFloat("FLOAT_MAX_VALUE", float.MaxValue);
        this.novelty_search_sort_compute_shader.SetInt("k_for_kNN", k_for_kNN);

        this.novelty_search_main_kernel = this.novelty_search_compute_shader.FindKernel("CSMain");
        this.novelty_search_sort_main_kernel = this.novelty_search_sort_compute_shader.FindKernel("CSMain");
    }

    public override void AddToArchive(object behavior)
    {
        BehaviorCharacterizationGPU behaviorGPU = (BehaviorCharacterizationGPU)behavior;

        if (behaviorGPU.datapoints.Length == 0)
        {
            Debug.LogWarning("not adding animat, no behavior datapoints");
            return;
        }
        
        int old_archive_size = this.novelty_archive_list.Count;
        int datapoints_start_idx = this.novelty_archive_datapoints_list.Count;

        BehaviorCharacterizationGPUMetadata metadata = new(index: old_archive_size, start_offset: datapoints_start_idx, behaviorGPU.datapoints.Length);
        this.novelty_archive_list.Add(behaviorGPU);
        this.novelty_archive_metadata_list.Add(metadata);
        this.novelty_archive_datapoints_list.AddRange(behaviorGPU.datapoints);


        if (LIMIT_SIZE)
        {
            if (this.novelty_archive_list.Count > SIZE_LIMIT)
            {
                Debug.LogError("todo");
                this.novelty_archive_list.RemoveAt(0);
            }
        }

        // set on GPU

 
        if (this.novelty_archive_datapoints_buffer == null
            || this.novelty_archive_datapoints_list.Count > this.novelty_archive_datapoints_buffer.count)
        {
            // new size exceeds the current allocated buffer size, so allocate new buffers
            int new_buffer_size;
            if (this.novelty_archive_datapoints_buffer != null)
            {
                new_buffer_size = this.novelty_archive_datapoints_buffer.count * 2;
                while (new_buffer_size < this.novelty_archive_datapoints_list.Count) new_buffer_size *= 2;


            }
            else
            {
                new_buffer_size = this.novelty_archive_datapoints_list.Count;
            }

            if (this.novelty_archive_datapoints_buffer != null) this.novelty_archive_datapoints_buffer.Dispose();
          
            this.novelty_archive_datapoints_buffer = new(new_buffer_size, Marshal.SizeOf(typeof(Color)));
        }
        this.novelty_archive_datapoints_buffer.SetData(this.novelty_archive_datapoints_list, 0, 0, this.novelty_archive_datapoints_list.Count);

        if (this.novelty_archive_metadata == null
            || this.novelty_archive_metadata_list.Count > this.novelty_archive_metadata.count)
        {
            int new_buffer_size;
            if (this.novelty_archive_metadata != null){
                new_buffer_size = this.novelty_archive_metadata.count * 2;
                while (new_buffer_size < this.novelty_archive_metadata_list.Count) new_buffer_size *= 2;
            }
            else {
                new_buffer_size = this.novelty_archive_metadata_list.Count;
            }

            if (this.novelty_archive_metadata != null) this.novelty_archive_metadata.Dispose();
            this.novelty_archive_metadata = new(new_buffer_size, Marshal.SizeOf(typeof(BehaviorCharacterizationGPUMetadata)));
        }

        this.novelty_archive_metadata.SetData(this.novelty_archive_metadata_list, 0, 0, this.novelty_archive_metadata_list.Count);

        Debug.Log("Added to archive, size is now " + this.novelty_archive_list.Count);
    }

 

    public override (bool, float) GetBehaviorNoveltyScoreAndTryAddToArchive(object behavior)
    {
        bool added = false;
       
        
        if (NeedsMoreEntries())
        {
            // just add it
            // we can't calculate its novelty we need more in the archive
            added = true;
            // so just give it a default novelty.
            this.AddToArchive(behavior);
            return (added, initial_novelty);
        }

        //float rnd = UnityEngine.Random.Range(0.0f, 1.0f);
        //if (rnd > chance_to_calc_novelty)
        //{
        //    return (false, 0);
        //}

        float novelty = GetAnimatNoveltyScore(behavior);

        float rnd = UnityEngine.Random.Range(0.0f, 1.0f);
        if (rnd < chance_to_add_to_archive)
        {
            added = true;
            this.AddToArchive(behavior);
        }

       // Debug.Log("Calculated novelty: " + novelty + " for behavior size " + ((BehaviorCharacterizationGPU)behavior).datapoints.Length);
        return (added,novelty);
    }

    public override float GetAnimatNoveltyScore(object behavior_characterization)
    {
        if (this.novelty_archive_list.Count == 0) return chance_to_add_to_archive;

        var single_animat_array = ((BehaviorCharacterizationGPU)behavior_characterization).datapoints;
        if (single_animat_array.Length == 0) return 0;
        ComputeBuffer single_animat_buffer = new(single_animat_array.Length, Marshal.SizeOf(typeof(Color)));
        single_animat_buffer.SetData(single_animat_array);

        ComputeBuffer single_animat_metadata_buffer = new(1, Marshal.SizeOf(typeof(BehaviorCharacterizationGPUMetadata)));
        var metadata = new BehaviorCharacterizationGPUMetadata(0, 0, ((BehaviorCharacterizationGPU)behavior_characterization).datapoints.Length);
        var behavior_metadata = new BehaviorCharacterizationGPUMetadata[] { metadata };
        single_animat_metadata_buffer.SetData(behavior_metadata);

        float novelty = GetSingleAnimatNoveltyScoreFromBuffers(single_animat_buffer, single_animat_metadata_buffer);
        single_animat_buffer.Dispose();
        single_animat_metadata_buffer.Dispose();

        return novelty;
    }

    public float[] GetAnimatNoveltyScoreBatch(List<BehaviorCharacterizationGPU> behavior_characterizations)
    {
        if (this.novelty_archive_list.Count == 0 || behavior_characterizations.Count == 0) return new float[0];

        List<Color> behavior_points = new();
        NativeArray<BehaviorCharacterizationGPUMetadata> behavior_metadata = new(behavior_characterizations.Count,Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
        for(int i=0;i< behavior_characterizations.Count; i++)
        {
            var characterization = behavior_characterizations[i];
            var metadata = new BehaviorCharacterizationGPUMetadata(i, behavior_points.Count, characterization.datapoints.Length);
            behavior_metadata[i] = metadata;
            behavior_points.AddRange(characterization.datapoints);
        }

        ComputeBuffer total_animat_buffer = new(behavior_points.Count, Marshal.SizeOf(typeof(Color)));
        total_animat_buffer.SetData(behavior_points);

        ComputeBuffer total_animat_metadata_buffer = new(behavior_metadata.Length, Marshal.SizeOf(typeof(BehaviorCharacterizationGPUMetadata)));
        total_animat_metadata_buffer.SetData(behavior_metadata.ToArray());

        float[] novelties = GetBatchAnimatNoveltyScoreFromBuffers(total_animat_buffer, total_animat_metadata_buffer, behavior_metadata);
        total_animat_buffer.Dispose();
        total_animat_metadata_buffer.Dispose();
        behavior_metadata.Dispose();

        return novelties;
    }

    public int NextPowerOf2(int n)
    {
        int value = 1;
        while(n > value)
        {
            value *= 2;
        }
        return value;
    }

    ComputeBuffer distances_buffer;


    private float[] GetNovelties(ComputeBuffer animat_buffer, ComputeBuffer animat_table_metadata_buffer)
    {
        // novelty score is the animat's sparsity compared to the archive
        if (this.novelty_archive_list.Count == 0) return new float[0];



        int power_of_2 = NextPowerOf2(this.novelty_archive_metadata_list.Count);
        int distances_buffer_size = power_of_2 * animat_table_metadata_buffer.count;
        if (this.distances_buffer == null || this.distances_buffer.count != distances_buffer_size)
        {
            if(this.distances_buffer != null)
            {
                this.distances_buffer.Dispose();
            }
            this.distances_buffer = new(distances_buffer_size, Marshal.SizeOf(typeof(float)));
            this.novelty_search_compute_shader.SetInt("allocated_archive_size_power_of_two", power_of_2);
            this.novelty_search_sort_compute_shader.SetInt("allocated_archive_size_power_of_two", power_of_2);
        }

        ComputeBuffer novelties_buffer = new(animat_table_metadata_buffer.count, Marshal.SizeOf(typeof(float)));


        DispatchComputeShaderCalculateArchiveDistances(animat_buffer, animat_table_metadata_buffer, distances_buffer);
        DispatchComputeShaderSortAndCalcNovelties(distances_buffer, novelties_buffer);

        float[] novelties = new float[novelties_buffer.count];
        novelties_buffer.GetData(novelties);

        return novelties;
    }

    private void DispatchComputeShaderSortAndCalcNovelties(ComputeBuffer results, ComputeBuffer novelties_buffer)
    {
        novelty_search_sort_compute_shader.SetBuffer(this.novelty_search_sort_main_kernel, "results", results);
        novelty_search_sort_compute_shader.SetBuffer(this.novelty_search_sort_main_kernel, "novelties", novelties_buffer);


        int remaining_items = novelties_buffer.count;

        int i = 0;
        int max_items_processed_per_dispatch = GlobalConfig.MAX_NUM_OF_THREAD_GROUPS * GlobalConfig.NUM_OF_GPU_THREADS_PER_THREADGROUP;
        while (remaining_items > 0)
        {
            this.novelty_search_sort_compute_shader.SetInt("index_offset", i * max_items_processed_per_dispatch);
            
            if (remaining_items > max_items_processed_per_dispatch)
            {
                novelty_search_sort_compute_shader.Dispatch(this.novelty_search_sort_main_kernel, GlobalConfig.MAX_NUM_OF_THREAD_GROUPS, 1, 1);
                remaining_items -= max_items_processed_per_dispatch;
            }
            else
            {
                int num_thread_groups = Mathf.CeilToInt((float)remaining_items / GlobalConfig.NUM_OF_GPU_THREADS_PER_THREADGROUP);
                novelty_search_sort_compute_shader.Dispatch(this.novelty_search_sort_main_kernel, num_thread_groups, 1, 1);
                remaining_items = 0;
            }
            i++;
        }
    }

    private float GetSingleAnimatNoveltyScoreFromBuffers(ComputeBuffer animat_buffer, ComputeBuffer animat_metadata_buffer)
    {
        float[] results = GetNovelties(animat_buffer, animat_metadata_buffer);
        return results[0];
    }


    private float[] GetBatchAnimatNoveltyScoreFromBuffers(ComputeBuffer animat_buffer, ComputeBuffer animat_metadata_buffer, NativeArray<BehaviorCharacterizationGPUMetadata> batch_behavior_metadata)
    {
        float[] results = GetNovelties(animat_buffer, animat_metadata_buffer);
        return results;
    }


    // [BurstCompile]
    //struct GetNoveltyJob : IJobParallelFor
    //{
    //    [ReadOnly] public NativeArray<BehaviorCharacterizationGPUMetadata> batch_behavior_metadata;
    //    [ReadOnly] public NativeArray<float> results;
    //    public NativeArray<float> novelties;
    //    public int archive_size;

    //    public void Execute(int i)
    //    {
    //        // Extract the behavior metadata
    //        var behavior_metadata = batch_behavior_metadata[i];

    //        // Prepare slice for processing
    //        List<float> sorted_slice = new();
    //        // Copy and sort slice
    //        for (int j = 0; j < archive_size; j++)
    //        {
    //            float value = results[archive_size * i + j];
    //            AddSorted(sorted_slice, value);
    //        }
    //        float novelty = GetKNNNovelty(sorted_slice, archive_size);
    //        float novelty2 = GetKNNNovelty(results, i * archive_size, archive_size);
    //        if (novelty != novelty2)
    //        {
    //            Debug.LogError("ERROR NOVELTIES NOT  MATCHING CPU  AND GPU");
    //        }
    //        novelties[i] = novelty;
    //    }
    //}

    // https://stackoverflow.com/questions/3663613/why-is-there-no-sortedlistt-in-net
    public static void AddSorted<T>(List<T> list, T value)
    {
        int x = list.BinarySearch(value);
        list.Insert((x >= 0) ? x : ~x, value);
    }

    // Example placeholder for BehaviorMetadata (replace with actual data structure)
    public struct BehaviorMetadata
    {
        public int someData;
        // Add relevant fields here
    }

    public void DispatchComputeShaderCalculateArchiveDistances(ComputeBuffer animats, ComputeBuffer animats_metadata, ComputeBuffer results)
    {
        novelty_search_compute_shader.SetBuffer(this.novelty_search_main_kernel, "animat_table", animats);
        novelty_search_compute_shader.SetBuffer(this.novelty_search_main_kernel, "animat_table_metadata", animats_metadata);
        novelty_search_compute_shader.SetBuffer(this.novelty_search_main_kernel, "novelty_archive", this.novelty_archive_datapoints_buffer);
        novelty_search_compute_shader.SetBuffer(this.novelty_search_main_kernel, "novelty_archive_metadata", this.novelty_archive_metadata);
        novelty_search_compute_shader.SetBuffer(this.novelty_search_main_kernel, "results", results);

        int remaining_items = results.count;

        this.novelty_search_compute_shader.SetInt("actual_archive_size", this.novelty_archive_list.Count);

        int i = 0;
        int max_items_processed_per_dispatch = GlobalConfig.MAX_NUM_OF_THREAD_GROUPS * GlobalConfig.NUM_OF_GPU_THREADS_PER_THREADGROUP;
        while (remaining_items > 0)
        {
            this.novelty_search_compute_shader.SetInt("index_offset", i * max_items_processed_per_dispatch);
            if (remaining_items > max_items_processed_per_dispatch)
            {
                novelty_search_compute_shader.Dispatch(this.novelty_search_main_kernel, GlobalConfig.MAX_NUM_OF_THREAD_GROUPS, 1, 1);
                remaining_items -= max_items_processed_per_dispatch;
            }
            else
            {
                int num_thread_groups = Mathf.CeilToInt((float)remaining_items / GlobalConfig.NUM_OF_GPU_THREADS_PER_THREADGROUP);
                novelty_search_compute_shader.Dispatch(this.novelty_search_main_kernel, num_thread_groups, 1, 1);
                remaining_items = 0;

            }
            i++;
        }
    }

    public override bool NeedsMoreEntries()
    {
        return this.novelty_archive_list.Count < k_for_kNN;
    }


}
