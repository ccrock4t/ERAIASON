
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static MutationHelpers;
public class NARSGenome : BrainGenome
{
    const float CHANCE_TO_MUTATE_BELIEF_SET = 0.35f;
    const float CHANCE_TO_MUTATE_TRUTH_VALUES = 0.8f;
    const float CHANCE_TO_MUTATE_PERSONALITY_PARAMETERS = 0.8f;


    public enum NARS_Evolution_Type
    {
        NARS_NO_CONTINGENCY_FIXED_PERSONALITY_LEARNING,
        NARS_NO_CONTINGENCY_RANDOM_PERSONALITY_LEARNING,

        NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_NO_LEARNING,
        NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_NO_LEARNING,

        NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_LEARNING,
        NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_LEARNING,

        NARS_EVOLVE_PERSONALITY_LEARNING,
        NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_LEARNING,

        NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_NO_LEARNING
    }

    public static NARS_Evolution_Type NARS_EVOLVE_TYPE = NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_NO_LEARNING;


    public static bool RANDOM_PERSONALITY()
    {
        return NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_NO_CONTINGENCY_RANDOM_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_NO_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_LEARNING;
    }

    public static bool USE_LEARNING()
    {
        return NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_NO_CONTINGENCY_FIXED_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_NO_CONTINGENCY_RANDOM_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_LEARNING;
    }

    public static bool EVOLVE_PERSONALITY()
    {
        return NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_LEARNING 
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_NO_LEARNING;
    }

    public static bool USE_AND_EVOLVE_CONTINGENCIES()
    {
        return NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_NO_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_NO_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_FIXED_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_CONTINGENCIES_RANDOM_PERSONALITY_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_LEARNING
            || NARS_EVOLVE_TYPE == NARS_Evolution_Type.NARS_EVOLVE_PERSONALITY_AND_CONTINGENCIES_NO_LEARNING;
    }

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

    public static bool sensorymotor_statements_initialized = false;
    public static StatementTerm move_op;
    public static StatementTerm rotate_right_op;
    //public static StatementTerm rotate_left_op;
    public static StatementTerm eat_op;
    public static StatementTerm fight_op;
    public static StatementTerm mate_op;
    public static StatementTerm asexual_op;

    public static StatementTerm food_far;
    public static StatementTerm food_medium;
    public static StatementTerm food_near;
    public static StatementTerm food_unseen;
    public static StatementTerm animat_far;
    public static StatementTerm animat_medium;
    public static StatementTerm animat_near;
    public static StatementTerm animat_unseen;
    public static StatementTerm energy_full;
    public static StatementTerm self_mated;


    public static StatementTerm[] SENSORY_TERM_SET;
    public static StatementTerm[] MOTOR_TERM_SET;

    public Dictionary<string, bool> belief_statement_strings = new();
    public List<EvolvableSentence> beliefs;
    public List<EvolvableSentence> goals;


    public struct PersonalityParameters
    {
        public float k;
        public float T;
        public int Anticipation_Window;
        public float Forgetting_Rate;
        public int Event_Buffer_Capacity;
        public int Table_Capacity;
        public int Evidential_Base_Length;
        public float Time_Projection_Event;
        public float Time_Projection_Goal;

        public float Get(int i)
        {
            if (i == 0) return k;
            else if (i == 1) return T;
            else if (i == 2) return Anticipation_Window;
            else if (i == 3) return Forgetting_Rate;
            else if (i == 4) return Event_Buffer_Capacity;
            else if (i == 5) return Table_Capacity;
            else if (i == 6) return Evidential_Base_Length;
            else if (i == 7) return Time_Projection_Event;
            else if (i == 8) return Time_Projection_Goal;
            else Debug.LogError("error"); 
            return -1;
        }

        public static string GetName(int i)
        {
            if (i == 0) return "k";
            else if (i == 1) return "T";
            else if (i == 2) return "Anticipation_Window";
            else if (i == 3) return "Forgetting_Rate";
            else if (i == 4) return "Event_Buffer_Capacity";
            else if (i == 5) return "Table_Capacity";
            else if (i == 6) return "Evidential_Base_Length";
            else if (i == 7) return "Time_Projection_Event";
            else if (i == 8) return "Time_Projection_Goal";
            else Debug.LogError("error");
            return "";
        }


        public void Set(int i, float value)
        {
            if (i == 0) k = (float)value;
            else if (i == 1) T = (float)value;
            else if (i == 2) Anticipation_Window = (int)value;
            else if (i == 3) Forgetting_Rate = (float)value;
            else if (i == 4) Event_Buffer_Capacity = (int)value;
            else if (i == 5) Table_Capacity = (int)value;
            else if (i == 6) Evidential_Base_Length = (int)value;
            else if (i == 7) Time_Projection_Event = (float)value;
            else if (i == 8) Time_Projection_Goal = (float)value;
            else Debug.LogError("error");
        }

