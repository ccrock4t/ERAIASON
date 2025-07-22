using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using static Brain;
using CVX_Voxel = System.IntPtr;

public class SoftVoxelRobot : AnimatBody
{
    public SoftVoxelObject soft_voxel_object;
    public MeshCollider mesh_collider;

    public const int NUM_OF_SENSOR_NEURONS = 3;
    public const int NUM_OF_MOTOR_NEURONS = 3;

    public enum RobotVoxel
    {
        Empty,
        Raycast_Vision_Sensor,

        // touch sensor and beyond are available for evolution
        Touch_Sensor,
       // SineWave_Generator,

        /*                      ,
         *              Bone,
                Fat,
         *      Voluntary_Muscle,
            *      Temp_Sensor,
                Involuntary_Muscle,
                Gravity_Sensor,
                Strain_Sensor,
                LinearAcceleration_Sensor,
                AngularAcceleration_Sensor,*/
    }


    public SoftVoxelRobot() : base()
    {

    }

    public void Initialize(SoftVoxelRobotBodyGenome genome, float? scale=null)
    {
        base.InitializeMetabolism();
        this.soft_voxel_object = new(genome.dimensions3D,
            genome.voxel_array,
            this.override_color);
      
        this.soft_voxel_object.gameobject.transform.parent = this.transform;
        this.mesh_collider = this.soft_voxel_object.gameobject.AddComponent<MeshCollider>();
        this.mesh_collider.convex = true;
        this.mesh_collider.isTrigger = true;
        this.soft_voxel_object.gameobject.transform.localPosition = Vector3.zero;
        this.soft_voxel_object.gameobject.layer = AnimatArena.ANIMAT_GAMEOBJECT_LAYER;
        if (scale != null)
        {
            this.scale = (float)scale;
        }
        else
        {
            this.scale = this.soft_voxel_object.GetScale();
        }
        this.base_scale = this.scale;

        this.transform.localPosition = new Vector3(0,0.25f,0);
        this.transform.rotation = Quaternion.identity;
        this.transform.localScale = Vector3.one * this.scale;
    }

    public void DoVoxelyzeTimestep()
    {
        this.soft_voxel_object.DoTimestep();
        this.mesh_collider.sharedMesh = this.soft_voxel_object.mesh.unity_mesh;
    }


    public override float3 _GetCenterOfMass()
    {
        return this.soft_voxel_object.GetCenterOfMass();
    }

    // Start is called before the first frame update
    void Start()
    {

    }


    private void OnDestroy()
    {
        this.soft_voxel_object.OnDestroy();
    }

    public override bool Crashed()
    {
        return this.soft_voxel_object.crashed || !this.soft_voxel_object.contains_solid_voxels;
    }

