using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace RendererTools
{
    public class RenderBuilderConfig
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
        internal bool PerInstance;
    }

    internal struct PropertyLayout
    {
        internal int Offset;
        internal int Size;
        internal bool PerInstance;
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

        public static RenderBuilderConfig WithProperty<T>(this RenderBuilderConfig config, int propertyID, bool perInstance = true) where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            config.Properties.Add(new ShaderProperty { ID = propertyID, Size = size, PerInstance = perInstance });
            return config;
        }

        public static RenderBuilderConfig WithTransformMatrix(this RenderBuilderConfig config, bool perInstance = true)
        {
            return config.WithProperty<float4x3>(BatchRendererGroupUtility.ObjectToWorldID, perInstance).WithProperty<float4x3>(BatchRendererGroupUtility.WorldToObjectID, perInstance);
        }

        public static RenderInstance Build(this RenderBuilderConfig config, int maxInstancesCount)
        {
            config.ValidateSettings();

            var instance = new RenderInstance();
            instance.BatchRendererGroup = new BatchRendererGroup(instance.OnPerformCulling, IntPtr.Zero);
            instance.BatchRendererGroup.SetGlobalBounds(config.Bounds);

            instance.MeshID = instance.BatchRendererGroup.RegisterMesh(config.Mesh);
            instance.MaterialID = instance.BatchRendererGroup.RegisterMaterial(config.Material);

            var instanceDataSize = 0;
            var bufferDataSize = 0;
            foreach (var property in config.Properties)
            {
                if (property.PerInstance) instanceDataSize += property.Size;
                else bufferDataSize += property.Size;
            }

            var renderParams = BatchRendererGroupUtility.CalculateRenderParams(maxInstancesCount, instanceDataSize, bufferDataSize);
            instance.RenderParams = renderParams;

            var totalBufferSize = renderParams.TotalBufferSize;

            instance.GraphicsBuffer = BatchRendererGroupUtility.CreateGraphicsBuffer(totalBufferSize);
            instance.BufferData = new NativeArray<byte>(totalBufferSize, Allocator.Persistent);

            var batchMetadata = new NativeArray<MetadataValue>(config.Properties.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            instance.PropertyLayoutMap = new NativeHashMap<int, PropertyLayout>(config.Properties.Count, Allocator.Persistent);

            var currentOffset = renderParams.SharedDataSize;
            var perBlockOffset = 0;
            for (var index = 0; index < config.Properties.Count; index++)
            {
                var property = config.Properties[index];
                var propertySize = property.Size;
                PropertyLayout propertyLayout;
                MetadataValue metadataValue;

                if (!property.PerInstance)
                {
                    propertyLayout = new PropertyLayout { Offset = perBlockOffset, Size = propertySize, PerInstance = false };
                    metadataValue = new MetadataValue { NameID = property.ID, Value = (uint)perBlockOffset | 0 };
                    perBlockOffset += propertySize;
                }
                else
                {
                    currentOffset = currentOffset.AlignedTo16Bytes();
                    propertyLayout = new PropertyLayout { Offset = currentOffset, Size = propertySize, PerInstance = true };

                    metadataValue = new MetadataValue
                    {
                        NameID = property.ID,
                        Value = (uint)currentOffset | BatchRendererGroupUtility.MetadataPerInstanceBit
                    };
                    
                    var blockSize = BatchRendererGroupUtility.IsConstantBuffer
                        ? propertySize * renderParams.InstancesPerWindow
                        : propertySize * maxInstancesCount;
                    
                    currentOffset += blockSize;
                }
                
                instance.PropertyLayoutMap.Add(property.ID, propertyLayout);
                batchMetadata[index] = metadataValue;
            }

            instance.Batches = new NativeArray<BatchID>(renderParams.WindowsCount, Allocator.Persistent);

            if (BatchRendererGroupUtility.IsConstantBuffer)
            {
                for (var i = 0; i < renderParams.WindowsCount; i++)
                    instance.Batches[i] = instance.BatchRendererGroup.AddBatch(batchMetadata,
                        instance.GraphicsBuffer.bufferHandle,
                        (uint)(i * renderParams.WindowSize),
                        (uint)renderParams.WindowSize);
            }
            else instance.Batches[0] = instance.BatchRendererGroup.AddBatch(batchMetadata, instance.GraphicsBuffer.bufferHandle);

            batchMetadata.Dispose();
            return instance;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ValidateSettings(this RenderBuilderConfig config)
        {
            if (config.Material == null) throw new NoNullAllowedException("RenderInstanceBuilder: Material is null");
            if (config.Mesh == null) throw new NoNullAllowedException("RenderInstanceBuilder: Mesh is null");
            if (config.Properties.Count == 0) throw new InvalidOperationException("RenderInstanceBuilder: Properties is empty");
        }
    }
}