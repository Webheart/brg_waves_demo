

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public struct RenderBuilderConfig
{
    internal Bounds Bounds;
    internal RenderParams Params;
    internal List<ShaderProperty> Properties;
    internal Mesh Mesh;
    internal Material Material;
}

internal struct ShaderProperty
{
    internal int ID;
    internal int Size;
}

public static class RenderInstanceBuilder
{
    public static RenderBuilderConfig Start()
    {
        return new RenderBuilderConfig
        {
            Bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f)),
            Properties = new List<ShaderProperty>(),
        };
    }

    public static RenderBuilderConfig WithBounds(this RenderBuilderConfig config, Bounds bounds)
    {
        config.Bounds = bounds;
        return config;
    }

    public static RenderBuilderConfig WithMesh(this RenderBuilderConfig config, Mesh mesh)
    {
        config.Mesh = mesh;
        return config;
    }

    public static RenderBuilderConfig WithMaterial(this RenderBuilderConfig config, Material material)
    {
        config.Material = material;
        return config;
    }

    public static RenderBuilderConfig WithProperty<T>(this RenderBuilderConfig config, int propertyID) where T : unmanaged
    {
        var size = UnsafeUtility.SizeOf<T>();
        config.Properties.Add(new ShaderProperty { ID = propertyID, Size = size });
        return config;
    }

    public static RenderBuilderConfig WithTransformMatrix(this RenderBuilderConfig config)
    {
        return config.WithProperty<float4x3>(BathRendererGroupUtility.ObjectToWorldID).WithProperty<float4x3>(BathRendererGroupUtility.WorldToObjectID);
    }

    public static RenderInstance Build(this RenderBuilderConfig config, int maxInstancesCount)
    {
        var instance = new RenderInstance();
        instance.BatchRendererGroup = new BatchRendererGroup(instance.OnPerformCulling, IntPtr.Zero);
        instance.BatchRendererGroup.SetGlobalBounds(config.Bounds);
        
        instance.MeshID = instance.BatchRendererGroup.RegisterMesh(config.Mesh);
        instance.MaterialID = instance.BatchRendererGroup.RegisterMaterial(config.Material);

        var instanceSize = 0;
        var batchMetadata = new NativeArray<MetadataValue>(config.Properties.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        instance.Properties = new NativeArray<ShaderProperty>(config.Properties.Count, Allocator.Persistent);

        for (var index = 0; index < config.Properties.Count; index++)
        {
            var property = config.Properties[index];

            batchMetadata[index] = new MetadataValue { NameID = property.ID, Value = (uint)instanceSize.AlignedTo4Bytes() | BathRendererGroupUtility.MetadataPerInstanceBit };
            instance.Properties[index] = property;
            instanceSize += property.Size;
        }

        var renderParams = BathRendererGroupUtility.CalculateRenderParams(maxInstancesCount, instanceSize);
        instance.RenderParams = renderParams;
        instance.GraphicsBuffer = BathRendererGroupUtility.CreateGraphicsBuffer(renderParams);
        instance.BufferData = new NativeArray<float4>(renderParams.TotalBufferSize / UnsafeUtility.SizeOf<float4>(), Allocator.Persistent);
        instance.Batches = new NativeArray<BatchID>(renderParams.WindowsCount, Allocator.Persistent);
        for (int i = 0; i < renderParams.WindowsCount; i++)
        {
            var offset = i * renderParams.WindowSize;

            var windowSize = BathRendererGroupUtility.IsConstantBuffer ? renderParams.WindowSize : 0;
            instance.Batches[i] = instance.BatchRendererGroup.AddBatch(batchMetadata, instance.GraphicsBuffer.bufferHandle, (uint)offset, (uint)windowSize);
        }

        batchMetadata.Dispose();

        return instance;
    }
}

public unsafe class RenderInstance
{
    internal BatchRendererGroup BatchRendererGroup;
    internal GraphicsBuffer GraphicsBuffer;
    internal BatchMeshID MeshID;
    internal BatchMaterialID MaterialID;

    internal NativeArray<ShaderProperty> Properties;
    internal NativeArray<float4> BufferData;

    internal NativeArray<BatchID> Batches;
    internal RenderParams RenderParams;
    
    public NativeArray<float4> GetBufferData() => BufferData;

    public void Dispose()
    {
        for (var index = 0; index < Batches.Length; index++)
            BatchRendererGroup.RemoveBatch(Batches[index]);
        BatchRendererGroup.UnregisterMaterial(MaterialID);
        BatchRendererGroup.UnregisterMesh(MeshID);
        BatchRendererGroup.Dispose();
        GraphicsBuffer.Dispose();
        Properties.Dispose();
        BufferData.Dispose();
        Batches.Dispose();
        RenderParams = default;
    }

    public void UploadBuffer()
    {
        var instanceCount = RenderParams.MaxInstancesCount;

        var completeWindows = instanceCount / RenderParams.InstancesPerWindow;
        if (completeWindows > 0)
        {
            int sizeInFloat4 = (completeWindows * RenderParams.WindowSize) / 16;
            GraphicsBuffer.SetData(BufferData, 0, 0, sizeInFloat4);
        }
        
        var lastBatchId = completeWindows;
        var itemInLastBatch = instanceCount - RenderParams.InstancesPerWindow * completeWindows;

        if (itemInLastBatch <= 0)
            return;
            
        var windowOffsetInFloat4 = lastBatchId * RenderParams.WindowSize / 16;

        var offset = 0;
        for (var i = 0; i < Properties.Length; i++)
        {
            var size = Properties[i].Size;
            var startIndex = windowOffsetInFloat4 + RenderParams.InstancesPerWindow * offset;
            var sizeInFloat4 = size / 16;
            offset += sizeInFloat4;
            
            GraphicsBuffer.SetData(BufferData, startIndex, startIndex, itemInLastBatch * sizeInFloat4);
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
        BatchCullingOutputDrawCommands drawCommands = new BatchCullingOutputDrawCommands();
        
        var instancesCount = RenderParams.MaxInstancesCount;
        var instancesPerWindow = RenderParams.InstancesPerWindow;
        var castShadows = false;

            // calculate the amount of draw commands we need in case of UBO mode (one draw command per window)
            int drawCommandCount = (instancesCount + instancesPerWindow - 1) / instancesPerWindow;
            int maxInstancePerDrawCommand = instancesPerWindow;
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
                int visibilityArraySize = maxInstancePerDrawCommand;
                if (instancesCount < visibilityArraySize)
                    visibilityArraySize = instancesCount;

                drawCommands.visibleInstances = Malloc<int>((uint)visibilityArraySize);

                // As we don't need any frustum culling in our context, we fill the visibility array with {0,1,2,3,...}
                for (int i = 0; i < visibilityArraySize; i++)
                    drawCommands.visibleInstances[i] = i;

                // Allocate the BatchDrawCommand array (drawCommandCount entries)
                // In SSBO mode, drawCommandCount will be just 1
                drawCommands.drawCommands = Malloc<BatchDrawCommand>((uint)drawCommandCount);
                int left = instancesCount;
                for (int b = 0; b < drawCommandCount; b++)
                {
                    int inBatchCount = left > maxInstancePerDrawCommand ? maxInstancePerDrawCommand : left;
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