using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class NARSGenome : BrainGenome
{
    const float CHANCE_TO_MUTATE_BELIEF_SET = 0.35f;
    const float CHANCE_TO_MUTATE_TRUTH_VALUES = 0.8f;
    const float CHANCE_TO_MUTATE_PERSONALITY_PARAMETERS = 0.1f;

    public struct EvolvableSentence
    {
        public StatementTerm statement;
        public EvidentialValue evidence;

        public EvolvableSentence(StatementTerm statement, float2 evidence)
        {
            this.statement = statement;
            this.evidence = new EvidentialValue(evidence.x, evidence.y);
        }
    }


    public static StatementTerm move_op = (StatementTerm)Term.from_string("((*,{SELF}) --> move)");
    public static StatementTerm rotate_op = (StatementTerm)Term.from_string("((*,{SELF}) --> turn)");
    public static StatementTerm eat_op = (StatementTerm)Term.from_string("((*,{SELF}) --> eat)");
    public static StatementTerm fight_op = (StatementTerm)Term.from_string("((*,{SELF}) --> fight)");
    public static StatementTerm mate_op = (StatementTerm)Term.from_string("((*,{SELF}) --> mate)");
    public static StatementTerm asexual_op = (StatementTerm)Term.from_string("((*,{SELF}) --> asexual)");

    public static StatementTerm food_far = (StatementTerm)Term.from_string("({food} --> [far])");
    public static StatementTerm food_near = (StatementTerm)Term.from_string("({food} --> [near])");
    public static StatementTerm food_unseen = (StatementTerm)Term.from_string("({food} --> [unseen])");
    public static StatementTerm animat_far = (StatementTerm)Term.from_string("({animat} --> [far])");
    public static StatementTerm animat_near = (StatementTerm)Term.from_string("({animat} --> [near])");
    public static StatementTerm animat_unseen = (StatementTerm)Term.from_string("({animat} --> [unseen])");
    public static StatementTerm energy_full = (StatementTerm)Term.from_string("({ENERGY} --> [FULL])");
    public static StatementTerm self_mated = (StatementTerm)Term.from_string("({SELF} --> [mated])");


    public static StatementTerm[] SENSORY_TERM_SET;
    public static StatementTerm[] MOTOR_TERM_SET;

    public Dictionary<string, bool> belief_statement_strings = new();
    public List<EvolvableSentence> beliefs;
    public List<EvolvableSentence> goals;


    public struct PersonalityParameters
    {
        public float k;
        public float T;

        public float Get(int i)
        {
            if (i == 0) return k;
            else if (i == 1) return T;
            else Debug.LogError("error"); 
            return -1;
        }

        public void Set(int i, float value)
        {
            if (i == 0) k = value;
            else if (i == 1) T = value;
            else Debug.LogError("error");
        }

        public static int GetParameterCount()
        {
            return 2;
        }
    }

    public PersonalityParameters personality_parameters;

    public const int num_of_personality_parameters = 2;

    BodyGenome body_genome;

    const int MAX_INITIAL_BELIEFS = 10;

    void InitializeForWheeledRobotBody(List<EvolvableSentence> beliefs_to_clone = null,
        List<EvolvableSentence> goals_to_clone = null)
    {

        if (SENSORY_TERM_SET == null)
        {
            SENSORY_TERM_SET = new StatementTerm[]
            {
                food_far,
                food_near,
                food_unseen,
                animat_far,
                animat_near,
                animat_unseen,
                energy_full,
                self_mated,
            };

            MOTOR_TERM_SET = new StatementTerm[]
            {
                move_op,
                rotate_op,
                eat_op,
                mate_op,
                asexual_op,
                fight_op
            };
        }
    }

    void InitializeForSoftVoxelRobotBody(List<EvolvableSentence> beliefs_to_clone = null,
        List<EvolvableSentence> goals_to_clone = null)
    {

        if (SENSORY_TERM_SET == null)
        {
            SENSORY_TERM_SET = new StatementTerm[]
            {
                food_far,
                food_near,
                food_unseen,
                animat_far,
                animat_near,
                animat_unseen,
                energy_full,
                self_mated,
            };

            MOTOR_TERM_SET = new StatementTerm[]
            {
                move_op,
                rotate_op,
                eat_op,
                mate_op,
                asexual_op,
                fight_op
            };
        }


    }

    public NARSGenome(BodyGenome body_genome,
        List<EvolvableSentence> beliefs_to_clone = null,
        List<EvolvableSentence> goals_to_clone = null,
        PersonalityParameters? personality_to_clone = null
    )
    {
        this.body_genome = body_genome;
        if (body_genome is WheeledRobotBodyGenome)
        {
            InitializeForWheeledRobotBody();
        }else if (body_genome is SoftVoxelRobotBodyGenome)
        {
            InitializeForSoftVoxelRobotBody();
        }
        else
        {
            Debug.LogError("not supported");
            return;
        }

            beliefs = new();
        if (beliefs_to_clone == null)
        {

            int rnd_amt = UnityEngine.Random.Range(1, MAX_INITIAL_BELIEFS);
            for (int i = 0; i < rnd_amt; i++)
            {
                AddNewRandomBelief();
            }
        }
        else
        {
            foreach (var belief in beliefs_to_clone)
            {
                AddNewBelief(belief);
            }

        }

        if (goals_to_clone == null)
        {
            goals = new();
            AddIdealGoals(goals);
        }
        else
        {
            goals = new(goals_to_clone);
        }

        personality_parameters = new();
        if (personality_to_clone != null)
        {
            personality_parameters = (PersonalityParameters)personality_to_clone;

        }
        else
        {
            personality_parameters.k = 1; // k
            personality_parameters.T = 0.51f; // T  
        }

    }

    public float getK()
    {
        return personality_parameters.k;

    }

    public float getT()
    {
        return personality_parameters.T;
    }

    public static void AddEvolvableSentences(List<EvolvableSentence> list, (StatementTerm, float?, float?)[] statement_strings)
    {

        foreach (var statement in statement_strings)
        {
            float f = statement.Item2 == null ? 1.0f : (float)statement.Item2;
            float c = statement.Item3 == null ? 0.99f : (float)statement.Item3;
            EvolvableSentence sentence = new(statement: statement.Item1,
                new float2(f, c));
            list.Add(sentence);
        }

    }


    //public static void AddMinimalBeliefsForLearning(List<EvolvableSentence> beliefs)
    //{
    //    (StatementTerm, float?, float?)[] statement_strings = new (StatementTerm, float?, float?)[]
    //    {
    //        (CreateContigencyString(animat_far,move_op,animat_near), null, null),
    //        (CreateContigencyString(food_unseen,move_op,food_far), null, null),

    //        (CreateContigencyString(null,eat_op,energy_increase), 0.6f, null),

    //        (CreateContigencyString(null,energy_increase,energy_full), 0.9f, null),

    //        (CreateContigencyString(animat_near,mate_op,self_mated), null, null),
    //        (CreateContigencyString(food_unseen,rotate_op,food_far), null, null),
    //        (CreateContigencyString(animat_unseen,move_op,animat_far), null, null),
    //        (CreateContigencyString(animat_unseen,rotate_op,animat_far), null, null),
    //    };

    //    AddEvolvableSentences(beliefs, statement_strings);
    //}


    public static void AddIdealBeliefs(List<EvolvableSentence> beliefs)
    {
        (StatementTerm, float?, float?)[] statement_strings = new (StatementTerm, float?, float?)[]
        {
            (CreateContingencyStatement(food_far,move_op,food_near), null, null),
            (CreateContingencyStatement(food_near,eat_op,energy_full), null, null),
            (CreateContingencyStatement(animat_far,move_op,animat_near), null, null),
            (CreateContingencyStatement(energy_full,asexual_op,self_mated), null, null),
            (CreateContingencyStatement(Term.from_string("(&/, " + energy_full + "," + animat_near + ")"),mate_op,self_mated), null, null),
            (CreateContingencyStatement(food_unseen,rotate_op,food_far), null, null),
            (CreateContingencyStatement(food_unseen,move_op,food_far), null, null),
            (CreateContingencyStatement(animat_unseen,rotate_op,animat_far), null, null),
            (CreateContingencyStatement(animat_unseen,move_op,animat_far), null, null),
        };

        AddEvolvableSentences(beliefs, statement_strings);
    }

    // create <S &/ ^M =/> P>
    public static StatementTerm CreateContingencyStatement(Term S, Term M, Term P)
    {
        if (S == null) return (StatementTerm)Term.from_string("(" + M.ToString() + " =/> " + P.ToString() + ")");
        return (StatementTerm)Term.from_string("((&/," + S.ToString() + "," + M.ToString() + ") =/> " + P.ToString() + ")");
    }

    public static void AddIdealGoals(List<EvolvableSentence> goals)
    {
        (StatementTerm, float?, float?)[] statement_strings = new (StatementTerm, float?, float?)[]
        {
            (energy_full, null, null),
            (self_mated, null, null),
        };
        AddEvolvableSentences(goals, statement_strings);
    }

    public override BrainGenome Clone()
    {
        NARSGenome cloned_genome = new(
            this.body_genome,
            beliefs,
            goals,
            this.personality_parameters);

        return cloned_genome;
    }


    public override void Mutate()
    {
        float rnd = UnityEngine.Random.Range(0f,1f);

        if(rnd < CHANCE_TO_MUTATE_BELIEF_SET)
        {
            int rnd2 = UnityEngine.Random.Range(0, 3);
            if (rnd2 == 0 || this.beliefs.Count == 0)
            {
                // add new belief
                AddNewRandomBelief();
            }
            else if (rnd2 == 1)
            {
                // remove a belief
                RemoveRandomBelief();
            }
            else
            {
                // change a belief
                ModifyRandomBelief();
            }
        }

        rnd = UnityEngine.Random.Range(0f, 1f);

        if (rnd < CHANCE_TO_MUTATE_TRUTH_VALUES)
        {
            for(int i=0; i < this.beliefs.Count; i++)
            {
                EvolvableSentence sentence = this.beliefs[i];
                float rnd2 = UnityEngine.Random.Range(0f, 1f);
                if(rnd2 < 0.9)
                {
                    sentence.evidence.frequency += UnityEngine.Random.Range(-0.1f, 0.1f);
                }
                else
                {
                    sentence.evidence.frequency = UnityEngine.Random.Range(0.5f, 1.0f);
                }
                sentence.evidence.frequency = math.clamp(sentence.evidence.frequency, 0.5f, 1f);
                this.beliefs[i] = sentence;
            }
        }

        rnd = UnityEngine.Random.Range(0f, 1f);
        if (rnd < CHANCE_TO_MUTATE_PERSONALITY_PARAMETERS)
        {
                //k
                this.personality_parameters.k += UnityEngine.Random.Range(-0.1f, 0.1f);
                this.personality_parameters.k = math.max(this.personality_parameters.k, 1f);
                // T
                this.personality_parameters.T += UnityEngine.Random.Range(-0.1f, 0.1f);
                this.personality_parameters.T = math.clamp(this.personality_parameters.T, 0f, 1f);
                
                
            
        }
        return;
    }

    const float DEFAULT_C = 0.99f;
    public void AddNewRandomBelief()
    {
        StatementTerm statement = CreateContingencyStatement(GetRandomSensoryTerm(), GetRandomMotorTerm(), GetRandomSensoryTerm());
        string statement_string = statement.ToString();
        if (!belief_statement_strings.ContainsKey(statement_string))
        {
            float f = UnityEngine.Random.Range(0.5f, 1f);
            EvolvableSentence sentence = new(statement: statement,
                   new float2(f, DEFAULT_C));
            this.beliefs.Add(sentence);
            belief_statement_strings.Add(statement_string, true);
        }
        else
        {
            Debug.LogWarning("genome already contained " + statement_string);
        }

    }

    public void RemoveRandomBelief()
    {
        if(this.beliefs.Count == 0) return;
        int rnd_idx = UnityEngine.Random.Range(0, this.beliefs.Count);
        var belief = this.beliefs[rnd_idx];
        this.beliefs.RemoveAt(rnd_idx);
        belief_statement_strings.Remove(belief.statement.ToString());
    }

    public void ModifyRandomBelief()
    {
        if (this.beliefs.Count == 0) return;
        int rnd_idx = UnityEngine.Random.Range(0, this.beliefs.Count);
        EvolvableSentence belief = this.beliefs[rnd_idx];

        string old_statement_string = belief.statement.ToString();
    
        // (S &/ ^M =/> P)
        StatementTerm implication = belief.statement;

   
        CompoundTerm subject = (CompoundTerm)implication.get_subject_term();
        StatementTerm predicate = (StatementTerm)implication.get_predicate_term();

        StatementTerm new_statement;
        int rnd = UnityEngine.Random.Range(0, 3);
        if (rnd == 0)
        {
            // replace S
            new_statement = CreateContingencyStatement(GetRandomSensoryTerm(), subject.subterms[1], predicate);
        }
        else if (rnd == 1)
        {
            // replace M
            new_statement = CreateContingencyStatement(subject.subterms[0], GetRandomMotorTerm(), predicate);
        }
        else //if (rnd == 2)
        {
            // replace P
            new_statement = CreateContingencyStatement(subject.subterms[0], subject.subterms[1], GetRandomSensoryTerm());
        }

        belief.statement = new_statement;
        string new_statement_string = new_statement.ToString();
        if (belief_statement_strings.ContainsKey(new_statement_string)) return;
        
        belief_statement_strings.Remove(old_statement_string);
        belief_statement_strings.Add(new_statement_string, true);

        this.beliefs[rnd_idx] = belief;
    }

    public StatementTerm GetRandomSensoryTerm()
    {
        int rnd = UnityEngine.Random.Range(0, SENSORY_TERM_SET.Length);
        return SENSORY_TERM_SET[rnd];
    }

    public StatementTerm GetRandomMotorTerm()
    {
        int rnd = UnityEngine.Random.Range(0, MOTOR_TERM_SET.Length);
        return MOTOR_TERM_SET[rnd];
    }

    public void AddNewBelief(EvolvableSentence belief)
    {
        string statement_string = belief.statement.ToString();
        if (!belief_statement_strings.ContainsKey(statement_string))
        {
            float f = UnityEngine.Random.Range(0.5f, 1f);
            this.beliefs.Add(belief);
            belief_statement_strings.Add(statement_string, true);
        }
        else
        {
            Debug.LogWarning("genome already contained " + statement_string);
        }

    }

    public override (BrainGenome, BrainGenome) Reproduce(BrainGenome parent2genome)
    {
        NARSGenome parent1 = this;
        NARSGenome parent2 =  (NARSGenome)parent2genome;
        int longer_array = math.max(parent1.beliefs.Count, parent2.beliefs.Count);

        NARSGenome offspring1 = new(parent1.body_genome);
        NARSGenome offspring2 = new(parent2.body_genome);
        for (int i = 0; i < longer_array; i++)
        {
            int rnd = UnityEngine.Random.Range(0, 2);

            if (rnd == 0)
            {
                if (i < parent1.beliefs.Count) offspring1.AddNewBelief(parent1.beliefs[i]);
                if (i < parent2.beliefs.Count) offspring2.AddNewBelief(parent2.beliefs[i]);
            }
            else
            {
                if (i < parent2.beliefs.Count) offspring1.AddNewBelief(parent2.beliefs[i]);
                if (i < parent1.beliefs.Count) offspring2.AddNewBelief(parent1.beliefs[i]);
            }
        }

        for (int i = 0; i < PersonalityParameters.GetParameterCount(); i++)
        {
            int rnd = UnityEngine.Random.Range(0, 2);
            if (rnd == 0)
            {
                offspring1.personality_parameters.Set(i, parent1.personality_parameters.Get(i));
                offspring2.personality_parameters.Set(i, parent2.personality_parameters.Get(i));
            }
            else
            {
                offspring1.personality_parameters.Set(i, parent2.personality_parameters.Get(i));
                offspring2.personality_parameters.Set(i, parent1.personality_parameters.Get(i));
            }
        }


        return (offspring1, offspring2);
    }

    public override float CalculateHammingDistance(BrainGenome other_genome)
    {
        int distance = 0;
        NARSGenome genome1 = this;
        NARSGenome genome2 = (NARSGenome)other_genome;

        for(int i=0; i < genome1.beliefs.Count; i++)
        {
            var belief1 = genome1.beliefs[i];
            if (!genome2.belief_statement_strings.ContainsKey(belief1.statement.ToString()))
            {
                distance++;
            }
        }

        
        for (int j = 0; j < genome2.beliefs.Count; j++)
        {
            var belief2 = genome2.beliefs[j];
            if (!genome1.belief_statement_strings.ContainsKey(belief2.statement.ToString()))
            {
                distance++;
            }
        }

        return distance;
    }
}
