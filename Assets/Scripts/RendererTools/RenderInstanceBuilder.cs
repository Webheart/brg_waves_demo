using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace RendererTools
{
    public class RenderBuilderConfig
    {
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
                Properties = new List<ShaderProperty>()
            };
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
            return BatchRendererSystem.Instance.CreateInstance(config, maxInstancesCount);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ValidateSettings(this RenderBuilderConfig config)
        {
            if (config.Material == null) throw new NoNullAllowedException("RenderInstanceBuilder: Material is null");
            if (config.Mesh == null) throw new NoNullAllowedException("RenderInstanceBuilder: Mesh is null");
            if (config.Properties.Count == 0) throw new InvalidOperationException("RenderInstanceBuilder: Properties is empty");
            foreach (var property in config.Properties)
            {
                if (property.ID == BatchRendererGroupUtility.ObjectToWorldID || property.ID == BatchRendererGroupUtility.WorldToObjectID) continue;
                if (!config.Material.HasProperty(property.ID)) throw new InvalidOperationException("RenderInstanceBuilder: Material does not contain property with ID: " + property.ID);
            }
        }
    }
}