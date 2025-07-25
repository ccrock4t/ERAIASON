using System;
using System.Collections.Generic;
using static ArticulatedRobotBodyGenome;
using static SoftVoxelRobotBodyGenome;

public abstract class BodyGenome
{
    public abstract (BodyGenome bodygenome1, BodyGenome bodygenome2) Reproduce(BodyGenome body_genome);

    public abstract BodyGenome Clone();

    internal static BodyGenome CreateTestGenome()
    {
        if (GlobalConfig.custom_genome != null)
        {
            return GlobalConfig.custom_genome;
        }
        if (GlobalConfig.BODY_METHOD == GlobalConfig.BodyMethod.WheeledRobot)
        {
            return WheeledRobotBodyGenome.CreateWheeledRobotTestGenome();
        }else if(GlobalConfig.BODY_METHOD == GlobalConfig.BodyMethod.ArticulatedRobot)
        {
            return new ArticulatedRobotBodyGenome(ArticulatedRobotBodyGenome.Creature.Quadruped);
        }
        else if(GlobalConfig.BODY_METHOD == GlobalConfig.BodyMethod.SoftVoxelRobot)
        {
            return SoftVoxelRobotBodyGenome.CreateSoftVoxelTestGenome();
        }
        else
        {
            return null;
        }
    }

    public float GetSpawnHeightOffset()
    {
        if (this is SoftVoxelRobotBodyGenome)
        {
            return 0;
        }
        else if(this is ArticulatedRobotBodyGenome)
        {
            return 2.0f;
        }
        else
        {
            return 1.0f;
        }
    }

    public enum dof
    {
        X, Y, Z
    }

    public enum VisionSensorType{
        Obstacle,
        Food,
        Animat,
        PickableVoxel
    }
    public struct VisionSensorKey
    {
        public int ray;
        public VisionSensorType sensor;

        public VisionSensorKey(int ray, VisionSensorType type)
        {
            this.ray = ray;
            this.sensor = type;
        }

        public override bool Equals(object obj)
        {
            return obj is VisionSensorKey other &&
                   ray == other.ray &&
                   sensor == other.sensor;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ray, sensor);
        }
    }
    public Dictionary<VisionSensorKey, int> visionSensorKeyToNodeID = new();

}