using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace RendererTools
{
    public unsafe class RenderInstance
    {
        public bool IsAlive { get; internal set; }

        public JobHandle Dependency
        {
            get => DependencyInternal;
            set
            {
                IsDirty = true;
                DependencyInternal = value;
            }
        }

        internal JobHandle DependencyInternal;

        internal NativeArray<byte> BufferData;
        internal NativeHashMap<int, PropertyLayout> PropertyLayoutMap;
        internal GraphicsBuffer GraphicsBuffer;
        internal RenderParams RenderParams;
        internal int VisibleCount;
        internal bool IsDirty;

        internal RenderInstance(RenderParams renderParams, int propertiesCount)
        {
            RenderParams = renderParams;
            GraphicsBuffer = BatchRendererGroupUtility.CreateGraphicsBuffer(renderParams.TotalBufferSize);
            BufferData = new NativeArray<byte>(renderParams.TotalBufferSize, Allocator.Persistent);
            PropertyLayoutMap = new NativeHashMap<int, PropertyLayout>(propertiesCount, Allocator.Persistent);
        }

        public PerInstanceDataWriter GetPerInstanceWriter(int propertyID)
        {
            var exists = PropertyLayoutMap.TryGetValue(propertyID, out var layout);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!exists) throw new ArgumentException($"Property {propertyID} not found");
            if (!layout.PerInstance) throw new ArgumentException($"Property {propertyID} is not per instance");
#endif
            return new PerInstanceDataWriter
            {
                Buffer = BufferData,
                PropertyOffset = layout.Offset,
                PropertySize = layout.Size,
                InstancesPerWindow = RenderParams.InstancesPerWindow,
                WindowSize = RenderParams.WindowSize
            };
        }

        public void WriteSharedProperty<T>(int propertyID, T value) where T : unmanaged
        {
            var exists = PropertyLayoutMap.TryGetValue(propertyID, out var layout);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!exists) throw new ArgumentException($"Property {propertyID} not found");
            if (layout.PerInstance) throw new ArgumentException($"Property {propertyID} is per instance");
#endif
            for (int i = 0; i < RenderParams.WindowsCount; i++)
            {
                var bufferPtr = (byte*)BufferData.GetUnsafePtr();
                var targetPtr = bufferPtr + layout.Offset + i * RenderParams.WindowSize;

                UnsafeUtility.CopyStructureToPtr(ref value, targetPtr);
            }

            IsDirty = true;
        }

        public void SetVisibleCount(int visibleCount)
        {
            if (visibleCount > RenderParams.MaxInstancesCount) throw new ArgumentOutOfRangeException(nameof(visibleCount));
            VisibleCount = visibleCount;
            IsDirty = true;
        }

        public void Dispose()
        {
            IsAlive = false;
            GraphicsBuffer?.Dispose();
            GraphicsBuffer = null;
            if (BufferData.IsCreated) BufferData.Dispose();
            if (PropertyLayoutMap.IsCreated) PropertyLayoutMap.Dispose();
            RenderParams = default;
        }

        public void Dump<T>() where T : unmanaged
        {
            var buffer = BufferData.Reinterpret<T>(1);

            for (var index = 0; index < buffer.Length; index++)
            {
                Debug.Log($"[{index}] {buffer[index]}");
            }

            Debug.Log($"{BufferData.Length} bytes, {RenderParams.TotalBufferSize}");
        }

        internal void UploadBuffer()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (VisibleCount < 0 || VisibleCount > RenderParams.MaxInstancesCount)
                throw new ArgumentOutOfRangeException(nameof(VisibleCount), $"Value must be between 0 and MaxInstancesCount ({RenderParams.MaxInstancesCount})");
#endif
            if (!BatchRendererGroupUtility.IsConstantBuffer)
            {
                GraphicsBuffer.SetData(BufferData);
                return;
            }

            var completeWindows = VisibleCount / RenderParams.InstancesPerWindow;
            var bytesPerWindow = RenderParams.WindowSize;

            if (completeWindows > 0)
            {
                var sizeInBytes = completeWindows * bytesPerWindow;
                GraphicsBuffer.SetData(BufferData, 0, 0, sizeInBytes);
            }

            var itemInLastBatch = VisibleCount - RenderParams.InstancesPerWindow * completeWindows;
            if (itemInLastBatch <= 0) return;

            var itemsInLastWindow = VisibleCount % RenderParams.InstancesPerWindow;
            if (itemsInLastWindow > 0)
            {
                var lastWindowOffset = completeWindows * bytesPerWindow;
                GraphicsBuffer.SetData(BufferData, lastWindowOffset, lastWindowOffset, bytesPerWindow);
            }
        }
    }
}