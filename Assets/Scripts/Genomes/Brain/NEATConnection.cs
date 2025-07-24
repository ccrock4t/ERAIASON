using System;
using System.Threading;

public class NEATConnection
{
    public int ID;
    public Brain.NeuronID fromID;
    public Brain.NeuronID toID;
    public double weight;
    public bool enabled;

    public double[] hebb_ABCDLR;

    public static int NEXT_GLOBAL_CONNECTION_ID = -1; //start late, to give room for the initial shared sensorymotor connections

    public NEATConnection(double weight, 
        Brain.NeuronID fromID, 
        Brain.NeuronID toID, 
        int ID)
    {
        if (ID == int.MinValue) ID = NEXT_GLOBAL_CONNECTION_ID++;
        this.ID = ID;
        this.fromID = fromID;
        this.toID = toID;
        this.weight = weight;
        this.enabled = true;

        this.hebb_ABCDLR = new double[5];
        for(int i = 0; i < 5; i++)
        {
            this.hebb_ABCDLR[i] = GetRandomInitialWeight(); 
        }
    }

    public NEATConnection Clone()
    {
        NEATConnection new_connection = new(this.weight,
            this.fromID,
            this.toID,
            this.ID);
        new_connection.enabled = this.enabled;

        for (int i = 0; i < 5; i++)
        {
            new_connection.hebb_ABCDLR[i] = this.hebb_ABCDLR[i];
        }

        return new_connection;
    }

    public static float GetRandomInitialWeight()
    {
        return UnityEngine.Random.Range(-5f, 5f);
    }
    private static readonly ThreadLocal<System.Random> threadRand =
    new ThreadLocal<System.Random>(() => new System.Random());

    public static int ThreadSafeSystemRandomRange(int min, int max)
    {
        return threadRand.Value.Next(min, max); // max is exclusive
    }

    public static float ThreadSafeSystemRandomRange(float min, float max)
    {
        double value = threadRand.Value.NextDouble();
        return (float)(min + (value * (max - min)));
    }

    internal static float GetRandomFloat()
    {
        return ThreadSafeSystemRandomRange(0.0f, 1.0f);
    }
}
