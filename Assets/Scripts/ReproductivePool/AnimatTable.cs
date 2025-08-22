using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static DataAnalyzer;
using static GlobalConfig;

// sorted table of animats
public class AnimatTable
{
    public const int SIZE_LIMIT = 100; // for reproduction
    public List<TableEntry> table = new();  // score, genomes
    public List<NoveltySearch.BehaviorCharacterizationGPU> GPU_table = new();  // score, genomes
    Comparer<TableEntry> ascending_score_comparer;

    public float total_score = 0;

    public SortingRule sorted;
    ScoreType score_type;

    public struct TableEntry
    {
        public float score;
        public AnimatData data;

        public TableEntry(float score, AnimatData animatData) : this()
        {
            this.score = score;
            this.data = animatData;
        }
    }

    public AnimatTable Clone()
    {
        var clone_table = (AnimatTable)this.MemberwiseClone();
        clone_table.table = new(this.table);
        clone_table.GPU_table = null;
        return clone_table;
    }
    public enum SortingRule
    {
        unsorted,
        sorted
    }

    public enum ScoreType
    {
        objective_fitness,
        novelty
    }


    public AnimatTable(SortingRule sorted, ScoreType score_type)
    {
        this.sorted = sorted;
        this.score_type = score_type;

        // best animats, sorted in ascending order by score (lowest score at index 0)
        this.ascending_score_comparer = Comparer<TableEntry>.Create((x, y) =>
        {
            int result = x.score.CompareTo(y.score);
            if (result == 0) return 1;
            else return result;
        });
    }

    public TableEntry? GetBest()
    {
        if (this.table.Count == 0) return null;
        return this.table[^1];
    }

    public void TryAdd(float score, Animat animat)
    {
        if (this.IsFull())
        {
            if (this.sorted == SortingRule.sorted && score < GetIndex0().score) return; // it's worst than worst elite, so just return

          
            // take out worst scoring (sorted) or least recent (unsorted) animat at idx 0
            RemoveIndex0();
          
        
        }
        else if (this.table.Count > SIZE_LIMIT)
        {
            Debug.LogError("ERROR");
        }

        this.table.Add(new TableEntry(score, DataAnalyzer.CreateAnimatData(animat)));

        if (GlobalConfig.novelty_search_processing_method == ProcessingMethod.GPU)
        {
            this.GPU_table.Add((NoveltySearch.BehaviorCharacterizationGPU)animat.behavior_characterization);
        }
            
        this.total_score += score;

        if (this.sorted != SortingRule.unsorted) this.table.Sort(this.ascending_score_comparer);

        if (float.IsInfinity(this.total_score)) Debug.LogError("error");
    }

    static object lockobj = new();
    public void UpdateAllNovelties()
    {

        var novelty_search = AnimatArena.GetInstance().novelty_search;
        if (novelty_search.NeedsMoreEntries()) return;
        //for (int i = 0; i < this.table.Count; i++)
        this.total_score = 0;

        float[] new_scores;
        if (novelty_search is NoveltySearchCPU)
        {
            new_scores = new float[this.table.Count];
            Parallel.For(0, this.table.Count, i =>
            {
                var animat = this.table[i];
                float new_novelty =  novelty_search.GetAnimatNoveltyScore(animat.data.behavior);

                // change score
                var entry = this.table[i];
                entry.score = new_novelty;
                this.table[i] = entry;
                new_scores[i] = new_novelty;
            });
        }else if(novelty_search is NoveltySearchGPU novelty_search_gpu)
        {
            new_scores = novelty_search_gpu.GetAnimatNoveltyScoreBatch(this.GPU_table);
            Parallel.For(0, this.table.Count, i =>
            {
                // change score
                var entry = this.table[i];
                entry.score = new_scores[i];
                this.table[i] = entry;
            });

            this.table.Sort(this.ascending_score_comparer);
        }
        else
        {
            Debug.LogError("error");
            return;
        }
       

        this.total_score = new_scores.Sum();
    }


    public TableEntry GetIndex0()
    {
        return this.table.ElementAt(0);
    }

    public void Remove(int idx)
    {
        float remove_score = this.table.ElementAt(idx).score;
        this.total_score -= remove_score;
        this.table.RemoveAt(idx);
        if(this.GPU_table.Count > 0) this.GPU_table.RemoveAt(idx);
    }

    public void RemoveIndex0()
    {
        Remove(0);
    }


    public bool IsFull()
    {
        return this.table.Count == SIZE_LIMIT;
    }

    // return genome and idx
    public (AnimatGenome, int) PeekProbabilistic(SelectionMethod? selection_method=null, int ignore_idx = -1)
    {
        if(selection_method == null)
        {
            int rnd = UnityEngine.Random.Range(0, 2);
            if(rnd == 0)
            {
                selection_method = SelectionMethod.RouletteWheel;
            }
            else
            {
                selection_method = SelectionMethod.Tournament;
            }
        }

        if (selection_method == SelectionMethod.RouletteWheel)
        {
            return PeekRouletteWheel(ignore_idx);
        }
        else if (selection_method == SelectionMethod.Tournament)
        {
            return PeekTournament(ignore_idx);
        }
        else
        {
            return (null, 0);
        }
    }

    int tournament_k = 7;
    public (AnimatGenome, int) PeekTournament(int ignore_idx = -1)
    {
        float max_score = -1;
        int max_score_idx = -1;
        for(int i=0; i < tournament_k; i++)
        {
            int randomly_selected_idx = UnityEngine.Random.Range(0, this.table.Count);
            if (randomly_selected_idx == ignore_idx)
            {
                randomly_selected_idx = (randomly_selected_idx + 1) % this.table.Count;
            }
            float entry_score = this.table[randomly_selected_idx].score;
         
            if(max_score_idx == -1)
            {
                max_score = entry_score;
                max_score_idx = randomly_selected_idx;
            }else if(entry_score > max_score)
            {
                max_score = entry_score;
                max_score_idx = randomly_selected_idx;
            }
        }
        var best = this.table[max_score_idx];
        return (best.data.genome, max_score_idx);
    }

    public (AnimatGenome, int) PeekRouletteWheel(int ignore_idx = -1)
    {
        int randomly_selected_idx;
        float total_score = this.total_score;

        if (ignore_idx >= 0)
            total_score -= this.table[ignore_idx].score; // assume table is a List<TableEntry>

        if (total_score <= 0f)
        {
            // fallback uniform random
            randomly_selected_idx = UnityEngine.Random.Range(0, this.table.Count);
            if (randomly_selected_idx == ignore_idx)
                randomly_selected_idx = (randomly_selected_idx + 1) % this.table.Count;
        }
        else
        {
            float rnd = UnityEngine.Random.value; // [0,1)
            float cumulative = 0f;

            for (int i = 0; i < this.table.Count; i++)
            {
                if (i == ignore_idx) continue;

                cumulative += this.table[i].score / total_score;
                if (rnd < cumulative)
                {
                    randomly_selected_idx = i;
                    goto Done;
                }
            }

            // Edge case: floating-point imprecision → pick last
            randomly_selected_idx = this.table.Count - 1;
        }

    Done:
        return (this.table[randomly_selected_idx].data.genome, randomly_selected_idx);
    }


    public int Count()
    {
        return this.table.Count;
    }

    public enum SelectionMethod
    {
        RouletteWheel, // randomly selected, proportional based on score
        Tournament // k are selected, then the #1 is selected
    }

}
