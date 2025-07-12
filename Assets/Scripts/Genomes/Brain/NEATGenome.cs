using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using static Brain;

public class NEATGenome : BrainGenome
{
    const bool EVOLVE_SIGMOID_SLOPE = false;
    public bool EVOLVE_TIME_CONSTANT = true;
    public bool EVOLVE_GAIN = true;
    public const bool EVOLVE_ACTIVATION_FUNCTIONS = false;
    public const bool DISABLE_CONNECTIONS = false;

    // user changeable parameters
    public static float CHANCE_TO_MUTATE_CONNECTION = 0.8f;
    public static float ADD_CONNECTION_MUTATION_RATE = 0.35f;
    public static float ADD_NODE_MUTATION_RATE = 0.09f;


    //constant parameters
    const float CHANCE_TO_MUTATE_HEBB = 0.8f;
    const float CHANCE_TO_MUTATE_SIGMOID_ALPHA = 0.8f;
    public const float CHANCE_TO_MUTATE_TIME_CONSTANT = 0.8f;
    public const float CHANCE_TO_MUTATE_GAIN = 0.8f;
    const float CHANCE_TO_MUTATE_ACTIVATION_FUNCTIONS = 0.02f;

    // const float CHANCE_TO_MUTATE_EACH_NODE = 0.8f;

    const float DISABLE_CONNECTION_MUTATION_RATE = 0.00f;
    const float DISABLE_NODE_MUTATION_RATE = 0.01f;
    const float RATE_MULTIPLIER = 1f;

    const bool ALLOW_OUTBOUND_MOTOR_CONNECTIONS = true;


    public Dictionary<NeuronID, int> nodeID_to_idx;
    public Dictionary<int4, int> connectionID_to_idx;
    public List<NEATNode> nodes;
    public List<NEATNode> sensor_and_hidden_nodes;
    public List<NEATNode> motor_and_hidden_nodes;
    public List<NEATNode> motor_nodes;
    public List<NEATNode> sensor_nodes;
    public List<NEATNode> hidden_nodes;
    public List<NEATConnection> connections;

    public struct RandomHashSet
    {

        List<int> set;
        Dictionary<int, int> element_to_idx;
        public RandomHashSet(bool dummy)
        {
            this.set = new();
            this.element_to_idx = new();
        }

        public void Add(int i)
        {
            set.Add(i);
            this.element_to_idx.Add(i, set.Count - 1);
        }

        public void Remove(int i)
        {
            int idx = this.element_to_idx[i];
            set.RemoveAt(idx);
            this.element_to_idx.Remove(i);
        }

        public int RemoveRandomElement()
        {
            int last_index = this.set.Count - 1;
            int rnd = UnityEngine.Random.Range(0, this.set.Count);
            int random_element = this.set[rnd];
            int last_element = this.set[last_index];
            this.element_to_idx.Remove(random_element);
            this.element_to_idx[last_element] = rnd;
            this.set[rnd] = last_element;
            this.set.RemoveAt(last_index);
            return random_element;
        }

        public int Count()
        {
            return this.set.Count();
        }
    }

    public RandomHashSet enabled_connection_idxs;

    public int sensorymotor_end_idx;

    public NEATGenome()
    {
        this.nodes = new();
        this.sensor_and_hidden_nodes = new();
        this.motor_and_hidden_nodes = new();
        this.motor_and_hidden_nodes = new();
        this.motor_nodes = new();
        this.hidden_nodes = new();
        this.sensor_nodes = new();
        this.connections = new();
        this.nodeID_to_idx = new();
        this.connectionID_to_idx = new();
        this.enabled_connection_idxs = new RandomHashSet(true);
    }

    public static NeuronID GetTupleIDFromInt3(int3 coords, int neuronID, Brain.Neuron.NeuronRole neuron_role)
    {
        if(coords.x < 0 || coords.y < 0 || coords.z < 0)
        {
            Debug.LogError("Dont use negative coordinates, they are reserved for other IDs");
        }
        return new NeuronID(new int4(coords, neuronID), neuron_role);
    }

    public static NeuronID GetTupleIDFromInt(int neuronID, Brain.Neuron.NeuronRole neuron_role)
    {
        return new NeuronID(new(new int3(0, 0, -5), neuronID), neuron_role);
    }


