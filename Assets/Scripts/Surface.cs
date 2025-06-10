using System;
using RendererTools;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Surface : MonoBehaviour
{
    static readonly int ColorProperty = Shader.PropertyToID("_Color32");
    static readonly int PositionProperty = Shader.PropertyToID("_WorldPosition");

    public int2 Size;
    public float2 Gap;
    public Mesh Mesh;
    public Material Material;

    public Color From, To;

    RenderBuilderConfig renderBuilderConfig;
    RenderInstance renderInstance;

    NativeArray<float3> transforms;

    void Awake()
    {
        renderBuilderConfig = RenderInstanceBuilder.Start()
            .WithMesh(Mesh).WithMaterial(Material)
            .WithTransformMatrix(false)
            .WithProperty<float3>(PositionProperty)
            .WithProperty<Color32>(ColorProperty);
    }

    void OnEnable()
    {
        var maxInstances = Size.x * Size.y;
        transforms = new NativeArray<float3>(maxInstances, Allocator.Persistent);
        renderInstance = renderBuilderConfig.Build(transforms.Length);
        renderInstance.SetVisibleCount(maxInstances);
    }

    void OnDisable()
    {
        transforms.Dispose();
        renderInstance.Dispose();
        renderInstance = null;
    }

    void OnDestroy()
    {
        renderInstance?.Dispose();
    }

    void Update()
    {
        var matrix = transform.TRS();
        renderInstance.WriteSharedProperty(BatchRendererGroupUtility.ObjectToWorldID, matrix);
        renderInstance.WriteSharedProperty(BatchRendererGroupUtility.WorldToObjectID, matrix.Inverse());
        
        renderInstance.Dependency = new UpdateJob
        {
            Size = Size,
            Gap = Gap,
            WorldPosition = transform.position,
            ColorWriter = renderInstance.GetPerInstanceWriter(ColorProperty),
            PositionWriter = renderInstance.GetPerInstanceWriter(PositionProperty),
            Transforms = transforms,
            Time = Time.time, From = From, To = To
        }.ScheduleParallel(transforms.Length, 10, renderInstance.Dependency);
    }

    [BurstCompile]
    struct UpdateJob : IJobFor
    {
        public NativeArray<float3> Transforms;
        public PerInstanceDataWriter PositionWriter;
        public PerInstanceDataWriter InvPositionWriter;
        public PerInstanceDataWriter ColorWriter;

        public float3 WorldPosition;
        public float Time;
        public int2 Size;
        public float2 Gap;
        public Color From, To;

        public void Execute(int index)
        {
            var totalSize = new float2((Size.x - 1) * Gap.x, (Size.y - 1) * Gap.y);

            var centerOffset = new float3(-totalSize.x * 0.5f, 0f, -totalSize.y * 0.5f);

            var cell = new int2(index % Size.x, index / Size.x);
            var yPos = math.sin(Time + cell.x * 0.2f + cell.y * 0.2f) * 2f;
            var pos = new float3(cell.x * Gap.x, yPos, cell.y * Gap.y) + centerOffset + WorldPosition;

            Transforms[index] = pos;
            var invLerp = math.unlerp(-2, 2, pos.y);
            var color = Color.Lerp(From, To, invLerp);
            ColorWriter.Write(index, (Color32)color.linear);
            PositionWriter.Write(index, pos);
        }
    }
}