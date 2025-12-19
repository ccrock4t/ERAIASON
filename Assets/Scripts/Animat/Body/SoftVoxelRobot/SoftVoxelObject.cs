using System.Collections.Generic;
using UnityEngine;

using CVoxelyze = System.IntPtr;
using CVX_Material = System.IntPtr;
using CVX_MeshRender = System.IntPtr;
using CVX_Voxel = System.IntPtr;
using UnityEngine.Rendering;
using Unity.Jobs;
using System;
using static SoftVoxelRobot;
using Unity.Mathematics;
using Unity.Collections;
using System.Threading.Tasks;
using System.Collections.Concurrent;

// remember for Voxelyze, the y and z axes are switched compared to Unity's axes
public class SoftVoxelObject
{
    public bool crashed = false;
    public const float TIMESTEP = -1f;

    public GameObject gameobject;
    public SoftVoxelMesh mesh;
    public CVoxelyze cpp_voxel_object;
    public Dictionary<RobotVoxel, CVX_Material> robot_voxel_to_voxelyze_materials;
    public Dictionary<CVX_Material, RobotVoxel> voxelyze_materials_to_robot_voxel;

    const float MPa = 1000000;

    public float recommended_timestep = 0;
    public float last_frame_elapsed_simulation_time = 0;

    Task physics_update_task;
    Task mesh_update_task;

    public int3 dimensions;

    public bool contains_solid_voxels = false;

    public const float BASE_TEMP = 0;
    public const float TEMP_MAX_MAGNITUDE = 39f;
    const float SINUSOID_SPEED = 16f;

    public int num_of_voxels = 0;

    float time = 0;

    public List<(int3, CVX_Voxel, RobotVoxel)> sensor_voxels;
    public Dictionary<int, CVX_Voxel> NARS_voxels;
    public List<(int3, CVX_Voxel)> raycast_sensor_voxels;
    public List<(int3, CVX_Voxel, RobotVoxel)> motor_voxels;
    public Dictionary<CVX_Voxel, double3> voxel_angular_velocities;
    public Dictionary<CVX_Voxel, double3> voxel_linear_velocities;
    public Dictionary<CVX_Voxel, float> voxel_sinusoid_states;

    ConcurrentDictionary<CVX_Voxel, Queue<double3>> recent_velocities;
    ConcurrentDictionary<CVX_Voxel, Queue<double3>> recent_temps;

    const float lattice_dimension = 0.01f;
    const float scale = 0.4f;



    JobHandle handle;
    int lowest_voxel_height = 0;

    // for jobs system
    NativeArray<int> diverged;
    NativeArray<int> current_stage;
    NativeArray<int> counter;
    int linksListSize; 
    int voxelsListSize;
    int collisionsListSize;
    int num_threads;

    const float CTE = 0.01f; // how much the voxel can expand

    static int[] world_dims = new int[] {
        GlobalConfig.WORLD_DIMENSIONS.x,
        GlobalConfig.WORLD_DIMENSIONS.y,
        GlobalConfig.WORLD_DIMENSIONS.z
    };

