using Unity.Mathematics;
using UnityEngine;
using static Brain;
using Color = UnityEngine.Color;

public abstract class AnimatBody : MonoBehaviour
{
    // metabolism
    public float energy;
    public float age;
    public float health = 100;
    public int number_of_voxels_held;

    // constants
    public const float MAX_AGE = 90; // in seconds
    public const float ENERGY_IN_A_FOOD = 100;
    public const float MAX_ENERGY = ENERGY_IN_A_FOOD * 5;
    public const float MAX_HEALTH = 100;
    public const float MAX_VOXELS_HELD = 5;
    const float SINE_SPEED = 3;
    //  reproduction
    public const float OFFSPRING_COST = AnimatBody.ENERGY_IN_A_FOOD / 2;


    //variables

    public float scale = 1.0f;
    public float base_scale= 1.0f;
    public VisionSensor vision_sensor;
    public Material material;

    // score
    public float number_of_food_eaten;
    public float food_approach_score = 0;
    public int times_reproduced;
    public int times_reproduced_asexually;
    public int times_reproduced_sexually;
    public float frames_food_detected = 0;
    public float total_frames_alive = 0;

    public Color? override_color = null;
    public float food_eaten_since_last_novelty_datapoint;
    public float food_was_seen;



    public abstract float3 _GetCenterOfMass();

    public abstract Quaternion GetRotation();
    public abstract bool Crashed();

    public virtual void Sense(Animat animat)
    {

        if (animat.mind is Brain brain)
        {
            // sense energy
            int sensory_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.INTERNAL_ENERGY_SENSOR];
            Neuron sensor_neuron = brain.GetNeuronCurrentState(sensory_neuron_idx);
            if (sensor_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
            sensor_neuron.activation = this.energy / MAX_ENERGY;
            brain.SetNeuronCurrentState(sensory_neuron_idx, sensor_neuron);

            //sense health
            sensory_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.INTERNAL_HEALTH_SENSOR];
            sensor_neuron = brain.GetNeuronCurrentState(sensory_neuron_idx);
            if (sensor_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
            sensor_neuron.activation = this.health / MAX_HEALTH;
            brain.SetNeuronCurrentState(sensory_neuron_idx, sensor_neuron);

            // sinewave
            sensory_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.SINEWAVE_SENSOR];
            sensor_neuron = brain.GetNeuronCurrentState(sensory_neuron_idx);
            if (sensor_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
            float current_time = Time.time;
            sensor_neuron.activation = math.sin(SINE_SPEED * 2 * math.PI * current_time);
            brain.SetNeuronCurrentState(sensory_neuron_idx, sensor_neuron);

            // pain
            sensory_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.PAIN_SENSOR];
            sensor_neuron = brain.GetNeuronCurrentState(sensory_neuron_idx);
            if (sensor_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
            sensor_neuron.activation = animat.was_hit;
            brain.SetNeuronCurrentState(sensory_neuron_idx, sensor_neuron);
            animat.was_hit = 0;
            
            if(GlobalConfig.WORLD_TYPE  == GlobalConfig.WorldType.VoxelWorld)
            {
                // internally held voxels
                sensory_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.INTERNAL_VOXEL_HELD];
                sensor_neuron = brain.GetNeuronCurrentState(sensory_neuron_idx);
                if (sensor_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
                sensor_neuron.activation = number_of_voxels_held / MAX_VOXELS_HELD;
                brain.SetNeuronCurrentState(sensory_neuron_idx, sensor_neuron);
            }

            
  
        }
        else if (animat.mind is NARS nar)
        {
            // sense energy
         

            if (this.energy > 2*OFFSPRING_COST)
            {
                var judgment = new Judgment(NARSGenome.energy_full, new EvidentialValue(), occurrence_time: nar.current_cycle_number);
                nar.SendInput(judgment);
            };

            // enter instinctual goals
            foreach (var goal_data in ((NARSGenome)animat.genome.brain_genome).goals)
            {
                var goal = new Goal(goal_data.statement, goal_data.evidence, occurrence_time: nar.current_cycle_number);
                nar.SendInput(goal);
            }
        }

        //vision
        this.vision_sensor.DoRaycastVisionAndMotorHandling(animat);
    }



    public virtual void MotorEffect(Animat animat)
    {
        double asexual_activation;

        if (animat.mind is Brain brain)
        {
            int motor_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.ASEXUAL_MOTOR_NEURON];
            Neuron motor_neuron = brain.GetNeuronCurrentState(motor_neuron_idx);
            if (motor_neuron.neuron_role != Neuron.NeuronRole.Motor) Debug.LogError("error");
            asexual_activation = motor_neuron.activation;

        }
        else if (animat.mind is NARS nar)
        {
            asexual_activation = nar.GetGoalActivation(NARSGenome.asexual_op);
        }else if(GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.Random)
        {
            asexual_activation = UnityEngine.Random.value;
            //if(!(this is WheeledRobot))
            //{
            //  //  Debug.LogError("random not yet configured for other robots");
            //}
        }
        else
        {
            Debug.LogError("error");
            return;
        }

