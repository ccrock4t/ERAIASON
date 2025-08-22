using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine; // for Debug.LogWarning (optional)
using UnityEngine.WSA;
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
        public Dictionary<string, float> scores;
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



    public Dictionary<string, StreamWriter> writers;
    string data_folder;
    public DataToolSocketClient()
    {
        if (GlobalConfig.RECORD_DATA_TO_DISK)
        {
            this.writers = new();
      
   
                // Generate filename with timestamp
                data_folder = "DataTool/";
                data_folder += GlobalConfig.WORLD_TYPE.ToString() + "/";
                data_folder += GlobalConfig.BODY_METHOD.ToString() + "/";
                if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.NARSCPU)
                {
                    data_folder += "NARS/NoLearning/";
                }
                else if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.NeuralNetworkCPU
                    || GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.NeuralNetworkGPU)
                {

                    data_folder += GlobalConfig.NEURAL_NETWORK_METHOD.ToString() + "/";
                    if (GlobalConfig.USE_HEBBIAN)
                    {
                        data_folder += GlobalConfig.HEBBIAN_METHOD.ToString() + "/";
                    }
                    else
                    {
                        data_folder += "NoLearning/";
                    }

                }
                else if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.Random)
                {
                    data_folder += "Random/";
                }

                data_folder += DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "/";
                Directory.CreateDirectory(data_folder);

     
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

    public static ReproductivePoolDatapoint CreateTableDatapoint(
        Dictionary<string, float> scores,
        List<List<float>> hamming_distance_matrix,
        List<AnimatSocketDatapoint> animat_datapoints)
    {
        var datapoint = new ReproductivePoolDatapoint();

        datapoint.scores = scores;
        // Non-float fields get filled explicitly (not from the dict)
        datapoint.hamming_distance_matrix = new List<HammingDistanceMatrix>(hamming_distance_matrix.Count);
        foreach (var row in hamming_distance_matrix)
            datapoint.hamming_distance_matrix.Add(new HammingDistanceMatrix { matrix = row });

        datapoint.animat_datapoints = animat_datapoints;
        return datapoint;
    }



    static JObject ToFlatTable(ReproductivePoolDatapoint t)
    {
        var o = new JObject();

        // 1) Flatten the dictionary so keys like "avg_fitness_score"
        //    appear directly under the table object (no "scores" wrapper).
        if (t.scores != null)
            foreach (var kv in t.scores)
                o[kv.Key] = kv.Value;

        // keep your other arrays as-is
        if (t.hamming_distance_matrix != null)
            o["hamming_distance_matrix"] = JToken.FromObject(t.hamming_distance_matrix);
        if (t.animat_datapoints != null)
            o["animat_datapoints"] = JToken.FromObject(t.animat_datapoints);

        return o;
    }

    public void SendReproductivePoolDatapoint(
        WorldDatapoint world_data,
        ReproductivePoolDatapoint objective_fitness_table,
        ReproductivePoolDatapoint novelty_table,
        ReproductivePoolDatapoint recent_population_table
    )
    {
        // 2) Force top-level order so the first item is WorldData
        var root = new JObject
        {
            ["WorldData"] = JToken.FromObject(world_data),
            ["ObjectiveFitnessEliteTable"] = ToFlatTable(objective_fitness_table),
            ["NoveltyEliteTable"] = ToFlatTable(novelty_table),
            ["RecentPopulationTable"] = ToFlatTable(recent_population_table),
        };

        string json = root.ToString(Formatting.None);
        Debug.Log("DATATOOL (Newtonsoft, flat): " + json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        HttpResponseMessage response = httpClient
            .PostAsync("http://" + serverIP + ":" + port + "/send_json", content).Result;
        string result = response.Content.ReadAsStringAsync().Result;
        Debug.Log("Server response: " + result);
    }

    HashSet<string> processed = new();

    public void WriteToDisk(WorldDatapoint world_data,
        ReproductivePoolDatapoint objective_fitness_table,
        ReproductivePoolDatapoint novelty_table,
        ReproductivePoolDatapoint recent_population_table)
    {
        processed.Clear();
        foreach (var kvp in objective_fitness_table.scores)
        {
            string key = kvp.Key;
            var value = kvp.Value;
            string line;


            int idx = key.IndexOf('_');
            string data_name = (idx >= 0 && idx + 1 < key.Length)
                ? key.Substring(idx + 1)
                : key; // fallback if no underscore

            if (processed.Contains(data_name)) continue;
            processed.Add(data_name);

            if (!writers.ContainsKey(data_name))
            {
                string filename = data_folder;
                filename += data_name;
                filename += ".csv";
                var writer = new StreamWriter(filename);
                if (data_name != "BTCratio")
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


            if (key == "BTCratio")
            {
                line = world_data.born_to_created_ratio.ToString();
            }
            else 
            {
                line = objective_fitness_table.scores["max_" + data_name] 
                    + "," + objective_fitness_table.scores["median_" + data_name] 
                    + "," + objective_fitness_table.scores["avg_" + data_name] 
                    + "," + objective_fitness_table.scores["min_" + data_name];

             
                line += "," + novelty_table.scores["max_" + data_name]
                    + "," + novelty_table.scores["median_" + data_name]
                    + "," + novelty_table.scores["avg_" + data_name]
                    + "," + novelty_table.scores["min_" + data_name];
                

                line += "," + recent_population_table.scores["max_" + data_name]
                     + "," + recent_population_table.scores["median_" + data_name]
                     + "," + recent_population_table.scores["avg_" + data_name]
                     + "," + recent_population_table.scores["min_" + data_name];
            }

            writers[data_name].WriteLine(line);
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