    public override void MotorEffect(Animat animat)
    {
        base.MotorEffect(animat);

        if(animat.mind is Brain brain)
        {
            var nodeID_to_idx = brain.nodeID_to_idx;
            SoftVoxelObject soft_voxel_object = this.soft_voxel_object;
            Parallel.ForEach(soft_voxel_object.motor_voxels, cvx_voxel =>
            //foreach ((int3, CVX_Voxel, RobotVoxel) cvx_voxel in this.body.soft_voxel_object.motor_voxels)
            {
                int3 coords = cvx_voxel.Item1;

                RobotVoxel robot_voxel = cvx_voxel.Item3;


                Span<Neuron> motor_neurons = stackalloc Neuron[SoftVoxelRobot.NUM_OF_MOTOR_NEURONS];
                Span<float> motor_activations = stackalloc float[motor_neurons.Length];
                for (int i = 0; i < motor_neurons.Length; i++)
                {
                    int motor_neuron_idx = nodeID_to_idx[NEATGenome.GetTupleIDFromInt3(coords, i, Neuron.NeuronRole.Motor)];
                    motor_neurons[i] = brain.GetNeuronCurrentState(motor_neuron_idx);
                    if (motor_neurons[i].neuron_role != Neuron.NeuronRole.Motor) Debug.LogError("error");
                    motor_activations[i] = motor_neurons[i].activation;
                    if (float.IsNaN(motor_activations[i]) || float.IsInfinity(motor_activations[i]))
                    {
                        Debug.LogWarning("Got NaN for motor activation");
                    }
                    if (motor_neurons[i].activation_function == Neuron.ActivationFunction.Sigmoid)
                    {
                        // from [0,1] to [-1,1]
                        motor_activations[i] = motor_activations[i] * 2 - 1;
                    }
                    // cap motor activation in [-1,1]
                    motor_activations[i] = math.min(motor_activations[i], 1);
                    motor_activations[i] = math.max(motor_activations[i], -1);
                }


                // neuron activation for regular muscle
                for (int i = 0; i < motor_activations.Length; i++)
                {
                    if (motor_activations.Length > 1)
                    {
                        // theres multiple activations so they can go on different axes
                        if (i == 0) soft_voxel_object.SetVoxelTemperatureXFromNeuronActivation(cvx_voxel.Item2, motor_activations[i]);
                        if (i == 1) soft_voxel_object.SetVoxelTemperatureYFromNeuronActivation(cvx_voxel.Item2, motor_activations[i]);
                        if (i == 2) soft_voxel_object.SetVoxelTemperatureZFromNeuronActivation(cvx_voxel.Item2, motor_activations[i]);
                    }
                    else
                    {
                        // theres only 1 activation so contract the whole voxel
                        soft_voxel_object.SetVoxelTemperatureFromNeuronActivation(cvx_voxel.Item2, motor_activations[i]);
                    }

                    // subtract the energy used
                    //this.energy_remaining -= math.abs(motor_activations[i]);
                }


            });

  
         
        }
        else if (animat.mind is NARS nar)
        {
            // move
            float move_activation = nar.GetGoalActivation(NARSGenome.move_op);

            // rotate
            float rotate_activation = nar.GetGoalActivation(NARSGenome.rotate_op);

            var cvx_voxel = soft_voxel_object.motor_voxels[0];
        
            soft_voxel_object.SetVoxelTemperatureFromNeuronActivation(cvx_voxel.Item2, move_activation);

            var cvx_voxel2 = soft_voxel_object.motor_voxels[1];

            soft_voxel_object.SetVoxelTemperatureFromNeuronActivation(cvx_voxel2.Item2, rotate_activation);

        }
        this.DoVoxelyzeTimestep();
        /*        if (this.energy_remaining > ENERGY_IN_A_FOOD)
                {
                    SoftVoxelRobotBodyGenome cloned_genome = ((SoftVoxelRobotBodyGenome)this.genome).Clone();
                    cloned_genome.Mutate();
                    Animat offspring = AnimatArena.GetInstance().SpawnGenomeInRandomSpot(cloned_genome, cloned_genome.generation + 1);
                    this.energy_remaining -= ENERGY_IN_A_FOOD;
                    offspring.override_color = Color.black;
                    this.times_reproduced++;
                    Debug.Log("Animat asexually reproduced itself.");
                }*/

    }

