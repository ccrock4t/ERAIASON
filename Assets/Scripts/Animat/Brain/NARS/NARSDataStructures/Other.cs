/*
    Author: Christian Hahm
    Created: May 25, 2022
    Purpose: Holds data structure implementations that are specific / custom to NARS
*/
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;


public class Table<T> where T : Sentence
{
    /*
        NARS Table, stored within Concepts.
        Tables store Narsese sentences using a priority queue, where priority is sentence confidence
        Sorted by highest-confidence.
        It purges lowest-confidence items when it overflows.
    */

    NARS nars;

    int capacity;
    SortedList<float, T> priority_queue;


    public Table(int capacity, NARS nars)
    {
        this.capacity = capacity;
        priority_queue = new(
            Comparer<float>.Create((x, y) =>
            {
                int result = y.CompareTo(x);

                if (result == 0) return 1;
                else return result;

            })
        );
        this.nars = nars;
    }

    public void put(T sentence)
    {
        /*
            Insert a Sentence into the depq, sorted by confidence (time-projected confidence if it's an event).
        */
        if (sentence.is_event() && this.GetCount() > 0)
        {
            var current_stored_event = this.take();
            bool can_interact = EvidentialBase.may_interact(sentence, current_stored_event);
          
            if (can_interact)
            {

                //revision
                T revised = (T)this.nars.inferenceEngine.localRules.Revision(sentence, current_stored_event);
                EvidentialValue revised_value = revised.evidential_value;
                this.priority_queue.Add(revised_value.confidence, revised);
            }
            else
            {
                // choice rule
                var c1 = this.nars.inferenceEngine.get_sentence_value_time_decayed(current_stored_event).confidence;
                var c2 = this.nars.inferenceEngine.get_sentence_value_time_decayed(sentence).confidence;

                if(c1 > c2)
                {
                    this.priority_queue.Add(c1, current_stored_event);
                }
                else
                {
                    this.priority_queue.Add(c2, sentence);
                }
            }


        }
        else
        {
            if (this.GetCount() > 0)
            {

                var interactable = this.peek_all_interactable(sentence);
                foreach(var interact in interactable)
                {
                    T revised = (T)this.nars.inferenceEngine.localRules.Revision(sentence, interact);
                    EvidentialValue revised_value = revised.evidential_value;
                    this.priority_queue.Add(revised_value.confidence, revised);
                }
            }

            EvidentialValue value = sentence.evidential_value;
            float priority = value.confidence;
            this.priority_queue.Add(priority, sentence);
        }



        while (this.GetCount() > this.capacity)
        {
            this.priority_queue.RemoveAt(0);
        }
    }

    public void Forget()
    {
        if (this.GetCount() == 0) return;

        if (this.peek().is_event()) return;

        var result = new List<T>(priority_queue.Count);

        foreach (var kvp in priority_queue)
        {
            var item = kvp.Value;
            item.evidential_value.confidence *= math.pow(2, -1/this.nars.config.FORGETTING_RATE);
    
            result.Add(item);
        }

        priority_queue.Clear();
        foreach (var decayed_sentence in result)
        {
            priority_queue.Add(decayed_sentence.evidential_value.confidence, decayed_sentence);
        }
    }

    public int GetCount()
    {
        return this.priority_queue.Count;
    }

    public T? take()
    {
        /*
            Take item with highest confidence from the depq
            O(1)
        */
        if (this.GetCount() == 0) return null;
        var item = this.priority_queue.ElementAt(0).Value;
        priority_queue.RemoveAt(0);
        return item;
    }

    public T? peek()
    {
        /*
            Peek item with highest confidence from the depq
            O(1)

            Returns null if depq != empty
        */
        if (this.GetCount() == 0) return null;
        return this.priority_queue.ElementAt(0).Value;
    }


    public T? peek_random_interactable(Sentence j)
    {
        /*
            Peek random item from the depq
            O(1)

            Returns null if depq is empty
        */
        if (this.GetCount() == 0) return null;
        List<T> interactable = new List<T>();
        foreach (var kvp in this.priority_queue)
        {
            T belief = kvp.Value;
            // loop starting with max confidence
            if (EvidentialBase.may_interact(j, belief))
            {
                interactable.Add(belief);
            }
        }
        if (interactable.Count == 0) return null;
        int rnd = this.nars.random.Next(0, interactable.Count);
        return interactable[rnd];
    }

    public T? peek_random()
    {
        /*
            Peek random item from the depq
            O(1)

            Returns null if depq is empty
        */
        if (this.GetCount() == 0) return null;
        int rnd = UnityEngine.Random.Range(0, this.GetCount());
        return priority_queue.ElementAt(rnd).Value;
    }

    public T? peek_first_interactable(Sentence j)
    {
        /*
            Returns a sentence in this hallOfFameTable that j may interact with
            null if there are none.
            O(N)

        :param j:
        :return:
        */
        foreach (var kvp in this.priority_queue)
        {
            T belief = kvp.Value;
            // loop starting with max confidence
            if (EvidentialBase.may_interact(j, belief))
            {
                return belief;
            }
        }
        return null;
    }

    public List<T> peek_all_interactable(Sentence j)
    {
        /*
            Returns a sentence in this hallOfFameTable that j may interact with
            null if there are none.
            O(N)

        :param j:
        :return:
        */
        List<T> list = new List<T>();
        foreach (var kvp in this.priority_queue)
        {
            T belief = kvp.Value;
            // loop starting with max confidence
            if (EvidentialBase.may_interact(j, belief))
            {
                list.Add(belief);
            }
        }
        return list;
    }
}


/*public class Task {
    *//*
       NARS Task
    *//*
    Sentence sentence;
    bool needs_to_be_answered_in_output;
    bool is_from_input;
    int creation_timestamp;

    public Task(Sentence sentence, int creation_timestamp, bool is_input_task=false){
        //Asserts.assert_sentence(sentence);
        this.sentence = sentence;
        this.creation_timestamp = creation_timestamp;  // save the task's creation time
        this.is_from_input = is_input_task;
        // only used for question tasks
        this.needs_to_be_answered_in_output = is_input_task;
}

    public Term get_term() {
        return this.sentence.statement;
    }

    public override string ToString() {
        return "TASK: " + this.sentence.get_term_string_no_id();
    }

                }*/