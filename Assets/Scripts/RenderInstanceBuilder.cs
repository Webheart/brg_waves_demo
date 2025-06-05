using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

internal struct PropertyLayout
{
    public int Offset;
    public int Size;
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

    // 1. Сначала вычисляем общий размер всех свойств для одного инстанса
    int instanceDataSize = 0;
    foreach (var property in config.Properties)
    {
        instanceDataSize += property.Size;
    }
    
    // 2. Создаем RenderParams на основе рассчитанного размера
    var renderParams = BathRendererGroupUtility.CalculateRenderParams(maxInstancesCount, instanceDataSize);
    instance.RenderParams = renderParams;

    var totalBufferSize = renderParams.TotalBufferSize;
    
    // 3. Инициализируем буферы
    instance.GraphicsBuffer = BathRendererGroupUtility.CreateGraphicsBuffer(totalBufferSize);
    instance.BufferData = new NativeArray<float4>(totalBufferSize / UnsafeUtility.SizeOf<float4>(), Allocator.Persistent);
    
    // 4. Настраиваем метаданные
    var batchMetadata = new NativeArray<MetadataValue>(config.Properties.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
    instance.PropertyLayoutMap = new NativeParallelHashMap<int, PropertyLayout>(config.Properties.Count, Allocator.Persistent);
    
    var currentOffset = 0;
    for (var index = 0; index < config.Properties.Count; index++)
    {
        var property = config.Properties[index];
        int propertySize = property.Size;
        
        instance.PropertyLayoutMap.TryAdd(property.ID, new PropertyLayout
        {
            Offset = currentOffset,
            Size = propertySize
        });
        
        currentOffset = currentOffset.AlignedTo16Bytes();
        
        batchMetadata[index] = new MetadataValue { 
            NameID = property.ID, 
            Value = (uint)currentOffset | BathRendererGroupUtility.MetadataPerInstanceBit 
        };
        
        
        
        // Размер блока для этого свойства
        int blockSize = BathRendererGroupUtility.IsConstantBuffer
            ? propertySize * renderParams.InstancesPerWindow
            : propertySize * maxInstancesCount;
        
        currentOffset += blockSize;
    }
    
    // 5. Регистрация батчей
    instance.Batches = new NativeArray<BatchID>(renderParams.WindowsCount, Allocator.Persistent);
    
    if (renderParams.WindowsCount > 1)
    {
        for (int i = 0; i < renderParams.WindowsCount; i++)
        {
            instance.Batches[i] = instance.BatchRendererGroup.AddBatch(
                batchMetadata,
                instance.GraphicsBuffer.bufferHandle,
                (uint)(i * renderParams.WindowSize),
                (uint)renderParams.WindowSize
            );
        }
    }
    else
    {
        instance.Batches[0] = instance.BatchRendererGroup.AddBatch(
            batchMetadata,
            instance.GraphicsBuffer.bufferHandle
        );
    }
    
    batchMetadata.Dispose();
    return instance;
}
}