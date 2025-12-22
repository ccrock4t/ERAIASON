/*
    Author: Christian Hahm
    Created: May 27, 2022
    Purpose: NARS definition
*/


using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class NARS : Mind
{
    /*
       NARS Class
    */

    public int NEXT_STAMP_ID = 0;
    public NARSConfig config;

    public NARSInferenceEngine inferenceEngine;
    public Memory memory;

    public ConcurrentQueue<Sentence> global_buffer;
    public TemporalModule temporal_module;

    public HelperFunctions helperFunctions;

    public int current_cycle_number;

    List<(int, StatementTerm, float, List<string>)> operation_queue; // operations the system has queued to executed
    Goal current_operation_goal_sequence;
    string last_executed = "";


    // enforce milliseconds per working cycle
    int cycle_begin_time = 0;

    // keeps track of number of working cycles per second
    int cycles_per_second_timer = 0;
    int last_working_cycle = 0;

    public System.Random random;


    public NARSGenome genome;
    public NARS(NARSGenome nars_genome)
    {
        this.random = new System.Random();
        this.config = new NARSConfig();
        this.inferenceEngine = new NARSInferenceEngine(this);
        this.memory = new Memory(this.config.MEMORY_CONCEPT_CAPACITY, this);

        this.helperFunctions = new HelperFunctions(this);

        this.global_buffer = new();
        this.temporal_module = new(this, this.config.EVENT_BUFFER_CAPACITY);


        this.operation_queue = new List<(int, StatementTerm, float, List<string>)>(); // operations the system has queued to executed

        this.genome = nars_genome;
        SetupUsingGenome(nars_genome);
    }

    void SetupUsingGenome(NARSGenome nars_genome)
    {
        // add the instinctual eternal beliefs
        foreach (var gene in nars_genome.beliefs)
        {
            var belief = new Judgment(this, gene.statement, gene.evidence);
            CompoundTerm subject = (CompoundTerm)((StatementTerm)belief.statement).get_subject_term();
            SendInput(belief);
        }


        this.config.k = nars_genome.personality_parameters.k;
        this.config.T = nars_genome.personality_parameters.T;
        this.config.ANTICIPATION_WINDOW = nars_genome.personality_parameters.Anticipation_Window;
        this.config.FORGETTING_RATE = nars_genome.personality_parameters.Forgetting_Rate;
        this.config.EVENT_BUFFER_CAPACITY = nars_genome.personality_parameters.Event_Buffer_Capacity;
        this.config.TABLE_DEFAULT_CAPACITY = nars_genome.personality_parameters.Table_Capacity;
        this.config.MAX_EVIDENTIAL_BASE_LENGTH = nars_genome.personality_parameters.Evidential_Base_Length;
        this.config.PROJECTION_DECAY_EVENT = nars_genome.personality_parameters.Time_Projection_Event;
        this.config.PROJECTION_DECAY_DESIRE = nars_genome.personality_parameters.Time_Projection_Goal;
        this.config.RUNTIME_COMPOUNDS1 = nars_genome.personality_parameters.RuntimeCompounds1 == 1;
        this.config.RUNTIME_COMPOUNDS2 = nars_genome.personality_parameters.RuntimeCompounds2 == 1;
        this.config.RUNTIME_COMPOUNDS3 = nars_genome.personality_parameters.RuntimeCompounds3 == 1;
        this.config.SINE_SPEED = nars_genome.personality_parameters.Sine_Speed;

    }


    public void do_working_cycle()
    {
        /*
            Performs 1 working cycle.
            In each working cycle, NARS either *Observes* OR *Considers*:
        */

        //time.sleep(0.1)
        this.current_cycle_number++;

        // OBSERVE
        this.Observe();

        // global buffer
        int buffer_len = this.global_buffer.Count();

        //if (buffer_len >= 3 * global_buffer.capacity / 4) Debug.LogWarning("NARS buffer about  to overflow");
        int tasks_left = buffer_len;
        while (tasks_left > 0)
        {
            this.global_buffer.TryDequeue(out Sentence buffer_item);
            tasks_left--;

            if (buffer_item is Goal && this.inferenceEngine.get_sentence_value_time_decayed(buffer_item).confidence < 0.1)
            {
                continue;
            }

            // process task
            this.process_sentence_initial(buffer_item);
            
        }




        //if (NARSGenome.USE_LEARNING())
        //{
        //    this.temporal_module.UpdateAnticipations();
        //    Parallel.ForEach(this.memory.concepts_bag, concept_item =>
        //    {
        //        var concept = concept_item.obj;
        //        concept.belief_table.Forget();
        //    });
        //}

    }


    //public void do_working_cycles(int cycles)
    //{
    //    /*
    //        Performs the given number of working cycles.
    //    */
    //    for (int i = 0; i < cycles; i++)
    //    {
    //        this.do_working_cycle();
    //    }
    //}

    public void Observe()
    {
        /*
            Process a task from the global buffer.

            This function should never produce new tasks.
        */
        //
    }

    public void Consider(Concept? concept = null)
    {
        ///*
        //    Process a belief from a random concept in memory.

        //    This function can result in new tasks

        //    :param: concept: concept to consider. If null, picks a random concept
        //*/
        //Item<Concept>? concept_item = null;
        //if (concept == null)
        //{
        //    concept_item = this.memory.get_random_concept_item();
        //    if (concept_item == null) return; // nothing to ponder
        //    concept = concept_item.obj;
        //}


        //// If concept is not named by a statement, get a related concept that is a statement
        //int attempts = 0;
        //int max_attempts = 2;
        //while (attempts < max_attempts && !(((concept.term is StatementTerm) || ((concept.term is CompoundTerm) && !((CompoundTerm)concept.term).is_first_order()))))
        //{
        //    if (concept.term_links.GetCount() > 0)
        //    {
        //        concept = concept.term_links.peek().obj;
        //    }
        //    else
        //    {
        //        break;
        //    }

        //    attempts += 1;
        //}

        //// debugs
        //if (this.config.DEBUG)
        //{
        //    string str = "Considering concept: " + concept.term.ToString();
        //    if (concept_item != null) str += concept_item.budget.ToString();
        //    if (concept.belief_table.GetCount() > 0) str += " expectation: " + this.inferenceEngine.get_expectation(concept.belief_table.peek()).ToString();
        //    if (concept.desire_table.GetCount() > 0) str += " desirability: " + concept.desire_table.peek().get_desirability(this);
        //    Debug.Log(str);
        //}

        ////Debug.Log("CONSIDER: " + str(concept))

        //if (concept != null && attempts != max_attempts)
        //{
        //    // process a belief && desire
        //    if (concept.belief_table.GetCount() > 0)
        //    {
        //        this.process_judgment_continued(concept.belief_table.peek());  // process most confident belief
        //    }


        //    if (concept.desire_table.GetCount() > 0)
        //    {
        //        this.process_goal_continued(concept.desire_table.peek()); // process most confident goal
        //    }


        //    // decay priority;
        //    if (concept_item != null)
        //    {
        //        this.memory.concepts_bag.decay_item(concept_item.key, this.config.PRIORITY_DECAY_VALUE);
        //    }
        //}
    }



    public void process_sentence_initial(Sentence j)
    {
        /*
            Initial processing for a Narsese sentence
        */
        Term task_statement_term = j.statement;
        // if (task_statement_term.contains_variable()) return; // todo handle variables

        // statement_concept_item = this.memory.peek_concept_item(task_statement_term)
        // statement_concept = statement_concept_item.object


        // get (|| create if necessary) statement concept, && sub-term concepts recursively
        if (j is Judgment)
        {
            this.process_judgment_initial((Judgment)j);
        }
        else if (j is Question)
        {
            this.process_question_initial((Question)j);
        }
        else if (j is Goal)
        {
            this.process_goal_initial((Goal)j);
        }

        //     if not task.sentence.is_event(){
        //         statement_concept_item.budget.set_quality(0.99)
        //         this.memory.concepts_bag.change_priority(key=statement_concept_item.key,
        //                                                  new_priority=0.99)

        // this.memory.concepts_bag.strengthen_item(key=statement_concept_item.key)
        //print("concept strengthen " + str(statement_concept_item.key) + " to " + str(statement_concept_item.budget))


    }


    public void process_judgment_initial(Judgment j)
    {
        /*
            Processes a Narsese Judgment Task
            Insert it into the belief hallOfFameTable && revise it with another belief

            :param Judgment Task to process
        */
        if (NARSGenome.USE_LEARNING())
        {
            if (j.is_event())
            {
                // only put non-derived atomic events in temporal module for now
                this.temporal_module.PUT_NEW(j);
            }
        }


        Item<Concept> task_statement_concept_item = this.memory.peek_concept_item(j.statement);
        if (task_statement_concept_item == null) return;

        // this.memory.concepts_bag.strengthen_item_quality(task_statement_concept_item.key);

        Concept statement_concept = task_statement_concept_item.obj;

        statement_concept.belief_table.put(j);

       // Judgment current_belief = statement_concept.belief_table.peek();
       // this.process_judgment_continued(current_belief);

        var j_term = j.get_statement_term();
        if (temporal_module.DoesAnticipate(j_term))
        {
            // stop anticipating now the event is  confirmed
            temporal_module.RemoveAnticipations(j_term);
        }
    }

    public void process_judgment_continued(Judgment j1, bool revise = true)
    {
        /*
            Continued processing for Judgment

            :param j1: Judgment
            :param related_concept: concept related to judgment with which to perform semantic inference
        */
        if (this.config.DEBUG)
        {
            Debug.Log("Continued Processing JUDGMENT: " + j1.ToString());
        }

        // get terms from sentence
        Term statement_term = j1.statement;

        // do regular semantic inference;
        //List<Sentence> results = this.process_sentence_semantic_inference(j1);
        //foreach (Sentence result in results)
        //{
        //    this.global_buffer.Enqueue(result);
        //}
    }


    public void process_question_initial(Question j)
    {
        Item<Concept> task_statement_concept_item = this.memory.peek_concept_item(j.statement);
        if (task_statement_concept_item == null) return;

        //this.memory.concepts_bag.strengthen_item_quality(task_statement_concept_item.key);

        Concept task_statement_concept = task_statement_concept_item.obj;
        // get the best answer from concept belief hallOfFameTable
        Judgment best_answer = task_statement_concept.belief_table.peek();
        Sentence? j1 = null;
        if (best_answer != null)
        {
            // Answer the question
            if (j.is_from_input && j.needs_to_be_answered_in_output)
            {
                Debug.Log("OUT: " + best_answer.ToString());
                j.needs_to_be_answered_in_output = false;
            }

            // do inference between answer && a related belief
            j1 = best_answer;
        }
        else
        {
            // do inference between question && a related belief
            j1 = j;
        }

        this.process_sentence_semantic_inference(j1);

    }


    public void process_goal_initial(Goal j)
    {
        /*
            Processes a Narsese Goal Task

            :param Goal Task to process
        */

        /*
            Initial Processing

            Insert it into the desire hallOfFameTable || revise with the most confident desire
        */

        Item<Concept> statement_concept_item = this.memory.peek_concept_item(j.statement);
        Concept statement_concept = statement_concept_item.obj;
        //this.memory.concepts_bag.change_quality(statement_concept_item.key, 0.999f);

        // store the most confident desire
        statement_concept.desire_table.put(j);

        Goal current_desire = statement_concept.desire_table.take();

        this.process_goal_continued(current_desire);

        statement_concept.desire_table.put(current_desire);


    }

    ConcurrentBag<Judgment> variable_unifications_for_goal_derivation = new();
    public void process_goal_continued(Goal j)
    {
        /*
            Continued processing for Goal

            :param j: Goal
            :param related_concept: concept related to goal with which to perform semantic inference
        */
        if (this.config.DEBUG) Debug.Log("Continued Processing GOAL: " + j.ToString());

        Term statement = j.statement;

        Concept statement_concept = this.memory.peek_concept(statement);

        // see if it should be pursued
        bool should_pursue = this.inferenceEngine.localRules.Decision(j);
        if (!should_pursue)
        {
            //Debug.Log("Goal failed decision-making rule " + j.ToString())
            if (this.config.DEBUG && statement.is_op())
            {
                Debug.Log("Operation failed decision-making rule " + j.ToString());

            }
            return;  // Failed decision-making rule
        }
        else
        {
            //Debug.Log("Goal passed decision-making rule " + j.ToString());
        }


        // at this point the system wants to pursue this goal.
        // now check if it should be inhibited (negation == more highly desired).
        // negated_statement = j.statement.get_negated_term()
        // negated_concept = this.memory.peek_concept(negated_statement)
        // if len(negated_concept.desire_table) > 0:
        //     desire = j.get_expectation()
        //     neg_desire = negated_concept.desire_table.peek().get_expectation()
        //     should_inhibit = neg_desire > desire
        //     if should_inhibit:
        //         Debug.Log("Event was inhibited " + j.get_term_string())
        //         return  // Failed inhibition decision-making rule
        if (statement.is_op() && j.statement.connector != TermConnector.Negation)
        {
            //if not j.executed:
            //this.queue_operation(j);
            //if(statement is CompoundTerm)
            //{
            //    this.execute_atomic_operation((StatementTerm)j.statement, j.get_desirability(this), new List<string>());
            //}
            //else
            //{
            //    this.execute_atomic_operation((StatementTerm)j.statement, j.get_desirability(this), new List<string>());
            //}
                
            //    j.executed = false
        }
        else
        {
            // check if goal already achieved
            Judgment desire_event = statement_concept.belief_table.peek();
            if (desire_event != null)
            {
                if (this.inferenceEngine.is_positive(desire_event))
                {
                    // Debug.Log(desire_event.ToString() + " is positive for goal: " + j.ToString());
                    return;  // Return if goal is already achieved
                }
            }

            if (statement is CompoundTerm compoundStatement && compoundStatement.connector == TermConnector.SequentialConjunction)
            {
                if (statement.connector == null)
                {
                    Debug.LogError("null connector for statement " + statement);
                    return;
                }
          
                // if it's a conjunction (A &/ B), simplify using true beliefs (e.g. A) or derive a goal for A! if A is false
                Term S = (Term)((CompoundTerm)statement).subterms[0];
                Concept first_subterm_concept = this.memory.peek_concept(S);

                Term sensory_predicate;
                Term sensory_subject;
                if (S is CompoundTerm sComp)
                {
                    StatementTerm first_statement_in_S = (StatementTerm)sComp.subterms[0];
                    sensory_predicate = first_statement_in_S.get_predicate_term();
                    sensory_subject = first_statement_in_S.get_subject_term();
                }
                else if(S is StatementTerm sState)
                {
                    sensory_predicate = sState.get_predicate_term();
                    sensory_subject = sState.get_subject_term();
                }
                else
                {
                    Debug.LogError("error");
                    return;
                }

                    Judgment S_belief = null;
                if (sensory_subject is VariableTerm)
                {
                    variable_unifications_for_goal_derivation.Clear();
                    // find judgments which satisfy the variable, if any
                    Parallel.ForEach(this.memory.concepts_bag, concept_item =>
                    {
                        var concept = concept_item.obj;
                        var term = concept.term;
                        if (term is StatementTerm statement && term.is_first_order())
                        {
                            if (statement.get_predicate_term() is AtomicTerm atom)
                            {
                                if (atom == sensory_predicate)
                                {
                                    // predicates match, so it matches the variable subjec
                                    var judgment = concept.belief_table.peek_first_interactable(j);
                                    if (judgment != null)
                                    {
                                        variable_unifications_for_goal_derivation.Add(judgment);
                                    }
                                }
                            }
                        }

                    });

                    var candidates = variable_unifications_for_goal_derivation.ToArray();
                    if (candidates.Length > 0)
                    {
                        // 1. Compute total weight (sum of frequencies)
                        float totalWeight = 0f;
                        foreach (var jdg in candidates)
                        {
                            // clamp to >= 0 just in case
                            float w = Mathf.Max(0f, this.inferenceEngine.get_expectation(jdg));   // <-- use the right property here
                            totalWeight += w;
                        }

                        Judgment picked = null;

                        if (totalWeight > 0f)
                        {
                            // 2. Pick a random point in [0, totalWeight)
                            float r = UnityEngine.Random.Range(0f, totalWeight);

                            // 3. Walk through and find where r falls
                            float cumulative = 0f;
                            foreach (var jdg in candidates)
                            {
                                float w = Mathf.Max(0f, this.inferenceEngine.get_expectation(jdg));  // same property
                                cumulative += w;
                                if (r <= cumulative)
                                {
                                    picked = jdg;
                                    break;
                                }
                            }
                        }

                        // Fallback: if all frequencies were 0 (or something weird), use uniform
                        if (picked == null)
                        {
                            int index = UnityEngine.Random.Range(0, candidates.Length);
                            picked = candidates[index];
                        }

                        S_belief = picked;
                    }


                }
                else
                {
                    S_belief = first_subterm_concept.belief_table.peek_first_interactable(j);
                }


                if (S_belief != null && this.inferenceEngine.is_positive(S_belief))
                {
                    List<Sentence> results = null;
                    if (sensory_subject is VariableTerm)
                    {
                        StatementTerm M = (StatementTerm)((CompoundTerm)statement).subterms[1];
                        CompoundTerm M_subject = (CompoundTerm)M.get_subject_term();
                        Term concretized_term = ((StatementTerm)S_belief.statement).get_subject_term();
                        var subterms = new List<Term> { M_subject.subterms[0], //SELF
                             concretized_term // block1, block2,etc
                        };
                        CompoundTerm unified_motor_compound = TermHelperFunctions.TryGetCompoundTerm(subterms, TermConnector.Product);
                        StatementTerm unified_goal_statement = new(unified_motor_compound, M.get_predicate_term(), Copula.Inheritance);
                        var value = this.inferenceEngine.truthValueFunctions.F_Deduction(
                            S_belief.evidential_value.frequency,
                            S_belief.evidential_value.confidence,
                            j.evidential_value.frequency,
                            j.evidential_value.confidence);
                        Goal derived_variable_unified_goal = new Goal(this, unified_goal_statement, value);

                        results = new()
                        {
                            derived_variable_unified_goal
                        };
                    }
                    else
                    {

                        // the first component of the goal is positive, do inference and derive the remaining goal component
                        results = this.inferenceEngine.do_semantic_inference_two_premise(j, S_belief);
                    }


                    foreach (Sentence result in results)
                    {
                        if(result is Goal && result.statement is CompoundTerm compResult)
                        {
                            foreach(StatementTerm element in compResult.subterms)
                            {
                                var goal = new Goal(this, element, result.evidential_value, occurrence_time: this.current_cycle_number);
                                this.global_buffer.Enqueue(result);
                            }
                        }
                        else
                        {
                            this.global_buffer.Enqueue(result);
                        }
                        
                    }
                    return; // done deriving goals
                }
                else
                {
                    // if (sensory_predicate is VariableTerm)
                    //  {
                        //var unification_term = Term.from_string(AlpineGridManager.GetRandomDirectionString());
                        //StatementTerm first_subterm_statement_unified = new StatementTerm(sensory_subject, unification_term, Copula.Inheritance);
                        //S = first_subterm_statement_unified;
                    //  }


                    //if(S is CompoundTerm c && S.connector == TermConnector.ParallelConjunction)
                    //{
                    //    foreach(var subterm in c.subterms)
                    //    {
                    //        Goal subtermgoal = (Goal)this.helperFunctions.create_resultant_sentence_one_premise(j, c.subterms[1], null, j.evidential_value);
                    //        this.global_buffer.Enqueue(subtermgoal);
                    //    }
                    //}
                    //else
                    //{
                        //first belief was not positive, so derive a goal to make it positive
                        Goal first_subterm_goal = (Goal)this.helperFunctions.create_resultant_sentence_one_premise(j, S, null, j.evidential_value);
                        this.global_buffer.Enqueue(first_subterm_goal);
                    //}
                }
                
                //else if (statement.connector == TermConnector.Negation && TermConnectorMethods.is_conjunction(((CompoundTerm)statement).subterms[0].connector))
                //{
                //    // if it's a negated conjunction (--,(A &/ B))!, simplify using true beliefs (e.g. A.)
                //    // (--,(A &/ B)) ==> D && A
                //    // induction
                //    // :- (--,(A &/ B)) && A ==> D :- (--,B) ==> D :- (--,B)!
                //    CompoundTerm conjunction = (CompoundTerm)((CompoundTerm)statement).subterms[0];
                //    Term subterm = conjunction.subterms[0];
                //    Concept subterm_concept = this.memory.peek_concept(subterm);
                //    Judgment belief = subterm_concept.belief_table.peek();
                //    if (belief != null && this.inferenceEngine.is_positive(belief))
                //    {
                //        // the first component of the goal is negative, do inference and derive the remaining goal component
                //        List<Sentence> results = this.inferenceEngine.do_semantic_inference_two_premise(j, belief);
                //        foreach (Sentence result in results)
                //        {
                //            this.global_buffer.Enqueue(result);
                //        }

                //        return; // done deriving goals
                //    }
                //}
            }
            else
            {
                // process j! with random context-relevant explanation E = (P =/> j).
                int explanation_count = statement_concept.explanation_links.GetCount();

                if (explanation_count == 0)
                {
                    if (NARSGenome.USE_LEARNING())
                    {
                        // no explanations, so babble to learn one
                        MotorBabble();
                    }
                    return;
                }
                // foreach (var explanation_concept in statement_concept.explanation_links)
                // {
                var random_belief = this.memory.get_random_bag_explanation(j); //explanation_concept.obj.belief_table.peek();
                if (random_belief != null)
                {

                    CompoundTerm subject = (CompoundTerm)((StatementTerm)random_belief.statement).get_subject_term();

                    var results = this.inferenceEngine.do_semantic_inference_two_premise(j, random_belief); // {E, J!} :- P!

                    foreach (var result in results)
                    {
                        if (result.statement is CompoundTerm)
                        {
                            bool is_conjunction = TermConnectorMethods.is_conjunction(result.statement.connector);
                            if (is_conjunction)  // if it's a conjunction (A &/ B), 
                            {

                                Term first_subterm_statement = ((CompoundTerm)result.statement).subterms[0];
                                Concept first_subterm_concept = this.memory.peek_concept(first_subterm_statement);
                                Judgment first_subterm_belief = first_subterm_concept.belief_table.peek_first_interactable(j);

                                Term second_subterm_statement = ((CompoundTerm)result.statement).subterms[1];
                                Concept second_subterm_concept = this.memory.peek_concept(second_subterm_statement);
                                Judgment second_subterm_belief = first_subterm_concept.belief_table.peek_first_interactable(j);

                                //if (NARSGenome.USE_LEARNING())
                                //{
                                //    if (S_belief != null
                                //        && this.inferenceEngine.is_positive(S_belief)
                                //        && second_subterm_statement.is_op())
                                //    {
                                //        // since the contextual event is true,and the second term  is a motor op, form an anticipation for the postcondition
                                //        this.temporal_module.Anticipate(j.get_statement_term());
                                //    }
                                //}

                            }
                        }

                        this.global_buffer.Enqueue(result);
                    }
                }
                //}




            }
        }
    }


    void MotorBabble()
    {
        var motor_terms = NARSGenome.MOTOR_TERM_SET;
        int rnd = UnityEngine.Random.Range(0, motor_terms.Count);
        var motor_term = motor_terms[rnd];
        SendInput(new Goal(this, motor_term, new EvidentialValue(1.0f, 0.99f), occurrence_time: current_cycle_number));
    }

    public List<Sentence> process_sentence_semantic_inference(Sentence j1, Concept? related_concept = null)
    {
        /*
            Processes a Sentence with a belief from a related concept.

            :param j1 - sentence to process
            :param related_concept - (Optional) concept from which to fetch a belief to process the sentence with

            #todo handle variables
        */
        List<Sentence> results = new List<Sentence>();
        if (this.config.DEBUG) Debug.Log("Processing: " + j1.ToString());
        Term statement_term = j1.statement;
        // get (or create if necessary) statement concept, and sub-term concepts recursively
        Concept statement_concept = this.memory.peek_concept(statement_term);

        if (related_concept == null)
        {
            if (this.config.DEBUG) Debug.Log("Processing: Peeking randomly related concept");

            if (statement_term is CompoundTerm)
            {
                //if (statement_concept.prediction_links.GetCount() > 0)
                //{
                //    related_concept = statement_concept.prediction_links.peek().obj;
                //}
            }
            else if (statement_term is StatementTerm && !((StatementTerm)statement_term).is_first_order())
            {

                // subject_term = statement_term.get_subject_term()
                // related_concept = this.memory.peek_concept(subject_term)
            }
            else if (statement_term is StatementTerm && ((StatementTerm)statement_term).is_first_order() && j1.is_event())
            {
                if (statement_concept.explanation_links.GetCount() > 0)
                {
                    related_concept = statement_concept.explanation_links.peek().obj;
                }
                //else if (statement_concept.superterm_links.GetCount() > 0)
                //{
                //    related_concept = statement_concept.superterm_links.peek().obj;
                //}
            }
            else
            {
                related_concept = this.memory.get_semantically_related_concept(statement_concept);
            }

            if (related_concept == null) return results;
        }
        else
        {
            Debug.Log("Processing: Using related concept " + related_concept.ToString());
        }


        // check for a belief we can interact with
        Sentence j2 = related_concept.belief_table.peek();

        if (j2 == null)
        {
            if (this.config.DEBUG) Debug.Log("No related beliefs found for " + j1.ToString());
            return results;  // done if can't interact
        }

        results = this.inferenceEngine.do_semantic_inference_two_premise(j1, j2);

        // check for a desire we can interact with
        j2 = related_concept.desire_table.peek_random();

        if (j2 == null)
        {
            if (this.config.DEBUG) Debug.Log("No related goals found for " + j1.ToString());
            return results; // done if can't interact
        }

        results.AddRange(this.inferenceEngine.do_semantic_inference_two_premise(j1, j2));

        return results;
    }

    /*
        OPERATIONS
    */




    public void execute_atomic_operation(StatementTerm operation_statement_to_execute, float desirability, List<string> parents)
    {
        Concept statement_concept = this.memory.peek_concept(operation_statement_to_execute);

        // execute an atomic operation immediately
        string predicate_str = operation_statement_to_execute.get_predicate_term().ToString();
        int current_cycle = this.current_cycle_number;
        string str = "EXE: ^" + predicate_str +
            " cycle #" + current_cycle +
            " based on desirability: " + desirability.ToString();

        //Debug.Log(str);

        // input the operation statement
        //Judgment operation_event = new Judgment(this, operation_statement_to_execute, new EvidentialValue(1.0f, 0.99f), this.current_cycle_number);
        //this.process_judgment_initial(operation_event);
    }


    public void SendInput(Sentence input_sentence)
    {

        //Debug.Log("Sending input: " + this.nars.helperFunctions.sentence_to_string(input_sentence));
        if (input_sentence == null)
        {
            Debug.LogError("NULl input to NARS is invalid");
            return;
        }
        input_sentence.is_from_input = true;

        this.global_buffer.Enqueue(input_sentence);
    }




    public void Dispose()
    {

    }

    public System.Threading.Tasks.Task task;

    public override void SaveToDisk()
    {
        Debug.LogWarning("Saving NARS not yet implemented");
    }


    public float GetGoalActivation(StatementTerm goal_statement)
    {
        var op_table = this.memory.peek_concept(goal_statement).desire_table;
        if (op_table.GetCount() > 0)
        {
            var op = op_table.peek();
            return op.get_motor_activation(this);
        }
        else
        {
            return 0;
        }
    }



    Dictionary<string, float> stored_activations = new();
    //this function can be used  even during NARS  working cycle
    public float GetStoredActivation(StatementTerm goal_statement)
    {
        return stored_activations[goal_statement.ToString()];
    }


    public void SetStoredActivation(StatementTerm goal_statement)
    {
        float activation = GetGoalActivation(goal_statement);
        stored_activations[goal_statement.ToString()] = activation;
    }

    public override void ScheduleWorkingCycle()
    {
        do_working_cycle();
    }

}