using System;
using Unity.Collections.LowLevel.Unsafe;

namespace RendererTools
{
    public unsafe struct PerInstanceDataWriter
    {
        [NativeDisableUnsafePtrRestriction]
        internal byte* BufferPtr;

        internal int PropertyOffset;
        internal int PropertySize;
        internal int InstancesPerWindow;
        internal int WindowSize;

        public void Write<T>(int globalInstanceIndex, T value) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (UnsafeUtility.SizeOf<T>() != PropertySize)
                throw new ArgumentException($"Data size mismatch for {typeof(T)}! Expected: {PropertySize}, Actual: {UnsafeUtility.SizeOf<T>()}");
#endif

            var windowIndex = globalInstanceIndex / InstancesPerWindow;
            var instanceIndex = globalInstanceIndex % InstancesPerWindow;

            var targetPtr = BufferPtr + windowIndex * WindowSize + PropertyOffset + instanceIndex * PropertySize;

            UnsafeUtility.CopyStructureToPtr(ref value, targetPtr);
        }
    }
}