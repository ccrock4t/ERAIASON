using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class DataToolSocketClient
{
    List<(float, AnimatGenome)> animat_data;
    public bool connected_to_socket = false;
    TcpClient client;
    NetworkStream stream;


    [Serializable]
    public class AnimatSocketDatapoint
    {
        public int num_of_neurons;
        public int num_of_synapses;
        public float fitness;
        public string name;
    }

    [Serializable] 
    public class HammingDistanceMatrix
    {
        public List<float> matrix;
    }

    [Serializable]
    public class ReproductivePoolDatapoint
    {
        public float avg_fitness_score;
        public float median_fitness_score;
        public float max_fitness_score;
        public float min_fitness_score;

        public float avg_distance_travelled;
        public float median_distance_travelled;
        public float max_distance_travelled;
        public float min_distance_travelled;

        public float avg_food_eaten;
        public float median_food_eaten;
        public float max_food_eaten;
        public float min_food_eaten;

        public float avg_times_reproduced;
        public float median_times_reproduced;
        public float max_times_reproduced;
        public float min_times_reproduced;

        public float avg_times_reproduced_asexually;
        public float median_times_reproduced_asexually;
        public float max_times_reproduced_asexually;
        public float min_times_reproduced_asexually;

        public float avg_times_reproduced_sexually;
        public float median_times_reproduced_sexually;
        public float max_times_reproduced_sexually;
        public float min_times_reproduced_sexually;

        public float avg_reproduction_chain;
        public float median_reproduction_chain;
        public float max_reproduction_chain;
        public float min_reproduction_chain;

        public float avg_hamming_distance;
        public float median_hamming_distance;
        public float max_hamming_distance;
        public float min_hamming_distance;

        public float avg_generation;
        public float median_generation;
        public float max_generation;
        public float min_generation;

        public float avg_num_of_neurons;
        public float median_num_of_neurons;
        public float max_num_of_neurons;
        public float min_num_of_neurons;

        public float avg_num_of_synapses;
        public float median_num_of_synapses;
        public float max_num_of_synapses;
        public float min_num_of_synapses;


        public float avg_NARS_num_beliefs;
        public float median_NARS_num_beliefs;
        public float max_NARS_num_beliefs;
        public float min_NARS_num_beliefs;


        public float avg_NARS_kValue;
        public float median_NARS_kValue;
        public float max_NARS_kValue;
        public float min_NARS_kValue;


        public float avg_NARS_TValue;
        public float median_NARS_TValue;
        public float max_NARS_TValue;
        public float min_NARS_TValue;

        public List<HammingDistanceMatrix> hamming_distance_matrix;
        public List<AnimatSocketDatapoint> animat_datapoints;
     
    }

    [Serializable]
    public class WorldDatapoint
    {
        public float born_to_created_ratio;
    }

    [Serializable]
    public class SocketMessage
    {
        public WorldDatapoint WorldData;
        public ReproductivePoolDatapoint ObjectiveFitnessEliteTable;
        public ReproductivePoolDatapoint NoveltyEliteTable;
        public ReproductivePoolDatapoint RecentPopulationTable;
    }


    HttpClient httpClient;
    const string serverIP = "127.0.0.1";
    const int port = 8089;

    string[] data_to_save_to_file = new string[]
    {
        "BTCratio",
        "fitness_score",
        "food_eaten",
        "times_reproduced",
        "times_reproduced_asexually",
        "times_reproduced_sexually",
        "reproduction_chain",
        "generation",
        "num_of_neurons",
        "num_of_synapses",
        "NARS_num_beliefs",
        "NARS_kValue",
        "NARS_TValue",
        "hamming_distance",
        "distance_travelled"
    };

    public Dictionary<string, StreamWriter> writers;
    public DataToolSocketClient()
    {
        if (GlobalConfig.RECORD_DATA_TO_DISK)
        {
            this.writers = new();
            foreach (var data_name in data_to_save_to_file)
            {
   
                // Generate filename with timestamp
                string folder = "DataTool/";
                folder += GlobalConfig.WORLD_TYPE.ToString() + "/";
                folder += GlobalConfig.BODY_METHOD.ToString() + "/";
                if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.NARSCPU)
                {
                    folder += "NARS/NoLearning/";
                }
                else if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.NeuralNetworkCPU
                    || GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.NeuralNetworkGPU)
                {

                    folder += GlobalConfig.NEURAL_NETWORK_METHOD.ToString() + "/";
                    if (GlobalConfig.USE_HEBBIAN)
                    {
                        folder += GlobalConfig.HEBBIAN_METHOD.ToString() + "/";
                    }
                    else
                    {
                        folder += "NoLearning/";
                    }

                }
                else if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.Random)
                {
                    folder += "Random/";
                }

                folder += DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "/";
                Directory.CreateDirectory(folder);
                string filename = folder;
                filename += data_name;
                filename += ".csv";
                // Initialize StreamWriter
                var writer = new StreamWriter(filename);

                if(data_name != "BTCratio")
                {
                    var EliteFitnessTableColumns = "EliteFitness Max, EliteFitness Median, EliteFitness Mean, EliteFitness Min";
                    var EliteNoveltyTableColumns = "EliteNovelty Max, EliteNovelty Median, EliteNovelty Mean, EliteNovelty Min";
                    var ContinuousFitnessTableColumns = "ContinuousFitness Max, ContinuousFitness Median, ContinuousFitness Mean, ContinuousFitness Min";
                    writer.WriteLine(EliteFitnessTableColumns
                        + ", " + EliteNoveltyTableColumns
                        + ", " + ContinuousFitnessTableColumns);
                }
                else
                {
                    writer.WriteLine("Ratio");
                }
       

                writers.Add(data_name, writer);
            }

     
        }


        if (GlobalConfig.RECORD_DATA_TO_WEB)
        {
            httpClient = new HttpClient();
        }
       

    }


    enum SOCKET_ID : int
    {
        AVG_REPRODUCTIVE_SCORE = 0
    }

    public static ReproductivePoolDatapoint CreateTableDatapoint(Dictionary<string, float> scores,
        List<List<float>> hamming_distance_matrix,
        List<AnimatSocketDatapoint> animat_datapoints) {

        ReproductivePoolDatapoint datapoint = new();

        datapoint.avg_fitness_score = scores["avg_fitness_score"];
        datapoint.max_fitness_score = scores["max_fitness_score"];
        datapoint.median_fitness_score = scores["median_fitness_score"];
        datapoint.min_fitness_score = scores["min_fitness_score"];

        datapoint.avg_distance_travelled = scores["avg_distance_travelled"];
        datapoint.max_distance_travelled = scores["max_distance_travelled"];
        datapoint.min_distance_travelled = scores["min_distance_travelled"];
        datapoint.median_distance_travelled = scores["median_distance_travelled"];

        datapoint.avg_food_eaten = scores["avg_food_eaten"];
        datapoint.max_food_eaten = scores["max_food_eaten"];
        datapoint.min_food_eaten = scores["min_food_eaten"];
        datapoint.median_food_eaten = scores["median_food_eaten"];

        datapoint.max_times_reproduced = scores["max_times_reproduced"];
        datapoint.min_times_reproduced = scores["min_times_reproduced"];
        datapoint.avg_times_reproduced = scores["avg_times_reproduced"];
        datapoint.median_times_reproduced = scores["median_times_reproduced"];

        datapoint.max_times_reproduced_asexually = scores["max_times_reproduced_asexually"];
        datapoint.min_times_reproduced_asexually = scores["min_times_reproduced_asexually"];
        datapoint.avg_times_reproduced_asexually = scores["avg_times_reproduced_asexually"];
        datapoint.median_times_reproduced_asexually = scores["median_times_reproduced_asexually"];

        datapoint.max_times_reproduced_sexually = scores["max_times_reproduced_sexually"];
        datapoint.min_times_reproduced_sexually = scores["min_times_reproduced_sexually"];
        datapoint.avg_times_reproduced_sexually = scores["avg_times_reproduced_sexually"];
        datapoint.median_times_reproduced_sexually = scores["median_times_reproduced_sexually"];

        datapoint.max_reproduction_chain = scores["max_reproduction_chain"];
        datapoint.min_reproduction_chain = scores["min_reproduction_chain"];
        datapoint.avg_reproduction_chain = scores["avg_reproduction_chain"];
        datapoint.median_reproduction_chain = scores["median_reproduction_chain"];

        datapoint.avg_hamming_distance = scores["avg_hamming_distance"];
        datapoint.median_hamming_distance = scores["median_hamming_distance"];
        datapoint.max_hamming_distance = scores["max_hamming_distance"];
        datapoint.min_hamming_distance = scores["min_hamming_distance"];

        datapoint.avg_generation = scores["avg_generation"];
        datapoint.max_generation = scores["max_generation"];
        datapoint.min_generation = scores["min_generation"];
        datapoint.median_generation = scores["median_generation"];

        datapoint.avg_num_of_neurons = scores["avg_num_of_neurons"];
        datapoint.max_num_of_neurons = scores["max_num_of_neurons"];
        datapoint.min_num_of_neurons = scores["min_num_of_neurons"];
        datapoint.median_num_of_neurons = scores["median_num_of_neurons"];

        datapoint.avg_num_of_synapses = scores["avg_num_of_synapses"];
        datapoint.max_num_of_synapses = scores["max_num_of_synapses"];
        datapoint.min_num_of_synapses = scores["min_num_of_synapses"];
        datapoint.median_num_of_synapses = scores["median_num_of_synapses"];

        datapoint.avg_NARS_num_beliefs = scores["avg_NARS_num_beliefs"];
        datapoint.max_NARS_num_beliefs = scores["max_NARS_num_beliefs"];
        datapoint.min_NARS_num_beliefs = scores["min_NARS_num_beliefs"];
        datapoint.median_NARS_num_beliefs = scores["median_NARS_num_beliefs"];


        datapoint.avg_NARS_kValue = scores["avg_NARS_kValue"];
        datapoint.max_NARS_kValue = scores["max_NARS_kValue"];
        datapoint.min_NARS_kValue = scores["min_NARS_kValue"];
        datapoint.median_NARS_kValue = scores["median_NARS_kValue"];


        datapoint.avg_NARS_TValue = scores["avg_NARS_TValue"];
        datapoint.max_NARS_TValue = scores["max_NARS_TValue"];
        datapoint.min_NARS_TValue = scores["min_NARS_TValue"];
        datapoint.median_NARS_TValue = scores["median_NARS_TValue"];



        datapoint.hamming_distance_matrix = new();
        foreach (var row in hamming_distance_matrix)
        {
            HammingDistanceMatrix matrix_row = new();
            matrix_row.matrix = row;
            datapoint.hamming_distance_matrix.Add(matrix_row);
        }

        datapoint.animat_datapoints = animat_datapoints;

        return datapoint;
    }

    int MAX_DATA_SEND_SIZE = 40000;
    public void SendReproductivePoolDatapoint(
        WorldDatapoint world_data,
        ReproductivePoolDatapoint objective_fitness_table,
        ReproductivePoolDatapoint novelty_table,
        ReproductivePoolDatapoint recent_population_table
        )
    {

        SocketMessage datapoint = new();

        
        datapoint.WorldData = world_data;

        datapoint.ObjectiveFitnessEliteTable = objective_fitness_table;
        datapoint.NoveltyEliteTable = novelty_table;
        datapoint.RecentPopulationTable = recent_population_table;

        Debug.Log("DATATOOL: Converting data to json ");
        string json = JsonUtility.ToJson(datapoint);

     
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = httpClient.PostAsync("http://" + serverIP + ":" + port + "/send_json", content).Result; // Blocking call
        string result = response.Content.ReadAsStringAsync().Result; // Blocking read



    }


    public void WriteToDisk(WorldDatapoint world_data,
        Dictionary<string,float> objective_fitness_table,
        Dictionary<string, float> novelty_table,
        Dictionary<string, float> recent_population_table)
    {
 
        foreach (var kvp in this.writers)
        {
            string key = kvp.Key;
            var value = kvp.Value;
            string line;
            if(key == "BTCratio")
            {
                line = world_data.born_to_created_ratio.ToString();
            }
            else 
            {
                line = objective_fitness_table["max_" + key] 
                    + "," + objective_fitness_table["median_" + key] 
                    + "," + objective_fitness_table["avg_" + key] 
                    + "," + objective_fitness_table["min_" + key];
                line += "," + novelty_table["max_" + key]
                     + "," + novelty_table["median_" + key]
                     + "," + novelty_table["avg_" + key]
                     + "," + novelty_table["min_" + key];
                line += "," + recent_population_table["max_" + key]
                     + "," + recent_population_table["median_" + key]
                     + "," + recent_population_table["avg_" + key]
                     + "," + recent_population_table["min_" + key];
            }

            value.WriteLine(line);
        }


    }

    internal void OnAppQuit()
    {
        foreach (var w in this.writers)
        {
            w.Value.Close();
        }
        httpClient.Dispose();
    }
}
