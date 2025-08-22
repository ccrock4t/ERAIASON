using System;
using Unity.Mathematics;
using UnityEngine;

public static class MutationHelpers
{

    static System.Random r = new();
    public static double GetPerturbationFromRange(double min, double max, double fraction = 0.1f)
    {
        double range = max - min;
        double stdDev = range * fraction;

        // Generate standard normal sample using Box-Muller
        double u, v, S;
        do
        {
            u = 2.0 * r.NextDouble() - 1.0;
            v = 2.0 * r.NextDouble() - 1.0;
            S = u * u + v * v;
        } while (S >= 1.0 || S == 0);

        double fac = Math.Sqrt(-2.0 * Math.Log(S) / S);
        double result = u * fac;

        result *= stdDev; // scale

        return math.clamp(result, min, max);
    }

    public static double GetPerturbationFromRange(Vector2 range, double fraction = 0.1f)
    {
        return GetPerturbationFromRange(range.x, range.y, fraction);
    }


    public static double GetPerturbationFromRange(Vector2Int range, double fraction = 0.1f)
    {
        return GetPerturbationFromRange(range.x, range.y, fraction);
    }
}