    public static NeuronID GetTupleIDFrom2Ints(int neuronID, int num2, Brain.Neuron.NeuronRole neuron_role)
    {
        return new NeuronID(new(new int3(-5, 0, num2), neuronID), neuron_role);
    }





    public override void Mutate()
    {
        bool should_mutate;
        float rnd;

        // first, mutate synapse parameters
        rnd = UnityEngine.Random.Range(0f, 1f);
        if (rnd < CHANCE_TO_MUTATE_CONNECTION)
        {
            foreach (NEATConnection connection in this.connections)
            {


                rnd = UnityEngine.Random.Range(0f, 1f);
                if (rnd < 0.9)
                {
                    connection.weight += GetPerturbationFromRange(-1f, 1f);
                }
                else
                {
                    connection.weight = NEATConnection.GetRandomInitialWeight();
                }

                //}

            }

            //  mutate bias (bias can be treated like a connection weight in some casesi )
            foreach (NEATNode node in this.nodes)
            {
                rnd = UnityEngine.Random.Range(0f, 1f);
                if (rnd < 0.9)
                {
                    node.bias += GetPerturbationFromRange(-1f, 1f);
                }
                else
                {
                    node.bias = NEATConnection.GetRandomInitialWeight();
                }
            }

        }

     
        if (GlobalConfig.USE_HEBBIAN)
        {
            rnd = UnityEngine.Random.Range(0f, 1f);
            if (rnd < CHANCE_TO_MUTATE_HEBB)
            {
                foreach (NEATConnection connection in this.connections)
                {
                    for (int b = 0; b < 5; b++)
                    {
                        rnd = UnityEngine.Random.Range(0f, 1f);
                        if (rnd < 0.9)
                        {
                            connection.hebb_ABCDLR[b] += GetPerturbationFromRange(-1f, 1f);
                        }
                        else
                        {
                            connection.hebb_ABCDLR[b] = NEATConnection.GetRandomInitialWeight();
                        }
                    }

                }

            }
        }


        MutateCTNNParameters();


        if (EVOLVE_SIGMOID_SLOPE)
        {
            rnd = UnityEngine.Random.Range(0f, 1f);
            if (rnd < CHANCE_TO_MUTATE_SIGMOID_ALPHA)
            {
                foreach (NEATNode node in this.nodes)
                {
                    node.sigmoid_alpha += GetPerturbationFromRange(0f, 1f);
                    if (node.sigmoid_alpha <= 0) node.sigmoid_alpha = 0.00001f;
                    node.sigmoid_alpha2 += GetPerturbationFromRange(0f, 1f);
                    if (node.sigmoid_alpha2 <= 0) node.sigmoid_alpha = 0.00001f;
                }
            }
        
        }




        if (EVOLVE_ACTIVATION_FUNCTIONS)
        {
            foreach (NEATNode node in this.nodes)
            {
                if (node.ID.neuron_role != Neuron.NeuronRole.Hidden) continue;
                rnd = UnityEngine.Random.Range(0f, 1f);
                if (rnd < CHANCE_TO_MUTATE_ACTIVATION_FUNCTIONS)
                {
                    node.activation_function = Brain.Neuron.GetRandomActivationFunction();
                }

            }
        }

        //  mutate CPG parameters
        MutateCPGParameters();


        // disable connection?
        if (DISABLE_CONNECTIONS)
        {
            rnd = UnityEngine.Random.Range(0f, 1f);
            if (rnd < DISABLE_CONNECTION_MUTATION_RATE * RATE_MULTIPLIER)
            {
                DisableRandomConnection();
            }
        }


        // add connection?
        rnd = UnityEngine.Random.Range(0f, 1f);
        if (rnd < ADD_CONNECTION_MUTATION_RATE * RATE_MULTIPLIER)
        {
            AddNewRandomConnection();
        }


        // add node?
        rnd = UnityEngine.Random.Range(0f, 1f);
        if (rnd < ADD_NODE_MUTATION_RATE * RATE_MULTIPLIER)
        {
            AddNewHiddenNodeAtRandomConnection();
        }




    }

