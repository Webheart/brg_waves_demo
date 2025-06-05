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

    NativeArray<float3> positions;

    void Awake()
    {
        var maxInstances = Size.x * Size.y;
        positions = new NativeArray<float3>(maxInstances, Allocator.Persistent);
        renderBuilderConfig = RenderInstanceBuilder.Start()
            .WithMesh(Mesh).WithMaterial(Material)
            .WithTransformMatrix()
            .WithProperty<Color>(BatchRendererGroupUtility.ColorID);
    }

    void OnEnable()
    {
        renderInstance = renderBuilderConfig.Build(positions.Length);
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
        positions.Dispose();
    }

    JobHandle updateHandle;
    void Update()
    {
        updateHandle.Complete();
        renderInstance.UploadBuffer();
        updateHandle = new UpdateJob
        {
            Size = Size,
            Gap = Gap,
            WorldPosition = transform.position,
            ColorWriter = renderInstance.GetWriter(BatchRendererGroupUtility.ColorID),
            ObjectToWorldWriter = renderInstance.GetWriter(BatchRendererGroupUtility.ObjectToWorldID),
            WorldToObjectWriter = renderInstance.GetWriter(BatchRendererGroupUtility.WorldToObjectID),
            Positions = positions,
            Time = Time.time
        }.ScheduleParallel(positions.Length, 10, updateHandle);
    }

    [BurstCompile]
    struct UpdateJob : IJobFor
    {
        public NativeArray<float3> Positions;
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

            Positions[index] = pos;

            var color = new Color(0.5f, 0.25f, math.lerp(0, 1, pos.y));
            ColorWriter.Write(index, color.linear);
            
            ObjectToWorldWriter.Write(index, new float4x3(new float4(1, 0, 0, 0), new float4(1, 0, 0, 0),new float4(1, pos.x, pos.y, pos.z)));
            WorldToObjectWriter.Write(index, new float4x3(new float4(1, 0, 0, 0),new float4(1, 0, 0, 0),new float4(1, 0, 0, 0)));
        }
    }
}