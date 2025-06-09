using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace RendererTools
{
    internal unsafe partial class BatchRendererSystem
    {
        static T* Malloc<T>(int count) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), Allocator.TempJob);
        }

        JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            var totalVisibleCount = 0;
            var totalDrawCommandCount = 0;
            var renderInstanceCount = renderInstances.Count;

            var visibleOffsets = new NativeArray<int>(renderInstanceCount, Allocator.TempJob);
            var drawCommandOffsets = new NativeArray<int>(renderInstanceCount, Allocator.TempJob);
            var drawCommandCounts = new NativeArray<int>(renderInstanceCount, Allocator.TempJob);
            var visibleCounts = new NativeArray<int>(renderInstanceCount, Allocator.TempJob);
            var instancesPerWindow = new NativeArray<int>(renderInstanceCount, Allocator.TempJob);

            for (var i = 0; i < renderInstanceCount; i++)
            {
                var ri = renderInstances[i];
                var visibleCount = ri.VisibleCount;
                var perWindow = ri.RenderParams.InstancesPerWindow;
                var dcCount = (visibleCount + perWindow - 1) / perWindow;

                visibleCounts[i] = visibleCount;
                instancesPerWindow[i] = perWindow;
                visibleOffsets[i] = totalVisibleCount;
                drawCommandOffsets[i] = totalDrawCommandCount;
                drawCommandCounts[i] = dcCount;

                totalVisibleCount += visibleCount;
                totalDrawCommandCount += dcCount;
            }

            var visibleInstances = Malloc<int>(totalVisibleCount);
            var drawRanges = Malloc<BatchDrawRange>(renderInstanceCount);
            var drawCommands = Malloc<BatchDrawCommand>(totalDrawCommandCount);

            JobHandle dependency = default;
            dependency = new FillVisibleInstancesJob
            {
                VisibleInstances = visibleInstances,
                VisibleOffsets = visibleOffsets,
                VisibleCounts = visibleCounts
            }.ScheduleParallel(renderInstanceCount, 1, dependency);

            dependency = new DrawRangesJob
            {
                DrawRanges = drawRanges,
                DrawCommandOffsets = drawCommandOffsets,
                DrawCommandCounts = drawCommandCounts
            }.ScheduleParallel(renderInstanceCount, 1, dependency);

            dependency = new DrawCommandsJob
            {
                DrawCommands = drawCommands,
                DrawCommandOffsets = drawCommandOffsets,
                DrawCommandCounts = drawCommandCounts,
                InstancesPerWindow = instancesPerWindow,
                VisibleCounts = visibleCounts,
                MaterialIDs = materialIDs.AsArray(),
                MeshIDs = meshIDs.AsArray(),
                AllBatches = batchIDs.AsArray(),
                BatchRanges = batchRanges.AsArray()
            }.ScheduleParallel(renderInstanceCount, 1, dependency);

            cullingOutput.drawCommands[0] = new BatchCullingOutputDrawCommands
            {
                visibleInstances = visibleInstances,
                visibleInstanceCount = totalVisibleCount,
                drawCommands = drawCommands,
                drawCommandCount = totalDrawCommandCount,
                drawRanges = drawRanges,
                drawRangeCount = renderInstanceCount,
                instanceSortingPositions = null,
                instanceSortingPositionFloatCount = 0
            };

            return dependency;
        }

        [BurstCompile]
        struct FillVisibleInstancesJob : IJobFor
        {
            [NativeDisableUnsafePtrRestriction]
            public int* VisibleInstances;

            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> VisibleOffsets;
            [ReadOnly] public NativeArray<int> VisibleCounts;

            public void Execute(int index)
            {
                var offset = VisibleOffsets[index];
                var count = VisibleCounts[index];

                for (var i = 0; i < count; i++)
                {
                    VisibleInstances[offset + i] = i;
                }
            }
        }

        [BurstCompile]
        struct DrawRangesJob : IJobFor
        {
            [NativeDisableUnsafePtrRestriction]
            public BatchDrawRange* DrawRanges;

            [ReadOnly] public NativeArray<int> DrawCommandOffsets;
            [ReadOnly] public NativeArray<int> DrawCommandCounts;

            public void Execute(int index)
            {
                DrawRanges[index] = new BatchDrawRange
                {
                    drawCommandsBegin = (uint)DrawCommandOffsets[index],
                    drawCommandsCount = (uint)DrawCommandCounts[index],
                    filterSettings = new BatchFilterSettings
                    {
                        renderingLayerMask = 1,
                        layer = 0,
                        motionMode = MotionVectorGenerationMode.Camera,
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = true,
                        staticShadowCaster = false,
                        allDepthSorted = false
                    }
                };
            }
        }

        [BurstCompile]
        struct DrawCommandsJob : IJobFor
        {
            [NativeDisableUnsafePtrRestriction]
            public BatchDrawCommand* DrawCommands;

            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> DrawCommandOffsets;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> DrawCommandCounts;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> InstancesPerWindow;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> VisibleCounts;

            [ReadOnly] public NativeArray<BatchMaterialID> MaterialIDs;
            [ReadOnly] public NativeArray<BatchMeshID> MeshIDs;
            [ReadOnly] public NativeArray<BatchID> AllBatches;
            [ReadOnly] public NativeArray<int2> BatchRanges;

            public void Execute(int index)
            {
                var instancesCount = VisibleCounts[index];
                if (instancesCount == 0) return;

                var perWindow = InstancesPerWindow[index];
                var drawCommandCount = DrawCommandCounts[index];
                var drawCommandOffset = DrawCommandOffsets[index];
                var batchOffset = BatchRanges[index].x;
                var remaining = instancesCount;

                for (var i = 0; i < drawCommandCount; i++)
                {
                    var countInBatch = Mathf.Min(remaining, perWindow);
                    DrawCommands[drawCommandOffset + i] = new BatchDrawCommand
                    {
                        visibleOffset = 0,
                        visibleCount = (uint)countInBatch,
                        batchID = AllBatches[batchOffset + i],
                        materialID = MaterialIDs[index],
                        meshID = MeshIDs[index],
                        submeshIndex = 0,
                        splitVisibilityMask = 0xff,
                        flags = BatchDrawCommandFlags.None,
                        sortingPosition = 0
                    };

                    remaining -= countInBatch;
                }
            }
        }
    }
}