    private void MutateCTNNParameters()
    {
        if (EVOLVE_TIME_CONSTANT)
        {
            float rnd = UnityEngine.Random.Range(0f, 1f);
            if (rnd < CHANCE_TO_MUTATE_TIME_CONSTANT)
            {
                foreach (NEATNode node in this.nodes)
                {
                    rnd = UnityEngine.Random.Range(0f, 1f);
                    if (rnd < 0.9)
                    {
                        node.time_constant += GetPerturbationFromRange(0f,1f);
                    }
                    else
                    {
                        node.time_constant = NEATConnection.GetRandomInitialWeight();
                    }
                    node.time_constant = math.abs(node.time_constant);
                }
            }

        }

        if (EVOLVE_GAIN)
        {
            float rnd = UnityEngine.Random.Range(0f, 1f);
            if (rnd < CHANCE_TO_MUTATE_GAIN)
            {
                foreach (NEATNode node in this.nodes)
                {
                    rnd = UnityEngine.Random.Range(0f, 1f);
                    if (rnd < 0.9)
                    {
                        node.gain += GetPerturbationFromRange(0f, 1f);
                    }
                    else
                    {
                        node.gain = NEATConnection.GetRandomInitialWeight();
                    }
                    node.gain = math.abs(node.gain);
                }
            }

        }
    }

    public void MutateCPGParameters()
    {
        //  mutate CPG
        float rnd;
        foreach (NEATNode node in this.nodes)
        {
            rnd = UnityEngine.Random.Range(0f, 1f);
            if (rnd < 0.1)
            {
                node.use_cpg = !node.use_cpg;
            }


            rnd = UnityEngine.Random.Range(0f, 1f);
            if (rnd < 0.9)
            {
                node.r += GetPerturbationFromRange(0f, 1f);
            }
            else
            {
                node.r = UnityEngine.Random.Range(0f, 1f);
            }

            node.r = math.clamp(node.r, 0f, 1f);

            rnd = UnityEngine.Random.Range(0f, 1f);
            if (rnd < 0.9)
            {
                node.w += GetPerturbationFromRange(0.5f, 10f);
            }
            else
            {
                node.w = UnityEngine.Random.Range(0.5f, 10f);
            }

            node.w = math.clamp(node.w, 0.5f, 10f);

            rnd = UnityEngine.Random.Range(0f, 1f);
            if (rnd < 0.9)
            {
                node.p += GetPerturbationFromRange(0f, 2f * math.PI);
            }
            else
            {
                node.p = UnityEngine.Random.Range(0, 2f * math.PI);
            }
  
            node.p = math.clamp(node.p, 0f, 2f*math.PI);
        }
    }

    public NEATConnection AddNewRandomConnection()
    {
        int from_idx;

        int to_idx;

        NEATNode from_neuron;

        NEATNode to_neuron;


        if (ALLOW_OUTBOUND_MOTOR_CONNECTIONS)
        {
            from_idx = UnityEngine.Random.Range(0, this.nodes.Count);
            from_neuron = this.nodes[from_idx];
        }
        else
        {
            from_idx = UnityEngine.Random.Range(0, this.sensor_and_hidden_nodes.Count);
            from_neuron = this.sensor_and_hidden_nodes[from_idx];
        }

        to_idx = UnityEngine.Random.Range(0, this.motor_and_hidden_nodes.Count);
        to_neuron = this.motor_and_hidden_nodes[to_idx];

        float rnd_weight = NEATConnection.GetRandomInitialWeight();
        NEATConnection new_connection = new(rnd_weight, from_neuron.ID, to_neuron.ID, int.MinValue);
        AddConnection(new_connection);
        return new_connection;
    }

    private void DisableRandomConnection()
    {
        // insert the node at a random connection
        if (this.enabled_connection_idxs.Count() == 0)
        {
            Debug.LogWarning("no enabled connections");
            return;
        }

        int random_connection_idx = this.enabled_connection_idxs.RemoveRandomElement();
        NEATConnection random_connection = this.connections[random_connection_idx];
        random_connection.enabled = false;
    }

    static System.Random r = new();
    public static float GetPerturbationFromRange(float min, float max, float fraction = 0.1f)
    {
        float range = max - min;
        float stdDev = range * fraction;

        // Generate standard normal sample using Box-Muller
        double u, v, S;
        do
        {
            u = 2.0 * r.NextDouble() - 1.0;
            v = 2.0 * r.NextDouble() - 1.0;
            S = u * u + v * v;
        } while (S >= 1.0 || S == 0);

        double fac = Math.Sqrt(-2.0 * Math.Log(S) / S);
        float result = (float)(u * fac);

        result *= stdDev; // scale
        return result;
    }

