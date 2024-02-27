using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PatataStudio
{
    public struct ChunkComponent : IComponentData
    {
        public NativeArray<ushort> Voxels;
    }
}