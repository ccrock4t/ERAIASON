public class NEATConnection
{
    public int ID;
    public Brain.NeuronID fromID;
    public Brain.NeuronID toID;
    public float weight;
    public bool enabled;

    public float[] hebb_ABCDLR;

    public static int NEXT_GLOBAL_CONNECTION_ID = -1; //start late, to give room for the initial shared sensorymotor connections

    public NEATConnection(float weight, 
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

        this.hebb_ABCDLR = new float[5];
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
        return UnityEngine.Random.Range(-1f, 1f);
    }
}