    public NEATNode AddNewHiddenNodeAtRandomConnection()
    {
        // insert the node at a random connection
        if (this.enabled_connection_idxs.Count() == 0)
        {
            Debug.LogWarning("no enabled connections");
            return null;
        }


        int random_connection_idx = this.enabled_connection_idxs.RemoveRandomElement();

        NEATConnection random_connection = this.connections[random_connection_idx];
        if (!random_connection.enabled) Debug.LogError("error");
        random_connection.enabled = false;

        // set node position to be used in BrainViewer
        NEATNode to_node = GetNode(random_connection.toID);
        NEATNode from_node = GetNode(random_connection.fromID);
        float4 coords = (to_node.brainviewer_coords + from_node.brainviewer_coords) / 2f;

        NEATNode new_node = AddDisconnectedHiddenNode(brainviewer_coords: coords);

        NEATConnection new_connectionA = new(weight: 1, fromID: random_connection.fromID, toID: new_node.ID, ID: int.MinValue);

        NEATConnection new_connectionB = new(weight: random_connection.weight, fromID: new_node.ID, toID: random_connection.toID, ID: int.MinValue);
        for (int i = 0; i < random_connection.hebb_ABCDLR.Length; i++)
        {
            new_connectionB.hebb_ABCDLR[i] = random_connection.hebb_ABCDLR[i];
        }



        this.AddConnection(new_connectionA);
        this.AddConnection(new_connectionB);


        return new_node;
    }

    public NEATNode AddDisconnectedHiddenNode(NeuronID? ID_override = null, float4? brainviewer_coords = null)
    {
        var ID_coords = ID_override == null ? new NeuronID(new int4(int3.zero, int.MinValue), Brain.Neuron.NeuronRole.Hidden) : (NeuronID)ID_override;
        NEATNode new_node = new(ID: ID_coords, InitialNEATGenomes.neuron_activation_function, brainviewer_coords);
        this.AddNode(new_node);
        return new_node;
    }

    public void AddNode(NEATNode node)
    {
        this.nodes.Add(node);
        this.nodeID_to_idx[node.ID] = this.nodes.Count - 1;

        if (node.ID.neuron_role == Brain.Neuron.NeuronRole.Motor)
        {
            this.motor_and_hidden_nodes.Add(node);
            this.motor_nodes.Add(node);
        }

        if (node.ID.neuron_role == Brain.Neuron.NeuronRole.Hidden)
        {
            this.motor_and_hidden_nodes.Add(node);
            this.sensor_and_hidden_nodes.Add(node);
            this.hidden_nodes.Add(node);
        }

        if (node.ID.neuron_role == Brain.Neuron.NeuronRole.Sensor)
        {
            this.sensor_and_hidden_nodes.Add(node);
            this.sensor_nodes.Add(node);
        }
    }

    public void AddConnection(NEATConnection connection)
    {
        if (!this.nodeID_to_idx.ContainsKey(connection.fromID)
            || !this.nodeID_to_idx.ContainsKey(connection.toID)) Debug.LogError("Error cant form connection to non-existent node");
        this.connections.Add(connection);
        this.connectionID_to_idx[connection.ID] = this.connections.Count - 1;
        if (connection.enabled) this.enabled_connection_idxs.Add(this.connections.Count - 1);
    }

