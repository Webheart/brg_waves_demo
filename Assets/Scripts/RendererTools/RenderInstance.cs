using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace RendererTools
{
    public unsafe class RenderInstance
    {
        internal BatchRendererGroup BatchRendererGroup;
        internal GraphicsBuffer GraphicsBuffer;
        internal BatchMeshID MeshID;
        internal BatchMaterialID MaterialID;
        internal NativeArray<float4> BufferData;
        internal NativeArray<BatchID> Batches;
        internal RenderParams RenderParams;
        internal NativeHashMap<int, PropertyLayout> PropertyLayoutMap;

        internal RenderInstance() {}
        
        public RendererDataWriter GetWriter(int propertyID)
        {
            if (!PropertyLayoutMap.TryGetValue(propertyID, out var layout))
                throw new ArgumentException($"Property {propertyID} not found");

            return new RendererDataWriter
            {
                BufferPtr = (byte*)BufferData.GetUnsafePtr(),
                PropertyOffset = layout.Offset,
                PropertySize = layout.Size,
                InstancesPerWindow = RenderParams.InstancesPerWindow,
                WindowSize = BatchRendererGroupUtility.IsConstantBuffer ? UnityEngine.Rendering.BatchRendererGroup.GetConstantBufferMaxWindowSize() : RenderParams.TotalBufferSize
            };
        }

        public void Dispose()
        {
            if (Batches.IsCreated)
            {
                for (var index = 0; index < Batches.Length; index++)
                    BatchRendererGroup.RemoveBatch(Batches[index]);
                Batches.Dispose();
            }

            if (BatchRendererGroup != null)
            {
                BatchRendererGroup.UnregisterMaterial(MaterialID);
                BatchRendererGroup.UnregisterMesh(MeshID);
                BatchRendererGroup.Dispose();
            }

            GraphicsBuffer?.Dispose();
            if (BufferData.IsCreated) BufferData.Dispose();
            if (PropertyLayoutMap.IsCreated) PropertyLayoutMap.Dispose();
            RenderParams = default;
        }

        public void DebugDumpBuffer()
        {
            Debug.Log("=== Buffer Dump ===");
            var buffer = BufferData;

            for (var index = 0; index < buffer.Length; index++)
            {
                var float4 = buffer[index];
                Debug.Log($"[{index}]: {float4}");
            }
        }

        public void UploadBuffer()
        {
            if (!BatchRendererGroupUtility.IsConstantBuffer)
            {
                GraphicsBuffer.SetData(BufferData);
                return;
            }

            var completeWindows = RenderParams.MaxInstancesCount / RenderParams.InstancesPerWindow;
            var float4PerWindow = RenderParams.WindowSize / sizeof(float4);

            if (completeWindows > 0)
            {
                var sizeInFloat4 = completeWindows * float4PerWindow;
                GraphicsBuffer.SetData(BufferData, 0, 0, sizeInFloat4);
            }

            var lastBatchId = completeWindows;
            var itemInLastBatch = RenderParams.MaxInstancesCount - RenderParams.InstancesPerWindow * completeWindows;

            if (itemInLastBatch <= 0)
                return;

            var windowOffsetInFloat4 = lastBatchId * float4PerWindow;

            foreach (var map in PropertyLayoutMap)
            {
                var layout = map.Value;
                var propertyOffsetInFloat4 = (windowOffsetInFloat4 * 16 + layout.Offset) / 16;

                var propertySizeInFloat4 = layout.Size / 16;
                if (layout.Size % 16 != 0) propertySizeInFloat4++;

                var totalSizeInFloat4 = propertySizeInFloat4 * itemInLastBatch;

                GraphicsBuffer.SetData(BufferData, propertyOffsetInFloat4, propertyOffsetInFloat4, totalSizeInFloat4);
            }
        }

        static T* Malloc<T>(uint count) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<T>() * count,
                UnsafeUtility.AlignOf<T>(),
                Allocator.TempJob);
        }

        internal JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            var drawCommands = new BatchCullingOutputDrawCommands();

            var instancesCount = RenderParams.MaxInstancesCount;
            var instancesPerWindow = RenderParams.InstancesPerWindow;
            var castShadows = false;

            // calculate the amount of draw commands we need in case of UBO mode (one draw command per window)
            var drawCommandCount = (instancesCount + instancesPerWindow - 1) / instancesPerWindow;
            var maxInstancePerDrawCommand = instancesPerWindow;
            drawCommands.drawCommandCount = drawCommandCount;

            // Allocate a single BatchDrawRange. ( all our draw commands will refer to this BatchDrawRange)
            drawCommands.drawRangeCount = 1;
            drawCommands.drawRanges = Malloc<BatchDrawRange>(1);
            drawCommands.drawRanges[0] = new BatchDrawRange
            {
                drawCommandsBegin = 0,
                drawCommandsCount = (uint)drawCommandCount,
                filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = 1,
                    layer = 0,
                    motionMode = MotionVectorGenerationMode.Camera,
                    shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    staticShadowCaster = false,
                    allDepthSorted = false
                }
            };

            if (drawCommands.drawCommandCount > 0)
            {
                // as we don't need culling, the visibility int array buffer will always be {0,1,2,3,...} for each draw command
                // so we just allocate maxInstancePerDrawCommand and fill it
                var visibilityArraySize = maxInstancePerDrawCommand;
                if (instancesCount < visibilityArraySize)
                    visibilityArraySize = instancesCount;

                drawCommands.visibleInstances = Malloc<int>((uint)visibilityArraySize);

                // As we don't need any frustum culling in our context, we fill the visibility array with {0,1,2,3,...}
                for (var i = 0; i < visibilityArraySize; i++)
                    drawCommands.visibleInstances[i] = i;

                // Allocate the BatchDrawCommand array (drawCommandCount entries)
                // In SSBO mode, drawCommandCount will be just 1
                drawCommands.drawCommands = Malloc<BatchDrawCommand>((uint)drawCommandCount);
                var left = instancesCount;
                for (var b = 0; b < drawCommandCount; b++)
                {
                    var inBatchCount = left > maxInstancePerDrawCommand ? maxInstancePerDrawCommand : left;
                    drawCommands.drawCommands[b] = new BatchDrawCommand
                    {
                        visibleOffset = (uint)0, // all draw command is using the same {0,1,2,3...} visibility int array
                        visibleCount = (uint)inBatchCount,
                        batchID = Batches[b],
                        materialID = MaterialID,
                        meshID = MeshID,
                        submeshIndex = 0,
                        splitVisibilityMask = 0xff,
                        flags = BatchDrawCommandFlags.None,
                        sortingPosition = 0
                    };
                    left -= inBatchCount;
                }
            }

            cullingOutput.drawCommands[0] = drawCommands;
            drawCommands.instanceSortingPositions = null;
            drawCommands.instanceSortingPositionFloatCount = 0;

            return new JobHandle();
        }
    }
}