        if(asexual_activation > VisionSensor.ASEXUAL_THRESHOLD)
        {
            if (this.energy > OFFSPRING_COST)
            {
                animat.AsexualReproduce();
            }
        }
    }

    public void FoodEaten(float food)
    {
        this.number_of_food_eaten += food;
        this.energy += food;
        this.health += food;
        this.energy = math.min(AnimatBody.MAX_ENERGY, this.energy);
        this.health = math.min(AnimatBody.MAX_HEALTH, this.health);
    }

    public void UpdateColor(Animat a)
    {
        double eat_color;
        double mate_color;
        double fight_color;
        if (a.mind is Brain brain)
        {
            int eat_motor_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.FIGHTING_MOTOR_NEURON_INDEX];
            Neuron eat_motor_neuron = brain.GetNeuronCurrentState(eat_motor_neuron_idx);
            eat_color = eat_motor_neuron.activation >= 0 ? eat_motor_neuron.activation : 0;

            int mate_motor_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.MATING_MOTOR_NEURON_INDEX];
            Neuron mate_motor_neuron = brain.GetNeuronCurrentState(mate_motor_neuron_idx);
            mate_color = mate_motor_neuron.activation >= 0 ? mate_motor_neuron.activation : 0;

            int fight_motor_neuron_idx = brain.nodeID_to_idx[InitialNEATGenomes.FIGHTING_MOTOR_NEURON_INDEX];
            Neuron fight_motor_neuron = brain.GetNeuronCurrentState(fight_motor_neuron_idx);
            fight_color = fight_motor_neuron.activation >= 0 ? fight_motor_neuron.activation : 0;
        }else if(a.mind is NARS nar)
        {

            eat_color = nar.GetStoredActivation(NARSGenome.eat_op); 
            mate_color = nar.GetStoredActivation(NARSGenome.mate_op);
            fight_color = nar.GetStoredActivation(NARSGenome.fight_op);
        }
        else
        {
            return;
        }

        double r = fight_color;
        double g = eat_color;
        double b = mate_color;

        Color new_color = new((float)r, (float)g, (float)b);

        new_color = Color.Lerp(new_color, Color.gray, 0.5f + 0.5f*(this.age / MAX_AGE));

        new_color = Color.Lerp(Color.black, new_color, this.health / MAX_HEALTH);


        SetColor(new_color);

    }

    public void SetColor(Color color)
    {
     
        if (this is WheeledRobot || this is ArticulatedRobot)
        {
            if (this.material == null) return;
            this.material.color = color;
        }
        else if (this is SoftVoxelRobot svr)
        {
            svr.soft_voxel_object.mesh.OverrideMeshColor(color);
        }
    }

    public virtual void InitializeMetabolism()
    {
        this.energy = OFFSPRING_COST;
        this.age = 0;
        this.health = AnimatBody.MAX_HEALTH;
        this.vision_sensor = new VisionSensor(); 
        var ray_preview = this.gameObject.AddComponent<RayPreview>();
        ray_preview.Init(this, vision_sensor);
    }

    private float lastScale = -1;
    private const float SCALE_CHANGE_THRESHOLD = 0.01f;

    internal void UpdateScale()
    {
        return;
        float newScale = this.base_scale * (0.5f + 0.5f * (this.age / AnimatBody.MAX_AGE));

        if (lastScale == -1) lastScale = 1.0f;

        if (Mathf.Abs(newScale - lastScale) < SCALE_CHANGE_THRESHOLD)
            return;

        this.scale = newScale;
        lastScale = newScale;

        this.transform.localScale = Vector3.one * this.scale;
    
        if (this is ArticulatedRobot art)
        {

            foreach (ArticulatedRobot.ArticulatedNode art_node in art.nodes)
            {
                float nodeScaleFactor = this.scale;

                // Update anchor positions
                var zDrive_ab = art_node.zDrive;
                zDrive_ab.anchorPosition = art_node.anchorPosZ * nodeScaleFactor;
                zDrive_ab.parentAnchorPosition = art_node.parentAnchorPosZ * nodeScaleFactor;

            }




        }

        Physics.SyncTransforms();

    }


    public abstract (Vector3, Vector3) GetVisionSensorPositionAndDirection();
    public abstract Vector3 GetVisionSensorUpDirection();
}
