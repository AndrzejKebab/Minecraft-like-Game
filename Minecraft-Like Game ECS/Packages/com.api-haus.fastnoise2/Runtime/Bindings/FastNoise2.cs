using Unity.Burst;

namespace FastNoise2.Bindings
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using FastNoise2.Generators;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using static Unity.Mathematics.math;

    public struct FastNoise : INativeDisposable, IEquatable<FastNoise>
    {
        #region IEquatable

        public readonly override int GetHashCode() => HashCode.Combine(mNodeHandle, m_MetadataId);

        public readonly bool Equals(FastNoise other) =>
            mNodeHandle == other.mNodeHandle && m_MetadataId == other.m_MetadataId;

        public override readonly bool Equals(object obj) =>
            obj != null && obj is FastNoise other && Equals(other);

        public static bool operator ==(FastNoise left, FastNoise right) => left.Equals(right);

        public static bool operator !=(FastNoise left, FastNoise right) => !left.Equals(right);

        #endregion

        public static FastNoise Invalid = new() { mNodeHandle = IntPtr.Zero, m_MetadataId = -1 };

        [StructLayout(LayoutKind.Sequential)]
        public struct OutputMinMax
        {
            public OutputMinMax(
                float minValue = float.PositiveInfinity,
                float maxValue = float.NegativeInfinity
            )
            {
                min = minValue;
                max = maxValue;
            }

            public OutputMinMax(float[] nativeOutputMinMax)
            {
                min = nativeOutputMinMax[0];
                max = nativeOutputMinMax[1];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Merge(OutputMinMax other)
            {
                min = min(min, other.min);
                max = max(max, other.max);
            }

            public float min;
            public float max;
        }

        public FastNoise(string metadataName)
        {
#if FN2_USER_SIGNED
            var def = FN2NodeRegistry.GetNodeDef(metadataName);
            m_MetadataId = def.Id;
            m_IsDisposed = false;
            mNodeHandle = fnNewFromMetadata(m_MetadataId);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        private FastNoise(IntPtr nodeHandle)
        {
#if FN2_USER_SIGNED
            mNodeHandle = nodeHandle;
            m_IsDisposed = false;
            m_MetadataId = fnGetMetadataID(nodeHandle);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        private struct DisposeNoiseJob : IJob
        {
            private FastNoise m_Noise;

            public DisposeNoiseJob(FastNoise fastNoise)
            {
                m_Noise = fastNoise;
            }

            public void Execute() => m_Noise.Dispose();
        }

        public void Dispose()
        {
#if FN2_USER_SIGNED
            if (!IsCreated || m_IsDisposed)
            {
                return;
            }
            fnDeleteNodeRef(mNodeHandle);
            m_IsDisposed = true;
#endif
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
#if FN2_USER_SIGNED
            return IsCreated //
                ? new DisposeNoiseJob(this).Schedule(inputDeps)
                : inputDeps;
#else
            return inputDeps;
#endif
        }

        public static FastNoise FromEncodedNodeTree(string encodedNodeTree)
        {
#if FN2_USER_SIGNED
            IntPtr nodeHandle = fnNewFromEncodedNodeTree(encodedNodeTree);

            if (nodeHandle == IntPtr.Zero)
            {
                return Invalid;
            }

            return new FastNoise(nodeHandle);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly uint GetSIMDLevel()
        {
#if FN2_USER_SIGNED
            return fnGetActiveFeatureSet(mNodeHandle);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly void Set(string memberName, float value)
        {
#if FN2_USER_SIGNED
            var def = FN2NodeRegistry.GetNodeDefById(m_MetadataId);
            if (!def.TryGetMember(FormatLookup(memberName), out var member))
                throw new ArgumentException("Failed to find member name: " + memberName);

            switch (member.Type)
            {
                case FN2MemberType.Float:
                    if (!fnSetVariableFloat(mNodeHandle, member.Index, value))
                        throw new ExternalException("Failed to set float value");
                    break;

                case FN2MemberType.Hybrid:
                    if (!fnSetHybridFloat(mNodeHandle, member.Index, value))
                        throw new ExternalException("Failed to set float value");
                    break;

                default:
                    throw new ArgumentException(memberName + " cannot be set to a float value");
            }
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly void Set(string memberName, int value)
        {
#if FN2_USER_SIGNED
            var def = FN2NodeRegistry.GetNodeDefById(m_MetadataId);
            if (!def.TryGetMember(FormatLookup(memberName), out var member))
                throw new ArgumentException("Failed to find member name: " + memberName);

            if (member.Type != FN2MemberType.Int)
                throw new ArgumentException(memberName + " cannot be set to an int value");

            if (!fnSetVariableIntEnum(mNodeHandle, member.Index, value))
                throw new ExternalException("Failed to set int value");
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly void Set(string memberName, string enumValue)
        {
#if FN2_USER_SIGNED
            var def = FN2NodeRegistry.GetNodeDefById(m_MetadataId);
            if (!def.TryGetMember(FormatLookup(memberName), out var member))
                throw new ArgumentException("Failed to find member name: " + memberName);

            if (member.Type != FN2MemberType.Enum)
                throw new ArgumentException(memberName + " cannot be set to an enum value");

            if (!member.TryGetEnumIndex(enumValue, out int enumIdx))
                throw new ArgumentException("Failed to find enum value: " + enumValue);

            if (!fnSetVariableIntEnum(mNodeHandle, member.Index, enumIdx))
                throw new ExternalException("Failed to set enum value");
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly void Set(string memberName, FastNoise nodeLookup)
        {
#if FN2_USER_SIGNED
            var def = FN2NodeRegistry.GetNodeDefById(m_MetadataId);
            if (!def.TryGetMember(FormatLookup(memberName), out var member))
                throw new ArgumentException("Failed to find member name: " + memberName);

            switch (member.Type)
            {
                case FN2MemberType.NodeLookup:
                {
                    IntPtr lookupHandle = nodeLookup.mNodeHandle;
                    if (!fnSetNodeLookup(mNodeHandle, member.Index, lookupHandle))
                        throw new ExternalException("Failed to set node lookup");
                    break;
                }

                case FN2MemberType.Hybrid:
                {
                    IntPtr lookupHandle = nodeLookup.mNodeHandle;
                    if (!fnSetHybridNodeLookup(mNodeHandle, member.Index, lookupHandle))
                        throw new ExternalException("Failed to set node lookup");
                    break;
                }

                default:
                    throw new ArgumentException(memberName + " cannot be set to a node lookup");
            }
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly OutputMinMax GenUniformGrid2D(
            float[] noiseOut,
            float xOffset,
            float yOffset,
            int xCount,
            int yCount,
            float xStepSize,
            float yStepSize,
            int seed
        )
        {
#if FN2_USER_SIGNED
            float[] minMax = new float[2];
            fnGenUniformGrid2D(
                mNodeHandle,
                noiseOut,
                xOffset,
                yOffset,
                xCount,
                yCount,
                xStepSize,
                yStepSize,
                seed,
                minMax
            );
            return new OutputMinMax(minMax);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly OutputMinMax GenUniformGrid3D(
            float[] noiseOut,
            float xOffset,
            float yOffset,
            float zOffset,
            int xCount,
            int yCount,
            int zCount,
            float xStepSize,
            float yStepSize,
            float zStepSize,
            int seed
        )
        {
#if FN2_USER_SIGNED
            float[] minMax = new float[2];
            fnGenUniformGrid3D(
                mNodeHandle,
                noiseOut,
                xOffset,
                yOffset,
                zOffset,
                xCount,
                yCount,
                zCount,
                xStepSize,
                yStepSize,
                zStepSize,
                seed,
                minMax
            );
            return new OutputMinMax(minMax);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly OutputMinMax GenUniformGrid4D(
            float[] noiseOut,
            float xOffset,
            float yOffset,
            float zOffset,
            float wOffset,
            int xCount,
            int yCount,
            int zCount,
            int wCount,
            float xStepSize,
            float yStepSize,
            float zStepSize,
            float wStepSize,
            int seed
        )
        {
#if FN2_USER_SIGNED
            float[] minMax = new float[2];
            fnGenUniformGrid4D(
                mNodeHandle,
                noiseOut,
                xOffset,
                yOffset,
                zOffset,
                wOffset,
                xCount,
                yCount,
                zCount,
                wCount,
                xStepSize,
                yStepSize,
                zStepSize,
                wStepSize,
                seed,
                minMax
            );
            return new OutputMinMax(minMax);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly OutputMinMax GenTileable2D(
            float[] noiseOut,
            int xSize,
            int ySize,
            float xStepSize,
            float yStepSize,
            int seed
        )
        {
#if FN2_USER_SIGNED
            float[] minMax = new float[2];
            fnGenTileable2D(
                mNodeHandle,
                noiseOut,
                xSize,
                ySize,
                xStepSize,
                yStepSize,
                seed,
                minMax
            );
            return new OutputMinMax(minMax);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly OutputMinMax GenPositionArray2D(
            float[] noiseOut,
            float[] xPosArray,
            float[] yPosArray,
            float xOffset,
            float yOffset,
            int seed
        )
        {
#if FN2_USER_SIGNED
            float[] minMax = new float[2];
            fnGenPositionArray2D(
                mNodeHandle,
                noiseOut,
                xPosArray.Length,
                xPosArray,
                yPosArray,
                xOffset,
                yOffset,
                seed,
                minMax
            );
            return new OutputMinMax(minMax);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly OutputMinMax GenPositionArray3D(
            float[] noiseOut,
            float[] xPosArray,
            float[] yPosArray,
            float[] zPosArray,
            float xOffset,
            float yOffset,
            float zOffset,
            int seed
        )
        {
#if FN2_USER_SIGNED
            float[] minMax = new float[2];
            fnGenPositionArray3D(
                mNodeHandle,
                noiseOut,
                xPosArray.Length,
                xPosArray,
                yPosArray,
                zPosArray,
                xOffset,
                yOffset,
                zOffset,
                seed,
                minMax
            );
            return new OutputMinMax(minMax);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly OutputMinMax GenPositionArray4D(
            float[] noiseOut,
            float[] xPosArray,
            float[] yPosArray,
            float[] zPosArray,
            float[] wPosArray,
            float xOffset,
            float yOffset,
            float zOffset,
            float wOffset,
            int seed
        )
        {
#if FN2_USER_SIGNED
            float[] minMax = new float[2];
            fnGenPositionArray4D(
                mNodeHandle,
                noiseOut,
                xPosArray.Length,
                xPosArray,
                yPosArray,
                zPosArray,
                wPosArray,
                xOffset,
                yOffset,
                zOffset,
                wOffset,
                seed,
                minMax
            );
            return new OutputMinMax(minMax);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly float GenSingle2D(float x, float y, int seed)
        {
#if FN2_USER_SIGNED
            return fnGenSingle2D(mNodeHandle, x, y, seed);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly float GenSingle3D(float x, float y, float z, int seed)
        {
#if FN2_USER_SIGNED
            return fnGenSingle3D(mNodeHandle, x, y, z, seed);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        public readonly float GenSingle4D(float x, float y, float z, float w, int seed)
        {
#if FN2_USER_SIGNED
            return fnGenSingle4D(mNodeHandle, x, y, z, w, seed);
#else
            throw new InvalidOperationException("FastNoise2 native libraries are not signed. Use Window > FastNoise2 to sign them.");
#endif
        }

        [NativeDisableUnsafePtrRestriction]
        internal IntPtr mNodeHandle;

        private int m_MetadataId;

        // Ignores spaces and caps, harder to mistype strings
        private static string FormatLookup(string s) => s.Replace(" ", "").ToLower();

#if FN2_USER_SIGNED
#if UNITY_IOS && !UNITY_EDITOR
        private const string NATIVE_LIB = "__Internal";
#else
        private const string NATIVE_LIB = "FastNoise";
#endif

        [DllImport(NATIVE_LIB)]
        internal static extern IntPtr fnNewFromMetadata(int id, uint simdLevel = ~0u);

        [DllImport(NATIVE_LIB)]
        internal static extern IntPtr fnNewFromEncodedNodeTree(
            [MarshalAs(UnmanagedType.LPStr)] string encodedNodeTree,
            uint simdLevel = ~0u
        );

        [DllImport(NATIVE_LIB)]
        internal static extern void fnDeleteNodeRef(IntPtr nodeHandle);

        [DllImport(NATIVE_LIB)]
        internal static extern uint fnGetActiveFeatureSet(IntPtr nodeHandle);

        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataID(IntPtr nodeHandle);

        [DllImport(NATIVE_LIB)]
        internal static extern void fnGenUniformGrid2D(
            IntPtr nodeHandle,
            float[] noiseOut,
            float xOffset,
            float yOffset,
            int xCount,
            int yCount,
            float xStepSize,
            float yStepSize,
            int seed,
            float[] outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern void fnGenUniformGrid3D(
            IntPtr nodeHandle,
            float[] noiseOut,
            float xOffset,
            float yOffset,
            float zOffset,
            int xCount,
            int yCount,
            int zCount,
            float xStepSize,
            float yStepSize,
            float zStepSize,
            int seed,
            float[] outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern void fnGenUniformGrid4D(
            IntPtr nodeHandle,
            float[] noiseOut,
            float xOffset,
            float yOffset,
            float zOffset,
            float wOffset,
            int xCount,
            int yCount,
            int zCount,
            int wCount,
            float xStepSize,
            float yStepSize,
            float zStepSize,
            float wStepSize,
            int seed,
            float[] outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern void fnGenTileable2D(
            IntPtr node,
            float[] noiseOut,
            int xSize,
            int ySize,
            float xStepSize,
            float yStepSize,
            int seed,
            float[] outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern void fnGenPositionArray2D(
            IntPtr node,
            float[] noiseOut,
            int count,
            float[] xPosArray,
            float[] yPosArray,
            float xOffset,
            float yOffset,
            int seed,
            float[] outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern void fnGenPositionArray3D(
            IntPtr node,
            float[] noiseOut,
            int count,
            float[] xPosArray,
            float[] yPosArray,
            float[] zPosArray,
            float xOffset,
            float yOffset,
            float zOffset,
            int seed,
            float[] outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern void fnGenPositionArray4D(
            IntPtr node,
            float[] noiseOut,
            int count,
            float[] xPosArray,
            float[] yPosArray,
            float[] zPosArray,
            float[] wPosArray,
            float xOffset,
            float yOffset,
            float zOffset,
            float wOffset,
            int seed,
            float[] outputMinMax
        );

        #region Unsafe versions

        [DllImport(NATIVE_LIB)]
        internal static extern unsafe void fnGenUniformGrid2D(
            IntPtr nodeHandle,
            void* noiseOut,
            float xOffset,
            float yOffset,
            int xCount,
            int yCount,
            float xStepSize,
            float yStepSize,
            int seed,
            void* outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern unsafe void fnGenUniformGrid3D(
            IntPtr nodeHandle,
            void* noiseOut,
            float xOffset,
            float yOffset,
            float zOffset,
            int xCount,
            int yCount,
            int zCount,
            float xStepSize,
            float yStepSize,
            float zStepSize,
            int seed,
            void* outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern unsafe void fnGenUniformGrid4D(
            IntPtr nodeHandle,
            void* noiseOut,
            float xOffset,
            float yOffset,
            float zOffset,
            float wOffset,
            int xCount,
            int yCount,
            int zCount,
            int wCount,
            float xStepSize,
            float yStepSize,
            float zStepSize,
            float wStepSize,
            int seed,
            void* outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern unsafe void fnGenTileable2D(
            IntPtr node,
            void* noiseOut,
            int xSize,
            int ySize,
            float xStepSize,
            float yStepSize,
            int seed,
            void* outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern unsafe void fnGenPositionArray2D(
            IntPtr node,
            void* noiseOut,
            int count,
            void* xPosArray,
            void* yPosArray,
            float xOffset,
            float yOffset,
            int seed,
            void* outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern unsafe void fnGenPositionArray3D(
            IntPtr node,
            void* noiseOut,
            int count,
            void* xPosArray,
            void* yPosArray,
            void* zPosArray,
            float xOffset,
            float yOffset,
            float zOffset,
            int seed,
            void* outputMinMax
        );

        [DllImport(NATIVE_LIB)]
        internal static extern unsafe void fnGenPositionArray4D(
            IntPtr node,
            void* noiseOut,
            int count,
            void* xPosArray,
            void* yPosArray,
            void* zPosArray,
            void* wPosArray,
            float xOffset,
            float yOffset,
            float zOffset,
            float wOffset,
            int seed,
            void* outputMinMax
        );

        #endregion

        [DllImport(NATIVE_LIB)]
        internal static extern float fnGenSingle2D(IntPtr node, float x, float y, int seed);

        [DllImport(NATIVE_LIB)]
        internal static extern float fnGenSingle3D(
            IntPtr node,
            float x,
            float y,
            float z,
            int seed
        );

        [DllImport(NATIVE_LIB)]
        internal static extern float fnGenSingle4D(
            IntPtr node,
            float x,
            float y,
            float z,
            float w,
            int seed
        );

        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataCount();

        [DllImport(NATIVE_LIB)]
        internal static extern IntPtr fnGetMetadataName(int id);

        // Variable
        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataVariableCount(int id);

        [DllImport(NATIVE_LIB)]
        internal static extern IntPtr fnGetMetadataVariableName(int id, int variableIndex);

        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataVariableType(int id, int variableIndex);

        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataVariableDimensionIdx(int id, int variableIndex);

        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataEnumCount(int id, int variableIndex);

        [DllImport(NATIVE_LIB)]
        internal static extern IntPtr fnGetMetadataEnumName(
            int id,
            int variableIndex,
            int enumIndex
        );

        [DllImport(NATIVE_LIB)]
        internal static extern bool fnSetVariableFloat(
            IntPtr nodeHandle,
            int variableIndex,
            float value
        );

        [DllImport(NATIVE_LIB)]
        internal static extern bool fnSetVariableIntEnum(
            IntPtr nodeHandle,
            int variableIndex,
            int value
        );

        // Node Lookup
        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataNodeLookupCount(int id);

        [DllImport(NATIVE_LIB)]
        internal static extern IntPtr fnGetMetadataNodeLookupName(int id, int nodeLookupIndex);

        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataNodeLookupDimensionIdx(int id, int nodeLookupIndex);

        [DllImport(NATIVE_LIB)]
        internal static extern bool fnSetNodeLookup(
            IntPtr nodeHandle,
            int nodeLookupIndex,
            IntPtr nodeLookupHandle
        );

        // Hybrid
        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataHybridCount(int id);

        [DllImport(NATIVE_LIB)]
        internal static extern IntPtr fnGetMetadataHybridName(int id, int nodeLookupIndex);

        [DllImport(NATIVE_LIB)]
        internal static extern int fnGetMetadataHybridDimensionIdx(int id, int nodeLookupIndex);

        [DllImport(NATIVE_LIB)]
        internal static extern bool fnSetHybridNodeLookup(
            IntPtr nodeHandle,
            int nodeLookupIndex,
            IntPtr nodeLookupHandle
        );

        [DllImport(NATIVE_LIB)]
        internal static extern bool fnSetHybridFloat(
            IntPtr nodeHandle,
            int nodeLookupIndex,
            float value
        );

        // Rich metadata queries
        [DllImport(NATIVE_LIB)] internal static extern IntPtr fnGetMetadataDescription(int id);
        [DllImport(NATIVE_LIB)] internal static extern int    fnGetMetadataGroupCount(int id);
        [DllImport(NATIVE_LIB)] internal static extern IntPtr fnGetMetadataGroupName(int id, int groupIndex);
        [DllImport(NATIVE_LIB)] internal static extern IntPtr fnGetMetadataVariableDescription(int id, int variableIndex);
        [DllImport(NATIVE_LIB)] internal static extern float  fnGetMetadataVariableDefaultFloat(int id, int variableIndex);
        [DllImport(NATIVE_LIB)] internal static extern int    fnGetMetadataVariableDefaultIntEnum(int id, int variableIndex);
        [DllImport(NATIVE_LIB)] internal static extern float  fnGetMetadataVariableMinFloat(int id, int variableIndex);
        [DllImport(NATIVE_LIB)] internal static extern float  fnGetMetadataVariableMaxFloat(int id, int variableIndex);
        [DllImport(NATIVE_LIB)] internal static extern IntPtr fnGetMetadataNodeLookupDescription(int id, int nodeLookupIndex);
        [DllImport(NATIVE_LIB)] internal static extern IntPtr fnGetMetadataHybridDescription(int id, int hybridIndex);
        [DllImport(NATIVE_LIB)] internal static extern float  fnGetMetadataHybridDefault(int id, int hybridIndex);
#endif // FN2_USER_SIGNED
        [MarshalAs(UnmanagedType.U1)]
        private bool m_IsDisposed;
        public readonly bool IsCreated => !m_IsDisposed && mNodeHandle != IntPtr.Zero;
    }
}
