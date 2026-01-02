using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Localization.Tables;
using static DataToolSocketClient;
using static NARSGenome;

public class DataAnalyzer : MonoBehaviour
{

    StreamWriter data_file;
    StreamWriter best_nars_data_file;

    // data variables
    DataToolSocketClient data_tool_socket_client;

    // handles
    static string data_folder = Path.Join(Path.Join(Application.dataPath, "ExperimentDataFiles"), "AnimatArena");
    string data_filename = Path.Join(data_folder, "arena_score_data.txt");
    const int WRITE_DATA_TO_FILE_TIMER = 400; //  50 is once per second, 100 is once per 2 seconds, etc.
    int write_data_timer = 0;
    private Comparer<(float, AnimatGenome)> ascending_score_comparer;

    Queue<AnimatTable[]> task_queue = new();
    AutoResetEvent itemAdded = new AutoResetEvent(false);
    int update_num = 0;

    string best_nars_filename = "BESTNARS.csv";

    public void Start()
    {
        this.ascending_score_comparer = Comparer<(float, AnimatGenome)>.Create((x, y) =>
        {
            int result = x.Item1.CompareTo(y.Item1);
            if (result == 0) return 1;
            else return result;
        });
        this.write_data_timer = 0;
        if (GlobalConfig.RECORD_DATA_TO_DISK)
        {
            WriteColumnHeader();
        }
        if (GlobalConfig.RECORD_DATA_TO_WEB || GlobalConfig.RECORD_DATA_TO_DISK)
        {
            // connect to data tool
            this.data_tool_socket_client = new();
        }

        if (GlobalConfig.RECORD_BEST_NARS_AGENT_DATA)
        {
           
            if (File.Exists(best_nars_filename))
            {
                File.Delete(best_nars_filename);
            }
            best_nars_data_file = File.CreateText(best_nars_filename);
            List<string> row = new();

            for (int p = 0; p < PersonalityParameters.GetParameterCount(); p++)
            {
                var parameter_name = NARSGenome.PersonalityParameters.GetName(p);
                row.Add(parameter_name);
            }
            // Join into CSV row
            string line = string.Join(",", row);

            // Append to the file
            best_nars_data_file.WriteLine(line);
            best_nars_data_file.Flush(); // ensure it’s written immediately
        }


    }

    void FixedUpdate()
    {
        if (!GlobalConfig.RECORD_DATA_TO_WEB && !GlobalConfig.RECORD_DATA_TO_DISK) return;
        if (write_data_timer < WRITE_DATA_TO_FILE_TIMER)
        {
            write_data_timer++;
        }

        if (write_data_timer >= WRITE_DATA_TO_FILE_TIMER)
        {
            write_data_timer = 0;

  


        }

    }

    public void WriteCSV()
    {
        Debug.Log("trying datatool update #" + update_num);
        SendDataToGUIAndWriteToFile();
        //task_queue.Enqueue(tables);
        //itemAdded.Set();
        update_num++;
    }

    enum ReproductionTables
    {
        FitnessHallOfFame,
        NoveltyHallOfFame,
        RecentPopulation
    }

    // Add this to a class field or tracking system:
    private static int populationSum = 0;
    private static int populationSamples = 0;
    private static int birthsThisWindow = 0;

    // Call this *every frame* or at frequent intervals to track averages
    public static void TrackPopulationSnapshot()
    {
        int currentPopulation = AnimatArena.GetInstance().current_generation.Count;
        populationSum += currentPopulation;
        populationSamples++;
    }

    // Call this every time a new animat is born
    public static void RegisterBirth()
    {
        birthsThisWindow++;
    }

    // Call this every 15 seconds
    public static float ComputeStandardizedBirthRate()
    {
        // Prevent divide by zero
        if (populationSamples == 0) return 0;

        float averagePopulation = (float)populationSum / populationSamples;
        float intervalSeconds = WRITE_DATA_TO_FILE_TIMER * Time.fixedDeltaTime;

        float standardizedBirthRate = (birthsThisWindow / averagePopulation) * (1000f / intervalSeconds);

        // Store or print your result

        // Reset for next window
        populationSum = 0;
        populationSamples = 0;
        birthsThisWindow = 0;

        return standardizedBirthRate;
    }

