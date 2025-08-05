using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static ArticulatedRobotBodyGenome;
using static BodyGenome;
using static Brain;

public class ArticulatedRobot : AnimatBody
{
    public ArticulationBody root_ab;

    public List<(ArticulationBody, ArticulationBody, ArticulationBody)> body_segments_Drive_abs;
   // public ArticulationBody[] body_segments_articulation_bodies;
    public List<ArticulatedRobotBodySegment> body_segments;
   

    public List<ArticulatedNode> nodes = new();

    const float MAX_DEGREES_ROTATED_PER_TIMESTEP = 10.0f;
    public static Vector3 DRIVE_LIMITS = new Vector3(30f, 30f, 30f); // angle
    public const float MASS = 1.0f;


    // === drive mode parameters ===
    // F = stiffness * (currentPosition — target) — damping * (currentVelocity — targetVelocity)

    private const ArticulationDriveType ARTICULATION_DRIVING_METHOD = ArticulationDriveType.Target;
    const bool USE_FORCE_MODE = false;

    // target modes
    public const float TARGET_MODE_FORCE_LIMIT = 750;
    public const float TARGET_MODE_STIFFNESS = 1000f;
    public const float TARGET_MODE_DAMPING = 100f;
    public const float TARGET_MODE_MAX_JOINT_VELOCITY = 10f; // rad/s

    // force mode
    public const float FORCE_MODE_FORCE_LIMIT = 10000.75f;
    public const float FORCE_MODE_STIFFNESS = 1000f;
    public const float FORCE_MODE_DAMPING = 27.75f;
    public const float FORCE_MODE_TORQUE_SCALE = 10.25f;

    // velocity mode
    public const float VELOCITY_MODE_FORCE_LIMIT = 0.75f;
    public const float VELOCITY_MODE_STIFFNESS = 500f;
    public const float VELOCITY_MODE_DAMPING = 250.0f;
    public const float VELOCITY_MODE_VELOCITY_SCALE = 10f;


    GameObject root_gameobject;
    GameObject vision_segment;
    static GameObject articulated_body_segment_prefab;
    ArticulatedRobotBodyGenome body_genome;

    public const int NUM_OF_SENSOR_NEURONS_PER_SEGMENT = 10; // 6 touch sensor, 4 rotation sensors
    public const int NUM_OF_MOTOR_NEURONS_PER_JOINT = 3;


    public ArticulatedRobot() : base()
    {
    }
  
