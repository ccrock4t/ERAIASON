using Unity.Mathematics;
using UnityEngine;

public class WheeledRobot : AnimatBody
{

    public const int MOVE_FORWARD_NEURON_ID = 0;
    public const int ROTATE_RIGHT_NEURON_ID = 1;
    public const int ROTATE_LEFT_NEURON_ID = 2;
    public const int JUMP_MOTOR_NEURON_ID = 3;

    public const int TOUCH_SENSOR_NEURON_ID = 4;

    const float movement_speed = 0.25f;
    const float rotate_speed = 2.5f;

    const float JUMP_THRESHOLD = 0.5f;

    const float climb_speed = 0.2f;

    public Rigidbody controller;

    ObjectTouchingCounter object_touching_counter;
    public WheeledRobot() : base()
    {

    }

    public const string robo_name = "wheeledrobot";
    public void Initialize()
    {
        base.InitializeMetabolism();
        GameObject robot = Instantiate(LoadResources.wheeled_robot_prefab);
        robot.name = robo_name;
        robot.transform.parent = this.transform;
        robot.transform.localPosition = new Vector3(0, 0.5f, 0);
        this.gameObject.layer = AnimatArena.ANIMAT_GAMEOBJECT_LAYER;
        robot.gameObject.layer = AnimatArena.ANIMAT_GAMEOBJECT_LAYER;

        this.controller = robot.GetComponent<Rigidbody>();
        robot.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
        this.material = new(Shader.Find("Standard")); 
        this.transform.Find(robo_name).Find("Plane").GetComponent<Renderer>().material = this.material;
        this.object_touching_counter = this.transform.Find(robo_name).GetComponent<ObjectTouchingCounter>();

        Physics.SyncTransforms();
        this.base_scale = 1.0f;
        this.scale = base_scale;
    }

    public override bool Crashed()
    {
        return false;
    }

    public override float3 _GetCenterOfMass()
    {
        return (float3)(controller.position);
    }

    public override void MotorEffect(Animat animat)
    {
        base.MotorEffect(animat);

        double move_activation = 0;
        double rotate_activation = 0;
        double jump_activation = 0;
        if (animat.mind is Brain brain)
        {
            // move forward
            int move_forward_neuron_idx = brain.nodeID_to_idx[MOVE_FORWARD_NEURON_ID];
            Brain.Neuron move_forward_neuron = brain.GetNeuronCurrentState(MOVE_FORWARD_NEURON_ID);


            move_activation = move_forward_neuron.activation;

            if (move_activation < 0)
            {
                move_activation = 0;
            }

            // rotate
            var rotate_right_neuron_ID = ROTATE_RIGHT_NEURON_ID;
            int rotate_right_neuron_idx = brain.nodeID_to_idx[rotate_right_neuron_ID];
            Brain.Neuron rotate_right_neuron = brain.GetNeuronCurrentState(rotate_right_neuron_idx);
            rotate_activation = rotate_right_neuron.activation;

            var rotate_left_neuron_ID = ROTATE_LEFT_NEURON_ID;
            int rotate_left_neuron_idx = brain.nodeID_to_idx[rotate_left_neuron_ID];
            Brain.Neuron rotate_left_neuron = brain.GetNeuronCurrentState(rotate_left_neuron_idx);
            rotate_activation -= rotate_left_neuron.activation;

         
            // Jump
            var jump_neuron_ID = JUMP_MOTOR_NEURON_ID;
            int jump_neuron_idx = brain.nodeID_to_idx[jump_neuron_ID];
            Brain.Neuron jump_neuron = brain.GetNeuronCurrentState(jump_neuron_idx);
            jump_activation = jump_neuron.activation;

            if (jump_activation < JUMP_THRESHOLD) jump_activation = 0;
            
        }
        else if (animat.mind is NARS nar)
        {

            // move
            move_activation = nar.GetGoalActivation(NARSGenome.move_op);

            // rotate
            rotate_activation = nar.GetGoalActivation(NARSGenome.rotate_right_op);

            // rotate
           // rotate_activation -= nar.GetGoalActivation(NARSGenome.rotate_left_op);//
        }
        else if (GlobalConfig.BRAIN_PROCESSING_METHOD == GlobalConfig.BrainProcessingMethod.Random)
        {

            // move
            move_activation = UnityEngine.Random.value;

            // rotate
            rotate_activation = UnityEngine.Random.value;
            rotate_activation -= UnityEngine.Random.value;
        }
        else 
        {
            Debug.LogError("error");
            return;
        }

        // move
        Vector3 motion_vector = controller.transform.forward;
        Vector3 new_position = controller.position + motion_vector * (float)move_activation * movement_speed;

        // jump
     
        if (this.object_touching_counter.num_objects_touching > 0)
        {
            new_position.y += (float)jump_activation * climb_speed;
        }
       


        this.controller.MovePosition(new_position);

        // rotate
        this.controller.MoveRotation(controller.rotation * Quaternion.Euler(Vector3.up * (float)rotate_activation * rotate_speed));

   
    }


    public override void Sense(Animat animat)
    {
        base.Sense(animat);

        if (animat.mind is Brain brain)
        {
            // touch sensor
            var touch_sensor_neuron_ID = TOUCH_SENSOR_NEURON_ID;
            int touch_sensor_neuron_idx = brain.nodeID_to_idx[touch_sensor_neuron_ID];
            Brain.Neuron touch_sensor_neuron = brain.GetNeuronCurrentState(touch_sensor_neuron_idx);
            if (this.object_touching_counter.num_objects_touching > 0)
            {
                touch_sensor_neuron.activation = 1;
            }
            else
            {
                touch_sensor_neuron.activation = 0;
            }
            brain.SetNeuronCurrentState(touch_sensor_neuron_idx, touch_sensor_neuron);
        }
        else if (animat.mind is NARS nar)
        {
        }
    }


    public override Quaternion GetRotation()
    {
        return this.controller.rotation;
    }


    public Transform GetRobotTransform()
    {
        return this.transform.Find("wheeledrobot");
    }

    public override (Vector3, Vector3) GetVisionSensorPositionAndDirection()
    {
        Transform robot_transform = GetRobotTransform();
        var raycast_direction = robot_transform.forward;
        var raycast_position = robot_transform.position + this.scale * (raycast_direction.normalized*1.2f  + robot_transform.up * 0.2f);

        return (raycast_position, raycast_direction);
    }

    public override Vector3 GetVisionSensorUpDirection()
    {
        Transform robot_transform = GetRobotTransform();
        var raycast_direction = robot_transform.up;
        return raycast_direction;
    }
}