    public SoftVoxelObject(int3 dimensions,RobotVoxel[] robot_voxels, Color? override_mesh_color = null)
    {
        this.dimensions = dimensions;
        this.cpp_voxel_object = VoxelyzeEngine.CreateCVoxelyze(lattice_dimension, world_dims);
        VoxelyzeEngine.SetGravity(this.cpp_voxel_object, 1f);

        if(GlobalConfig.WORLD_TYPE == GlobalConfig.WorldType.VoxelWorld)
        {
            lowest_voxel_height = 4;
        }
        else if (GlobalConfig.WORLD_TYPE == GlobalConfig.WorldType.FlatPlane)
        {
            lowest_voxel_height = 0;
        }

    
        this.robot_voxel_to_voxelyze_materials = new();
        this.voxelyze_materials_to_robot_voxel = new();
        this.motor_voxels = new();
        this.sensor_voxels = new();
        this.NARS_voxels = new();
        this.raycast_sensor_voxels = new();
        this.voxel_angular_velocities = new();
        this.voxel_linear_velocities = new();
        this.voxel_sinusoid_states = new();
        

        float density = MPa/4;
        float poisson_ratio = 0.35f;
        float soft_material_modulus = 50 * MPa;
        float medium_material_modulus = 50 * MPa;
        float hard_material_modulus = 50 * MPa;
        Color brown_color = new Color(100 / 255f, 65 / 255f, 23 / 255f);
      
        this.robot_voxel_to_voxelyze_materials[RobotVoxel.Touch_Sensor]
            = VoxelyzeEngine.AddNewMaterial(this.cpp_voxel_object, medium_material_modulus, density, poisson_ratio, Color.magenta);
        this.robot_voxel_to_voxelyze_materials[RobotVoxel.Raycast_Vision_Sensor]
            = VoxelyzeEngine.AddNewMaterial(this.cpp_voxel_object, medium_material_modulus, density, poisson_ratio, Color.green);
        /*  this.robot_voxel_to_voxelyze_materials[RobotVoxel.SineWave_Generator]
            = VoxelyzeEngine.AddNewMaterial(this.cpp_voxel_object, medium_material_modulus, density, poisson_ratio, Color.cyan);
              this.robot_voxel_to_voxelyze_materials[RobotVoxel.Fat]
                    = VoxelyzeEngine.AddNewMaterial(this.cpp_voxel_object, soft_material_modulus, density, poisson_ratio, Color.yellow);
                this.robot_voxel_to_voxelyze_materials[RobotVoxel.Bone]
                    = VoxelyzeEngine.AddNewMaterial(this.cpp_voxel_object, hard_material_modulus, density, poisson_ratio, Color.white);*/

      
        float static_friction = 1f;
        float kinetic_friction = 0.7f;
        foreach (KeyValuePair<RobotVoxel, CVoxelyze> pair in this.robot_voxel_to_voxelyze_materials)
        {
            CVoxelyze voxelyze_material = pair.Value;
            VoxelyzeEngine.SetInternalDamping(voxelyze_material, 1);
            VoxelyzeEngine.SetGlobalDamping(voxelyze_material, 0.01f);
            VoxelyzeEngine.SetCollisionDamping(voxelyze_material, 0.8f);
            VoxelyzeEngine.SetFriction(voxelyze_material, static_friction, kinetic_friction);

            VoxelyzeEngine.SetCoefficientOfThermalExpansion(voxelyze_material, CTE);

            this.voxelyze_materials_to_robot_voxel[pair.Value] = pair.Key;
        }
        //VoxelyzeEngine.SetCoefficientOfThermalExpansion(this.voxelyze_materials[RobotVoxel.Voluntary_Muscle], CTE);
        // pre-processing
        for (int i = 0; i < robot_voxels.Length; i++)
        { 
            RobotVoxel material_enum = robot_voxels[i];
            if (material_enum == RobotVoxel.Empty) continue;

            int3 coords = GlobalUtils.Index_int3FromFlat(i, dimensions);

            // record lowest voxel height, so we can spawn the object on the ground
            this.contains_solid_voxels = true;
    
        }

        // spawn the voxels
        for (int i = 0; i < robot_voxels.Length; i++)
        {
            RobotVoxel material_enum = robot_voxels[i];
            int3 coords = GlobalUtils.Index_int3FromFlat(i, dimensions);


            if (material_enum == RobotVoxel.Empty)
            {
                // can't add a voxel that doesn't exist, so skip this
                continue;
            }
            CVX_Material voxelyze_material = this.robot_voxel_to_voxelyze_materials[material_enum];
                
            CVX_Voxel cvx_voxel = AddVoxel(voxelyze_material, coords.x, coords.y - lowest_voxel_height, coords.z);

            // this voxel can be controlled by the brain
            this.motor_voxels.Add((coords, cvx_voxel, material_enum));

            // this voxel can be sensed by the brain
            this.sensor_voxels.Add((coords, cvx_voxel, material_enum));

            this.NARS_voxels.Add(i, cvx_voxel);
   
            if (material_enum == RobotVoxel.Raycast_Vision_Sensor)
            {
                this.raycast_sensor_voxels.Add((coords, cvx_voxel));
            }
               
            this.voxel_angular_velocities[cvx_voxel] = double3.zero;
            this.voxel_linear_velocities[cvx_voxel] = double3.zero;
            num_of_voxels++;
        }
        


       
        VoxelyzeEngine.EnableFloor(this.cpp_voxel_object);
        // VoxelyzeEngine.EnableCollisions(this.cpp_voxel_object);

        // now create mesh and gameobject;
        this.mesh = new SoftVoxelMesh(this.cpp_voxel_object, override_mesh_color);
        this.gameobject = new GameObject("SoftVoxelObject");
       // this.gameobject.transform.Rotate(new Vector3(-90, 0, 0)); //roate since y and z axes are flipped in Voxelyze compared to Unity
        //this.gameobject.transform.Rotate(new Vector3(0, 180, 0)); //roate since y and z axes are flipped in Voxelyze compared to Unity
        //this.gameobject.transform.localScale = Vector3.one * 100f;
        MeshRenderer mr = this.gameobject.AddComponent<MeshRenderer>();
        mr.material = LoadResources.vertex_color;
        MeshFilter filter = this.gameobject.AddComponent<MeshFilter>();
        filter.mesh = this.mesh.unity_mesh;

        SetAmbientTemperature(BASE_TEMP);

        this.linksListSize = VoxelyzeEngine.GetLinksListSize(this.cpp_voxel_object);
        this.voxelsListSize = VoxelyzeEngine.GetVoxelsListSize(this.cpp_voxel_object);
        this.collisionsListSize = VoxelyzeEngine.GetCollisionsListSize(this.cpp_voxel_object);
        this.num_threads = math.max(math.max(this.linksListSize, this.voxelsListSize), this.collisionsListSize);
        diverged = new(1, Allocator.Persistent);
        counter = new(1, Allocator.Persistent);
        current_stage = new(1, Allocator.Persistent);
        diverged[0] = 0;

        this.recent_velocities = new();
        this.recent_temps = new();
    }