    public void Initialize(ArticulatedRobotBodyGenome body_genome, bool enable_colliders=true)
    {
        base.InitializeMetabolism();
        if (articulated_body_segment_prefab == null) articulated_body_segment_prefab = (GameObject)Resources.Load("Prefabs/Body/ArticulatedRobot/BodySegmentType0");
        

        this.material = new(Shader.Find("Standard"));
        this.base_scale = 0.5f;
        this.scale = this.base_scale;
        this.body_genome = body_genome;
        this.CreateGenomeAndInstantiateBody(body_genome);
        this.root_ab = this.root_gameobject.GetComponent<ArticulationBody>();
        this.vision_segment = this.root_gameobject;

        // ignore self collisions
        Collider[] colliders = GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enable_colliders;
            for (int j = i + 1; j < colliders.Length; j++)
            {
                Physics.IgnoreCollision(colliders[i], colliders[j]);
            }
        }


    }

    public GameObject GetVisionSensorSegment()
    {
        return vision_segment;
    }

    public override bool Crashed()
    {
        return false;
    }

    Vector3? center_of_mass = null;
    public override float3 _GetCenterOfMass()
    {
        if(center_of_mass == null)
        {
            center_of_mass = Vector3.zero;
            for (int j = 0; j < this.body_segments_Drive_abs.Count; j++)
            {
                var segment = this.body_segments[j];
                center_of_mass += segment.transform.position;
            }
            center_of_mass /= this.body_segments_Drive_abs.Count;
        }
 
        return (float3)center_of_mass;
    }

    public override Quaternion GetRotation()
    {
        throw new System.NotImplementedException();
    }

    public override void MotorEffect(Animat animat)
    {
        base.MotorEffect(animat);

        if(animat.mind is Brain brain)
        {
            center_of_mass = Vector3.zero;
            // effect environment with motor neurons
            Dictionary<string, int> motor_indices = new();// this.brain.neuron_indices[Brain.MOTOR_NEURON_KEY];
            for (int j = 0; j < this.body_segments_Drive_abs.Count; j++)
            {
                var segment = this.body_segments[j];
                center_of_mass += segment.transform.position;
                if (j == 0) continue; // skip the root ab
                (ArticulationBody ab_xDrive, ArticulationBody ab_yDrive, ArticulationBody ab_zDrive) = this.body_segments_Drive_abs[j];

                Vector3 output = new Vector3(0, 0, 0);
                for (int i = 0; i < NUM_OF_MOTOR_NEURONS_PER_JOINT; i++)
                {
                    var motorKey = new ArticulatedMotorKey(j,(dof)i);
                    var neuronID = body_genome.articulatedMotorKeyToNodeID[motorKey];
                    int brain_idx = brain.nodeID_to_idx[neuronID];

                    Neuron motor_neuron = brain.GetNeuronCurrentState(brain_idx);
                    double motor_activation = motor_neuron.activation;


                    if (!double.IsFinite(motor_activation))
                    {
                        Debug.LogWarning("Activation " + motor_activation + " is not finite. Setting to zero. ");
                        motor_activation = 0;
                    }
                    else if (double.IsNaN(motor_activation))
                    {
                        Debug.LogWarning("Activation " + motor_activation + " is NaN. Setting to zero. ");
                        motor_activation = 0;
                    }


                    if (motor_neuron.activation_function == Neuron.ActivationFunction.Sigmoid)
                    {
                        // from [0,1] to [-1,1]
                        motor_activation = motor_activation * 2 - 1;
                    }

                    //output.y = motor_activation;

                    if (i == 0)
                    {
                        output.x = (float)motor_activation;
                    }
                    else if (i == 1)
                    {

                        output.y = (float)motor_activation;

                    }
                    else if (i == 2)
                    {
                        output.z = (float)motor_activation;
                    }
                    else
                    {
                        Debug.LogError("error");
                        return;
                    }
                }

                if (ArticulatedRobot.ARTICULATION_DRIVING_METHOD == ArticulationDriveType.Target)
                {
                    //output = Vector3.one * math.sin(Time.time);
                    this.SetXDrive(ab_xDrive, output.x);
                    this.SetXDrive(ab_yDrive, output.y);
                    this.SetXDrive(ab_zDrive, output.z);

                }
                else if (ArticulatedRobot.ARTICULATION_DRIVING_METHOD == ArticulationDriveType.Force)
                {
                    ab_zDrive.AddTorque(output * FORCE_MODE_TORQUE_SCALE, ForceMode.Force);
                    //this.energy_remaining -= (math.abs(output.x) + math.abs(output.y) + math.abs(output.z));
                }
                else if (ArticulatedRobot.ARTICULATION_DRIVING_METHOD == ArticulationDriveType.Velocity)
                {
                    output *= VELOCITY_MODE_VELOCITY_SCALE;
                    ab_zDrive.SetDriveTargetVelocity(ArticulationDriveAxis.X, output.x);
                    ab_zDrive.SetDriveTargetVelocity(ArticulationDriveAxis.Y, output.y);
                    ab_zDrive.SetDriveTargetVelocity(ArticulationDriveAxis.Z, output.z);
                }
            }

            center_of_mass /= this.body_segments_Drive_abs.Count;
        }
        else if (animat.mind is NARS nar)
        {
            Debug.LogError("todo");
        }
    }

    public override void Sense(Animat animat)
    {
        base.Sense(animat);

        if (animat.mind is Brain brain)
        {
            // detect environment with sensory neurons
            for (int j = 0; j < this.body_segments_Drive_abs.Count; j++)
            {
                ArticulationBody body_segment_ab_zDrive = this.body_segments_Drive_abs[j].Item3;
                ArticulatedRobotBodySegment body_segment = this.body_segments[j];
                Quaternion canonicalRotation = body_segment.transform.rotation;

                // --- This is the fix ---
                // If w is negative, flip all components to get the canonical version.
                if (canonicalRotation.w < 0.0f)
                {
                    canonicalRotation.x *= -1f;
                    canonicalRotation.y *= -1f;
                    canonicalRotation.z *= -1f;
                    canonicalRotation.w *= -1f;
                }
                for (int i = 0; i < NUM_OF_SENSOR_NEURONS_PER_SEGMENT; i++)
                {
                    var sensorKey = new ArticulatedSensorKey(j, (ArticulatedSensorType)i);
                    var neuronID = body_genome.articulatedSensorKeyToNodeID[sensorKey];
                    int brain_idx = brain.nodeID_to_idx[neuronID];

                    Neuron sensory_neuron = brain.GetNeuronCurrentState(brain_idx);



                    //first 6 are touch sensors
                    if (i <= 5)
                    {
                        sensory_neuron.activation = body_segment.touch_sensor.faceContactStates[(ArticulatedRobotTouchSensor.BoxFace)i] > 0 ? 1.0f : 0f;
                    }
                    else if (i == 6)
                    {
                        sensory_neuron.activation = canonicalRotation.x;
                    }
                    else if (i == 7)
                    {
                        sensory_neuron.activation = canonicalRotation.y;
                    }
                    else if (i == 8)
                    {
                        sensory_neuron.activation = canonicalRotation.z;
                    }
                    else if (i == 9)
                    {
                        sensory_neuron.activation = canonicalRotation.w;

                    }

                    brain.SetNeuronCurrentState(brain_idx, sensory_neuron);
                }
            }

        }
        else if (animat.mind is NARS nar)
        {
            Debug.LogError("todo");
        }
    }
        


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public Transform GetFrontSegment()
    {
        return transform.GetChild(0).GetChild(1).GetChild(1).GetChild(0).Find("Cube");
    }



    public string GetSensorimotorJointKey(int i)
    {
        if (this.body_genome.creature == Creature.Hexapod)
        {
            switch (i)
            {
                case 0:
                    return "TOPBODYSEG";
                    break;
                case 1:
                    return "MIDBODYSEG";
                    break;
                case 2:
                    return "BOTBODYSEG";
                    break;
                case 3:
                    return "LEFTBODYHALF_BOTLEG_TOPLEGSEG";
                    break;
                case 4:
                    return "LEFTBODYHALF_BOTLEG_BOTLEGSEG";
                    break;
                case 5:
                    return "LEFTBODYHALF_BOTLEG_FOOTSEG";
                    break;
                case 6:
                    return "RIGHTBODYHALF_BOTLEG_TOPLEGSEG";
                    break;
                case 7:
                    return "RIGHTBODYHALF_BOTLEG_BOTLEGSEG";
                    break;
                case 8:
                    return "RIGHTBODYHALF_BOTLEG_FOOTSEG";
                    break;
                case 9:
                    return "LEFTBODYHALF_MIDLEG_TOPLEGSEG";
                    break;
                case 10:
                    return "LEFTBODYHALF_MIDLEG_BOTLEGSEG";
                    break;
                case 11:
                    return "LEFTBODYHALF_MIDLEG_FOOTSEG";
                    break;
                case 12:
                    return "RIGHTBODYHALF_MIDLEG_TOPLEGSEG";
                    break;
                case 13:
                    return "RIGHTBODYHALF_MIDLEG_BOTLEGSEG";
                    break;
                case 14:
                    return "RIGHTBODYHALF_MIDLEG_FOOTSEG";
                    break;
                case 15:
                    return "LEFTBODYHALF_TOPLEG_TOPLEGSEG";
                    break;
                case 16:
                    return "LEFTBODYHALF_TOPLEG_BOTLEGSEG";
                    break;
                case 17:
                    return "LEFTBODYHALF_TOPLEG_FOOTSEG";
                    break;
                case 18:
                    return "RIGHTBODYHALF_TOPLEG_TOPLEGSEG";
                    break;
                case 19:
                    return "RIGHTBODYHALF_TOPLEG_BOTLEGSEG";
                    break;
                case 20:
                    return "RIGHTBODYHALF_TOPLEG_FOOTSEG";
                    break;
                default:
                    Debug.LogError("ERROR for " + i);
                    return "";
            }
        }
        else if (this.body_genome.creature == Creature.Quadruped)
        {
            switch (i)
            {
                case 0:
                    return "TOPBODYSEG";
                    break;
                case 1:
                    return "LEFTBODYHALF_TOPLEG_TOPLEGSEG";
                    break;
                case 2:
                    return "LEFTBODYHALF_TOPLEG_BOTLEGSEG";
                    break;
                case 3:
                    return "LEFTBODYHALF_TOPLEG_FOOTSEG";
                    break;
                case 4:
                    return "RIGHTBODYHALF_TOPLEG_TOPLEGSEG";
                    break;
                case 5:
                    return "RIGHTBODYHALF_TOPLEG_BOTLEGSEG";
                    break;
                case 6:
                    return "RIGHTBODYHALF_TOPLEG_FOOTSEG";
                    break;
                case 7:
                    return "MIDBODYSEG";
                    break;
                case 8:
                    return "LEFTBODYHALF_MIDLEG_TOPLEGSEG";
                    break;
                case 9:
                    return "LEFTBODYHALF_MIDLEG_BOTLEGSEG";
                    break;
                case 10:
                    return "LEFTBODYHALF_MIDLEG_FOOTSEG";
                    break;
                case 11:
                    return "RIGHTBODYHALF_MIDLEG_TOPLEGSEG";
                    break;
                case 12:
                    return "RIGHTBODYHALF_MIDLEG_BOTLEGSEG";
                    break;
                case 13:
                    return "RIGHTBODYHALF_MIDLEG_FOOTSEG";
                    break;
                default:
                    Debug.LogError("ERROR for " + i);
                    return "";
            }
        }
        else
        {
            Debug.LogError("ERROR");
            return "";
        }

    }





    /// <summary>
    /// 
    /// </summary>
    /// <param name="joint"></param>
    /// <param name="activation"></param>
    /// 

    Dictionary<ArticulationBody, float> last_activations = new();
    private int number;

    public void SetXDrive(ArticulationBody joint, float activation)
    {
        joint.xDrive = SetDrive(joint.xDrive, activation);
    }


    ///// <summary>
    ///// 
    ///// </summary>
    ///// <param name="joint"></param>
    ///// <param name="activation"></param>
    //public void SetYDrive(ArticulationBody joint, float activation)
    //{
    //    joint.yDrive = SetDrive(joint.yDrive, activation);
    //}
    ///// <summary>
    ///// 
    ///// </summary>
    ///// <param name="joint"></param>
    ///// <param name="activation"></param>
    //public void SetZDrive(ArticulationBody joint, float activation)
    //{
    //    joint.zDrive = SetDrive(joint.zDrive, activation);
    //}




    /// <summary>
    /// 
    /// </summary>
    /// <param name="joint"></param>
    /// <param name="activation"></param>
    public ArticulationDrive SetDrive(ArticulationDrive drive, float activation)
    {
        /*      // period
              float seconds_per_degree = 0.1f;

              float speed = Time.fixedDeltaTime/seconds_per_degree;
              float rotationChange = activation * speed;

              drive.target += rotationChange;

              drive.target = Mathf.Min(drive.target, DRIVE_LIMITS);
              drive.target = Mathf.Max(drive.target, -DRIVE_LIMITS);*/

        float normalized_activation = (activation + 1f) / 2f; // normalize from [-1,1] to [0,1]
        float target = Mathf.Lerp(drive.lowerLimit, drive.upperLimit, normalized_activation);
        drive.target = target;
        return drive;
    }

    /// <summary>
    /// The genome is instantiated once the script is created
    /// </summary>
    public void CreateGenomeAndInstantiateBody(ArticulatedRobotBodyGenome genome)
    {

        this.body_segments_Drive_abs = new();
        this.body_segments = new();
        InstantiateNode(genome.node_array, genome.node_array[0], null,Vector3Int.one);


    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="n"></param>
    /// <param name="parentInfo"></param>
    /// <param name="initial_recursions_remaining">null if not recurring</param>
    /// <returns></returns>

    public class ArticulatedNode
    {
        public ArticulationBody zDrive;
        public ArticulationBody yDrive;
        public ArticulationBody xDrive;
        public Vector3 originalPosition;
        public Vector3 anchorPosZ;
        public Vector3 parentAnchorPosZ;
        internal Transform segment;
        internal Vector3 dimensions;
        internal Vector3 anchorPosY;
        internal Vector3 parentAnchorPosY;
        internal Vector3 anchorPosX;
        internal Vector3 parentAnchorPosX;
        internal float AbsoluteScaleFactor;
    }

    public void InstantiateNode(MorphologyNode[] g, MorphologyNode n, (Transform, MorphologyConnection, float)? parentInfo, Vector3Int negate_rotation)
    {
        if (n.recursive_limit <= 0) return;

        GameObject nodeGO;
        float scaleFactor;
        nodeGO = Instantiate(articulated_body_segment_prefab);
        ArticulatedNode art_node = new();

        Transform segment = nodeGO.transform.GetChild(0).GetChild(0).Find("Segment");

        ArticulationBody xDrive_ab, yDrive_ab;
        ArticulationBody zDrive_ab = nodeGO.GetComponent<ArticulationBody>();
        ArticulationJointType joint_type;

        if (parentInfo == null)
        {
            // this is the root, it has no parent
            scaleFactor = this.base_scale; // Use this.scale for root scale factors
            segment.localScale = (n.dimensions) * scaleFactor;

            nodeGO.transform.parent = this.transform;
            nodeGO.transform.localPosition = Vector3.zero;
            joint_type = ArticulationJointType.SphericalJoint;
            art_node.AbsoluteScaleFactor = scaleFactor; // Store the relative factor
        }
        else
        {
   
            (Transform parent_transform, MorphologyConnection parent_connection, float parentScaleFactor) = ((Transform, MorphologyConnection, float))parentInfo;
            scaleFactor = parentScaleFactor * parent_connection.scale;
            nodeGO.transform.parent = parent_transform;
            art_node.AbsoluteScaleFactor = scaleFactor; // Store the relative factor
            // Apply consistent scaling to position
            nodeGO.transform.localPosition = (parent_connection.position_offset) * scaleFactor;

            nodeGO.transform.localRotation = Quaternion.identity;
            segment.localScale = (n.dimensions) * scaleFactor;
            zDrive_ab.enabled = false;
            joint_type = parent_connection.joint_type;

            // Scale the joint anchor positions
            Vector3 scaledAnchorPos = zDrive_ab.anchorPosition;
            scaledAnchorPos.Scale(new Vector3(scaleFactor, scaleFactor, scaleFactor));
            zDrive_ab.anchorPosition = scaledAnchorPos;

            Vector3 scaledParentAnchorPos = zDrive_ab.parentAnchorPosition;
            scaledParentAnchorPos.Scale(new Vector3(scaleFactor, scaleFactor, scaleFactor));
            zDrive_ab.parentAnchorPosition = scaledParentAnchorPos;

            Vector3 rotation = parent_connection.rotation;
            if (negate_rotation.x < 0) rotation.x += parent_connection.negative_offset.x;
            if (negate_rotation.y < 0) rotation.y += parent_connection.negative_offset.y;
            if (negate_rotation.z < 0) rotation.z += parent_connection.negative_offset.z;
            zDrive_ab.anchorRotation = Quaternion.identity;
            nodeGO.transform.Rotate(rotation, Space.Self);
        }

        ArticulationDrive drive;

        ArticulationDrive SetDriveProperties(ArticulationDrive local_drive, float drive_limits)
        {
            float stiffness, damping, forceLimit;
            ArticulationDriveType drive_type;
            if (ARTICULATION_DRIVING_METHOD == ArticulationDriveType.Target)
            {
                stiffness = TARGET_MODE_STIFFNESS;
                damping = TARGET_MODE_DAMPING;
                forceLimit = TARGET_MODE_FORCE_LIMIT;
                drive_type = ArticulationDriveType.Force;
            }
            else if (ARTICULATION_DRIVING_METHOD == ArticulationDriveType.Force)
            {
                stiffness = FORCE_MODE_STIFFNESS;
                damping = FORCE_MODE_DAMPING;
                forceLimit = FORCE_MODE_FORCE_LIMIT;
                drive_type = ArticulationDriveType.Force;
            }
            if (ARTICULATION_DRIVING_METHOD == ArticulationDriveType.Velocity)
            {
                stiffness = FORCE_MODE_STIFFNESS;
                damping = FORCE_MODE_DAMPING;
                forceLimit = FORCE_MODE_FORCE_LIMIT;
                drive_type = ArticulationDriveType.Force;
            }
            local_drive.stiffness = stiffness;
            local_drive.damping = damping;
            local_drive.upperLimit = drive_limits;
            local_drive.lowerLimit = -drive_limits;
            local_drive.driveType = drive_type;
            local_drive.forceLimit = forceLimit;
            return local_drive;
        }

        if (joint_type == ArticulationJointType.SphericalJoint)
        {
            drive = zDrive_ab.xDrive;
            drive = SetDriveProperties(drive, DRIVE_LIMITS.x);
            zDrive_ab.xDrive = drive;

            yDrive_ab = zDrive_ab.transform.GetChild(0).GetComponent<ArticulationBody>();
            // Scale joint anchors for Y drive
            Vector3 yAnchorPos = yDrive_ab.anchorPosition;
            yAnchorPos.Scale(new Vector3(scaleFactor, scaleFactor, scaleFactor));
            yDrive_ab.anchorPosition = yAnchorPos;

            Vector3 yParentAnchorPos = yDrive_ab.parentAnchorPosition;
            yParentAnchorPos.Scale(new Vector3(scaleFactor, scaleFactor, scaleFactor));
            yDrive_ab.parentAnchorPosition = yParentAnchorPos;

            drive = yDrive_ab.xDrive;
            drive = SetDriveProperties(drive, DRIVE_LIMITS.y);
            yDrive_ab.xDrive = drive;

            xDrive_ab = yDrive_ab.transform.GetChild(0).GetComponent<ArticulationBody>();
            // Scale joint anchors for X drive
            Vector3 xAnchorPos = xDrive_ab.anchorPosition;
            xAnchorPos.Scale(new Vector3(scaleFactor, scaleFactor, scaleFactor));
            xDrive_ab.anchorPosition = xAnchorPos;

            Vector3 xParentAnchorPos = xDrive_ab.parentAnchorPosition;
            xParentAnchorPos.Scale(new Vector3(scaleFactor, scaleFactor, scaleFactor));
            xDrive_ab.parentAnchorPosition = xParentAnchorPos;

            drive = xDrive_ab.xDrive;
            drive = SetDriveProperties(drive, DRIVE_LIMITS.z);
            xDrive_ab.xDrive = drive;

            zDrive_ab.maxJointVelocity = TARGET_MODE_MAX_JOINT_VELOCITY;
            yDrive_ab.maxJointVelocity = TARGET_MODE_MAX_JOINT_VELOCITY;
            xDrive_ab.maxJointVelocity = TARGET_MODE_MAX_JOINT_VELOCITY;

            zDrive_ab.linearLockX = ArticulationDofLock.LimitedMotion;
            yDrive_ab.linearLockX = ArticulationDofLock.LimitedMotion;
            xDrive_ab.linearLockX = ArticulationDofLock.LimitedMotion;
            zDrive_ab.twistLock = ArticulationDofLock.LimitedMotion;
            zDrive_ab.swingYLock = ArticulationDofLock.LimitedMotion;
            zDrive_ab.swingZLock = ArticulationDofLock.LimitedMotion;
            yDrive_ab.twistLock = ArticulationDofLock.LimitedMotion;
            yDrive_ab.swingYLock = ArticulationDofLock.LimitedMotion;
            yDrive_ab.swingZLock = ArticulationDofLock.LimitedMotion;
            xDrive_ab.twistLock = ArticulationDofLock.LimitedMotion;
            xDrive_ab.swingYLock = ArticulationDofLock.LimitedMotion;
            xDrive_ab.swingZLock = ArticulationDofLock.LimitedMotion;

        }
        else
        {
            Debug.LogError("Not yet supported");
            return;
        }

        body_segments_Drive_abs.Add((xDrive_ab, yDrive_ab, zDrive_ab));
        xDrive_ab.gameObject.layer = AnimatArena.ANIMAT_GAMEOBJECT_LAYER;
        yDrive_ab.gameObject.layer = AnimatArena.ANIMAT_GAMEOBJECT_LAYER;
        zDrive_ab.gameObject.layer = AnimatArena.ANIMAT_GAMEOBJECT_LAYER;

        var body_segment = xDrive_ab.gameObject.GetComponent<ArticulatedRobotBodySegment>();
        this.body_segments.Add(body_segment);

        ArticulationBody lowest_drive_in_hierarchy = zDrive_ab.transform.GetChild(0).GetChild(0).GetComponent<ArticulationBody>();

        if (n == g[0] && root_gameobject == null)
        {
            this.root_gameobject = nodeGO;
            this.root_gameobject.transform.parent = this.transform;
            this.root_gameobject.transform.localRotation = Quaternion.identity;
        }
        nodeGO.name = n.name + " " + number;
        number++;

        // Scale mass appropriately with volume scaling
        zDrive_ab.mass = MASS * n.dimensions.x * n.dimensions.y * n.dimensions.z;// * (scaleFactor * scaleFactor * scaleFactor);

        var mesh = segment.Find("Cube");
        var collider = segment.transform.parent.GetComponent<BoxCollider>();
        collider.center = Vector3.Scale(collider.center, segment.transform.localScale);
        collider.size = Vector3.Scale(collider.size, segment.transform.localScale);

        mesh.GetComponent<Renderer>().material = this.material;

        art_node.originalPosition = nodeGO.transform.localPosition;
        // Store unscaled anchor positions for reference
        art_node.anchorPosZ = zDrive_ab.anchorPosition;
        art_node.parentAnchorPosZ = zDrive_ab.parentAnchorPosition;
        art_node.anchorPosY = yDrive_ab.anchorPosition;
        art_node.parentAnchorPosY = yDrive_ab.parentAnchorPosition;
        art_node.anchorPosX = xDrive_ab.anchorPosition;
        art_node.parentAnchorPosX = xDrive_ab.parentAnchorPosition;
        art_node.zDrive = zDrive_ab;
        art_node.yDrive = yDrive_ab;
        art_node.xDrive = xDrive_ab;
        art_node.segment = segment;
        art_node.dimensions = n.dimensions;

        nodes.Add(art_node);

        n.recursive_limit--; // lower the recursive limit in case this node occurs in a cycle
        foreach (MorphologyConnection connection in n.connections)
        {
            MorphologyNode to_node = connection.to_node;
            if (!connection.terminal_only || (connection.terminal_only && n.recursive_limit == 0))
            {
                Vector3Int negate_next_rotation = negate_rotation;
                if (connection.position_offset.x < -0.001)
                {
                    negate_next_rotation.x *= -1;
                }
                if (connection.position_offset.y > 0.002)
                {
                    negate_next_rotation.y *= -1;
                }
                if (connection.position_offset.z < -0.001)
                {
                    negate_next_rotation.z *= -1;
                }

                InstantiateNode(g, to_node, (lowest_drive_in_hierarchy.transform, connection, scaleFactor), negate_next_rotation);
            }
        }
        n.recursive_limit++;

        zDrive_ab.enabled = true;
    }

    public override (Vector3, Vector3) GetVisionSensorPositionAndDirection()
    {
        var vision_transform = GetVisionSensorSegment().transform;
        return (vision_transform.position, -vision_transform.up);
    }

    public override Vector3 GetVisionSensorUpDirection()
    {
        var vision_transform = GetVisionSensorSegment().transform;
        return vision_transform.forward;
    }
}
