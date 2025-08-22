/*
    Author: Christian Hahm
    Created: May 24, 2022
    Purpose: Holds data structure implementations that are specific / custom to NARS
*/
using Priority_Queue;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Unity.Entities.UniversalDelegates;
using UnityEngine.Purchasing;
using static BodyGenome;


public class Buffer<T> : ItemContainer<T>
{
    PriorityQueue<Item<T>, float> priority_queue;

    public Buffer(int capacity) : base(capacity)
    {
        this.priority_queue = new PriorityQueue<Item<T>, float>(new DecendingComparer<float>());
    }

    public Item<T>? take() {
        /*
            Take the max priority item
            :return:
        */
        if(this.GetCount() == 0) return null;
        Item<T> item = this.priority_queue.Dequeue();
        this._take_from_lookup_dict(item.key);
        return item;
    }

    public Item<T>? peek(string? key) {
        /*
            Peek item with highest priority
            O(1)

            Returns None if depq is empty
        */
        if(this.GetCount() == 0) return null;
        if (key == null) {
            return this.priority_queue.First;
        }
        else {
            return base.peek_using_key(key);
        }

    }


    public override Item<T> PUT_NEW(T obj)
    {
        Item<T> item = base.PUT_NEW(obj);
        this.priority_queue.Enqueue(item, item.budget.get_priority());
        return item;
    }

    class DecendingComparer<TKey> : IComparer<float>
    {
        public int Compare(float x, float y)
        {
            return y.CompareTo(x);
        }
    }
}


public class TemporalModule
{
    /*
        Performs temporal composition
                and
            anticipation (negative evidence for predictive implications)
    */
    private readonly NARS nars;
    private readonly int capacity;
    private readonly List<Judgment> temporal_chain;

    public TemporalModule(NARS nars, int capacity)
    {
        this.nars = nars;
        this.capacity = capacity;
        this.temporal_chain = new List<Judgment>(capacity);
    }

    /// <summary>
    /// Inserts a new judgment, keeps list sorted by occurrence_time,
    /// and pops the oldest if capacity is exceeded.
    /// </summary>
    public Judgment PUT_NEW(Judgment obj)
    {
        // Insert in sorted order by occurrence_time
        int idx = temporal_chain.BinarySearch(obj, JudgmentTimeComparer.Instance);
        if (idx < 0) idx = ~idx; // BinarySearch returns bitwise complement of insert index
        temporal_chain.Insert(idx, obj);

        // Check capacity
        Judgment popped = null;
        if (temporal_chain.Count > capacity)
        {
            // Oldest = first element (smallest occurrence_time)
            popped = temporal_chain[0];
            temporal_chain.RemoveAt(0);
        }

        this.process_temporal_chaining();

        return popped;
    }

    public int GetCount() => temporal_chain.Count;

    // Optional: expose read-only access
    public IReadOnlyList<Judgment> Items => temporal_chain.AsReadOnly();

    /// <summary>
    /// Comparer for sorting judgments by occurrence_time
    /// </summary>
    private class JudgmentTimeComparer : IComparer<Judgment>
    {
        public static readonly JudgmentTimeComparer Instance = new JudgmentTimeComparer();
        public int Compare(Judgment x, Judgment y)
        {
            return x.stamp.occurrence_time.CompareTo(y.stamp.occurrence_time);
        }
    }


    void process_temporal_chaining() {
        if (this.GetCount() >= 3)
        {
            this.temporal_chaining();
            //this.temporal_chaining_2_imp();
        }

    }

    public Judgment GetMostRecentEventTask()
    {
        if (temporal_chain == null || temporal_chain.Count == 0)
            return null;

        // Assuming Judgment.Object is actually an EventTask
        return temporal_chain[^1];
    }


