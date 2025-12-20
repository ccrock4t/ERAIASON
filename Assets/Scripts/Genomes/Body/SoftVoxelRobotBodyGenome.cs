using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using static ArticulatedRobotBodyGenome;
using static SoftVoxelRobot;


public class SoftVoxelRobotBodyGenome : BodyGenome
{
    public RobotVoxel[] voxel_array;

    public int3 dimensions3D;

    public struct SoftVoxelSensorKey
    {
        public int3 voxel;
        public SoftVoxelSensorType sensor;

        public SoftVoxelSensorKey(int3 voxel, SoftVoxelSensorType type)
        {
            this.voxel = voxel;
            this.sensor = type;
        }

        public override bool Equals(object obj)
        {
            return obj is SoftVoxelSensorKey other &&
                   math.all(voxel == other.voxel) &&
                   sensor == other.sensor;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(voxel, sensor);
        }
    }
    public enum SoftVoxelSensorType
    {
        Touch,
        ForwardTilt,
        SideTilt
    }

    public struct SoftVoxelMotorKey
    {
        public int3 voxel;
        public dof dof;
        public SoftVoxelMotorKey(int3 voxel, dof dof)
        {
            this.voxel = voxel;
            this.dof = dof;
        }

        public override bool Equals(object obj)
        {
            return obj is SoftVoxelMotorKey other &&
                   math.all(voxel == other.voxel) &&
                   dof == other.dof;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(voxel, dof);
        }
    }
    
    // for NEAT
    public Dictionary<SoftVoxelSensorKey, int> svrSensorKeyToNodeID = new();
    public Dictionary<SoftVoxelMotorKey, int> svrMotorKeyToNodeID = new();

    public SoftVoxelRobotBodyGenome(int3 dimensions3D) {

        this.dimensions3D = dimensions3D;
        this.voxel_array = new RobotVoxel[dimensions3D.x * dimensions3D.y * dimensions3D.z];
    }




    private bool IsOnEdge(int x, int z, int3 dimensions3D)
    {
        return (x == 0 || z == 0) ||  // Top-left corner
           (x == dimensions3D.x - 1 || z == dimensions3D.z - 1);    // Bottom-right corner
    }

    bool IsInCorner(int x, int z, int3 dimensions3D)
    {
        // Check if the point matches any of the four corners
        return (x == 0 && z == 0) ||  // Top-left corner
               (x == dimensions3D.x-1 && z == 0) ||  // Top-right corner
               (x == 0 && z == dimensions3D.z - 2) ||  // Bottom-left corner
               (x == dimensions3D.x-1 && z == dimensions3D.z - 2);    // Bottom-right corner
    }

    public static SoftVoxelRobotBodyGenome CreateSoftVoxelTestGenome()
    {
        int3 dims = new(CPPNGenome.SOFT_VOXEL_SUBSTRATE_DIMENSIONS.x, CPPNGenome.SOFT_VOXEL_SUBSTRATE_DIMENSIONS.y, CPPNGenome.SOFT_VOXEL_SUBSTRATE_DIMENSIONS.z);
        SoftVoxelRobotBodyGenome genome = new(dims);

        // set body
        for (int x = 0; x < genome.dimensions3D.x; x++)
        {
            for (int y = 0; y < genome.dimensions3D.y; y++)
            {
                for (int z = 0; z < genome.dimensions3D.z; z++)
                {
                    int i = GlobalUtils.Index_FlatFromint3(x, y, z, genome.dimensions3D);
                    RobotVoxel voxel = RobotVoxel.Touch_Sensor;
                    if (z == genome.dimensions3D.z - 1)
                    {
                        if (x == dims.x/2 && y == dims.y-1)
                        {
                            voxel = RobotVoxel.Raycast_Vision_Sensor;
                        }
                        else
                        {
                            voxel = RobotVoxel.Empty;
                        }

                    }

                    // quadruped legs
                    if (z < genome.dimensions3D.z - 2 && z > 0 && y < dims.y - 1)
                    {
                        //if ((z == 0 || z == genome.dimensions3D.z - 2) && x == 2)
                        //{
                        //    // third leg
                        //}
                        //else
                        //{
                        voxel = RobotVoxel.Empty;
                        //}

                    }

                    if (x == dims.x/2 && y <= dims.y/2)
                    {
                        voxel = RobotVoxel.Empty;
                    }

                    //if(z == 0 && y == 0)
                    //{
                    //    //if ((z == 0 || z == genome.dimensions3D.z - 2) && x == 2)
                    //    //{
                    //    //    // third leg
                    //    //}
                    //    //else
                    //    //{
                    //    voxel = RobotVoxel.Empty;
                    //    //}

                    //}

                    //if (y == 0)
                    //{
                    //    voxel = RobotVoxel.Empty;
                    //}

                    //// no back legsd
                    //if (z == 0 && y<=1)
                    //{
                    //    voxel = RobotVoxel.Empty
                    //}
                    //if (y == 2 && x != 1 && z == dimensions3D.z - 2)
                    //{
                    //    voxel = RobotVoxel.Empty;
                    //}
                    genome.voxel_array[i] = voxel;
                }
            }
        }
        return genome;
    }

    
    public override BodyGenome Clone()
    {
        SoftVoxelRobotBodyGenome clone = this;
        return clone;
    }