    public override (BrainGenome, BrainGenome) Reproduce(BrainGenome parent2super)
    {
        NEATGenome offspring1 = new();
        NEATGenome offspring2 = new();
        NEATGenome parent1 = this;
        NEATGenome parent2 = (NEATGenome)parent2super;

        foreach (NEATGenome offspring in new NEATGenome[] { offspring1, offspring2 })
        {
            foreach (NEATNode parent1_node in parent1.nodes)
            {
                offspring.AddNode(parent1_node.Clone());
            }
        }

        foreach (NEATGenome offspring in new NEATGenome[] { offspring1, offspring2 })
        {
            foreach (NEATNode parent2_node in parent2.nodes)
            {
                if (offspring.nodeID_to_idx.ContainsKey(parent2_node.ID))
                {
                    // node already came from parent 1 (same ID), now decide what to do with parent 2 node
                    float rnd = UnityEngine.Random.value;
                    if (rnd < 0.5f)
                    {
                        // use parent 2 node instead
                        int idx = offspring.nodeID_to_idx[parent2_node.ID];
                        offspring.nodes[idx] = parent2_node.Clone();
                    }
                    else
                    {
                        // keep parent 1 node
                    }
                }
                else
                {
                    // node is unique to parent2, so add it
                    offspring.AddNode(parent2_node.Clone());
                }
            }
        }


        //
        // now do connections
        //

        foreach (NEATGenome offspring in new NEATGenome[] { offspring1, offspring2 })
        {
            foreach (NEATConnection parent1_connection in parent1.connections)
            {
                offspring.AddConnection(parent1_connection.Clone());
            }
        }

        foreach (NEATGenome offspring in new NEATGenome[] { offspring1, offspring2 })
        {
            foreach (NEATConnection parent2_connection in parent2.connections)
            {
                if (offspring.connectionID_to_idx.ContainsKey(parent2_connection.ID))
                {
                    // it already came from parent 1 (same ID), now decide what to do with parent 2
                    float rnd = UnityEngine.Random.value;
                    if (rnd < 0.5f)
                    {
                        // use parent 2 instead
                        int idx = offspring.connectionID_to_idx[parent2_connection.ID];
                        offspring.connections[idx] = parent2_connection.Clone();
                    }
                    else
                    {
                        // keep parent 1 node
                    }
                }
                else
                {
                    // it is unique to parent2, so add it
                    offspring.AddConnection(parent2_connection.Clone());
                    offspring.connectionID_to_idx[parent2_connection.ID] = offspring.connections.Count - 1;

                }


            }
        }


        foreach (NEATGenome offspring in new NEATGenome[] { offspring1, offspring2 })
        {
            for (int i = 0; i < offspring.connections.Count; i++)
            {
                if (!offspring.connections[i].enabled)
                {
                    bool was_enabled = offspring.connections[i].enabled;
                    offspring.connections[i].enabled = UnityEngine.Random.value < 0.75f ? false : true;
                    if (was_enabled && !offspring.connections[i].enabled) offspring.enabled_connection_idxs.Remove(i);
                }

            }
        }


        return (offspring1, offspring2);
    }

    public override BrainGenome Clone()
    {
        NEATGenome cloned_genome = new();
        int i = 0;
        foreach (NEATNode n in this.nodes)
        {
            cloned_genome.AddNode(n.Clone());
            i++;
        }
        foreach (NEATConnection c in this.connections)
        {
            cloned_genome.AddConnection(c.Clone());
        }
        return cloned_genome;
    }

    public static bool IsFightingNeuron(NeuronID ID)
    {
        return ID.coords.w == InitialNEATGenomes.FIGHTING_MOTOR_NEURON_INDEX;
    }

    public override float CalculateHammingDistance(BrainGenome other_genome)
    {

        NEATGenome genome1 = this;
        NEATGenome genome2 = (NEATGenome)other_genome;
        int distance = 0;

        for (int i = 0; i < genome1.nodes.Count; i++)
        {
            var node = genome1.nodes[i];
            if (genome2.nodeID_to_idx.ContainsKey(node.ID))
            {
                // both  contain node at ID
            }
            else
            {
                // this is in  genome1 but not genome2
                distance++;
            }
        }
        for (int i = 0; i < genome1.connections.Count; i++)
        {
       
            var connection = genome1.connections[i];
            if (genome2.connectionID_to_idx.ContainsKey(connection.ID))
            {
                // both  contain node at ID
            }
            else
            {
                // this is in genome1 but not genome2
                distance++;
            }
        }
        for (int i = 0; i < genome2.nodes.Count; i++)
        {
            var node = genome2.nodes[i];
            if (genome1.nodeID_to_idx.ContainsKey(node.ID))
            {
                // both contain node at ID
            }
            else
            {
                // this is in genome2 but not genome1
                distance++;
            }
        }
        for (int i = 0; i < genome2.connections.Count; i++)
        {
            var connection = genome2.connections[i];
            if (genome1.connectionID_to_idx.ContainsKey(connection.ID))
            {
                // both contain node at ID
            }
            else
            {
                // this is in genome2 but not genome1
                distance++;
            }
        }
        return distance;
    }

    internal NEATNode GetNode(NeuronID ID)
    {
        if (!this.nodeID_to_idx.ContainsKey(ID)) Debug.LogError("No node with that ID!");
        return this.nodes[this.nodeID_to_idx[ID]];
    }
}