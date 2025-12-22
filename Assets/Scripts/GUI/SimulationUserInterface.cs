using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using static GlobalConfig;

public class SimulationUserInterface : MonoBehaviour
{
    // other tools visible in the interface
    BrainViewer brain_viewer;
    GenomeCreator brain_creator;

    // UI

    public TMP_InputField animat_info_minimum_population;

    public Text high_score_text;
    public Text generation_text;

    public TMP_Text animat_info_index;
    public TMP_Text animat_info_generation;
    public TMP_Text animat_info_num_neurons;
    public TMP_Text animat_info_num_connections;
    public TMP_Text animat_info_period;
    public TMP_Text animat_info_energy;
    public TMP_Text animat_info_health;
    public TMP_Text animat_info_lifespan;
    public RawImage animat_vision_view;

    // cameras
    public Camera animat_creator_camera, brain_viewer_camera;
    int currently_viewed_animat_idx;

    // objects
    AnimatArena arena;
    List<Animat> animats;

    // Start is called before the first frame update
    void Start()
    {
        // get objects
        this.arena = AnimatArena.GetInstance();
        this.animats = this.arena.current_generation;
        this.brain_creator = GameObject.FindFirstObjectByType<GenomeCreator>();
        this.brain_viewer = GameObject.FindFirstObjectByType<BrainViewer>();

        // set text
        this.animat_info_minimum_population.text = this.arena.MINIMUM_POPULATION_QUANTITY.ToString();
        UpdateHighScoreText(0);
        this.animat_info_minimum_population.onValueChanged.AddListener(ChangedMinimumPopulationUI);
        this.currently_viewed_animat_idx = 0;
        this.SwitchViewToAnimat(0);
    }

    void Update()
    {
        CameraTrackCurrentlySelectedAnimat();

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            ViewNextAnimat();
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            ViewPreviousAnimat();
        }

        if (this.currently_viewed_animat_idx >= 0 && this.currently_viewed_animat_idx < this.animats.Count) { 
            this.SetAnimatInfoGUI(this.animats[this.currently_viewed_animat_idx]);
        }

        
    }

    public void OnAfterAnimatDied(int dead_animat_idx)
    {
        if(dead_animat_idx == this.currently_viewed_animat_idx)
        {
            if(this.animats.Count == 0)
            {
                return;
            }
            this.currently_viewed_animat_idx = this.currently_viewed_animat_idx % this.animats.Count;
            SwitchViewToAnimat(this.currently_viewed_animat_idx);
        }
    }

    public void SetAnimatInfoGUI(Animat animat)
    {
        if(!animat.initialized) return;
        
        animat_info_energy.text = "Energy: " + animat.body.energy;
        animat_info_health.text = "Health: " + animat.body.health;
        animat_info_lifespan.text = "Lifespan: " + animat.body.age;
        if(animat.mind is Brain brain)
        {
            animat_info_num_neurons.text = "Neurons: " + brain.GetNumberOfNeurons();
            animat_info_num_connections.text = "Connections: " + brain.GetNumberOfSynapses();
        }

        animat_info_period.text = "Update Period: " + GlobalConfig.ANIMAT_BRAIN_UPDATE_PERIOD;
        animat_info_generation.text = "Generation: " + animat.genome.generation + "";
        //this.currently_viewed_animat = animat;
        //animat_info_index.text = this.currently_viewed_animat_idx + "";
    }
    void SetNewAnimatForBrainViewer(Animat animat)
    {
        StaticSceneManager.animat = animat;
        StaticSceneManager.genome = animat.genome;

        if (this.brain_viewer != null && this.brain_viewer.animat != animat) this.brain_viewer.Initialize();
        if (this.brain_creator != null) this.brain_creator.Initialize();
    }

    void CameraTrackCurrentlySelectedAnimat()
    {
        if (this.currently_viewed_animat_idx >= this.animats.Count)
        {
            Debug.LogWarning("Index exceeds bounds, can't view animat " + this.currently_viewed_animat_idx);
            return;
        }
        Animat animat = this.animats[this.currently_viewed_animat_idx];
        if (!animat.initialized) return;
        animat_creator_camera.transform.position = Vector3.Lerp(new float3(0, 15, 0) + animat.GetCenterOfMass(), animat_creator_camera.transform.position, 0.25f);
    }

    public void SwitchViewToAnimat(int i)
    {
        if (i >= this.animats.Count)
        {
            Debug.LogWarning("Index exceeds bounds, can't view animat " + i);
            return;
        }
        Animat animat = this.animats[i];
        this.currently_viewed_animat_idx = i;
        int generation;
        if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.CPPN)
        {
            Debug.LogError("error. CPPN disabled");
            // generation = animat.unified_CPPN_genome.generation;
        }
        else if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.NEAT)
        {
            generation = animat.genome.generation;
        }
        else
        {
            Debug.LogError("error not implemented");
            return;
        }

        /*     if (animat.body.vision_sensor != null)
             {
                 animat_vision_view.texture = animat.body.vision_sensor.currentTex2d;
             }*/
        this.SetNewAnimatForBrainViewer(animat);
    }

    public void ViewNextAnimat()
    {
        Debug.Log("viewing next animat");
        SwitchViewToAnimat(MathHelper.mod(currently_viewed_animat_idx + 1, this.animats.Count));
    }

    public void ViewPreviousAnimat()
    {
        Debug.Log("viewing previous animat");
        SwitchViewToAnimat(MathHelper.mod(currently_viewed_animat_idx - 1, this.animats.Count));
    }

    public void NextGeneration()
    {
        this.arena.KillAnimat(this.currently_viewed_animat_idx, false);
    }
    public void UpdateGenerationText(int gen)
    {
        this.generation_text.text = "GENERATION: " + gen;
    }

    public void UpdateHighScoreText(float high_score)
    {
        this.high_score_text.text = "HIGH SCORE: " + high_score;
    }

    public void ChangedMinimumPopulationUI(string empty)
    {
        string new_number_string = this.animat_info_minimum_population.text;
        int new_num;
        if (int.TryParse(new_number_string, out new_num))
        {
            this.arena.MINIMUM_POPULATION_QUANTITY = new_num;
        }
        else
        {
            Debug.LogWarning("couldnt parse " + new_number_string);
        }

        this.arena.MINIMUM_POPULATION_QUANTITY = math.clamp(this.arena.MINIMUM_POPULATION_QUANTITY, 1, AnimatArena.MAXIMUM_POPULATION_QUANTITY);
    }

    public void SaveCurrentAnimatBrain()
    {
        Animat animat = this.animats[currently_viewed_animat_idx];
        if (GlobalConfig.BRAIN_GENOME_METHOD == BrainGenomeMethod.CPPN)
        {
            Debug.LogError("error. CPPN not implemented");
            //animat.unified_CPPN_genome.SaveToDisk();
        }
        else
        {
            Debug.LogError("error");
        }
        if (animat.initialized)
        {
            animat.mind.SaveToDisk();
        }
        else
        {
            Debug.LogWarning("WARNING: Could only save brain genome, not brain, since it was not created yet.");
        }
    }
}
