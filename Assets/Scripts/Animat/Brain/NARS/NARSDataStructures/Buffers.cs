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


/*
public class SpatialBuffer {
    /*
        Holds the current sensation signals in a spatial layout / array.

        The data is converted to Narsese when extracted.
    /

    int[,] dimensions;

    public SpatialBuffer(int[,] dimensions){
        /*
        :param dimensions: dimensions of the 2d buffer as a tuple (y, x)
        /
        this.dimensions = dimensions
        this.array = np.full(shape=dimensions,
                             fill_value=EvidentialValue(0.0,0.9))
        this.components_bag = Bag(item_type=object,
                                  capacity=1000,
                                  granularity=100)

        this.pooled_array = np.full(shape=dimensions,
                             fill_value=EvidentialValue(0.0,0.9))
        this.pooled_components_bag = Bag(item_type=object,
                                  capacity=1000,
                                  granularity=100)

        this.last_taken_img_array = null
        this.last_sentence = null

        // initialize with uniform probabilility

    def blank_image(this){
        this.set_image(np.empty(shape=this.array.shape))

    def set_image(this, img){
        this.img = img
        original_event_array = this.transduce_raw_vision_array(img)

        // assert event_array.shape == this.dimensions,\
        //     "ERROR: Data dimensions are incompatible with Spatial Buffer dimensions " \
        //     + str(event_array.shape) + " && " + str(this.dimensions)

        this.array = np.array(original_event_array)
        this.components_bag.clear()

        maximum = 0
        for indices, sentence in np.ndenumerate(this.array){
            if sentence.value.frequency > WorldConfig.POSITIVE_THRESHOLD \
                    && not (isinstance(sentence.statement,
                                        CompoundTerm) && sentence.statement.connector == NALSyntax.TermConnector.Negation){
                maximum = max(maximum, sentence.value.frequency * sentence.value.confidence)

        for indices, sentence in np.ndenumerate(this.array){
            if sentence.value.frequency > WorldConfig.POSITIVE_THRESHOLD \
                && not (isinstance(sentence.statement,CompoundTerm) && sentence.statement.connector == NALSyntax.TermConnector.Negation){
                priority = sentence.value.frequency * sentence.value.confidence / maximum
                object = indices
                this.components_bag.PUT_NEW(object)
                this.components_bag.change_priority(Item.get_key_from_object(object), priority)



        // pooled
        this.pooled_array = this.create_pooled_sensation_array(original_event_array, stride=2)
        #this.pooled_array = this.create_pooled_sensation_array(this.pooled_array , stride=2)
        this.pooled_components_bag.clear()

        maximum = 0
        for indices, sentence in np.ndenumerate(this.pooled_array){
            if sentence.value.frequency > WorldConfig.POSITIVE_THRESHOLD \
                    && not (isinstance(sentence.statement,
                                        CompoundTerm) && sentence.statement.connector == NALSyntax.TermConnector.Negation){
                maximum = max(maximum, sentence.value.frequency * sentence.value.confidence)

        for indices, sentence in np.ndenumerate(this.pooled_array){
            if sentence.value.frequency > WorldConfig.POSITIVE_THRESHOLD \
                    && not (isinstance(sentence.statement,
                                        CompoundTerm) && sentence.statement.connector == NALSyntax.TermConnector.Negation){
                priority = sentence.value.frequency * sentence.value.confidence / maximum
                object = indices
                this.pooled_components_bag.PUT_NEW(object)
                this.pooled_components_bag.change_priority(Item.get_key_from_object(object), priority)

    def take(this, pooled){
        /*
            Probabilistically select a spatial subset of the buffer.
            :return: an Array Judgment of the selected subset.
        /
        if pooled:
            bag = this.pooled_components_bag
            array = this.pooled_array
        else:
            bag = this.components_bag
            array = this.array

        // probabilistically peek the 2 vertices of the box
        // selection 1: small fixed windows

        indices = bag.peek()
        if indices == null: return null

        y, x = indices.object
        radius = 1#random.randint(1,2)
        min_x, min_y = max(x - radius, 0), max(y - radius, 0)
        max_x, max_y = min(x + radius, array.shape[1] - 1), min(y + radius,array.shape[0] - 1)

        extracted = array[min_y:max_y+1, min_x:max_x+1]
        sentence_subset= []
        for idx,sentence in np.ndenumerate(extracted){
            if not (isinstance(sentence.statement, CompoundTerm)
                && sentence.statement.connector == NALSyntax.TermConnector.Negation){
                sentence_subset.append(sentence)

        total_truth = null
        statement_subset = []
        for sentence in sentence_subset:
            if total_truth == null:
                total_truth = sentence.value
            else:
                total_truth = this.nars.inferenceEngine.truthValueFunctions.F_Intersection(sentence.value.frequency,
                                                                     sentence.value.confidence,
                                                                     total_truth.frequency,
                                                                     total_truth.confidence)
            statement_subset.append(sentence.statement)


        // create conjunction of features
        statement = CompoundTerm(subterms=statement_subset,
                                                  term_connector=NALSyntax.TermConnector.Conjunction)

        event_sentence = Judgment(statement=statement,
                                      value=total_truth,
                                      occurrence_time=Global.Global.get_current_cycle_number())


        if pooled:
            img_min_x, img_min_y, img_max_x, img_max_y = 2*min_x, 2*min_y, 2*max_x, 2*max_y
        else:
            img_min_x, img_min_y, img_max_x, img_max_y = min_x, min_y, max_x, max_y
        last_taken_img_array = np.zeros(shape=this.img.shape)
        last_taken_img_array[img_min_y+1:(img_max_y+1)+1, img_min_x+1:(img_max_x+1)+1] = this.img[img_min_y+1:(img_max_y+1)+1, img_min_x+1:(img_max_x+1)+1]
        this.last_taken_img_array = last_taken_img_array  // store for visualization

        return event_sentence

    def create_spatial_conjunction(this, subset){
                    /*

                    :param subset: 2d Array of positive (non-negated) sentences / events
                    :return:
                    /
                    conjunction_truth_value = null
                    terms_array = np.empty(shape=subset.shape,
                                           dtype=Term)
                    for indices, sentence in np.ndenumerate(subset){
                        truth_value = sentence.value
                        term = sentence.statement

                        if conjunction_truth_value == null:
                            conjunction_truth_value = truth_value
                        else:
                            conjunction_truth_value = this.nars.inferenceEngine.truthValueFunctions.F_Intersection(conjunction_truth_value.frequency,
                                                           conjunction_truth_value.confidence,
                                                           truth_value.frequency,
                                                           truth_value.confidence)

                        terms_array[indices] = term

                    spatial_conjunction_term = SpatialTerm(spatial_subterms=terms_array,
                                                                            connector=NALSyntax.TermConnector.ArrayConjunction)
                    spatial_conjunction = Judgment(statement=spatial_conjunction_term,
                                                  value=conjunction_truth_value,
                                                  occurrence_time=Global.Global.get_current_cycle_number())

                    return spatial_conjunction

                def create_pooled_sensation_array(this, array, stride){
                    /*
                        Takes an array of events, && returns an array of events except 2x2 pooled with stride
                    :param array:
                    :param stride:
                    :return:
                    //*
                    pad_sentence = Global.Global.ARRAY_NEGATIVE_SENTENCE
        stride_original_sentences = np.empty(shape=(2,2),
                                             dtype=Sentence)
        if stride == 1:
            pool_terms_array = np.empty(shape=(array.shape[0] - 1, array.shape[1] - 1),
                                      dtype=Term)
        else if stride == 2:
            height = array.shape[0] // 2 if array.shape[0] % 2 == 0 else (array.shape[0]+1) // 2
            width = array.shape[1] // 2 if array.shape[1] % 2 == 0 else (array.shape[1]+1) // 2
            pool_terms_array = np.empty(shape=(height, width), dtype=Term)
        else:
            assert false,"ERROR: Does not support stride > 2"

        for indices,sentence in np.ndenumerate(array){
            y, x = indices
            y, x = int(y), int(x)
            if stride == 2 && not (x % 2 == 0 || y % 2 == 0){ continue // only use even indices for stride 2

            pool_y = y // 2 if stride == 2 else y
            pool_x = x // 2 if stride == 2 else x

            // pool sensation
            if y < array.shape[0] - 1 && x < array.shape[1] - 1:
                // not last column || row yet
                stride_original_sentences = np.array(array[y:y+2, x:x+2])  // get 4x4

            else if y == array.shape[0] - 1 && x < array.shape[1] - 1:
                // last row, not last column
                if stride == 1: continue
                stride_original_sentences[0,:] = np.array([array[y, x], array[y, x+1]])
                stride_original_sentences[1,:] = np.array([pad_sentence, pad_sentence])

            else if y < array.shape[0] - 1 && x == array.shape[1] - 1:
                // last column, not last row
                if stride == 1: continue
                stride_original_sentences[0,:] = np.array([array[y, x], pad_sentence])
                stride_original_sentences[1,:] = np.array([array[y+1, x], pad_sentence])

            else if y == array.shape[0] - 1 && x == array.shape[1] - 1:
                if stride == 1: continue
                #last row && column
                stride_original_sentences[0, :] = np.array([array[y, x], pad_sentence])
                stride_original_sentences[1, :] = np.array([pad_sentence, pad_sentence])

            pool_terms_array[pool_y, pool_x] = this.create_spatial_disjunction(np.array(stride_original_sentences))

        return pool_terms_array

    def create_spatial_disjunction(this, array_of_events){
            /*

            :param terms: 2x2 Array of positive (non-negated) sentences / events
            :param terms_array: 2x2 array of potentially negated Terms
            :return:
            /
# TODO FINISH THIS
            all_negative = true
        for i,event in np.ndenumerate(array_of_events){
            all_negative = all_negative \
                           && (isinstance(event.statement, CompoundTerm) && event.statement.connector == NALSyntax.TermConnector.Negation)

        disjunction_truth = null
        disjunction_subterms = np.empty(shape=array_of_events.shape,
                                  dtype=Term)

        for indices, event in np.ndenumerate(array_of_events){
            if isinstance(event.statement, CompoundTerm) \
                    && event.statement.connector == NALSyntax.TermConnector.Negation:
                // negated event, get positive
                truth_value = this.nars.inferenceEngine.truthValueFunctions.F_Negation(event.value.frequency,
                                                                 event.value.confidence) // get regular positive back
                new_statement = event.statement.subterms[0]
            else:
                // already positive
                truth_value = event.value
                new_statement = event.statement

            disjunction_subterms[indices] = new_statement

            if disjunction_truth == null:
                disjunction_truth = truth_value
            else:
                // OR
                disjunction_truth = this.nars.inferenceEngine.truthValueFunctions.F_Union(disjunction_truth.frequency,
                                                                                  disjunction_truth.confidence,
                                                                                  truth_value.frequency,
                                                                                  truth_value.confidence)

        disjunction_term = SpatialTerm(spatial_subterms=disjunction_subterms,
                                                                connector=NALSyntax.TermConnector.ArrayDisjunction)

        if all_negative:
            disjunction_truth = this.nars.inferenceEngine.truthValueFunctions.F_Negation(disjunction_truth.frequency,
                                                                           disjunction_truth.confidence)
            disjunction_term = CompoundTerm(subterms=[disjunction_term],
                                                            term_connector=NALSyntax.TermConnector.Negation)


        spatial_disjunction = Judgment(statement=disjunction_term,
                                      value=disjunction_truth)

        return spatial_disjunction

    def transduce_raw_vision_array(this, img_array){
        /*
            Transduce raw vision data into NARS truth-values
            :param img_array:
            :return: python array of NARS truth values, with the same dimensions as given raw data
        /
        max_value = 255

        def create_2d_truth_value_array(*indices){
            coords = tuple([int(var) for var in indices])
            y,x = coords
            pixel_value = float(img_array[y, x])

            f = pixel_value / max_value
            if f > 1: f = 1

            relative_indices = []
            offsets = (img_array.shape[0]-1)/2, (img_array.shape[1]-1)/2
            for i in range(2){
                relative_indices.append((indices[i] - offsets[i]) / offsets[i])

            unit = HelperFunctions.get_unit_evidence()
            c = unit*math.exp(-1*((WorldConfig.FOCUSY ** 2)*(relative_indices[0]**2) + (WorldConfig.FOCUSX ** 2)*(relative_indices[1]**2)))

            predicate_name = 'B'
            subject_name = str(y) + "_" + str(x)


            if f > WorldConfig.POSITIVE_THRESHOLD:
                truth_value = EvidentialValue(f, c)
                statement = from_string("(" + subject_name + "-->" + predicate_name + ")")
            else:
                truth_value = EvidentialValue(ExtendedBooleanOperators.bnot(f), c)
                statement = from_string("(--,(" + subject_name + "-->" + predicate_name + "))")

            // create the common predicate

            return Judgment(statement=statement,
                                                       value=truth_value)


        func_vectorized = np.vectorize(create_2d_truth_value_array)
        truth_value_array = np.fromfunction(function=func_vectorized,
                                            shape=img_array.shape)

        return truth_value_array;
    }
*/

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

   