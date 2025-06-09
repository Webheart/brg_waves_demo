using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace RendererTools
{
    public unsafe struct PerInstanceDataWriter
    {
        [NativeDisableContainerSafetyRestriction]
        internal NativeArray<byte> Buffer;

        internal int PropertyOffset;
        internal int PropertySize;
        internal int InstancesPerWindow;
        internal int WindowSize;

        public void Write<T>(int globalInstanceIndex, T value) where T : unmanaged
        {
            var windowIndex = globalInstanceIndex / InstancesPerWindow;
            var instanceIndex = globalInstanceIndex % InstancesPerWindow;
            var indexInBuffer = windowIndex * WindowSize + PropertyOffset + instanceIndex * PropertySize;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!Buffer.IsCreated)
                throw new InvalidOperationException("PerInstanceDataWriter: Buffer has not been created");
            if (UnsafeUtility.SizeOf<T>() != PropertySize)
                throw new ArgumentException($"PerInstanceDataWriter: Data size mismatch for {typeof(T)}! Expected: {PropertySize}, Actual: {UnsafeUtility.SizeOf<T>()}");
            if (indexInBuffer >= Buffer.Length)
                throw new IndexOutOfRangeException("PerInstanceDataWriter: Index out of range");
#endif

            var bufferPtr = (byte*)Buffer.GetUnsafePtr();
            var targetPtr = bufferPtr + indexInBuffer;
            UnsafeUtility.CopyStructureToPtr(ref value, targetPtr);
        }
    }
}