using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Localization.Plugins.XLIFF.V20;
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
    public static float ADD_CONNECTION_MUTATION_RATE = 0.2f;
    public static float ADD_NODE_MUTATION_RATE = 0.1f;


    //constant parameters


    const float CHANCE_TO_MUTATE_ACTIVATION_FUNCTIONS = 0.02f;

    // const float CHANCE_TO_MUTATE_EACH_NODE = 0.8f;

    const float DISABLE_CONNECTION_MUTATION_RATE = 0.00f;
    const float DISABLE_NODE_MUTATION_RATE = 0.01f;
    const float RATE_MULTIPLIER = 1f;

    const bool ALLOW_OUTBOUND_MOTOR_CONNECTIONS = true;


    public Dictionary<int, int> nodeID_to_idx;
    public Dictionary<int4, int> connectionID_to_idx;
    public List<NEATNode> nodes;
    public List<NEATNode> sensor_and_hidden_nodes;
    public List<NEATNode> motor_and_hidden_nodes;
    public List<NEATNode> motor_nodes;
    public List<NEATNode> sensor_nodes;
    public List<NEATNode> hidden_nodes;
    public List<NEATConnection> connections;

    public class RandomHashSet<T>
    {
        // Private fields to prevent direct manipulation from outside
        private readonly List<T> _items;
        private readonly Dictionary<T, int> _itemToIndex;
        private readonly System.Random _random = new();

        public RandomHashSet()
        {
            _items = new List<T>();
            _itemToIndex = new Dictionary<T, int>();
        }

        // Expose count as a property
        public int Count => _items.Count;

        // Returns true if the item was added, false if it already existed
        public bool Add(T item)
        {
            if (_itemToIndex.ContainsKey(item))
            {
                return false; // Item already exists
            }

            _items.Add(item);
            _itemToIndex.Add(item, _items.Count - 1);
            return true;
        }

        // The correct O(1) remove implementation
        // Returns true if the item was found and removed, otherwise false
        public bool Remove(T item)
        {
            if (!_itemToIndex.TryGetValue(item, out int indexToRemove))
            {
                return false; // Item not in the set
            }

            // Get the last item in the list
            int lastIndex = _items.Count - 1;
            T lastItem = _items[lastIndex];

            // Move the last item to the position of the item being removed
            _items[indexToRemove] = lastItem;
            _itemToIndex[lastItem] = indexToRemove; // Update the moved item's index

            // Remove the item from the end of the list and the dictionary
            _items.RemoveAt(lastIndex);
            _itemToIndex.Remove(item);

            return true;
        }

        // Removes and returns a random element
        public T RemoveRandomElement()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Cannot remove from an empty set.");
            }

            // This re-uses the same logic as the standard Remove method
            int randomIndex = _random.Next(0, Count);
            T randomItem = _items[randomIndex];

            // This is now safe and correct because Remove(T) handles all cases
            Remove(randomItem);

            return randomItem;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }
    }

    public RandomHashSet<int> enabled_connection_idxs;

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
        this.enabled_connection_idxs = new RandomHashSet<int>();
    }


    public override void Mutate()
    {
        // --- Pre-computation Step ---
        bool shouldMutateHebbian = GlobalConfig.USE_HEBBIAN;

        bool is_CTRNN = GlobalConfig.NEURAL_NETWORK_METHOD == Brain.Neuron.NeuronClass.CTRNN;
        bool shouldMutateTimeConstant = is_CTRNN && EVOLVE_TIME_CONSTANT;
        bool shouldMutateGain = is_CTRNN && EVOLVE_GAIN;

        bool shouldMutateSigmoidAlpha = EVOLVE_SIGMOID_SLOPE;

        // --- 1. Single Loop for All Connection Mutations ---
        // This loop handles all modifications to the 'connections' collection.
        const float hebb_mut_rate = 0.2f;
        int connection_idx = 0;
        foreach (NEATConnection connection in this.connections)
        {
            // a) Mutate standard connection weight (this was the first loop).
            if (NEATConnection.GetRandomFloat() < 0.9f)
            {
                connection.weight += GetPerturbationFromRange(-1f, 1f);
            }
            else
            {
                connection.weight = NEATConnection.GetRandomInitialWeight();
            }

            // b) Mutate Hebbian learning parameters if the pre-computed check passed.
            if (shouldMutateHebbian)
            {
                if(NEATConnection.GetRandomFloat() < hebb_mut_rate)
                {
                    // Assuming hebb_ABCDLR is an array or list of size 5.
                    for (int i = 0; i < connection.hebb_ABCDLR.Length; i++)
                    {
                        if (NEATConnection.GetRandomFloat() < 0.9f)
                        {
                            connection.hebb_ABCDLR[i] += GetPerturbationFromRange(-1f, 1f);
                        }
                        else
                        {
                            connection.hebb_ABCDLR[i] = NEATConnection.GetRandomInitialWeight();
                        }
                    }
                }

            }


            // flip enabled
            if (connection.enabled)
            {
                if (NEATConnection.GetRandomFloat() < 0.01f)
                {
                    connection.enabled = false;
                    this.enabled_connection_idxs.Remove(connection_idx);
                }
            }
            else
            {
                if (NEATConnection.GetRandomFloat() < 0.002f)
                {
                    connection.enabled = true;
                    this.enabled_connection_idxs.Add(connection_idx);
                }
            }

            connection_idx++;
        }

        // --- 2. Single Loop for All Node Mutations ---
        // This loop handles all modifications to the 'nodes' collection.
        var r_range = CPGRanges.GetRRange();
        var w_range = CPGRanges.GetWRange();
        var thetaRange = CPGRanges.GetThetaRange();
        var rGainRange = CPGRanges.GetRGainRange();
        var pGainRange = CPGRanges.GetPGainRange();
        var muRange = CPGRanges.GetMuRange();
        var kRange = CPGRanges.GetKRange();
        var miRange = CPGRanges.GetMaxInputRange();
        var giRange = CPGRanges.GetOscInjectGainRange();  
        var phaseOffsetRange = CPGRanges.GetPhaseOffsetRange();
        var blendRange = CPGRanges.GetBlendRange();


        const float bias_mut_rate = 0.5f;
        const float timeconstant_mut_rate = 0.5f;
        const float gain_mut_rate = 0.5f;
        const float cpg_mut_rate = 0.5f;
        foreach (NEATNode node in this.nodes)
        {
            // a) Mutate bias.
            if (NEATConnection.GetRandomFloat() < bias_mut_rate)
            {
                if (NEATConnection.GetRandomFloat() < 0.9f)
                {
                    node.bias += GetPerturbationFromRange(-1f, 1f);
                }
                else
                {
                    node.bias = NEATConnection.GetRandomInitialWeight();
                }
            }


            // b) Mutate CTRNN time constant 
            if (shouldMutateTimeConstant)
            {
                if (NEATConnection.GetRandomFloat() < timeconstant_mut_rate)
                {
                    if (NEATConnection.GetRandomFloat() < 0.9f)
                    {
                        node.time_constant += GetPerturbationFromRange(0f, 1f);
                    }
                    else
                    {
                        node.time_constant = NEATConnection.GetRandomInitialWeight();
                    }
                    node.time_constant = math.abs(node.time_constant);
                }
            }

            // c) Mutate CTRNN gain 
            if (shouldMutateGain)
            {
                if (NEATConnection.GetRandomFloat() < gain_mut_rate)
                {
                    if (NEATConnection.GetRandomFloat() < 0.9f)
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

            // d) Mutate sigmoid slope parameters 
            if (shouldMutateSigmoidAlpha)
            {
                node.sigmoid_alpha += GetPerturbationFromRange(0f, 1f);
                if (node.sigmoid_alpha <= 0) node.sigmoid_alpha = 0.00001f;

                node.sigmoid_alpha2 += GetPerturbationFromRange(0f, 1f);
                // Potential typo fix: The original code set 'sigmoid_alpha' here, not 'sigmoid_alpha2'.
                if (node.sigmoid_alpha2 <= 0) node.sigmoid_alpha2 = 0.00001f;
            }

            // e) Mutate all CPG (Central Pattern Generator) parameters.

            if (NEATConnection.GetRandomFloat() < cpg_mut_rate)
            {
                const float perturb_rate = 0.97f;
                MutateParameter(ref node.r, r_range, perturb_rate);
                MutateParameter(ref node.w, w_range, perturb_rate);
                MutateParameter(ref node.theta, thetaRange, perturb_rate);
                MutateParameter(ref node.r_gain, rGainRange, perturb_rate);
                MutateParameter(ref node.p_gain, pGainRange, perturb_rate);
                MutateParameter(ref node.mu, muRange, perturb_rate);
                MutateParameter(ref node.K, kRange, perturb_rate);
                MutateParameter(ref node.max_input, miRange, perturb_rate);
                MutateParameter(ref node.osc_inject_gain, giRange, perturb_rate);
                MutateParameter(ref node.phase_offset, phaseOffsetRange, perturb_rate);
                MutateParameter(ref node.blend, blendRange, perturb_rate);
            }
        }

        float rnd;


        // disable connection?
        if (DISABLE_CONNECTIONS)
        {
            rnd = NEATConnection.GetRandomFloat();
            if (rnd < DISABLE_CONNECTION_MUTATION_RATE * RATE_MULTIPLIER)
            {
                DisableRandomConnection();
            }
        }


        // add connection?
        rnd = NEATConnection.GetRandomFloat();
        if (rnd < ADD_CONNECTION_MUTATION_RATE * RATE_MULTIPLIER)
        {
            AddNewRandomConnection();
        }


        // add node?
        rnd = NEATConnection.GetRandomFloat();
        if (rnd < ADD_NODE_MUTATION_RATE * RATE_MULTIPLIER)
        {
            AddNewHiddenNodeAtRandomConnection();
        }




    }

    void MutateParameter(ref double param, Vector2 range, float perturb_change=0.9f)
    {
        if (NEATConnection.GetRandomFloat() < perturb_change)
        {
            param += GetPerturbationFromRange(range.x, range.y);
        }
        else
        {
            param = NEATConnection.ThreadSafeSystemRandomRange(range.x, range.y);
        }
        param = math.clamp(param, range.x, range.y);
    }

    public static class CPGRanges
    {

        public static Vector2 GetWRange() =>
            GlobalConfig.CPG_TYPE != GlobalConfig.CPGtype.Matsuoka
                ? new Vector2(0.1f, 2f)   // Hopf
                : new Vector2(0.3f, 2f);  // Matsuoka

        public static Vector2 GetThetaRange() =>
            // same for both (0 → 2π)
            new Vector2(0f, math.PI2);

        public static Vector2 GetPhaseOffsetRange() =>
            // same for both (0 → 2π)
            new Vector2(0f, math.PI2);

        public static Vector2 GetRRange() =>
            GlobalConfig.CPG_TYPE != GlobalConfig.CPGtype.Matsuoka
                ? new Vector2(0.1f, 2f)   // Hopf amplitude
                : new Vector2(0f, 1f);    // Matsuoka initial potential

        public static Vector2 GetMuRange() =>
            GlobalConfig.CPG_TYPE != GlobalConfig.CPGtype.Matsuoka
                ? new Vector2(0.1f, 1f)   // Hopf damping μ
                : new Vector2(0.1f, 1f);  // Matsuoka τ_r

        public static Vector2 GetOscInjectGainRange() =>
            GlobalConfig.CPG_TYPE != GlobalConfig.CPGtype.Matsuoka
                ? new Vector2(-1f, 1f)    // Hopf injection
                : new Vector2(0.1f, 2f);  // Matsuoka injection

        public static Vector2 GetMaxInputRange() =>
            // same for both
            new Vector2(0.1f, 20f);

        public static Vector2 GetKRange() =>
            GlobalConfig.CPG_TYPE != GlobalConfig.CPGtype.Matsuoka
                ? new Vector2(-1f, 1f)    // Hopf coupling K
                : new Vector2(1.5f, 5f);  // Matsuoka β

        public static Vector2 GetPGainRange() =>
            GlobalConfig.CPG_TYPE != GlobalConfig.CPGtype.Matsuoka
                ? new Vector2(-0.5f, 0.5f) // Hopf p_gain
                : new Vector2(-5f, 5f);   // Matsuoka p_gain

        public static Vector2 GetRGainRange() =>
            GlobalConfig.CPG_TYPE != GlobalConfig.CPGtype.Matsuoka
                ? new Vector2(0f, 1f)     // Hopf tonic bias
                : new Vector2(-1f, 1f);   // Matsuoka tonic bias

        public static Vector2 GetBlendRange() => new Vector2(0f, 1f);

    }

    public NEATConnection AddNewRandomConnection()
    {
        int from_idx;

        int to_idx;

        NEATNode from_neuron;

        NEATNode to_neuron;


        if (ALLOW_OUTBOUND_MOTOR_CONNECTIONS)
        {
            from_idx = NEATConnection.ThreadSafeSystemRandomRange(0, this.nodes.Count);
            from_neuron = this.nodes[from_idx];
        }
        else
        {
            from_idx = NEATConnection.ThreadSafeSystemRandomRange(0, this.sensor_and_hidden_nodes.Count);
            from_neuron = this.sensor_and_hidden_nodes[from_idx];
        }

        to_idx = NEATConnection.ThreadSafeSystemRandomRange(0, this.motor_and_hidden_nodes.Count);
        to_neuron = this.motor_and_hidden_nodes[to_idx];

        float rnd_weight = NEATConnection.GetRandomInitialWeight();
        NEATConnection new_connection = new(rnd_weight, from_neuron.ID, to_neuron.ID, int.MinValue);
        AddConnection(new_connection);
        return new_connection;
    }

    private void DisableRandomConnection()
    {
        // insert the node at a random connection
        if (this.enabled_connection_idxs.Count == 0)
        {
            Debug.LogWarning("no enabled connections");
            return;
        }

        int random_connection_idx = this.enabled_connection_idxs.RemoveRandomElement();
        NEATConnection random_connection = this.connections[random_connection_idx];
        random_connection.enabled = false;
    }


    static System.Random r = new();
    public static double GetPerturbationFromRange(double min, double max, double fraction = 0.1f)
    {
        double range = max - min;
        double stdDev = range * fraction;

        // Generate standard normal sample using Box-Muller
        double u, v, S;
        do
        {
            u = 2.0 * r.NextDouble() - 1.0;
            v = 2.0 * r.NextDouble() - 1.0;
            S = u * u + v * v;
        } while (S >= 1.0 || S == 0);

        double fac = Math.Sqrt(-2.0 * Math.Log(S) / S);
        double result = u * fac;

        result *= stdDev; // scale

        return math.clamp(result, min, max);
    }

    public NEATNode AddNewHiddenNodeAtRandomConnection()
    {
        // insert the node at a random connection
        if (this.enabled_connection_idxs.Count == 0)
        {
            Debug.LogWarning("no enabled connections");
            return null;
        }


        int random_connection_idx = this.enabled_connection_idxs.RemoveRandomElement();

        NEATConnection random_connection = this.connections[random_connection_idx];
        if (!random_connection.enabled) Debug.LogError("error");
        random_connection.enabled = false;

        // set node position to be used in BrainViewer
        NEATNode to_node = GetNode(random_connection.toNodeID);
        NEATNode from_node = GetNode(random_connection.fromNodeID);
        float4 coords = (to_node.brainviewer_coords + from_node.brainviewer_coords) / 2f;

        NEATNode new_node = AddDisconnectedHiddenNode(brainviewer_coords: coords);

        NEATConnection new_connectionA = new(weight: 1, fromNodeID: random_connection.fromNodeID, toNodeID: new_node.ID, ID: int.MinValue);

        NEATConnection new_connectionB = new(weight: random_connection.weight, fromNodeID: new_node.ID, toNodeID: random_connection.toNodeID, ID: int.MinValue);
        for (int i = 0; i < random_connection.hebb_ABCDLR.Length; i++)
        {
            new_connectionB.hebb_ABCDLR[i] = random_connection.hebb_ABCDLR[i];
        }



        this.AddConnection(new_connectionA);
        this.AddConnection(new_connectionB);


        return new_node;
    }

    public NEATNode AddDisconnectedHiddenNode(int? ID_override=null, float4? brainviewer_coords = null)
    {
        NEATNode new_node = new(ID: ID_override ?? int.MinValue, InitialNEATGenomes.neuron_activation_function, Brain.Neuron.NeuronRole.Hidden,brainviewer_coords);
        this.AddNode(new_node);
        return new_node;
    }

    public void AddNode(NEATNode node)
    {
        this.nodes.Add(node);
        this.nodeID_to_idx[node.ID] = this.nodes.Count - 1;

        if (node.neuron_role == Brain.Neuron.NeuronRole.Motor)
        {
            this.motor_and_hidden_nodes.Add(node);
            this.motor_nodes.Add(node);
        }

        if (node.neuron_role == Brain.Neuron.NeuronRole.Hidden)
        {
            this.motor_and_hidden_nodes.Add(node);
            this.sensor_and_hidden_nodes.Add(node);
            this.hidden_nodes.Add(node);
        }

        if (node.neuron_role == Brain.Neuron.NeuronRole.Sensor)
        {
            this.sensor_and_hidden_nodes.Add(node);
            this.sensor_nodes.Add(node);
        }
    }

    public void AddConnection(NEATConnection connection)
    {
        if (!this.nodeID_to_idx.ContainsKey(connection.fromNodeID)
            || !this.nodeID_to_idx.ContainsKey(connection.toNodeID)) Debug.LogError("Error cant form connection to non-existent node");
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

        bool parent1_selected_for_disjoint = NEATConnection.GetRandomFloat() < 0.5f;



        foreach (NEATGenome offspring in new NEATGenome[] { offspring1, offspring2 })
        {
            //  add any mising sensorymotor nodes

            for (int parent1_sensor_idx = 0; parent1_sensor_idx < parent1.sensor_nodes.Count; parent1_sensor_idx++)
            {
                var parent1_sense_node = parent1.sensor_nodes[parent1_sensor_idx];
                var ID = parent1_sense_node.ID;
                var parent2_sensor_node_idx = parent2.nodeID_to_idx[(int)ID];
                var parent2_sense_node = parent2.nodes[parent2_sensor_node_idx];
                if (parent1_sense_node.ID != parent2_sense_node.ID) Debug.LogError("error");

                if (!offspring.nodeID_to_idx.ContainsKey(ID))
                {
                    NEATNode node_to_clone = null;
                    float rnd = NEATConnection.GetRandomFloat();
                    if (parent1_selected_for_disjoint)//NEATConnection.GetRandomFloat() < 0.5f) //parent1_selected_for_disjoint)
                    {
                        node_to_clone = parent1_sense_node;

                    }
                    else
                    {
                        node_to_clone = parent2_sense_node;
                    }

                    offspring.AddNode(node_to_clone.Clone());
                }
            }
            for (int parent1_motor_idx = 0; parent1_motor_idx < parent1.motor_nodes.Count; parent1_motor_idx++)
            {
                var parent1_motor_node = parent1.motor_nodes[parent1_motor_idx];
                var ID = parent1_motor_node.ID;
                var parent2_motor_node_idx = parent2.nodeID_to_idx[(int)ID];
                var parent2_motor_node = parent2.nodes[parent2_motor_node_idx];
                if (parent1_motor_node.ID != parent2_motor_node.ID) Debug.LogError("error");
                if (!offspring.nodeID_to_idx.ContainsKey(ID))
                {
                    NEATNode node_to_clone = null;
                    //float rnd = NEATConnection.GetRandomFloat();
                    if (parent1_selected_for_disjoint)//NEATConnection.GetRandomFloat() < 0.5f) //parent1_selected_for_disjoint)
                    {
                        node_to_clone = parent1_motor_node;
                    }
                    else
                    {
                        node_to_clone = parent2_motor_node;
                    }

                    offspring.AddNode(node_to_clone.Clone());
                }
            }
            //
            // do connections and hidden nodes
            //
            foreach (NEATConnection parent1_connection in parent1.connections)
            {
                NEATConnection connection_to_clone = null;
                NEATConnection parent2_connection = null;
                if (parent2.connectionID_to_idx.ContainsKey(parent1_connection.ID))
                {
              
                    parent2_connection = parent2.connections[parent2.connectionID_to_idx[parent1_connection.ID]];
                    // it is present in both parents, so it must be added
                    float rnd = NEATConnection.GetRandomFloat();
                    if (rnd < 0.5f)
                    {
                        // use parent 2  
                        connection_to_clone = parent2_connection;
                       
                    }
                    else
                    {
                        // use parent 1 
                        connection_to_clone = parent1_connection;
                    }
 
                }
                else
                {
                    // connection is unique to parent1, so 50% chance to add it
                   
                    if (parent1_selected_for_disjoint)//NEATConnection.GetRandomFloat() < 0.5f) //)
                    {
                        connection_to_clone = parent1_connection;
                    }
                }

                if(connection_to_clone == null) continue;

                NEATGenome parent_giving_connection;
                if (connection_to_clone == parent1_connection)
                {
                    parent_giving_connection = parent1;
                }
                else
                {
                    parent_giving_connection = parent2;
                }
                
                var fromNodeIdx = parent_giving_connection.nodeID_to_idx[connection_to_clone.fromNodeID];
                var toNodeIdx = parent_giving_connection.nodeID_to_idx[connection_to_clone.toNodeID];
                var fromNode = parent_giving_connection.nodes[fromNodeIdx];
                var toNode = parent_giving_connection.nodes[toNodeIdx];
                if (!offspring.nodeID_to_idx.ContainsKey(fromNode.ID)) offspring.AddNode(fromNode.Clone());
                if (!offspring.nodeID_to_idx.ContainsKey(toNode.ID)) offspring.AddNode(toNode.Clone());
                offspring.AddConnection(connection_to_clone.Clone());

                //enabled/disabled
                if (parent2_connection == null) continue;
                if (parent1_connection.enabled != parent2_connection.enabled)
                {
                    var ID = connection_to_clone.ID;
                    //  its disabled in one of the parents
                    var idx = offspring.connectionID_to_idx[ID];
                    var connection = offspring.connections[idx];
                    bool was_enabled = connection.enabled;
                    connection.enabled = NEATConnection.GetRandomFloat() < 0.75f ? false : true;

                    if (!was_enabled && connection.enabled) offspring.enabled_connection_idxs.Add(idx);
                    else if (was_enabled && !connection.enabled) offspring.enabled_connection_idxs.Remove(idx);
                }
            }


            foreach (NEATConnection parent2_connection in parent2.connections)
            {
                NEATConnection connection_to_clone = null;
                NEATConnection parent1_connection = null;
                if (parent1.connectionID_to_idx.ContainsKey(parent2_connection.ID))
                {
                    // it is present in both parents, it must already be added
                    continue;
                }
                else
                {
                    // connection is unique to parent2, so 50% chance to add it
                    //float rnd = NEATConnection.GetRandomFloat();
                    if(!parent1_selected_for_disjoint)//NEATConnection.GetRandomFloat() < 0.5f) //)
                    {
                        connection_to_clone = parent2_connection;
                    }
                }

                if (connection_to_clone == null) continue;

                NEATGenome parent_giving_connection = parent2;
                var fromNodeIdx = parent_giving_connection.nodeID_to_idx[connection_to_clone.fromNodeID];
                var toNodeIdx = parent_giving_connection.nodeID_to_idx[connection_to_clone.toNodeID];
                var fromNode = parent_giving_connection.nodes[fromNodeIdx];
                var toNode = parent_giving_connection.nodes[toNodeIdx];
                if (!offspring.nodeID_to_idx.ContainsKey(fromNode.ID)) offspring.AddNode(fromNode.Clone());
                if (!offspring.nodeID_to_idx.ContainsKey(toNode.ID)) offspring.AddNode(toNode.Clone());
                offspring.AddConnection(connection_to_clone.Clone());
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

    public static bool IsFightingNeuron(int ID)
    {
        return ID == InitialNEATGenomes.FIGHTING_MOTOR_NEURON_INDEX;
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

    internal NEATNode GetNode(int ID)
    {
        if (!this.nodeID_to_idx.ContainsKey(ID)) Debug.LogError("No node with that ID!");
        return this.nodes[this.nodeID_to_idx[ID]];
    }

    // Option 1: ConcurrentDictionary with integer keys (most flexible)
    public class IndexableConcurrentList<T> : IEnumerable<T>
    {
        private readonly ConcurrentDictionary<int, T> _items = new();
        private int _count = 0;

        public T this[int index]
        {
            get => _items.TryGetValue(index, out T value) ? value : throw new IndexOutOfRangeException();
            set => _items[index] = value;
        }

        public int Add(T item)
        {
            int index = Interlocked.Increment(ref _count) - 1;
            _items[index] = item;
            return index;
        }

        public int Count => _count;
        public bool TryGetValue(int index, out T value) => _items.TryGetValue(index, out value);
        public void Clear() { _items.Clear(); _count = 0; }

        // IEnumerable<T> implementation
        public IEnumerator<T> GetEnumerator()
        {
            // Enumerate in index order (0, 1, 2, ...)
            for (int i = 0; i < _count; i++)
            {
                if (_items.TryGetValue(i, out T value))
                {
                    yield return value;
                }
                // Skip missing indices (though shouldn't happen with sequential adds)
            }
        }

        // Non-generic IEnumerable implementation
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // Alternative: Enumerate by actual stored keys (may be unordered)
        public IEnumerable<T> GetUnorderedValues()
        {
            return _items.Values;
        }

        // Get key-value pairs for debugging
        public IEnumerable<KeyValuePair<int, T>> GetIndexedPairs()
        {
            return _items.OrderBy(kvp => kvp.Key);
        }
    }

}