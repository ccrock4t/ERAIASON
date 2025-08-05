using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class BrainCPU : Brain
{
    public NativeArray<Neuron> current_state_neurons; // 1-to-1 mapping NeuronID --> neuron

    public NativeArray<Synapse> current_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.

    public NativeArray<Neuron> next_state_neurons; // 1-to-1 mapping NeuronID --> neuron
    public NativeArray<Synapse> next_state_synapses; // 1-to-many mapping NeuronID --> synapse1, synapse2, synapse3, etc.



    public BrainCPU(NativeArray<Neuron> neurons, NativeArray<Synapse> synapses, List<int> motor_neuron_indices)
    { 
        this.current_state_neurons = neurons;
        this.current_state_synapses = synapses;
        this.next_state_neurons = new(neurons, Allocator.Persistent);
        this.next_state_synapses = new(synapses, Allocator.Persistent);
        this.motor_neuron_indices = motor_neuron_indices;
    }




    public void SwapCurrentAndNextStates()
    {
        //move next state to the current state, to get the motor activations
        NativeArray<Neuron> swap_array = this.current_state_neurons;
        this.current_state_neurons = this.next_state_neurons;
        this.next_state_neurons = swap_array;

        NativeArray<Synapse> swap_synapses = this.current_state_synapses;
        this.current_state_synapses = this.next_state_synapses;
        this.next_state_synapses = swap_synapses;
    }

    public JobHandle update_job_handle;
    public override void ScheduleWorkingCycle()
    {
        ParallelNeuralUpdateCPU job = new()
        {
            current_state_neurons = this.current_state_neurons,
            current_state_synapses = this.current_state_synapses,
            next_state_neurons = this.next_state_neurons,
            next_state_synapses = this.next_state_synapses,
            use_hebb = GlobalConfig.USE_HEBBIAN,
            hebb_rule = GlobalConfig.HEBBIAN_METHOD,
            time = Time.time,
            cpgtype =GlobalConfig.CPG_TYPE
        };
        update_job_handle = job.Schedule(this.next_state_neurons.Length, 128);
    }

    

    // Update is called once per frame
    void Update()
    {
        
    }

    public override void DisposeOfNativeCollections()
    {
        this.update_job_handle.Complete();
        this.current_state_neurons.Dispose();
        this.current_state_synapses.Dispose();
        this.next_state_neurons.Dispose();
        this.next_state_synapses.Dispose();
    }

    public override int GetNumberOfSynapses()
    {
        return this.current_state_synapses.Length;
    }

    public override int GetNumberOfNeurons()
    {
        return this.current_state_neurons.Length;
    }

    public override int CountNumberOfHiddenNeurons()
    {
        int cnt = 0;
        for (int i = 0; i < this.current_state_neurons.Length; i++)
        {
            if (this.current_state_neurons[i].neuron_role == Neuron.NeuronRole.Hidden)
            {
                cnt++;
            }
        }
        return cnt;
    }

    public override void SaveToDisk()
    {
        string[] existing_saves = Directory.GetFiles(path: GlobalConfig.save_file_path, searchPattern: GlobalConfig.save_file_base_name + "*" + save_file_extension);
        int num_files = existing_saves.Length;
        string full_path = GlobalConfig.save_file_path + GlobalConfig.save_file_base_name + num_files.ToString() + save_file_extension;
        Debug.Log("Saving brain to disk: " + full_path);
        StreamWriter data_file;
        data_file = new(path: full_path, append: false);


        BinaryFormatter formatter = new BinaryFormatter();
        object[] objects_to_save = new object[] { this.current_state_neurons.ToArray(), this.current_state_synapses.ToArray() };
        formatter.Serialize(data_file.BaseStream, objects_to_save);
        data_file.Close();
    }

    public static (NativeArray<Neuron>, NativeArray<Synapse>) LoadFromDisk(string filename="")
    {
     
        if (filename == "")
        {
            string[] existing_saves = Directory.GetFiles(path: GlobalConfig.save_file_path, searchPattern: GlobalConfig.save_file_base_name + "*" + save_file_extension);
            int num_files = existing_saves.Length-1;
            filename = GlobalConfig.save_file_base_name + num_files.ToString();
        }


        Neuron[] neuron_array = null;
        Synapse[] synapse_array = null;

        BinaryFormatter formatter = new BinaryFormatter();
        string full_path = GlobalConfig.save_file_path + filename + save_file_extension;
        // loading
        using (FileStream fs = File.Open(full_path, FileMode.Open))
        {
            object obj = formatter.Deserialize(fs);
            // = new object[] { this.current_state_neurons.ToArray(), this.current_state_synapses.ToArray() };
            var newlist = (object[])obj;
            for(int i=0; i < newlist.Length; i++) 
            {
                if (i == 0)
                {
                    neuron_array = (Neuron[])newlist[i];
                }
                else if(i == 1)
                {
                    synapse_array = (Synapse[])newlist[i];
                }
                else
                {
                    Debug.LogWarning("ERROR LOADING BRAIN");
                }
                
            }
        }

        NativeArray<Neuron> native_neuron_array = new(neuron_array.Length, Allocator.Persistent);
        NativeArray<Synapse> native_synapse_array = new(synapse_array.Length, Allocator.Persistent);

        for(int i = 0; i < neuron_array.Length; i++)
        {
            native_neuron_array[i] = neuron_array[i];
        }

        for (int i = 0; i < synapse_array.Length; i++)
        {
            native_synapse_array[i] = synapse_array[i];
        }

        return (native_neuron_array, native_synapse_array);

    }

    public override Neuron GetNeuronCurrentState(int index)
    {
        return this.current_state_neurons[index];
    }

    public override void SetNeuronCurrentState(int index, Neuron neuron)
    {
        //if(neuron.neuron_role == Neuron.NeuronRole.Sensor && GlobalConfig.NEURAL_NETWORK_METHOD == Neuron.NeuronClass.CTRNN)
        //{
        //    float sensor_input = neuron.activation; // was set before coming into this function
        //    float voltage_change = -neuron.voltage;
        //    voltage_change += sensor_input;
        //    var delta = (voltage_change * GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD / neuron.tau_time_constant);
        //    neuron.voltage += delta;
        //    neuron.voltage = math.clamp(neuron.voltage, -1000000, 1000000);
        //    neuron.activation = neuron.RunActivationFunction(neuron.voltage + neuron.bias);
        //}
        this.current_state_neurons[index] = neuron;
    }
}
