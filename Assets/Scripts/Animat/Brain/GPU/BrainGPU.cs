using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

public class BrainGPU : Brain
{


    public static ComputeShader compute_shader_static;
    public ComputeShader compute_shader;
    public int main_kernel;

    public ComputeBuffer current_state_neurons_buffer; // 1-to-1 mapping NeuronID --> neuron
    public ComputeBuffer current_state_synapses_buffer; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    public ComputeBuffer next_state_neurons_buffer; // 1-to-1 mapping NeuronID --> neuron
    public ComputeBuffer next_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    public Neuron[] current_neurons;
   // public Neuron[] current_neurons_regulararray;
 
    public NativeArray<Synapse> current_synapses;



    public const bool RETRIEVE_SYNAPSE_DATA = false;

    public int sensorymotor_end_idx;

    public BrainGPU(NativeArray<Neuron> neurons, NativeArray<Synapse> synapses, int sensorymotor_end_idx, List<int> motor_neuron_indices)
    {
        this.motor_neuron_indices = motor_neuron_indices;
        this.sensorymotor_end_idx = sensorymotor_end_idx;

        // local arrays
        // this.current_neurons = neurons;
        this.current_neurons = neurons.ToArray();
    
        this.current_synapses = synapses;

        // compute buffers
        this.current_state_neurons_buffer = new(neurons.Length, Marshal.SizeOf(typeof(Neuron)));
        this.current_state_synapses_buffer = new(synapses.Length == 0 ? 1 : synapses.Length, Marshal.SizeOf(typeof(Synapse)));
        this.next_state_neurons_buffer = new(this.current_state_neurons_buffer.count, this.current_state_neurons_buffer.stride);
        this.next_state_synapses = new(this.current_state_synapses_buffer.count, this.current_state_synapses_buffer.stride);

        this.current_state_neurons_buffer.SetData(neurons);
        this.current_state_synapses_buffer.SetData(synapses);
        this.next_state_neurons_buffer.SetData(neurons);
        this.next_state_synapses.SetData(synapses);

        if (compute_shader_static == null)
        {
            compute_shader_static = (ComputeShader)Resources.Load("ParallelNeuralUpdateGPU");
        }

        compute_shader = (ComputeShader)GameObject.Instantiate(compute_shader_static);

        this.main_kernel = compute_shader.FindKernel("CSMain");

        neurons.Dispose();

        this.SetBuffersOnGPUVariables();
  

    }





    public void SetBuffersOnGPUVariables()
    {
        compute_shader.SetBuffer(this.main_kernel, "current_state_neurons", this.current_state_neurons_buffer);
        compute_shader.SetBuffer(this.main_kernel, "current_state_synapses", this.current_state_synapses_buffer);
        compute_shader.SetBuffer(this.main_kernel, "next_state_neurons", this.next_state_neurons_buffer);
        compute_shader.SetBuffer(this.main_kernel, "next_state_synapses", this.next_state_synapses);
    }

    public bool dispatching = false;
    public override void ScheduleWorkingCycle()
    {
   
        //todo set the sensory neurons only
        // then, dispatch the proper number of thread groups
        this.current_state_neurons_buffer.SetData(this.current_neurons);

        int remaining_neurons = this.current_state_neurons_buffer.count;

        int i = 0;
        int max_neurons_processed_per_dispatch = GlobalConfig.MAX_NUM_OF_THREAD_GROUPS * GlobalConfig.NUM_OF_GPU_THREADS_PER_THREADGROUP;
        while (remaining_neurons > 0)
        {

            compute_shader.SetInt("index_offset", i * max_neurons_processed_per_dispatch);
            if (remaining_neurons > max_neurons_processed_per_dispatch)
            {
                compute_shader.Dispatch(this.main_kernel, GlobalConfig.MAX_NUM_OF_THREAD_GROUPS, 1, 1);
                remaining_neurons -= max_neurons_processed_per_dispatch;
            }
            else
            {
                compute_shader.Dispatch(this.main_kernel, Mathf.CeilToInt((float)remaining_neurons / GlobalConfig.NUM_OF_GPU_THREADS_PER_THREADGROUP), 1, 1);
                remaining_neurons = 0;
         
            }
            i++;
        }


        //move next state to the current state
        //

       // dispatching = true;
        //var request = AsyncGPUReadback.Request(this.next_state_neurons_buffer, GPUReadNeuronsCallback);

   


      
 
    }

    public void SwapCurrentAndNextStateBuffers()
    {
        (this.current_state_neurons_buffer, this.next_state_neurons_buffer) = (this.next_state_neurons_buffer, this.current_state_neurons_buffer);
        (this.current_state_synapses_buffer, this.next_state_synapses) = (this.next_state_synapses, this.current_state_synapses_buffer);


        SetBuffersOnGPUVariables();
    }


    //private void GPUReadNeuronsCallback(AsyncGPUReadbackRequest request)
    //{
    //    if (request.hasError) throw new Exception("AsyncGPUReadback.RequestIntoNativeArray");
    //    this.current_neurons = request.GetData<Neuron>().ToArray();
    //    dispatching = false;
    //}

    public override void DisposeOfNativeCollections()
    {
        this.current_state_neurons_buffer.Dispose();
        this.current_state_synapses_buffer.Dispose();
        this.next_state_neurons_buffer.Dispose();
        this.next_state_synapses.Dispose();
        //this.current_neurons.Dispose();
        this.current_synapses.Dispose();
    }

    public override int GetNumberOfSynapses()
    {
        if (this.current_state_synapses_buffer == null) return 0;
        return this.current_state_synapses_buffer.count;
    }

    public override int GetNumberOfNeurons()
    {
        if (this.current_state_neurons_buffer == null) return 0;
        return this.current_state_neurons_buffer.count;
    }

    public override Neuron GetNeuronCurrentState(int index)
    {
        //Neuron[] data = new Neuron[1];
        //current_state_neurons.GetData(data, 0, index, 1);
        // return data[0];
        return this.current_neurons[index];
    }


    public override void SetNeuronCurrentState(int index, Neuron neuron)
    {
        if (this.current_neurons == null) return;
        this.current_neurons[index] = neuron;
        /* Neuron[] data = new Neuron[1];
         data[0] = neuron;
         current_state_neurons.SetData(data, 0, index, 1);*/
    }

    public Synapse GetCurrentSynapse(int index)
    {
        //Neuron[] data = new Neuron[1];
        //current_state_neurons.GetData(data, 0, index, 1);
        // return data[0];
        return this.current_synapses[index];
    }




    public void SetCurrentSynapse(int index, Synapse synapse)
    {
        if (this.current_synapses == null) return;
        this.current_synapses[index] = synapse;
        /* Neuron[] data = new Neuron[1];
         data[0] = neuron;
         current_state_neurons.SetData(data, 0, index, 1);*/
    }


    public override void SaveToDisk()
    {
        throw new System.NotImplementedException();
    }

    public override int CountNumberOfHiddenNeurons()
    {
        throw new System.NotImplementedException();
    }
}