    public RobotVoxel GetVoxelType(CVX_Voxel voxel)
    {
        CVX_Material material = VoxelyzeEngine.GetVoxelMaterial(voxel);
        return this.voxelyze_materials_to_robot_voxel[material];
    }

    public RobotVoxel GetVoxelType(int3 coords)
    {
        CVX_Material material = VoxelyzeEngine.GetVoxelMaterialFromCoords(this.cpp_voxel_object, coords.x, coords.y - lowest_voxel_height, coords.z);
        if (material == IntPtr.Zero) return RobotVoxel.Empty;
        return this.voxelyze_materials_to_robot_voxel[material];
    }

    double max_angular_acceleration = 0;
    double max_linear_acceleration = 0;
    double max_strain = 0;

    public float3 GetVoxelAngularVelocity(CVX_Voxel voxel)
    {
        double3 result = VoxelyzeEngine.GetVoxelAngularVelocity(voxel);
        return (float3)result;
    }

    // this should be called once per step
    const int NUM_OF_VELOCITIES_TO_STORE = 5;
    const int NUM_OF_TEMPS_TO_STORE = 1;
    public void UpdateVoxelLinearVelocityCache(CVX_Voxel voxel)
    {
        float3 velocity = (float3)VoxelyzeEngine.GetVoxelVelocity(voxel);
        //velocity = Quaternion.Inverse(GetVoxelRotation(voxel)) * velocity; // transform global acceleration to local rotation (is this necessary?)
        if (!this.recent_velocities.ContainsKey(voxel)) this.recent_velocities[voxel] = new();
        this.recent_velocities[voxel].Enqueue(velocity);
        if(this.recent_velocities[voxel].Count > NUM_OF_VELOCITIES_TO_STORE)
        {
            this.recent_velocities[voxel].Dequeue();
        }
    }

