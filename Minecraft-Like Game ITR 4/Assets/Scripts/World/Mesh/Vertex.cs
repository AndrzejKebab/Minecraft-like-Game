using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using UtilityLibrary.Unity.Runtime;

namespace PatataStudio.World.Mesh
{
    [BurstCompile, StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public half4 Position;
        public sbyte4 Normal;
        public Color32 Color;
        public half2 UV;
        public half2 UV3;
        public half4 UV4;
        
        public Vertex(half4 position, sbyte4 normal, Color32 color, half2 uv, half2 uv3, half4 uv4)
        {
            Position = position;
            Normal = normal;
            Color = color;
            UV = uv;
            UV3 = uv3;
            UV4 = uv4;
        }
    }
}