/*
    Author: Christian Hahm
    Created: May 24, 2022
    Purpose: Holds data structure implementations that are specific / custom to NARS
*/



using System.Collections.Generic;
using Unity.Mathematics;

public class Bag<T> : ItemContainer<T>
{
    /*
        Probabilistic priority-queue

        --------------------------------------------

        An array of buckets, where each bucket holds items of a certain priority
        (e.g. 100 buckets, bucket 1 - hold items with 0.01 priority,  bucket 50 - hold items with 0.50 priority)
    */
    int level;
    int granularity;
    Dictionary<int, List<Item<T>>> priority_buckets;
   // Dictionary<int, List<Item<T>>> quality_buckets;
    System.Random random;
    public Bag(int capacity, int granularity) : base(capacity)
    {
        this.level = 0;
        this.priority_buckets = new Dictionary<int, List<Item<T>>>();
        //this.quality_buckets = new Dictionary<int, List<Item<T>>>(); // store by inverted quality for deletion
        this.granularity = granularity;

        for (int i = 0; i < granularity; i++)
        {
            this.priority_buckets[i] = new List<Item<T>>();
            //this.quality_buckets[i] = new List<Item<T>>();
        }
        random = new System.Random();
    }

    public void Clear()
    {
        this.level = 0;
        for (int i = 0; i < granularity; i++)
        {
            this.priority_buckets[i].Clear();
            //this.quality_buckets[i].Clear();
        }
        base.Clear();
    }

    public override Item<T> PUT_NEW(T obj)
    {
        /*
            Place a NEW Item into the bag.

            :param Bag Item to place into the Bag
            :returns the new item
        */
        // remove lowest priority item if over capacity
        if (this.GetCount() > this.capacity)
        {
            Item<T> purged_item = this._TAKE_MIN();
        }

        // add new item
        Item<T> item = base.PUT_NEW(obj);
        this.add_item_to_bucket(item);
        //this.add_item_to_quality_bucket(item);

        return item;
    }

    public Item<T>? peek(string? key = null)
    {
        /*
            Peek an object from the bag using its key.
            If key is null, peeks probabilistically

            :returns An item peeked from the Bag; null if item could not be peeked from the Bag
        */
        if (this.GetCount() == 0) return null;  // no items
        

        Item<T>? item = null;
        if (key == null)
        {
            item = this._peek_probabilistically(this.priority_buckets);
        }
        else
        {
            if (!this.item_lookup_dict.ContainsKey(key)) return null;
            item = peek_using_key(key);
        }

        return item;
    }

    public void change_priority(string key, float new_priority)
    {
        /*
            Changes an item priority in the bag
        :param key:
        :return:
        */
        Item<T> item = this.peek_using_key(key);

        this.remove_item_from_its_bucket(item);

        // change item priority attribute, && GUI if necessary
        item.budget.set_priority(new_priority);

        // if Config.GUI_USE_INTERFACE:
        //     NARSDataStructures.ItemContainers.ItemContainer._take_from_lookup_dict(key)
        //     NARSDataStructures.ItemContainers.ItemContainer._put_into_lookup_dict(this,item)
        // add to new bucket
        this.add_item_to_bucket(item);
    }


    //public void change_quality(string key, float new_quality)
    //{
    //    Item<T> item = this.peek_using_key(key);

    //    // remove from sorted
    //    this.remove_item_from_its_quality_bucket(item);

    //    // change item quality
    //    item.budget.set_quality(new_quality);

    //    // if Config.GUI_USE_INTERFACE:
    //    //     NARSDataStructures.ItemContainers.ItemContainer._take_from_lookup_dict(key)
    //    //     NARSDataStructures.ItemContainers.ItemContainer._put_into_lookup_dict(item)

    //    // add back to sorted
    //    this.add_item_to_quality_bucket(item);
    //}


    public void add_item_to_bucket(Item<T> item)
    {
        // add to appropriate bucket
        int bucket_num = this.calc_bucket_num_from_value(item.budget.get_priority());
        List<Item<T>> bucket = this.priority_buckets[bucket_num];
        bucket.Add(item); // convert to ID so
        item.bucket_num = bucket_num;
    }


    public void remove_item_from_its_bucket(Item<T> item)
    {
        // take from bucket
        List<Item<T>> bucket = this.priority_buckets[(int)item.bucket_num];
        bucket.Remove(item);
        item.bucket_num = null;
    }

