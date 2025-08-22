/*
==== ==== ==== ==== ==== ====
==== NAL Inference Rules - Extended Boolean Operators ====
==== ==== ==== ==== ==== ====

    Author: Christian Hahm
    Created: May 13, 2022
    Purpose: Defines the NAL inference rules
            Assumes the given sentences do not have evidential overlap.
            Does combine evidential bases in the Resultant Sentence.
*/

using Unity.Mathematics;

public static class ExtendedBooleanOperators
{

    public static float band(float[] argv)
    {
        /*
            Boolean AND

            -----------------

            Input:
                argv: NAL Boolean Values

            Returns:
                argv1*argv2*...*argvn
        */
        //return band_average(argv);
        //float res = argv[0];
        //for (int i = 1; i < argv.Length; i++)
        //{
        //    float arg = argv[i];
        //    res = math.min(res,arg);
        //}
        var res = band_geometric_average(argv);
        return res;
    }

    public static float band_geometric_average(float[] argv)
    {
        /*
           //modified band average
            Boolean AND, with an exponent inversely proportional to the number of terms being ANDed.

            -----------------

            Input:
                argv: NAL Boolean Values

            Returns:
                (argv1*argv2*...*argvn)^(1/n)
        */

        float res = argv[0];
        for (int i = 1; i < argv.Length; i++)
        {
            float arg = argv[i];
            res *= arg;
        }
        float exp = 1 / argv.Length;
        return math.pow(res, exp);
    }
    public static float band_average(float[] argv)
    {
        /*
           //modified band average
            Boolean AND, with an exponent inversely proportional to the number of terms being ANDed.

            -----------------

            Input:
                argv: NAL Boolean Values

            Returns:
                (argv1*argv2*...*argvn)^(1/n)
        */

        float res = 0;
        foreach (float arg in argv)
        {
            res += arg;
        }
        
       // float exp = 1 / argv.Length;
        return res / argv.Length;// UnityEngine.Mathf.Pow(res, exp);
    }


    public static float bor(float[] argv)
    {
        /*
            Boolean OR

            -----------------

            Input:
                argv: NAL Boolean Values

            Returns:
                 1-((1-argv1)*(1-argv2)*...*(1-argvn))
        */
        float[] negations = new float[argv.Length];
   
        for (int i = 0; i < argv.Length; i++)
        {
            negations[i] = bnot(argv[i]);
        }
        return bnot(band(negations));
    }


    public static float bnot(float arg)
    {
        /*
            Boolean Not

            -----------------

            Input:
                arg: NAL Boolean Value

            Returns:
                1 minus arg
        */
        return 1 - arg;
    }
}