    public void temporal_chaining()
    {
        /*
            Perform temporal chaining

            Produce all possible forward implication statements using temporal induction && intersection (A && B)

            for the latest statement in the chain
        */

        var temporalChain = this.temporal_chain;
        int numOfEvents = temporalChain.Count;

        if (numOfEvents == 0) return;


        void ProcessSentence(Sentence derivedSentence)
        {
            if (derivedSentence != null && this.nars != null)
            {
                this.nars.global_buffer.PUT_NEW(derivedSentence);
            }
        }

        // Loop over all earlier events A
        for (int i = 0; i < numOfEvents - 2; i++)
        {
            var eventA = temporalChain[i];
            if (eventA == null) continue;



            // Validate
            if (!(eventA.statement is StatementTerm))
            {
                continue;
            }
            for (int j = i + 1; j < numOfEvents - 1; j++)
            {
                var eventB = temporalChain[j];
                if (eventA == null) continue;



                // Validate
                if (!(eventB.statement is StatementTerm))
                {
                    continue;
                }

                for (int k = j + 1; k < numOfEvents; k++)
                {
                    var eventC = temporalChain[k];
                    if (eventC == null) continue;
                    // Validate
                    if (!(eventC.statement is StatementTerm))
                    {
                        continue;
                    }

                    if (eventA.statement == eventB.statement || eventA.statement == eventC.statement || eventB.statement == eventC.statement)
                    {
                        continue;
                    }
                    if (!eventB.statement.is_op()) continue;
                    if (eventA.statement.is_op() || eventC.statement.is_op()) continue;
         
                    // Do inference
                    var conjunction = this.nars.inferenceEngine.temporalRules.TemporalIntersection(eventA, eventB);
                 
                    conjunction.stamp.occurrence_time = eventA.stamp.occurrence_time;
                    var implication = this.nars.inferenceEngine.temporalRules.TemporalInduction(conjunction, eventC);
                    implication.evidential_value.frequency = 1.0f;
                    implication.evidential_value.confidence = this.nars.helperFunctions.get_unit_evidence();
                    ProcessSentence(implication);

                }

            }
        }
    }

    public struct Anticipation
    {
        public Term term_expected;
        public int time_remaining;
    }

    public List<Anticipation> anticipations = new();
    Dictionary<Term, int> anticipations_dict = new();
    public void Anticipate(Term term_to_anticipate)
    {
        Anticipation anticipation = new Anticipation();
        anticipation.term_expected = term_to_anticipate;
        anticipation.time_remaining = this.nars.config.ANTICIPATION_WINDOW;
        anticipations.Add(anticipation);
        if (anticipations_dict.ContainsKey(term_to_anticipate))
        {
            anticipations_dict[term_to_anticipate]++;
        }
        else
        {
            anticipations_dict.Add(term_to_anticipate, 1);
        }
    }

    public void UpdateAnticipations()
    {
        for (int i = anticipations.Count - 1; i >= 0; i--)
        {
            Anticipation a = anticipations[i];

            a.time_remaining--;

            if (a.time_remaining <= 0)
            {
                anticipations.RemoveAt(i);
                anticipations_dict[a.term_expected]--;
                if (anticipations_dict[a.term_expected] <= 0)
                {
                    anticipations_dict.Remove(a.term_expected);
                }

                // disappoint; the anticipation failed
                var disappoint = new Judgment(this.nars, a.term_expected,new EvidentialValue(0.0f,this.nars.helperFunctions.get_unit_evidence()));
                this.nars.global_buffer.PUT_NEW(disappoint);
            }
            else
            {
                anticipations[i] = a; // write back updated struct
            }
        }
    }

    internal bool DoesAnticipate(Term term)
    {
        return anticipations_dict.ContainsKey(term);
    }

    public void RemoveAnticipations(Term term)
    {
        for (int i = anticipations.Count - 1; i >= 0; i--)
        {
            Anticipation a = anticipations[i];

            if (a.term_expected == term) 
            {
                anticipations.RemoveAt(i);
            }
        }
        anticipations_dict.Remove(term);
    }
}

   