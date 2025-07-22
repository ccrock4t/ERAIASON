using System.IO;
using Unity.Mathematics;
using UnityEngine;
using static GlobalConfig;

[System.Serializable]
public class SimulationSettings
{

    // simulation parameters
    public WorldType worldType;
    public BrainProcessingMethod brainProcessingMethod;
    public Brain.Neuron.NeuronClass neuralNetworkMethod;

    public BodyMethod bodyMethod;

    public int3 worldDimensions;
    public int worldSecondsPerTimestep;
    public int brainUpdatePeriod;
    public int brainViewerUpdatePeriod;
    public bool useHebbian;

    // NEAT parameters
    float CHANCE_TO_MUTATE_CONNECTION;
    float ADD_CONNECTION_MUTATION_RATE;
    float ADD_NODE_MUTATION_RATE;

    public SimulationSettings(WorldType worldType,
        int3 worldDimensions,
        BrainProcessingMethod brainProcessingMethod,
        Brain.Neuron.NeuronClass neuralNetworkMethod,
        bool hebb,
        BodyMethod bodyMethod
    )
    {
        this.worldType = worldType;
        this.worldDimensions = worldDimensions;
        this.brainProcessingMethod = brainProcessingMethod;
        this.neuralNetworkMethod = neuralNetworkMethod;
        this.worldSecondsPerTimestep = 3;
        this.brainUpdatePeriod = 2;
        this.brainViewerUpdatePeriod = 4;
        this.useHebbian = hebb;
        this.bodyMethod = bodyMethod;
        this.CHANCE_TO_MUTATE_CONNECTION = 0.8f;
        this.ADD_CONNECTION_MUTATION_RATE = 0.35f;
        this.ADD_NODE_MUTATION_RATE = 0.09f;
    }

    public void ApplyToGlobal()
    {
        GlobalConfig.WORLD_TYPE = this.worldType;
        GlobalConfig.WORLD_DIMENSIONS = this.worldDimensions;
        GlobalConfig.BRAIN_PROCESSING_METHOD = this.brainProcessingMethod;
        GlobalConfig.NEURAL_NETWORK_METHOD = this.neuralNetworkMethod;
        GlobalConfig.WORLD_AUTOMATA_UPDATE_PERIOD = this.worldSecondsPerTimestep;
        GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD = this.brainUpdatePeriod;
        GlobalConfig.BRAIN_VIEWER_UPDATE_PERIOD = this.brainViewerUpdatePeriod;
        GlobalConfig.USE_HEBBIAN = this.useHebbian;

        if(this.brainProcessingMethod == BrainProcessingMethod.NARSCPU)
        {
            if(bodyMethod == BodyMethod.ArticulatedRobot)
            {
                GlobalConfig.BODY_METHOD = BodyMethod.WheeledRobot;
            }
            else
            {
                GlobalConfig.BODY_METHOD = this.bodyMethod;
            }
            
        }
        else
        {
            GlobalConfig.BODY_METHOD = this.bodyMethod;
        }

        NEATGenome.CHANCE_TO_MUTATE_CONNECTION = CHANCE_TO_MUTATE_CONNECTION;
        NEATGenome.ADD_CONNECTION_MUTATION_RATE = ADD_CONNECTION_MUTATION_RATE;
        NEATGenome.ADD_NODE_MUTATION_RATE = ADD_NODE_MUTATION_RATE;
    }

}

public static class SimulationSettingsManager
{
    public static void Save(SimulationSettings s, string filename)
    {
        string path = Application.persistentDataPath + filename;
        File.WriteAllText(path, JsonUtility.ToJson(s));
    }

    public static SimulationSettings Load(string filename)
    {
        string path = Application.persistentDataPath + filename;
        if (File.Exists(path))
        {
            return JsonUtility.FromJson<SimulationSettings>(File.ReadAllText(path));
        }  
        else
        {
            return null;
        }
    }
}