    public override (BodyGenome bodygenome1, BodyGenome bodygenome2) Reproduce(BodyGenome body_genome)
    {
        SoftVoxelRobotBodyGenome clone = this;
        SoftVoxelRobotBodyGenome clone2 = this;
        return (clone, clone2);
    }

    internal static SoftVoxelRobotBodyGenome LoadFromFile(string filepath)
    {
        var bytes = File.ReadAllBytes(filepath);
    
        var (loadedArray, x, y, z) = BytesToIntArrayWithDimensions(bytes);
        var genome = new SoftVoxelRobotBodyGenome(new int3(x,y,z));
        genome.voxel_array = loadedArray;
        return genome;
    }


    public static int[] EnumArrayToIntArray<T>(T[] enumArray) where T : Enum
    {
        int[] intArray = new int[enumArray.Length];
        for (int i = 0; i < enumArray.Length; i++)
        {
            intArray[i] = Convert.ToInt32(enumArray[i]);
        }
        return intArray;
    }

    public static byte[] IntArrayWithDimensionsToBytes(RobotVoxel[] robot_data, int dimX, int dimY, int dimZ)
    {
        int[] data = EnumArrayToIntArray<RobotVoxel>(robot_data);
        int totalInts = data.Length + 3; // 3 for dimensions
        byte[] bytes = new byte[totalInts * sizeof(int)];

        // Write dimensions first
        Buffer.BlockCopy(new int[] { dimX, dimY, dimZ }, 0, bytes, 0, 3 * sizeof(int));

        // Write actual data next
        Buffer.BlockCopy(data, 0, bytes, 3 * sizeof(int), data.Length * sizeof(int));

        return bytes;
    }

    public static (RobotVoxel[] data, int dimX, int dimY, int dimZ) BytesToIntArrayWithDimensions(byte[] bytes)
    {
        // Read dimensions
        int[] dims = new int[3];
        Buffer.BlockCopy(bytes, 0, dims, 0, 3 * sizeof(int));
        int dimX = dims[0], dimY = dims[1], dimZ = dims[2];

        // Read data
        int dataLength = (bytes.Length / sizeof(int)) - 3;
        int[] data = new int[dataLength];
        Buffer.BlockCopy(bytes, 3 * sizeof(int), data, 0, dataLength * sizeof(int));

        RobotVoxel[] robot_data = IntArrayToEnumArray<RobotVoxel>(data);

        return (robot_data, dimX, dimY, dimZ);
    }

    public static T[] IntArrayToEnumArray<T>(int[] intArray) where T : Enum
    {
        T[] enumArray = new T[intArray.Length];
        for (int i = 0; i < intArray.Length; i++)
        {
            enumArray[i] = (T)Enum.ToObject(typeof(T), intArray[i]);
        }
        return enumArray;
    }
}