    //public void add_item_to_quality_bucket(Item<T> item)
    //{
    //    // add to appropriate bucket
    //    int bucket_num = this.calc_bucket_num_from_value(1 - item.budget.get_quality()); // higher quality should have lower probability of being selected for deletion
    //    List<Item<T>> bucket = this.quality_buckets[bucket_num];
    //    bucket.Add(item);
    //    item.quality_bucket_num = bucket_num;
    //}

    //public void remove_item_from_its_quality_bucket(Item<T> item)
    //{
    //    // take from bucket
    //    List<Item<T>> bucket = this.quality_buckets[(int)item.quality_bucket_num];
    //    bucket.Remove(item);
    //    item.quality_bucket_num = null;
    //}

    public void strengthen_item_priority(string key, float multiplier)
    {
        /*
            Strenghtens an item in the bag
        :param key:
        :return:
        */
        Item<T> item = this.peek_using_key(key);
        // change item priority attribute, && GUI if necessary
        float new_priority = ExtendedBooleanOperators.bor(new float[] { item.budget.get_priority(), multiplier });
        this.change_priority(key, new_priority);
    }


    //public void strengthen_item_quality(string key)
    //{
    //    /*
    //        Decays an item in the bag
    //    :param key:
    //    :return:
    //    */
    //    Item<T> item = this.peek_using_key(key);
    //    // change item priority attribute, && GUI if necessary
    //    float new_quality = ExtendedBooleanOperators.bor(new float[] { item.budget.get_quality(), 0.1f });
    //    this.change_quality(key, new_quality);
    //}



    public void decay_item(string key, float multiplier)
    {
        /*
            Decays an item in the bag
        :param key:
        :return:
        */
        Item<T> item = this.peek_using_key(key);
        float new_priority = ExtendedBooleanOperators.band(new float[] { item.budget.get_priority(), multiplier });
        this.change_priority(key, new_priority);
    }

    public Item<T> TAKE_USING_KEY(string key)
    {
        /*
        Take an item from the bag using the key

        :param key: key of the item to remove from the Bag
        :return: the item which was removed from the bucket
        */
        //Asserts.assert(this.item_lookup_dict.ContainsKey(key), "Given key does not exist in this bag");
        Item<T> item = this._take_from_lookup_dict(key);
        this.remove_item_from_its_bucket(item);
        //this.remove_item_from_its_quality_bucket(item);
        return item;
    }


    public Item<T>? _TAKE_MIN()
    {
        /*
            :returns the lowest quality item taken from the Bag
        */
        Item<T> item;
        try
        {
            item = this._peek_probabilistically(this.priority_buckets);
            //Asserts.assert(this.item_lookup_dict.ContainsKey(item.key), "Given key does not exist in this bag");
            item = _take_from_lookup_dict(item.key);
            this.remove_item_from_its_bucket(item);
            //sthis.remove_item_from_its_quality_bucket(item);
        }
        catch
        {
            item = null;
        }
        return item;
    }

    System.Random rng = new System.Random();
    public Item<T>? _peek_probabilistically(
        Dictionary<int, List<Item<T>>> buckets,
        bool weightByItemCount = false)
    {
        // 1) Build weights
        // Example priority = (level + 1). Change this if you have your own per-level weights.
        double[] weights = new double[this.granularity];
        double total = 0;

        for (int level = 0; level < granularity; level++)
        {
            if (!buckets.TryGetValue(level, out var list) || list == null || list.Count == 0)
            {
                weights[level] = 0;
                continue;
            }

            double w = (level + 1);                 // priority weight
            if (weightByItemCount) w *= list.Count; // item-weighted variant
            weights[level] = w;
            total += w;
        }

        if (total <= 0) return null; // all empty

        // 2) Sample a bucket via CDF
        double r = rng.NextDouble() * total;
        int pickedLevel = 0;
        double cumulative = 0;
        for (; pickedLevel < granularity; pickedLevel++)
        {
            cumulative += weights[pickedLevel];
            if (r < cumulative) break;
        }

        var bucket = buckets[pickedLevel];
        // 3) Pick an item uniformly within that bucket
        int idx = rng.Next(0, bucket.Count);
        return bucket[idx];
    }


    public int calc_bucket_num_from_value(float val)
    {
        return math.min((int)math.floor(val * this.granularity), this.granularity - 1);
    }
}