    int data_written_count = 0;

    private void SendDataToGUIAndWriteToFile()
    {
        if(data_written_count == 241)
        {
            Application.Quit();
        }
        data_written_count++;

        Debug.Log("DATATOOL: Preparing data");
        if (GlobalConfig.RECORD_DATA_TO_DISK && data_file == null)
        {
            Debug.LogError("No data file write stream.");
        }

        if (GlobalConfig.RECORD_BEST_NARS_AGENT_DATA && GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.NARSCPU)
        {
            AnimatTable.TableEntry? best = AnimatArena.GetInstance().objectiveFitnessTable.GetBest();
            if(best != null)
            {
                var best_nar_genome = (NARSGenome)best?.data.genome.brain_genome;
                // Collect parameter values into a row
                List<string> row = new();
                for (int p = 0; p < PersonalityParameters.GetParameterCount(); p++)
                {
                    // Assuming you have an instance called best_params holding the current values:



                    float value = best_nar_genome.personality_parameters.Get(p);

                    // Escape commas if needed, otherwise just ToString
                    row.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                // Join into CSV row
                string line = string.Join(",", row);

                // Append to the file
                best_nars_data_file.WriteLine(line);
                best_nars_data_file.Flush(); // ensure it’s written immediately
            }

        }


        ReproductivePoolDatapoint[] table_datapoints = new ReproductivePoolDatapoint[3];


        // calculate world data
        DataToolSocketClient.WorldDatapoint world_data = new();

        var arena = AnimatArena.GetInstance();

        world_data.born_to_created_ratio = ComputeStandardizedBirthRate();

        // now calculate Table data

        var elite_fitness_table = arena.objectiveFitnessTable.Clone();
        var elite_novelty_table = arena.noveltyTable.Clone();
        var continuous_fitness_table = arena.recentPopulationTable.Clone();

        if (data_update_task != null && !data_update_task.IsCompleted)
        {
            data_update_task.Wait();
        }
        data_update_task = Task.Run(() =>
        {
            AnimatTable[] tables = new AnimatTable[3];

            tables[(int)ReproductionTables.FitnessHallOfFame] = elite_fitness_table;
            tables[(int)ReproductionTables.NoveltyHallOfFame] = elite_novelty_table;
            tables[(int)ReproductionTables.RecentPopulation] = continuous_fitness_table;


            Dictionary<string, float> elite_fitness_table_data = null;
            Dictionary<string, float> elite_novelty_table_data = null;
            Dictionary<string, float> continuous_fitness_table_data = null;

            //Parallel.For(0, tables.Length, i =>
           for(int i=0;i < tables.Length; i++)
            {
                var elite_table = tables[i];
                if (elite_table == null) continue;

                Dictionary<string, List<float>> medians = new();
                medians["fitness_score"] = new();
                medians["distance_travelled"] = new();
                medians["food_eaten"] = new();
                medians["times_reproduced"] = new();
                medians["times_reproduced_asexually"] = new();
                medians["times_reproduced_sexually"] = new();
                medians["hamming_distance"] = new();
                medians["generation"] = new();
                medians["reproduction_chain"] = new();
                medians["num_of_neurons"] = new();
                medians["num_of_synapses"] = new();
                medians["NARS_num_beliefs"] = new();

                // write to file
                float max_distance = 0;
                float min_distance = float.MaxValue;
                float avg_distance = 0;
                float median_distance = 0;
                float total_distance = 0;


                float max_food_eaten = 0;
                float min_food_eaten = float.MaxValue;
                float avg_food_eaten = 0;
                float median_food_eaten = 0;
                float total_food_eaten = 0;


                float max_times_reproduced = 0;
                float min_times_reproduced = float.MaxValue;
                float avg_times_reproduced = 0;
                float median_times_reproduced = 0;
                float total_times_reproduced = 0;

                float max_times_reproduced_asexually = 0;
                float min_times_reproduced_asexually = float.MaxValue;
                float avg_times_reproduced_asexually = 0;
                float median_times_reproduced_asexually = 0;
                float total_times_reproduced_asexually = 0;

                float max_times_reproduced_sexually = 0;
                float min_times_reproduced_sexually = float.MaxValue;
                float avg_times_reproduced_sexually = 0;
                float median_times_reproduced_sexually = 0;
                float total_times_reproduced_sexually = 0;

                float max_reproductive_score = 0;
                float min_reproductive_score = float.MaxValue;
                float avg_reproductive_score = 0;
                float median_reproductive_score = 0;
                float total_reproductive_score = 0;

                float max_reproduction_chain = 0;
                float min_reproduction_chain = float.MaxValue;
                float avg_reproduction_chain = 0;
                float median_reproduction_chain = 0;
                float total_reproduction_chain = 0;


                float max_generation = 0;
                float min_generation = float.MaxValue;
                float avg_generation = 0;
                float median_generation = 0;
                float total_generation = 0;

                float total_num_of_neurons = 0;
                float avg_num_of_neurons = 0;
                float median_num_of_neurons = 0;
                float max_num_of_neurons = 0;
                float min_num_of_neurons = float.MaxValue;

                float total_num_of_synapses = 0;
                float avg_num_of_synapses = 0;
                float median_num_of_synapses = 0;
                float max_num_of_synapses = 0;
                float min_num_of_synapses = float.MaxValue;


                float total_NARS_num_beliefs = 0;
                float avg_NARS_num_beliefs = 0;
                float median_NARS_num_beliefs = 0;
                float max_NARS_num_beliefs = 0;
                float min_NARS_num_beliefs = float.MaxValue;

                Dictionary<string, float> NARS_personality_value_statistics = new();

                for (int p = 0; p < PersonalityParameters.GetParameterCount(); p++)
                {
                    var parameter_name = NARSGenome.PersonalityParameters.GetName(p);
                    NARS_personality_value_statistics.Add("total_NARS_" + parameter_name, 0);
                    NARS_personality_value_statistics.Add("avg_NARS_" + parameter_name, 0);
                    NARS_personality_value_statistics.Add("median_NARS_" + parameter_name, 0);
                    NARS_personality_value_statistics.Add("max_NARS_" + parameter_name, 0);
                    NARS_personality_value_statistics.Add("min_NARS_" + parameter_name, float.MaxValue);
                    medians.Add("NARS_" + parameter_name, new());
                }


                float max_hamming_distance = 0;
                float min_hamming_distance = float.MaxValue;
                float avg_hamming_distance = 0;
                float median_hamming_distance = 0;

                List<List<float>> hamming_distance_matrix = new();
                List<AnimatSocketDatapoint> animat_datapoints = new();


                for(int k=0; k<elite_table.table.Count; k++)
                {
                    var entry = elite_table.table[k];
                    AnimatData data = entry.data;

                    float score = entry.score;
                    total_reproductive_score += score;
                    max_reproductive_score = math.max(max_reproductive_score, score);
                    min_reproductive_score = math.min(min_reproductive_score, score);
                    medians["fitness_score"].Add(score);

                    float distance = data.displacement;
                    total_distance += distance;
                    max_distance = math.max(max_distance, distance);
                    min_distance = math.min(min_distance, distance);
                    medians["distance_travelled"].Add(distance);

                    float food_eaten = data.food_eaten;
                    total_food_eaten += food_eaten;
                    max_food_eaten = math.max(max_food_eaten, food_eaten);
                    min_food_eaten = math.min(min_food_eaten, food_eaten);
                    medians["food_eaten"].Add(food_eaten);

                    int times_reproduced = data.times_reproduced;
                    total_times_reproduced += times_reproduced;
                    max_times_reproduced = math.max(max_times_reproduced, times_reproduced);
                    min_times_reproduced = math.min(min_times_reproduced, times_reproduced);
                    medians["times_reproduced"].Add(times_reproduced);

                    int times_reproduced_asexually = data.times_reproduced_asexually;
                    total_times_reproduced_asexually += times_reproduced_asexually;
                    max_times_reproduced_asexually = math.max(max_times_reproduced_asexually, times_reproduced_asexually);
                    min_times_reproduced_asexually = math.min(min_times_reproduced_asexually, times_reproduced_asexually);
                    medians["times_reproduced_asexually"].Add(times_reproduced_asexually);

                    int times_reproduced_sexually = data.times_reproduced_sexually;
                    total_times_reproduced_sexually += times_reproduced_sexually;
                    max_times_reproduced_sexually = math.max(max_times_reproduced_sexually, times_reproduced_sexually);
                    min_times_reproduced_sexually = math.min(min_times_reproduced_sexually, times_reproduced_sexually);
                    medians["times_reproduced_sexually"].Add(times_reproduced_sexually);

                    int reproduction_chain = data.reproduction_chain;
                    total_reproduction_chain += reproduction_chain;
                    max_reproduction_chain = math.max(max_reproduction_chain, reproduction_chain);
                    min_reproduction_chain = math.min(min_reproduction_chain, reproduction_chain);
                    medians["reproduction_chain"].Add(reproduction_chain);

                    int generation = data.generation;
                    total_generation += generation;
                    max_generation = math.max(max_generation, generation);
                    min_generation = math.min(min_generation, generation);
                    medians["generation"].Add(generation);

                    int num_of_neurons = data.num_of_neurons;
                    total_num_of_neurons += num_of_neurons;
                    max_num_of_neurons = math.max(max_num_of_neurons, num_of_neurons);
                    min_num_of_neurons = math.min(min_num_of_neurons, num_of_neurons);
                    medians["num_of_neurons"].Add(num_of_neurons);

                    int num_of_synapses = data.num_of_synapses;
                    total_num_of_synapses += num_of_synapses;
                    max_num_of_synapses = math.max(max_num_of_synapses, num_of_synapses);
                    min_num_of_synapses = math.min(min_num_of_synapses, num_of_synapses);
                    medians["num_of_synapses"].Add(num_of_synapses);

                    int NARS_num_beliefs = data.NARS_num_beliefs;
                    total_NARS_num_beliefs += NARS_num_beliefs;
                    max_NARS_num_beliefs = math.max(max_NARS_num_beliefs, NARS_num_beliefs);
                    min_NARS_num_beliefs = math.min(min_NARS_num_beliefs, NARS_num_beliefs);
                    medians["NARS_num_beliefs"].Add(NARS_num_beliefs);

                    if(data.NARS_Parameters != null)
                    {
                        for (int p = 0; p < PersonalityParameters.GetParameterCount(); p++)
                        {
                            var parameter_name = NARSGenome.PersonalityParameters.GetName(p);
                            float NARS_value = data.NARS_Parameters[parameter_name];
                            medians["NARS_" + parameter_name].Add(NARS_value);
                            NARS_personality_value_statistics["total_NARS_" + parameter_name] += NARS_value;
                            NARS_personality_value_statistics["max_NARS_" + parameter_name] = math.max(NARS_personality_value_statistics["max_NARS_" + parameter_name], NARS_value);
                            NARS_personality_value_statistics["min_NARS_" + parameter_name] = math.min(NARS_personality_value_statistics["min_NARS_" + parameter_name], NARS_value);
                        }
                    }

                AnimatSocketDatapoint animat_datapoint = new();
                    animat_datapoint.fitness = score;
                    animat_datapoint.num_of_synapses = num_of_synapses;
                    animat_datapoint.num_of_neurons = num_of_neurons;
                    animat_datapoint.name = data.name;
                    animat_datapoints.Add(animat_datapoint);


                    //List<float> hamming_distances = new();
                    for (int j = k; j < elite_table.table.Count; j++)
                    {
                        var entry2 = elite_table.table[j];
                        float score2 = entry2.score;
                        AnimatData data2 = entry2.data;
                        BrainGenome genome1 = data.genome.brain_genome;
                        BrainGenome genome2 = data2.genome.brain_genome;
                        float hamming_distance = 0;
                        if (genome1 != genome2)
                        {
                            hamming_distance = 0;// genome1.CalculateHammingDistance(genome2);
                        }
                        avg_hamming_distance += hamming_distance;
                        max_hamming_distance = math.max(max_hamming_distance, hamming_distance);
                        min_hamming_distance = math.min(min_hamming_distance, hamming_distance);
                        medians["hamming_distance"].Add(hamming_distance);
                        // hamming_distances.Add(hamming_distance);
                    }
                    // hamming_distance_matrix.Add(hamming_distances);
                }

                //median
                foreach (var list in medians.Values)
                {
                    list.Sort();
                }

                int count = medians["fitness_score"].Count();
                if(count != 0)
                {
                    int hamming_distance_count = count * (count - 1) / 2;

                    int median_idx = count / 2;
                    median_reproductive_score = medians["fitness_score"].ElementAt(median_idx);
                    median_food_eaten = medians["food_eaten"].ElementAt(median_idx);
                    median_distance = medians["distance_travelled"].ElementAt(median_idx);
                    median_times_reproduced = medians["times_reproduced"].ElementAt(median_idx);
                    median_times_reproduced_asexually = medians["times_reproduced_asexually"].ElementAt(median_idx);
                    median_times_reproduced_sexually = medians["times_reproduced_sexually"].ElementAt(median_idx);
                    median_reproduction_chain = medians["reproduction_chain"].ElementAt(median_idx);
                    median_generation = medians["generation"].ElementAt(median_idx);
                    median_num_of_neurons = medians["num_of_neurons"].ElementAt(median_idx);
                    median_num_of_synapses = medians["num_of_synapses"].ElementAt(median_idx);
                    median_NARS_num_beliefs = medians["NARS_num_beliefs"].ElementAt(median_idx);
                    foreach (var key in NARS_personality_value_statistics.Keys.ToList())
                    {
                        if (!key.StartsWith("median_")) continue;

                        string parameter = key.Substring("median_".Length);
                        if (!medians.TryGetValue(parameter, out var seq)) continue;

                        NARS_personality_value_statistics[key] = seq.ElementAt(median_idx);
                    }
                    //special count, all unique genome pairs
                    median_hamming_distance = medians["hamming_distance"].ElementAt(hamming_distance_count / 2);


                    //mean
                    avg_reproductive_score = total_reproductive_score / count;
                    avg_food_eaten = total_food_eaten / count;
                    avg_distance = total_distance / count;
                    avg_times_reproduced = total_times_reproduced / count;
                    avg_times_reproduced_asexually = total_times_reproduced_asexually / count;
                    avg_times_reproduced_sexually = total_times_reproduced_sexually / count;
                    avg_reproduction_chain = total_reproduction_chain / count;
                    avg_generation = total_generation / count;
                    avg_num_of_neurons = total_num_of_neurons / count;
                    avg_num_of_synapses = total_num_of_synapses / count;
                    avg_hamming_distance /= hamming_distance_count;
                    avg_NARS_num_beliefs = total_NARS_num_beliefs / count;
                    foreach (var key in NARS_personality_value_statistics.Keys.ToList())
                    {
                        if (!key.StartsWith("total_")) continue;

                        string parameter_name = key.Substring("total_".Length);
                        float total = NARS_personality_value_statistics[key];
                        NARS_personality_value_statistics["avg_" + parameter_name] = total / count;
                    }
                }




                Dictionary<string, float> scores = new()
                {
                    { "avg_fitness_score", avg_reproductive_score},
                    { "max_fitness_score", max_reproductive_score },
                    { "min_fitness_score", min_reproductive_score },
                    { "median_fitness_score", median_reproductive_score },

                     { "avg_distance_travelled", avg_distance },
                    { "max_distance_travelled", max_distance },
                    { "min_distance_travelled", min_distance },
                    { "median_distance_travelled", median_distance },

                    { "avg_food_eaten", avg_food_eaten },
                    { "max_food_eaten", max_food_eaten },
                    { "min_food_eaten", min_food_eaten },
                    { "median_food_eaten", median_food_eaten },

                    { "avg_times_reproduced", avg_times_reproduced },
                    { "max_times_reproduced", max_times_reproduced },
                    { "min_times_reproduced", min_times_reproduced },
                    { "median_times_reproduced", median_times_reproduced },

                    { "avg_times_reproduced_asexually", avg_times_reproduced_asexually },
                    { "max_times_reproduced_asexually", max_times_reproduced_asexually },
                    { "min_times_reproduced_asexually", min_times_reproduced_asexually },
                    { "median_times_reproduced_asexually", median_times_reproduced_asexually },

                    { "avg_times_reproduced_sexually", avg_times_reproduced_sexually },
                    { "max_times_reproduced_sexually", max_times_reproduced_sexually },
                    { "min_times_reproduced_sexually", min_times_reproduced_sexually },
                    { "median_times_reproduced_sexually", median_times_reproduced_sexually },

                    { "avg_reproduction_chain", avg_reproduction_chain },
                    { "max_reproduction_chain", max_reproduction_chain },
                    { "min_reproduction_chain", min_reproduction_chain },
                    { "median_reproduction_chain", median_reproduction_chain },

                    { "avg_hamming_distance", avg_hamming_distance },
                    { "max_hamming_distance", max_hamming_distance },
                    { "min_hamming_distance", min_hamming_distance },
                    { "median_hamming_distance", median_hamming_distance },

                    { "avg_generation", avg_generation },
                    { "max_generation", max_generation },
                    { "min_generation", min_generation },
                    { "median_generation", median_generation },

                    { "avg_num_of_neurons", avg_num_of_neurons },
                    { "max_num_of_neurons", max_num_of_neurons },
                    { "min_num_of_neurons", min_num_of_neurons },
                    { "median_num_of_neurons", median_num_of_neurons },


                    { "avg_num_of_synapses", avg_num_of_synapses },
                    { "max_num_of_synapses", max_num_of_synapses },
                    { "min_num_of_synapses", min_num_of_synapses },
                    { "median_num_of_synapses", median_num_of_synapses },


                    { "avg_NARS_num_beliefs", avg_NARS_num_beliefs },
                    { "max_NARS_num_beliefs", max_NARS_num_beliefs },
                    { "min_NARS_num_beliefs", min_NARS_num_beliefs },
                    { "median_NARS_num_beliefs", median_NARS_num_beliefs }
                };
                foreach(var kvp in NARS_personality_value_statistics)
                {
                    if (kvp.Key.StartsWith("total")) continue;
                    scores.Add(kvp.Key, kvp.Value);
                }


                if (count == 0)
                {
                    var keys = scores.Keys.ToList();

                    foreach (var key in keys)
                    {
                        scores[key] = 0;
                    }
                }

                animat_datapoints.Reverse(); //from ascending to descendidng

                ReproductivePoolDatapoint datapoint = DataToolSocketClient.CreateTableDatapoint(scores,
                    hamming_distance_matrix,
                    animat_datapoints);


                table_datapoints[i] = datapoint;


                if (elite_table == elite_fitness_table)
                {
                    elite_fitness_table_data = scores;
                }
                else if (elite_table == elite_novelty_table)
                {
                    elite_novelty_table_data = scores;
                }
                else if (elite_table == continuous_fitness_table)
                {
                    continuous_fitness_table_data = scores;
                }
            }


            //====
            Debug.Log("DATATOOL: Done preparing data");

            if (GlobalConfig.RECORD_DATA_TO_WEB)
            {
                try
                {
                    this.data_tool_socket_client.SendReproductivePoolDatapoint(
                        world_data,
                        table_datapoints[0],
                        table_datapoints[1],
                        table_datapoints[2]);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }

            if (GlobalConfig.RECORD_DATA_TO_DISK)
            {
                try
                {
                    this.data_tool_socket_client.WriteToDisk(world_data,
                     table_datapoints[0],
                     table_datapoints[1],
                     table_datapoints[2]);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }

            GUIDatapoint gui_datapoint = new();
            gui_datapoint.world_data = world_data;
            gui_datapoint.hall_of_fame_fitness = table_datapoints[(int)ReproductionTables.FitnessHallOfFame];
            gui_data.Enqueue(gui_datapoint);

        });

    }

    public Task data_update_task = null;

    public void WriteColumnHeader()
    {
        Directory.CreateDirectory(data_folder); // Does nothing if already exists
        if (File.Exists(data_filename))
        {
            File.Delete(data_filename);
        }
        data_file = File.CreateText(data_filename);
        string title = "";
        title += "Num of Elite Animats";
        title += ",";
        title += "Average Distance Score";
        title += ",";
        title += "Max Distance Score";
        title += ",";
        title += "Average Food Eaten";
        title += ",";
        title += "Max Food Eaten";
        title += ",";
        title += "Average Reproductive Score";
        title += ",";
        title += "Max Reproductive Score";
        title += ",";
        title += "Average Times Self-Reproduced";
        title += ",";
        title += "Max Times Self-Reproduced";
        title += ",";
        title += "Generation";
        data_file.WriteLine(title);
        data_file.Close();
    }

    public static AnimatData CreateAnimatData(Animat animat)
    {
        int num_neurons = 0;
        int num_synapses = 0;
        int num_beliefs_in_genome = 0;
        Dictionary<string, float> NARS_parameters = null;
        if (animat.mind is Brain brain)
        {
            num_neurons = brain.CountNumberOfHiddenNeurons();
            num_synapses = ((NEATGenome)animat.genome.brain_genome).enabled_connection_idxs.Count;
        }
        else if(animat.mind is NARS nar)
        {
            NARSGenome nars_genome = ((NARSGenome)animat.genome.brain_genome);
            num_beliefs_in_genome = nars_genome.beliefs.Count;
            NARS_parameters = new();
            for (int i = 0; i < PersonalityParameters.GetParameterCount(); i++)
            {
                NARS_parameters.Add(NARSGenome.PersonalityParameters.GetName(i), (float)nars_genome.personality_parameters.Get(i));
            }
         
        }
        return new AnimatData
            (
            animat.GetDisplacementFromBirthplace(),
            animat.GetDistanceTravelled(),
            animat.body.number_of_food_eaten,
            animat.body.times_reproduced,
            animat.body.times_reproduced_asexually,
            animat.body.times_reproduced_sexually,
            animat.genome.generation,
            animat.genome.reproduction_chain,
            num_neurons,
            num_synapses,
            num_beliefs_in_genome,
            NARS_parameters,
            animat.genome,
            animat.genome.uniqueName,
            animat.behavior_characterization_CPU
            );
    }



    ///
    /// structs
    ///
    public struct AnimatData
    {
        public float displacement;
        public float distance;
        public float food_eaten;
        public int times_reproduced;
        public int times_reproduced_asexually;
        public int times_reproduced_sexually;
        public int generation;
        public int num_of_neurons;
        public int num_of_synapses;
        public string name;
        public AnimatGenome genome;
        public NoveltySearch.BehaviorCharacterizationCPU behavior;
        public int reproduction_chain;

        //  NARS
        public int NARS_num_beliefs;
        public Dictionary<string, float> NARS_Parameters;

        public AnimatData(float displacement,
            float distance,
            float food_eaten,
            int times_reproduced,
            int times_reproduced_asexually,
            int times_reproduced_sexually,
            int generation,
            int reproduction_chain,
            int num_of_neurons,
            int num_of_synapses,
            int num_beliefs_in_genome,
            Dictionary<string, float> NARS_Parameters,
            AnimatGenome genome,
            string name,
            NoveltySearch.BehaviorCharacterizationCPU behavior)
        {
            this.displacement = displacement;
            this.distance = distance;
            this.food_eaten = food_eaten;
            this.times_reproduced = times_reproduced;
            this.times_reproduced_asexually = times_reproduced_asexually;
            this.times_reproduced_sexually = times_reproduced_sexually;
            this.generation = generation;
            this.reproduction_chain = reproduction_chain;
            this.num_of_neurons = num_of_neurons;
            this.num_of_synapses = num_of_synapses;
            this.genome = genome;
            this.name = name;
            this.behavior = behavior;

            // NARS
            this.NARS_num_beliefs = num_beliefs_in_genome;
            this.NARS_Parameters = NARS_Parameters;
        }
    }


    internal void OnAppQuit()
    {

        this.data_tool_socket_client.OnAppQuit();
        data_file.Close();
        best_nars_data_file.Close();
    }

    public struct GUIDatapoint
    {
        public DataToolSocketClient.WorldDatapoint world_data;
        public DataToolSocketClient.ReproductivePoolDatapoint hall_of_fame_fitness;
    }

    public static ConcurrentQueue<GUIDatapoint> gui_data = new();
}