    public void UpdateVoxelTemperatureCache(CVX_Voxel voxel)
    {
        if (!this.recent_temps.ContainsKey(voxel)) this.recent_temps[voxel] = new();
        double[] temp = new double[3];
        temp[0] = 0;
        temp[1] = 0;
        temp[2] = 0;
        VoxelyzeEngine.GetVoxelTemperature(voxel, temp);
        double3 result = new();
        result.x = temp[0];
        result.y = temp[1];
        result.z = temp[2];

        this.recent_temps[voxel].Enqueue(result);
        if (this.recent_temps[voxel].Count > NUM_OF_TEMPS_TO_STORE)
        {
            this.recent_temps[voxel].Dequeue();
        }
    }

    public float3 GetAverageTemp(CVX_Voxel voxel)
    {
        double3 result = double3.zero;
        foreach (double3 temperature in this.recent_temps[voxel])
        {
            result += temperature;
        }
        result /= this.recent_temps[voxel].Count;
        return (float3)result;
    }


    public float3 GetAverageVoxelLinearVelocity(CVX_Voxel voxel)
    {
        double3 result = double3.zero;
        foreach(double3 velocity in this.recent_velocities[voxel])
        {
            result += velocity;
        }
        result /= this.recent_velocities[voxel].Count;
        return (float3)result;
    }
    

    public float3 GetAverageVoxelLinearVelocityNthDifference(CVX_Voxel voxel)
    {
        List<double3> nth_difference = new(this.recent_velocities[voxel]);

        while(nth_difference.Count > 1)
        {
            List<double3> new_nth_difference = new();
            for(int i=1; i< nth_difference.Count; i++)
            {
                new_nth_difference.Add(nth_difference[i] - nth_difference[i - 1]);
            }
            nth_difference = new_nth_difference;
        }

        return (float3)nth_difference[0];
    }


    internal float3 GetVoxelStrainNormalizedAndCache(CVoxelyze cvx_voxel)
    {
        double3 result = VoxelyzeEngine.GetVoxelStrain(cvx_voxel);
        max_strain = math.max(max_strain, math.abs(result.x));
        max_strain = math.max(max_strain, math.abs(result.y));
        max_strain = math.max(max_strain, math.abs(result.z));
        if (max_strain > 0) result /= max_strain;
        return (float3)result;
    }