    public override void Sense(Animat animat)
    {
        base.Sense(animat);


        if(animat.mind is Brain brain) { 
           
            var nodeID_to_idx = brain.nodeID_to_idx;
            Vector3 animat_location = animat.GetCenterOfMass();

            // random
            uint seed = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            SoftVoxelObject soft_voxel_object = this.soft_voxel_object;
            // first do most of the sensors
            // foreach (KeyValuePair<int3, CVX_Voxel> cvx_voxel_pair in this.body.soft_voxel_object.sensor_voxels)
            Parallel.ForEach(soft_voxel_object.sensor_voxels, (cvx_voxel_pair, loop_state, loop_index) =>
            {
                int3 coords = cvx_voxel_pair.Item1;
                CVX_Voxel cvx_voxel = cvx_voxel_pair.Item2;

                RobotVoxel robot_voxel = cvx_voxel_pair.Item3;
                Quaternion voxel_rotation = soft_voxel_object.GetVoxelRotation(cvx_voxel);
                //float3 voxel_angular_acceleration = this.body.soft_voxel_object.GetVoxelAngularAccelerationAndCache(cvx_voxel);
                //float3 voxel_linear_acceleration = this.body.soft_voxel_object.GetVoxelLinearAccelerationAndCache(cvx_voxel);
                //float3 voxel_angular_velocity = this.body.soft_voxel_object.GetVoxelAngularVelocity(cvx_voxel);
                soft_voxel_object.UpdateVoxelLinearVelocityCache(cvx_voxel);
                soft_voxel_object.UpdateVoxelTemperatureCache(cvx_voxel);
                float3 voxel_linear_velocity = soft_voxel_object.GetAverageVoxelLinearVelocity(cvx_voxel);
                float3 voxel_linear_velocity_nth_diff = soft_voxel_object.GetAverageVoxelLinearVelocityNthDifference(cvx_voxel);
                // draw velocity rays
                // Debug.DrawRay(GetVoxelCenterOfMass(transform_position, cvx_voxel), voxel_linear_velocity * MAX_VISION_DISTANCE, Color.blue, brain_update_period);

                //voxel_angular_acceleration = Quaternion.Inverse(voxel_rotation) * voxel_angular_acceleration; // transform global angular accelration to local rotation (is this necessary?)
                //voxel_linear_acceleration = Quaternion.Inverse(voxel_rotation) * voxel_linear_acceleration; // transform global acceleration to local rotation (is this necessary?)
                //voxel_angular_velocity = Quaternion.Inverse(voxel_rotation) * voxel_angular_velocity; // transform global acceleration to local rotation (is this necessary?)

                float3 voxel_strain = soft_voxel_object.GetVoxelStrainNormalizedAndCache(cvx_voxel);

                bool is_touching_ground = VoxelyzeEngine.GetVoxelFloorPenetration(cvx_voxel) > 0;

                float3 temp = soft_voxel_object.GetAverageTemp(cvx_voxel);


                // populate all the sensor neurons for this cell



                // touch sensor
                int touch_sensory_neuron_idx = nodeID_to_idx[new NeuronID(new int4(coords, 0), Neuron.NeuronRole.Sensor)];
                Neuron touch_sensory_neuron = brain.GetNeuronCurrentState(touch_sensory_neuron_idx);
                if (touch_sensory_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");
                touch_sensory_neuron.activation = is_touching_ground ? 1 : 0;
                brain.SetNeuronCurrentState(touch_sensory_neuron_idx, touch_sensory_neuron);

                // voxel rotation
                for (int i = 1; i < NUM_OF_SENSOR_NEURONS; i++)
                {
                    int rotation_sensory_neuron_idx = nodeID_to_idx[new NeuronID(new int4(coords, i), Neuron.NeuronRole.Sensor)];
                    Neuron rotation_sensory_neuron = brain.GetNeuronCurrentState(rotation_sensory_neuron_idx);
                    if (rotation_sensory_neuron.neuron_role != Neuron.NeuronRole.Sensor) Debug.LogError("error");

                    float angle = 0;
                    if (i == 1)
                    {
                        // get a ray pointing forward from the voxel
                        Vector3 direction = Vector3.forward;
                        direction = voxel_rotation * direction;
                        direction = Vector3.Normalize(direction);

                        // project it to horizontal (XZ) plane, angle between surface forward and animat local forward
                        Vector3 forward = new(direction.x, 0, direction.z);
                        float angle_magnitude = math.abs(Vector3.Angle(forward, direction));
                        angle = angle_magnitude * math.sign(direction.y) / 90f;
           
                    }
                    else if (i == 2)
                    {


                        // get a ray pointing right from the voxel
                        Vector3 direction = Vector3.right;
                        direction = voxel_rotation * direction;
                        direction = Vector3.Normalize(direction);


                        Vector3 right = new(direction.x, 0, direction.z);
                        float angle_magnitude = math.abs(Vector3.Angle(right, direction));
                        angle = angle_magnitude * math.sign(direction.y) / 90f;
                    }

                    rotation_sensory_neuron.activation = angle;
                    brain.SetNeuronCurrentState(rotation_sensory_neuron_idx, rotation_sensory_neuron);
                }
            });
        }
        else if (animat.mind is NARS nar)
        {
        }
    }



    public Vector3 GetVoxelCenterOfMass(CVX_Voxel cvx_voxel)
    {
        double3 voxel_relative_position = VoxelyzeEngine.GetVoxelCenterOfMass(cvx_voxel);
        return this.transform.position 
            + new Vector3((float)voxel_relative_position.x, (float)voxel_relative_position.y, (float)voxel_relative_position.z) * this.transform.localScale.x;
    }

    public override Quaternion GetRotation()
    {
        throw new NotImplementedException();
    }

    public override (Vector3, Vector3) GetVisionSensorPositionAndDirection()
    {
        SoftVoxelRobot svr = (SoftVoxelRobot)this;
        var svo = svr.soft_voxel_object;
        var cvx_voxel_pair = svo.raycast_sensor_voxels[0];
        var voxel_coords = cvx_voxel_pair.Item1;
        var cvx_voxel = cvx_voxel_pair.Item2;

        (var raycast_position, var direction) = svo.GetRaycastPositionAndDirectionFromVoxel(svr, cvx_voxel, voxel_coords);
        return (raycast_position + 0.3f*direction*(this.scale/40f), direction);
    }

    public override Vector3 GetVisionSensorUpDirection()
    {

        var cvx_voxel_pair = soft_voxel_object.raycast_sensor_voxels[0];

        CVX_Voxel cvx_voxel = cvx_voxel_pair.Item2;

       
        Quaternion voxel_rotation = soft_voxel_object.GetVoxelRotation(cvx_voxel);
        // get a ray pointing right from the voxel
        Vector3 direction = Vector3.up;
        direction = voxel_rotation * direction;
        direction = Vector3.Normalize(direction);
        return direction;
    }


}