        public static int GetParameterCount()
        {
            return 9;
        }
    }

    public PersonalityParameters personality_parameters;

    public const int num_of_personality_parameters = 2;

    WheeledRobotBodyGenome body_genome;

    const int MAX_INITIAL_BELIEFS = 10;

    Judgment move_instinct;

    PersonalityParameters defaults;

    public NARSGenome(WheeledRobotBodyGenome body_genome,
        List<EvolvableSentence> beliefs_to_clone = null,
        List<EvolvableSentence> goals_to_clone = null,
        PersonalityParameters? personality_to_clone = null
        )
    {
        if (!sensorymotor_statements_initialized)
        {
            move_op = (StatementTerm)Term.from_string("((*,{SELF}) --> move)");
            rotate_right_op = (StatementTerm)Term.from_string("((*,{SELF}) --> turnRight)");
            //  rotate_left_op = (StatementTerm)Term.from_string("((*,{SELF}) --> turnLeft)");
            eat_op = (StatementTerm)Term.from_string("((*,{SELF}) --> eat)");
            fight_op = (StatementTerm)Term.from_string("((*,{SELF}) --> fight)");
            mate_op = (StatementTerm)Term.from_string("((*,{SELF}) --> mate)");
            asexual_op = (StatementTerm)Term.from_string("((*,{SELF}) --> asexual)");

            food_far = (StatementTerm)Term.from_string("({food} --> [far])");
            food_medium = (StatementTerm)Term.from_string("({food} --> [medium])");
            food_near = (StatementTerm)Term.from_string("({food} --> [near])");
            food_unseen = (StatementTerm)Term.from_string("({food} --> [unseen])");
            animat_far = (StatementTerm)Term.from_string("({animat} --> [far])");
            animat_medium = (StatementTerm)Term.from_string("({animat} --> [medium])");
            animat_near = (StatementTerm)Term.from_string("({animat} --> [near])");
            animat_unseen = (StatementTerm)Term.from_string("({animat} --> [unseen])");
            energy_full = (StatementTerm)Term.from_string("({ENERGY} --> [FULL])");
            self_mated = (StatementTerm)Term.from_string("({SELF} --> [mated])");
            sensorymotor_statements_initialized = true;

            defaults = CreateDefaultPersonality();
        }


        if (SENSORY_TERM_SET == null)
        {
            SENSORY_TERM_SET = new StatementTerm[]
            {
                food_far,
                food_medium,
                food_near,
                food_unseen,
                animat_far,
                animat_medium,
                animat_near,
                animat_unseen,
                energy_full,
                self_mated,
            };

            MOTOR_TERM_SET = new StatementTerm[]
            {
                move_op,
                rotate_right_op,
             //   rotate_left_op,
                eat_op,
                mate_op,
                asexual_op,
                fight_op
            };
        }

        this.body_genome = body_genome;

        beliefs = new();
        if (USE_AND_EVOLVE_CONTINGENCIES())
        {
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

        this.personality_parameters = new();
        if (personality_to_clone != null)
        {
            this.personality_parameters = (PersonalityParameters)personality_to_clone;

        }
        else
        {
            if (EVOLVE_PERSONALITY())
            {
                RandomizePersonalityParameters(ref this.personality_parameters);
            }
            else
            {
                this.personality_parameters = defaults;
            }
        }

        if (NARSGenome.RANDOM_PERSONALITY())
        {
            RandomizePersonalityParameters(ref this.personality_parameters);
        }
    }

    private PersonalityParameters CreateDefaultPersonality()
    {
        PersonalityParameters parameters = new();
        RandomizePersonalityParameters(ref parameters);
        return parameters;
    }

    public static PersonalityParameters DefaultParameters()
    {
        PersonalityParameters personality_parameters = new();
        // k
        personality_parameters.k = 1;

        // T
        personality_parameters.T = 0.51f;

        // Anticipation window
        personality_parameters.Anticipation_Window = 5;

        // Forgetting rate
        personality_parameters.Forgetting_Rate = 10;

        // Event buffer capacity
        personality_parameters.Event_Buffer_Capacity = 10;

        // Table capacity
        personality_parameters.Table_Capacity = 5;

        // Evidential base length
        personality_parameters.Evidential_Base_Length = 20;

        // Time Projection Event
        personality_parameters.Time_Projection_Event = 10;

        // Time ProjectionGoal
        personality_parameters.Time_Projection_Goal = 1;


        return personality_parameters;
    }

    public static void RandomizePersonalityParameters(ref PersonalityParameters personality_parameters)
    {
        // k
        var kRange = GetKRange();
        personality_parameters.k = UnityEngine.Random.Range(kRange.x, kRange.y);

        // T
        var tRange = GetTRange();
        personality_parameters.T = UnityEngine.Random.Range(tRange.x, tRange.y);

        // Anticipation window
        var awRange = GetAnticipationWindowRange();
        personality_parameters.Anticipation_Window = UnityEngine.Random.Range(awRange.x, awRange.y + 1);

        // Forgetting rate
        var frRange = GetForgettingRateRange();
        personality_parameters.Forgetting_Rate = UnityEngine.Random.Range(frRange.x, frRange.y);

        // Event buffer capacity
        var ebcRange = GetEventBufferCapacityRange();
        personality_parameters.Event_Buffer_Capacity = UnityEngine.Random.Range(ebcRange.x, ebcRange.y + 1);

        // Table capacity
        var tcRange = GetTableCapacityRange();
        personality_parameters.Table_Capacity = UnityEngine.Random.Range(tcRange.x, tcRange.y + 1);

        // Evidential base length
        var eblRange = GetEvidentialBaseLengthRange();
        personality_parameters.Evidential_Base_Length = UnityEngine.Random.Range(eblRange.x, eblRange.y + 1);

        // Time Projection Event
        var timeProjectionEventRange = GetTimeProjectionEventRange();
        personality_parameters.Time_Projection_Event = UnityEngine.Random.Range(timeProjectionEventRange.x, timeProjectionEventRange.y);

        // Time ProjectionGoal
        var timeProjectionGoalRange = GetTimeProjectionGoalRange();
        personality_parameters.Time_Projection_Goal = UnityEngine.Random.Range(timeProjectionGoalRange.x, timeProjectionGoalRange.y);
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
    public static void AddIdealBeliefs(List<EvolvableSentence> beliefs)
    {
        (StatementTerm, float?, float?)[] statement_strings = new (StatementTerm, float?, float?)[]
        {
            (CreateContingencyStatement(food_far,move_op,food_near), null, null),
            (CreateContingencyStatement(food_near,eat_op,energy_full), null, null),
            (CreateContingencyStatement(animat_far,move_op,animat_near), null, null),
            (CreateContingencyStatement(energy_full,asexual_op,self_mated), null, null),
            (CreateContingencyStatement(Term.from_string("(&/, " + energy_full + "," + animat_near + ")"),mate_op,self_mated), null, null),
            (CreateContingencyStatement(food_unseen,rotate_right_op,food_far), null, null),
            (CreateContingencyStatement(food_unseen,move_op,food_far), null, null),
            (CreateContingencyStatement(animat_unseen,rotate_right_op,animat_far), null, null),
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
           // (self_mated, null, null),
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

    private static Vector2 GetKRange() => new(1f, 10f);
    private static Vector2 GetTRange() => new(0f, 1f);
    private static Vector2 GetTimeProjectionEventRange() => new(0.0000001f, 10f);
    private static Vector2 GetTimeProjectionGoalRange() => new(0.0000001f, 10f);
    private static Vector2 GetForgettingRateRange() => new(1, 250f);
    private static Vector2Int GetAnticipationWindowRange() => new(1, 30);
    private static Vector2Int GetEventBufferCapacityRange() => new(3, 20);
    private static Vector2Int GetTableCapacityRange() => new(1, 20);
    private static Vector2Int GetEvidentialBaseLengthRange() => new(1, 50);

    // Local helpers
    void MutateFloat(ref float field, Vector2 range, float replaceChance, float mutate_chance)
    {
        if (UnityEngine.Random.value < (1f- mutate_chance)) return; // certain chance to mutate
        if (UnityEngine.Random.value < replaceChance)
        {
            field = UnityEngine.Random.Range(range.x, range.y);
        }
        else
        {
            field += (float)GetPerturbationFromRange(range);
        }
        field = math.clamp(field, range.x, range.y);
    }

    void MutateInt(ref int field, Vector2Int range, float replaceChance, float mutate_chance)
    {
        if (UnityEngine.Random.value < (1f - mutate_chance)) return; // certain chance to mutate

        if (UnityEngine.Random.value < replaceChance)
        {
            // int Random.Range max is exclusive; add +1 to include range.y
            field = UnityEngine.Random.Range(range.x, range.y + 1);
        }
        else
        {
            field += (int)GetPerturbationFromRange(range);
        }
        field = (int)math.clamp(field, range.x, range.y);
    }


    static Vector2 truth_range = new(0f, 1f);
    public override void Mutate()
    {

        float rnd = 0;



        if (USE_AND_EVOLVE_CONTINGENCIES())
        {
            rnd = UnityEngine.Random.value;
            if (rnd < CHANCE_TO_MUTATE_BELIEF_SET)
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

            rnd = UnityEngine.Random.value;
            if (rnd < CHANCE_TO_MUTATE_TRUTH_VALUES)
            {
                const float CHANCE_TO_REPLACE_TRUTH_VALUE = 0.05f;
                const float CHANCE_TO_MUTATE = 0.6f;
                for (int i = 0; i < this.beliefs.Count; i++)
                {
                    EvolvableSentence sentence = this.beliefs[i];
                    MutateFloat(ref sentence.evidence.frequency, truth_range, CHANCE_TO_REPLACE_TRUTH_VALUE, CHANCE_TO_MUTATE);
                    MutateFloat(ref sentence.evidence.confidence, truth_range, CHANCE_TO_REPLACE_TRUTH_VALUE, CHANCE_TO_MUTATE);
                    sentence.evidence.confidence = math.clamp(sentence.evidence.confidence, 0.0001f, 0.9999f);
                    this.beliefs[i] = sentence;
                }
            }
           
        }

        rnd = UnityEngine.Random.value;

        if (EVOLVE_PERSONALITY() && rnd < CHANCE_TO_MUTATE_PERSONALITY_PARAMETERS)
        {
            // tweakable: probability to *replace* a field instead of perturbing it
            const float CHANCE_TO_REPLACE_PARAM = 0.05f;

            const float CHANCE_TO_MUTATE = 0.5f;

            // --- k ---
            var kRange = GetKRange();
            MutateFloat(ref this.personality_parameters.k, kRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

            // --- T ---
            var TRange = GetTRange();
            MutateFloat(ref this.personality_parameters.T, TRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

            // --- Anticipation window ---
            var AnticipationWindowRange = GetAnticipationWindowRange();
            MutateInt(ref this.personality_parameters.Anticipation_Window, AnticipationWindowRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

            // --- Forgetting rate ---
            var ForgettingRateRange = GetForgettingRateRange();
            MutateFloat(ref this.personality_parameters.Forgetting_Rate, ForgettingRateRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

            // --- Event buffer capacity ---
            var EventBufferCapacityRange = GetEventBufferCapacityRange();
            MutateInt(ref this.personality_parameters.Event_Buffer_Capacity, EventBufferCapacityRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

            // --- Table capacity ---
            var TableCapacityRange = GetTableCapacityRange();
            MutateInt(ref this.personality_parameters.Table_Capacity, TableCapacityRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

            // --- Evidential base length ---
            var EvidentialBaseLengthRange = GetEvidentialBaseLengthRange();
            MutateInt(ref this.personality_parameters.Evidential_Base_Length, EvidentialBaseLengthRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

            // --- Time Projection Event ---
            var timeProjectionEventRange = GetTimeProjectionEventRange();
            MutateFloat(ref this.personality_parameters.Time_Projection_Event, timeProjectionEventRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);

            // --- Time Projection Goal ---
            var timeProjectionGoalRange = GetTimeProjectionGoalRange();
            MutateFloat(ref this.personality_parameters.Time_Projection_Goal, timeProjectionGoalRange, CHANCE_TO_REPLACE_PARAM, CHANCE_TO_MUTATE);
        }

    }



 
    public void AddNewRandomBelief()
    {
        StatementTerm statement = CreateContingencyStatement(GetRandomSensoryTerm(), GetRandomMotorTerm(), GetRandomSensoryTerm());
        string statement_string = statement.ToString();
        if (!belief_statement_strings.ContainsKey(statement_string))
        {
            float f = UnityEngine.Random.Range(0.5f, 1f);
            float c = UnityEngine.Random.Range(0.0f, 1f);
            EvolvableSentence sentence = new(statement: statement,
                   new float2(f, c));
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

    public (NARSGenome, NARSGenome) Reproduce(NARSGenome parent2genome)
    {
        NARSGenome parent1 = this;
        NARSGenome parent2 = (NARSGenome)parent2genome;
        int longer_array = math.max(parent1.beliefs.Count, parent2.beliefs.Count);

        NARSGenome offspring1 = new();
        offspring1.beliefs.Clear();
        offspring1.belief_statement_strings.Clear();
        NARSGenome offspring2 = new();
        offspring2.beliefs.Clear();
        offspring2.belief_statement_strings.Clear();

        if (USE_AND_EVOLVE_CONTINGENCIES())
        {
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
        }

        if (EVOLVE_PERSONALITY())
        {
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
