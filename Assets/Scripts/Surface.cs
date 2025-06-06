using System;
using RendererTools;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


public class Surface : MonoBehaviour
{
    public int2 Size;
    public float2 Gap;
    public Mesh Mesh;
    public Material Material;

    RenderBuilderConfig renderBuilderConfig;
    RenderInstance renderInstance;
    RendererDataWriter objectToWorldWriter;
    RendererDataWriter worldToObjectWriter;
    RendererDataWriter colorWriter;

    NativeArray<NonUniformTransform> transforms;

    void Awake()
    {
        var maxInstances = Size.x * Size.y;
        transforms = new NativeArray<NonUniformTransform>(maxInstances, Allocator.Persistent);
        renderBuilderConfig = RenderInstanceBuilder.Start().WithBounds(new Bounds(transform.position, new(Size.x * Gap.x, 1, Size.y * Gap.y)))
            .WithMesh(Mesh).WithMaterial(Material)
            .WithTransformMatrix()
            .WithProperty<Color>(BatchRendererGroupUtility.ColorID);
    }

    void OnEnable()
    {
        renderInstance = renderBuilderConfig.Build(transforms.Length);
        objectToWorldWriter = renderInstance.GetWriter(BatchRendererGroupUtility.ObjectToWorldID);
        worldToObjectWriter = renderInstance.GetWriter(BatchRendererGroupUtility.WorldToObjectID);
        colorWriter = renderInstance.GetWriter(BatchRendererGroupUtility.ColorID);
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
        updateHandle = new UpdateJob
        {
            Size = Size,
            Gap = Gap,
            WorldPosition = transform.position,
            ColorWriter = colorWriter,
            ObjectToWorldWriter = objectToWorldWriter,
            WorldToObjectWriter = worldToObjectWriter,
            Transforms = transforms,
            Time = Time.time
        }.ScheduleParallel(transforms.Length, 10, updateHandle);
    }

    [BurstCompile]
    struct UpdateJob : IJobFor
    {
        public NativeArray<NonUniformTransform> Transforms;
        public RendererDataWriter ObjectToWorldWriter;
        public RendererDataWriter WorldToObjectWriter;
        public RendererDataWriter ColorWriter;

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

            var transform = new NonUniformTransform(pos, quaternion.identity, 0.5f);
            Transforms[index] = transform;

            var color = new Color(0.5f, 0.25f, math.lerp(0, 1, pos.y));
            ColorWriter.Write(index, color.linear);
            
            ObjectToWorldWriter.Write(index, transform.TRS());
            WorldToObjectWriter.Write(index, transform.TRS().Inverse());
        }
    }
}