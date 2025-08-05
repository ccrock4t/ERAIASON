using Unity.Mathematics;

public class AnimatGenome
{
    public string uniqueName;
    public string momName;
    public string dadName;
    public int generation;
    public int reproduction_chain;

    public BrainGenome brain_genome;
    public BodyGenome body_genome;

    public AnimatGenome(BrainGenome brain_genome, BodyGenome body_genome, int generation)
    {
        this.brain_genome = brain_genome;
        this.body_genome = body_genome;
        this.generation = generation;
    }


    internal AnimatGenome Clone()
    {
        BrainGenome braingenome = this.brain_genome.Clone();
        BodyGenome bodygenome = this.body_genome.Clone();

        AnimatGenome offspring_genome = new(braingenome, bodygenome, generation);

        return offspring_genome;
    }

    internal (AnimatGenome offspring1_genome, AnimatGenome offspring2_genome) Reproduce(AnimatGenome parent2)
    {
        int generation = math.max(this.generation, parent2.generation);
        generation += 1;

        (BrainGenome braingenome1, BrainGenome braingenome2) = this.brain_genome.Reproduce(parent2.brain_genome);
        (BodyGenome bodygenome1, BodyGenome bodygenome2) = this.body_genome.Reproduce(parent2.body_genome);

        AnimatGenome offspring1_genome = new(braingenome1, bodygenome1, generation);
        AnimatGenome offspring2_genome = new(braingenome2, bodygenome2, generation);

        foreach(var offspring in new AnimatGenome[] { offspring1_genome, offspring2_genome })
        {
            offspring.body_genome.visionSensorKeyToNodeID = this.body_genome.visionSensorKeyToNodeID;
            if (offspring.body_genome is ArticulatedRobotBodyGenome)
            {
                ((ArticulatedRobotBodyGenome)offspring.body_genome).articulatedSensorKeyToNodeID = ((ArticulatedRobotBodyGenome)this.body_genome).articulatedSensorKeyToNodeID;
                ((ArticulatedRobotBodyGenome)offspring.body_genome).articulatedMotorKeyToNodeID = ((ArticulatedRobotBodyGenome)this.body_genome).articulatedMotorKeyToNodeID;
            }
            else if (offspring.body_genome is SoftVoxelRobotBodyGenome)
            {
                ((SoftVoxelRobotBodyGenome)offspring.body_genome).svrSensorKeyToNodeID = ((SoftVoxelRobotBodyGenome)this.body_genome).svrSensorKeyToNodeID;
                ((SoftVoxelRobotBodyGenome)offspring.body_genome).svrMotorKeyToNodeID = ((SoftVoxelRobotBodyGenome)this.body_genome).svrMotorKeyToNodeID;
            }
        }
        return (offspring1_genome, offspring2_genome);
    }
}