    // activation in [-1,1]
    public void SinusoidalMovementFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        if (!this.voxel_sinusoid_states.ContainsKey(voxel)) this.voxel_sinusoid_states[voxel] = 0;
        this.voxel_sinusoid_states[voxel] += activation;
        float new_temp = TEMP_MAX_MAGNITUDE * math.sin(SINUSOID_SPEED * this.voxel_sinusoid_states[voxel]);
        SetVoxelTemperature(voxel, new_temp);
    }


    // activation in [-1,1]
    public void SetVoxelTemperatureFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        SetVoxelTemperature(voxel, activation * TEMP_MAX_MAGNITUDE);
    }

    // activation in [-1,1]
    public void SetVoxelTemperatureXFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        VoxelyzeEngine.SetVoxelTemperatureXdirection(voxel, activation * TEMP_MAX_MAGNITUDE);
    }

    // activation in [-1,1]
    public void SetVoxelTemperatureYFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        // Z in Unity is Y in voxelyze
        VoxelyzeEngine.SetVoxelTemperatureYdirection(voxel, activation * TEMP_MAX_MAGNITUDE);
    }


    // activation in [-1,1]
    public void SetVoxelTemperatureZFromNeuronActivation(CVX_Voxel voxel, float activation)
    {
        // Z in Unity is Y in voxelyze
        VoxelyzeEngine.SetVoxelTemperatureZdirection(voxel, activation * TEMP_MAX_MAGNITUDE);
    }

    // temp in 
    public void SetVoxelTemperature(CVX_Voxel voxel, float temperature)
    {
        VoxelyzeEngine.SetVoxelTemperature(voxel, temperature);
    }

    public static int3[] neighbor_offsets = new int3[]
    {
            new int3(0, 0, 1),
            new int3(0, 1, 0),
            new int3(1, 0, 0),
            new int3(0, 0, -1),
            new int3(0, -1, 0),
            new int3(-1, 0, 0),
    };


    public float3 GetCenterOfMass()
    {
        if (!contains_solid_voxels) return float3.zero;
        double3 result = VoxelyzeEngine.GetVoxelyzeCenterOfMass(this.cpp_voxel_object);
        return (float3)result * GetScale();
    }

    public float GetScale()
    {
        return (1/lattice_dimension) * scale;
    }

    // give input in [-1,1]
    public void SetAmbientTemperature(float temp)
    {
       VoxelyzeEngine.SetAmbientTemperature(this.cpp_voxel_object, temp);// + temp * TEMP_MAX);
    }

    Mesh.MeshDataArray mesh_data_array;
    Mesh.MeshData mesh_data;
    public void DoTimestep()
    {
        if (crashed)
        {
            Debug.LogError("Can't do timestep; Voxelyze crashed.");
            return;
        }
        if (physics_update_task != null) physics_update_task.Wait();
        if (mesh_update_task != null) {
            mesh_update_task.Wait();
            this.mesh.UpdateMesh(mesh_data_array, mesh_data);
        }
        this.mesh_data_array = Mesh.AllocateWritableMeshData(1);
        float fixed_time = Time.fixedDeltaTime;
        physics_update_task = Task.Run(() =>
        {
            float elapsed_simulation_time = 0;
            int iterations = 0;
            while (elapsed_simulation_time < fixed_time)
            {
                if (iterations >= GlobalConfig.MAX_VOXELYZE_ITERATIONS) break;
                this.recommended_timestep = VoxelyzeEngine.GetRecommendedTimestep(this.cpp_voxel_object);
                elapsed_simulation_time += this.recommended_timestep;
                if (float.IsNaN(time) || float.IsInfinity(time)) time = 0;

                try
                {
                    if (!VoxelyzeEngine.DoNextTimestep(this.cpp_voxel_object, this.recommended_timestep)) crashed = true;
                }
                catch
                {
                    crashed = true;
                }

                if (crashed) break;
                iterations++;
            }
            if (iterations > GlobalConfig.MAX_VOXELYZE_ITERATIONS) Debug.LogWarning("Capped out Voxelyze iterations.");
            time += elapsed_simulation_time;
            last_frame_elapsed_simulation_time = elapsed_simulation_time;
        });
        
        mesh_update_task = physics_update_task.ContinueWith((previousTask) =>
        {
            VoxelyzeEngine.UpdateMesh(this.mesh.voxel_mesh);
           
            this.mesh_data = mesh_data_array[0];
            mesh_data.SetIndexBufferParams(this.mesh.num_of_triangle_idxs, IndexFormat.UInt32);
            mesh_data.SetVertexBufferParams(this.mesh.num_of_vertices, new VertexAttributeDescriptor[] {
                new VertexAttributeDescriptor(VertexAttribute.Position),
                new VertexAttributeDescriptor(VertexAttribute.Normal,stream: 1)
            });

            // get vertices
            ParallelGetSoftVoxelMeshVertices job_vertices = new()
            {
                mesh_data = mesh_data,
                voxel_mesh = this.mesh.voxel_mesh
            };
            JobHandle job_vertices_handle = job_vertices.Schedule(this.mesh.num_of_vertices, 256);


            // get triangles
            ParallelGetSoftVoxelMeshTriangles job_triangles = new()
            {
                mesh_data = mesh_data,
                voxel_mesh = this.mesh.voxel_mesh
            };
            JobHandle job_triangles_handle = job_triangles.Schedule(this.mesh.num_of_quads, 256);

            // complete the jobs
            job_vertices_handle.Complete();
            job_triangles_handle.Complete();
        });



        // if (!crashed) 

    }



  /*  public void DoUnityJobTimestep()
    {
       // Debug.LogError("don't use this unless experimenting, it doesn't work well."); return;
        if (this.crashed)
        {
            Debug.LogError("Can't do timestep; Voxelyze crashed.");
            return;
        }
        if (update_task != null) update_task.Wait();
        this.mesh.UpdateMesh();

*       
        update_task = Task.Run(() =>
        {
       
            SetAllInvoluntaryMusclesToTemp(TEMP_MAX_MAGNITUDE * math.sin(SINUSOID_SPEED * this.time));
     
            float elapsed_simulation_time = 0;
            int iterations = 0;
            
            while (elapsed_simulation_time < this.fixed_delta_time)
            {
                this.recommended_timestep = VoxelyzeEngine.GetRecommendedTimestep(this.cpp_voxel_object);
                this.counter[0] = 0;
                this.current_stage[0] = 0;
                ThreadPool.SetMaxThreads(this.num_threads, this.num_threads);
                Parallel.For(0, this.num_threads, new ParallelOptions { MaxDegreeOfParallelism = this.num_threads }, i =>
                {
                    while (current_stage[0] <= 6) // stage 6 means we are done
                    {
                        int stage = current_stage[0];
                        if (stage == 7)
                        {
                            while (true)
                            {
                                int x = 1;
                            }
                        }
                        int success = 0;
                        if (stage == 1 || stage == 3 || stage == 6)
                        {
                            if (i == 0)
                            {
                                // run with 1 thread, so just call the function with thread 0
                                success = VoxelyzeEngine.DoTimeStepInUnityJob(cpp_voxel_object, recommended_timestep, stage, 0);
                            }
                        }
                        else if (stage == 0 || stage == 2 || stage == 4 || stage == 5)
                        {
                            bool thread_does_work = (stage == 0 && i < linksListSize)
                                || (stage == 2 && i < voxelsListSize)
                                || (stage == 4 && i < collisionsListSize)
                                || (stage == 5 && i < voxelsListSize);

                            if (thread_does_work) success = VoxelyzeEngine.DoTimeStepInUnityJob(cpp_voxel_object, recommended_timestep, stage, i);


                        }


                        if (success != 0)
                        {
                            this.diverged[0] = success; // any thread can set it to true
                            break;
                        }
                        // post-processing
                        if (i == 0)
                        {
                            while (this.counter[0] < (this.num_threads - 1))
                            {
                            }
                            this.counter[0] = 0; // all threads are finished, so reset the counter
                            current_stage[0]++; // signal all threads to go to next stage
                        }
                        else
                        {
                            this.counter[0]++; // count this thread as finished
                            while (stage == current_stage[0])
                            {
                                // otherwise, we are done, so wait for thread 0 to change the current stage
                                if (this.diverged[0] != 0) break; // break out if the simulation broke
                            }
                        }
                        if (this.diverged[0] != 0) break; // break out if the simulation broke
                    }
                });
*                ParallelDoTimestep job = new ParallelDoTimestep()
                {
                    cpp_voxel_object = this.cpp_voxel_object,
                    recommended_timestep = this.recommended_timestep,
                    num_threads = this.num_threads,
                    linksListSize = this.linksListSize,
                    voxelsListSize = this.voxelsListSize,
                    collisionsListSize = this.collisionsListSize,
                    current_stage = this.current_stage,
                    diverged = this.diverged,
                    counter = this.counter,
                    test = test
                };
               // handle = job.Schedule(this.num_threads, 64);

               // handle.Complete();
                // done with timestep
                elapsed_simulation_time += this.recommended_timestep;
                if (this.diverged[0] != 0) this.crashed = true;
                if (this.crashed) break;
                iterations++;
                test++;
            }
   


            if (iterations > GlobalConfig.MAX_VOXELYZE_ITERATIONS) Debug.LogWarning("Capped out Voxelyze iterations.");
            time += elapsed_simulation_time;
            last_frame_elapsed_simulation_time = elapsed_simulation_time;
       // });

    }*/

    public CVX_Voxel AddVoxel(CVX_Material pMaterial, int x, int y, int z)
    {
        this.contains_solid_voxels = true;
        CVX_Voxel voxel = VoxelyzeEngine.SetVoxelMaterial(this.cpp_voxel_object, pMaterial, x, y, z); //Voxel at index x=0, y=0. z=0
        return voxel;
    }

    public CVX_Voxel AddVoxel(RobotVoxel robot_voxel, int x, int y, int z)
    {
        return AddVoxel(this.robot_voxel_to_voxelyze_materials[robot_voxel], x, y, z);
    }

    public void OnDestroy()
    {
        this.counter.Dispose();
        this.current_stage.Dispose();
        this.diverged.Dispose();
    }

    public Quaternion GetVoxelRotation(CVX_Voxel cvx_voxel)
    {
        double[] result = new double[4];
        VoxelyzeEngine.GetVoxelRotation(cvx_voxel, result);
        return new Quaternion((float)result[1], (float)result[2], (float)result[3], (float)result[0]);
    }



    public class SoftVoxelMesh
    {
        public int num_of_vertex_coordinates;
        public int num_of_quad_vertex_idxs;
        public int num_of_vertices = 0;
        public int num_of_quads;
        public int num_of_triangle_idxs;
        public CVX_MeshRender voxel_mesh;
        public Mesh unity_mesh;
        Color[] colors;

        public SoftVoxelMesh(CVoxelyze voxel_object, Color? override_mesh_color)
        {
            this.voxel_mesh = VoxelyzeEngine.CreateMesh(voxel_object);

            this.num_of_vertex_coordinates = VoxelyzeEngine.GetMeshNumberOfVertices(voxel_mesh);

            this.num_of_quad_vertex_idxs = VoxelyzeEngine.GetMeshNumberOfQuads(voxel_mesh);

            this.num_of_vertices = this.num_of_vertex_coordinates / 3; // 3 coordinates per vertex
            this.num_of_quads = this.num_of_quad_vertex_idxs / 4; // 4 vertices per quad
            this.num_of_triangle_idxs = num_of_quads * 2 * 3; // 2 triangles per quad, 3 vertices per triangle

            this.unity_mesh = new Mesh();

            if (override_mesh_color == null)
            {
                this.colors = GetVertexColors();
            }
            else
            {
                colors = new Color[this.num_of_vertices];
                for (int i = 0; i < this.num_of_vertices; i++)
                {
                    colors[i] = (Color)override_mesh_color;
                }
            }
        }

        public void OverrideMeshColor(Color color)
        {
            colors = new Color[this.num_of_vertices];
            for (int i = 0; i < this.num_of_vertices; i++)
            {
                colors[i] = (Color)color;
            }
        }

        // call DLL to get vertex colors
        public Color[] GetVertexColors()
        {
            List<int> quad_vertex_idxs = new();
            for (int i = 0; i < this.num_of_quad_vertex_idxs; i++)
            {
                int quad_vertex_idx = VoxelyzeEngine.GetNextQuadValue(voxel_mesh, i);
                quad_vertex_idxs.Add(quad_vertex_idx);
            }

            Color[] quad_colors = new Color[this.num_of_quads];
            for (int i = 0; i < this.num_of_quads; i++)
            {

                float quad_color_r = VoxelyzeEngine.GetNextQuadColorValue(voxel_mesh, 3 * i);
                float quad_color_g = VoxelyzeEngine.GetNextQuadColorValue(voxel_mesh, 3 * i + 1);
                float quad_color_b = VoxelyzeEngine.GetNextQuadColorValue(voxel_mesh, 3 * i + 2);
                quad_colors[i] = new(quad_color_r, quad_color_g, quad_color_b);
            }


            // loop over quad colors, and determine the color sets of their vertices
            Dictionary<int, List<Color>> vertexIdx_to_color = new();

            int quad_idx = 0;
            for (int i = 0; i < quad_vertex_idxs.Count; i += 4)
            {
                Color quad_color = quad_colors[quad_idx];

                int v1 = quad_vertex_idxs[i + 0];
                int v2 = quad_vertex_idxs[i + 1];
                int v3 = quad_vertex_idxs[i + 2];
                int v4 = quad_vertex_idxs[i + 3];

                int[] vert_idxs = new int[] { v1, v2, v3, v4 };

                foreach (int vert_idx in vert_idxs)
                {
                    if (!vertexIdx_to_color.ContainsKey(vert_idx))
                    {
                        vertexIdx_to_color[vert_idx] = new();

                    }
                    vertexIdx_to_color[vert_idx].Add(quad_color);
                }
                quad_idx++;
            }

            Color[] vertex_colors = new Color[this.num_of_vertices];
            // loop over vertices and set colors by averaging color sets
            for (int i = 0; i < this.num_of_vertices; i++)
            {
                Color average_color = new(0, 0, 0, 0);
                foreach (Color color in vertexIdx_to_color[i])
                {
                    average_color += color;
                }
                average_color /= vertexIdx_to_color[i].Count;
                vertex_colors[i] = average_color;
                vertex_colors[i].a = 1;
       
            }

            return vertex_colors;
        }

        // call DLL to update mesh
        public void UpdateMesh(Mesh.MeshDataArray mesh_data_array, Mesh.MeshData mesh_data)
        {

            //finalize mesh

            mesh_data.subMeshCount = 1;
            mesh_data.SetSubMesh(0, new SubMeshDescriptor(0, mesh_data.GetIndexData<UInt32>().Length));

            Mesh.ApplyAndDisposeWritableMeshData(mesh_data_array, this.unity_mesh);
            this.unity_mesh.RecalculateBounds();
            this.unity_mesh.RecalculateNormals();
            this.unity_mesh.colors = this.colors;

        }
    }

    public (Vector3, Vector3) GetRaycastPositionAndDirectionFromVoxel(SoftVoxelRobot animat, CVX_Voxel cvx_voxel, int3 coords)
    {
        Vector3 voxel_relative_position = animat.GetVoxelCenterOfMass(cvx_voxel);
        Quaternion voxel_rotation = this.GetVoxelRotation(cvx_voxel);
        int num_of_raycasts = 0;
        Vector3 direction = Vector3.zero;
        for (int i = 0; i < SoftVoxelObject.neighbor_offsets.Length; i++)
        {
            int3 offset = SoftVoxelObject.neighbor_offsets[i];

            int3 neighbor_coords = coords + offset;

            // if the neighbor is not out of bounds, we have to check for a blocking voxel
            int neighbor_idx = GlobalUtils.Index_FlatFromint3(neighbor_coords, this.dimensions);
            RobotVoxel neighbor_voxel = this.GetVoxelType(neighbor_coords);
            if (neighbor_voxel != RobotVoxel.Empty) continue; // continue to next face if this face is blocked


            if (i == 0)
            {
                direction = (float3)offset;
            }
            else
            {
                direction = (float3)offset + (float3)direction;
            }
            num_of_raycasts++;
        }
        direction /= num_of_raycasts;
        direction = voxel_rotation * direction;
        direction = Vector3.Normalize(direction);

        Vector3 voxel_global_position = voxel_relative_position;

        voxel_rotation = this.GetVoxelRotation(cvx_voxel);
        var additional_rotation = Quaternion.Inverse(voxel_rotation);

        return (voxel_global_position, direction);
    }
}
