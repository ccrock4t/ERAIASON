using System.Runtime.CompilerServices;

namespace Unity.Mathematics
{
    /// <summary>A 5 component vector of ints.</summary>
    [System.Serializable]
    public struct int5
    {
        /// <summary>x component of the vector.</summary>
        public int x;
        /// <summary>y component of the vector.</summary>
        public int y;
        /// <summary>z component of the vector.</summary>
        public int z;
        /// <summary>w component of the vector.</summary>
        public int w;
        /// <summary>v component of the vector.</summary>
        public int v;

        /// <summary>int4 zero value.</summary>
        public static readonly int5 zero;

        /// <summary>Constructs a int4 vector from four int values.</summary>
        /// <param name="x">The constructed vector's x component will be set to this value.</param>
        /// <param name="y">The constructed vector's y component will be set to this value.</param>
        /// <param name="z">The constructed vector's z component will be set to this value.</param>
        /// <param name="w">The constructed vector's w component will be set to this value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int5(int x, int y, int z, int w, int v)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
            this.v = v;
        }
    }
}