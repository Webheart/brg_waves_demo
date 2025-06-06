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

    RenderBuilderConfig renderBuilderConfig;
    RenderInstance renderInstance;
    PerInstanceDataWriter positionWriter;
    PerInstanceDataWriter colorWriter;

    NativeArray<float3> transforms;

    void Awake()
    {
        var maxInstances = Size.x * Size.y;
        transforms = new NativeArray<float3>(maxInstances, Allocator.Persistent);
        renderBuilderConfig = RenderInstanceBuilder.Start().WithBounds(new Bounds(transform.position, new(Size.x * Gap.x, 1, Size.y * Gap.y)))
            .WithMesh(Mesh).WithMaterial(Material)
            .WithTransformMatrix(false)
            // .WithProperty<Color>(BatchRendererGroupUtility.ColorID, false);
            .WithProperty<float3>(PositionProperty)
            .WithProperty<Color32>(ColorProperty);
    }

    void OnEnable()
    {
        renderInstance = renderBuilderConfig.Build(transforms.Length);
    }

    void OnDisable()
    {
        updateHandle.Complete();
        renderInstance.Dispose();
        renderInstance = null;
    }

    void OnDestroy()
    {
        updateHandle.Complete();
        renderInstance?.Dispose();
        transforms.Dispose();
    }

    JobHandle updateHandle;

    void Update()
    {
        updateHandle.Complete();
        renderInstance.UploadBuffer(transforms.Length);

        if (Input.GetKeyDown(KeyCode.Space)) renderInstance.Dump<float4>();

        var nonUniformTransform = new NonUniformTransform
        {
            Position = transform.position,
            Rotation = transform.rotation,
            Scale = transform.localScale
        };
        
        var matr = nonUniformTransform.TRS();
        renderInstance.WriteSharedProperty(BatchRendererGroupUtility.ObjectToWorldID, matr);
        renderInstance.WriteSharedProperty(BatchRendererGroupUtility.WorldToObjectID, matr.Inverse());
        
        updateHandle = new UpdateJob
        {
            Size = Size,
            Gap = Gap,
            WorldPosition = transform.position,
            ColorWriter = renderInstance.GetPerInstanceWriter(ColorProperty),
            PositionWriter = renderInstance.GetPerInstanceWriter(PositionProperty),
            // ColorWriter = renderInstance.GetWriter(BatchRendererGroupUtility.ColorID),
            // PositionWriter = renderInstance.GetWriter(BatchRendererGroupUtility.ObjectToWorldID),
            // InvPositionWriter = renderInstance.GetWriter(BatchRendererGroupUtility.WorldToObjectID),
            Transforms = transforms,
            Time = Time.time
        }.ScheduleParallel(transforms.Length, 10, updateHandle);
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

        public void Execute(int index)
        {
            var totalSize = new float2((Size.x - 1) * Gap.x, (Size.y - 1) * Gap.y);

            var centerOffset = new float3(-totalSize.x * 0.5f, 0f, -totalSize.y * 0.5f);

            var cell = new int2(index % Size.x, index / Size.x);
            var yPos = math.sin(Time + cell.x * 0.2f + cell.y * 0.2f) * 2f;
            var pos = new float3(cell.x * Gap.x, yPos, cell.y * Gap.y) + centerOffset + WorldPosition;

            Transforms[index] = pos;
            // var t = new NonUniformTransform(pos).TRS();

            var color = new Color(0.5f, 0.25f, math.lerp(0, 1, pos.y));
            ColorWriter.Write(index, (Color32)color.linear);
            PositionWriter.Write(index, pos);
            // ColorWriter.Write(index, color.linear);
            // PositionWriter.Write(index, t);
            // InvPositionWriter.Write(index, t.Inverse());
        }
    }
}