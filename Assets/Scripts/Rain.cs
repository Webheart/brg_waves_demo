using RendererTools;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;
using Utils;
using Random = UnityEngine.Random;

[DefaultExecutionOrder(JobOrder.DataWriter)]
public class Rain : MonoBehaviour
{
    [Header("Render Properties")]
    [RuntimeReadOnly, SerializeField] Mesh mesh;
    [RuntimeReadOnly, SerializeField] Material material;

    [Header("Rain Properties")]
    [RuntimeReadOnly, SerializeField] int maxCount;
    [SerializeField] MinMaxAABB spawnArea;
    [SerializeField] float dropsInterval = 0.5f;
    [SerializeField] MinMaxAABB deadThreshold;
    [SerializeField] float2 massRange = 1;
    [SerializeField] float3 gravity = new float3(0, -9.81f, 0);
    [SerializeField] float3 initialVelocity = new float3(0, -9.81f, 0);

    [Header("Write Data Properties")]
    [RuntimeReadOnly, SerializeField] RainDropsData rainData;

    RenderBuilderConfig renderBuilderConfig;
    RenderInstance renderInstance;

    float nextDropTimer;
    MinMaxAABB currentDeadThreshold;

    void Awake()
    {
        renderBuilderConfig = RenderInstanceBuilder.Start().WithMesh(mesh).WithMaterial(material).WithTransformMatrix();
    }

    void OnEnable()
    {
        renderInstance = renderBuilderConfig.Build(maxCount);
        renderInstance.SetVisibleCount(maxCount);
        rainData.Initialize();
        nextDropTimer = dropsInterval;
    }

    void OnDisable()
    {
        renderInstance.Dispose();
        rainData.Dispose();
    }

    void Update()
    {
        UpdateDeadThreshold();
        GenerateNewDrops();
        var dataWriter = rainData.GetWriter();
        renderInstance.SetVisibleCount(dataWriter.Data.Length);

        dataWriter.Dependency = new DropsGravityJob
        {
            Drops = dataWriter.Data.AsDeferredJobArray(),
            DeltaTime = Time.deltaTime,
            Gravity = gravity
        }.Schedule(dataWriter.Data, 64, dataWriter.Dependency);

        dataWriter.Dependency = new ShowDropsJob
        {
            Drops = dataWriter.Data.AsDeferredJobArray(),
            ObjToWorldWriter = renderInstance.GetPerInstanceWriter(ShaderProperties.ObjectToWorld),
            WorldToObjWriter = renderInstance.GetPerInstanceWriter(ShaderProperties.WorldToObject)
        }.Schedule(dataWriter.Data, 64, dataWriter.Dependency);

        dataWriter.Dependency = new RemoveDeadDropsJob
        {
            Drops = dataWriter.Data,
            DeadThreshold = currentDeadThreshold
        }.Schedule(dataWriter.Dependency);

        renderInstance.Dependency = dataWriter.Dependency;
    }

    void UpdateDeadThreshold()
    {
        var centerOffset = deadThreshold.Center;
        var extents = deadThreshold.Extents;
        currentDeadThreshold = MinMaxAABB.CreateFromCenterAndExtents(centerOffset + (float3)transform.position, extents);
    }

    void GenerateNewDrops()
    {
        nextDropTimer -= Time.deltaTime;
        if (nextDropTimer > 0) return;
        nextDropTimer = dropsInterval;
        var writer = rainData.GetWriter();
        if (writer.Data.Length >= maxCount) return;
        var xPos = Random.Range(spawnArea.Min.x, spawnArea.Max.x);
        var yPos = Random.Range(spawnArea.Min.y, spawnArea.Max.y);
        var zPos = Random.Range(spawnArea.Min.z, spawnArea.Max.z);
        var mass = Random.Range(massRange.x, massRange.y);
        var pos = (float3)transform.position + new float3(xPos, yPos, zPos);

        var drop = new RainDrop
        {
            Position = pos, PrevPosition = pos,
            Mass = mass,
            Velocity = initialVelocity
        };
        writer.Data.Add(drop);
    }

    [BurstCompile]
    struct DropsGravityJob : IJobParallelForDefer
    {
        public NativeArray<RainDrop> Drops;
        public float3 Gravity;
        public float DeltaTime;

        public void Execute(int index)
        {
            var drop = Drops[index];
            drop.PrevPosition = drop.Position;
            drop.Velocity += Gravity * drop.Mass * DeltaTime;
            drop.Position += drop.Velocity * DeltaTime;
            Drops[index] = drop;
        }
    }

    [BurstCompile]
    struct RemoveDeadDropsJob : IJob
    {
        public NativeList<RainDrop> Drops;
        public MinMaxAABB DeadThreshold;

        public void Execute()
        {
            for (int i = Drops.Length - 1; i >= 0; i--)
            {
                if (DeadThreshold.Contains(Drops[i].Position)) continue;
                Drops.RemoveAtSwapBack(i);
            }
        }
    }

    [BurstCompile]
    struct ShowDropsJob : IJobParallelForDefer
    {
        [ReadOnly] public NativeArray<RainDrop> Drops;

        public PerInstanceDataWriter ObjToWorldWriter;
        public PerInstanceDataWriter WorldToObjWriter;

        public void Execute(int index)
        {
            var drop = Drops[index];
            var matrix = drop.Position.ToCompactMatrix(quaternion.identity, drop.Mass);
            ObjToWorldWriter.Write(index, matrix);
            WorldToObjWriter.Write(index, matrix.Inverse());
        }
    }
}