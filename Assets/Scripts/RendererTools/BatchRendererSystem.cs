using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace RendererTools
{
    internal partial class BatchRendererSystem : MonoBehaviour
    {
        static BatchRendererSystem instance;

        internal static BatchRendererSystem Instance
        {
            get
            {
                if (instance) return instance;
                instance = new GameObject("BatchRendererSystem").AddComponent<BatchRendererSystem>();
                instance.Initialize();
                DontDestroyOnLoad(instance.gameObject);
                return instance;
            }
        }

        BatchRendererGroup batchRendererGroup;
        List<RenderInstance> renderInstances;
        NativeList<BatchID> batchIDs;
        NativeList<BatchMaterialID> materialIDs;
        NativeList<BatchMeshID> meshIDs;
        NativeList<int2> batchRanges;

        void Initialize()
        {
            batchRendererGroup = new BatchRendererGroup(instance.OnPerformCulling, IntPtr.Zero);
            renderInstances = new();
            batchIDs = new NativeList<BatchID>(Allocator.Persistent);
            materialIDs = new NativeList<BatchMaterialID>(Allocator.Persistent);
            meshIDs = new NativeList<BatchMeshID>(Allocator.Persistent);
            batchRanges = new NativeList<int2>(Allocator.Persistent);
        }

        void LateUpdate()
        {
            RemoveDisposedInstances();
            UploadBuffers();
        }

        void OnDestroy()
        {
            while (renderInstances.Count > 0)
            {
                RemoveInstance(renderInstances[0]);
            }

            batchRendererGroup?.Dispose();
            batchRendererGroup = null;
            renderInstances.Clear();
            batchIDs.Dispose();
            materialIDs.Dispose();
            meshIDs.Dispose();
            batchRanges.Dispose();
        }

        void UploadBuffers()
        {
            foreach (var renderInstance in renderInstances)
            {
                if (!renderInstance.IsDirty) continue;
                renderInstance.DependencyInternal.Complete();
                renderInstance.UploadBuffer();
                renderInstance.IsDirty = false;
            }
        }

        void RemoveDisposedInstances()
        {
            for (var i = renderInstances.Count - 1; i >= 0; i--)
            {
                if (renderInstances[i].IsAlive) continue;
                RemoveInstance(renderInstances[i]);
            }
        }

        internal RenderInstance CreateInstance(RenderBuilderConfig config, int maxInstancesCount)
        {
            var instanceDataSize = 0;
            var bufferDataSize = 0;
            foreach (var property in config.Properties)
            {
                if (property.PerInstance) instanceDataSize += property.Size;
                else bufferDataSize += property.Size;
            }

            var renderParams = BatchRendererGroupUtility.CalculateRenderParams(maxInstancesCount, instanceDataSize, bufferDataSize);
            var renderInstance = new RenderInstance(renderParams, config.Properties.Count);
            renderInstances.Add(renderInstance);

            meshIDs.Add(batchRendererGroup.RegisterMesh(config.Mesh));
            materialIDs.Add(batchRendererGroup.RegisterMaterial(config.Material));

            var batchMetadata = new NativeArray<MetadataValue>(config.Properties.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

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

                renderInstance.PropertyLayoutMap.Add(property.ID, propertyLayout);
                batchMetadata[index] = metadataValue;
            }

            batchRanges.Add(new(batchIDs.Length, renderParams.WindowsCount));
            if (BatchRendererGroupUtility.IsConstantBuffer)
            {
                for (var i = 0; i < renderParams.WindowsCount; i++)
                    batchIDs.Add(batchRendererGroup.AddBatch(batchMetadata,
                        renderInstance.GraphicsBuffer.bufferHandle,
                        (uint)(i * renderParams.WindowSize),
                        (uint)renderParams.WindowSize));
            }
            else batchIDs.Add(batchRendererGroup.AddBatch(batchMetadata, renderInstance.GraphicsBuffer.bufferHandle));

            batchMetadata.Dispose();

            renderInstance.IsAlive = true;
            return renderInstance;
        }

        void RemoveInstance(RenderInstance renderInstance)
        {
            var index = renderInstances.IndexOf(renderInstance);
            if (index < 0) return;

            var batchRange = batchRanges[index];

            for (var i = 0; i < batchRange.y; i++)
            {
                batchRendererGroup.RemoveBatch(batchIDs[batchRange.x + i]);
            }

            batchIDs.RemoveRange(batchRange.x, batchRange.y);
            for (int i = index + 1; i < batchRanges.Length; i++)
            {
                var range = batchRanges[i];
                batchRanges[i] = new int2(range.x - batchRange.y, range.y);
            }

            batchRendererGroup.UnregisterMaterial(materialIDs[index]);
            batchRendererGroup.UnregisterMesh(meshIDs[index]);
            materialIDs.RemoveAt(index);
            meshIDs.RemoveAt(index);
            renderInstances.RemoveAt(index);
            batchRanges.RemoveAt(index);
        }
    }
}