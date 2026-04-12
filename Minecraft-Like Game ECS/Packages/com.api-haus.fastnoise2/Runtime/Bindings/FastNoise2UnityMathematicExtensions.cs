using Unity.Mathematics;

namespace FastNoise2.Bindings
{
    public static class FastNoise2UnityMathematicsExtensions
    {
        public static float GenSingle2D(this FastNoise fn, float2 c, int seed) =>
            fn.GenSingle2D(c.x, c.y, seed);

        public static float GenSingle3D(this FastNoise fn, float3 c, int seed) =>
            fn.GenSingle3D(c.x, c.y, c.z, seed);

        public static float GenSingle4D(this FastNoise fn, float4 c, int seed) =>
            fn.GenSingle4D(c.x, c.y, c.z, c.w, seed);
    }
}
