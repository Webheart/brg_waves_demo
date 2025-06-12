using RendererTools;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;

[DefaultExecutionOrder(JobOrder.DataReader)]
public class Surface : MonoBehaviour
{
    static readonly int ColorProperty = Shader.PropertyToID("_Color32");
    static readonly int PositionProperty = Shader.PropertyToID("_WorldPosition");

    [Header("Render Properties")]
    [RuntimeReadOnly, SerializeField] Mesh mesh;
    [RuntimeReadOnly, SerializeField] Material material;

    [Header("Grid Properties")]
    [RuntimeReadOnly, SerializeField] int2 gridSize;
    [SerializeField] float2 gridPivot;
    [SerializeField] Color from, to;

    [Header("ReadOnly Data Properties")]
    [RuntimeReadOnly, SerializeField] WavesData wavesData;

    public MinMaxAABB AABB => MinMaxAABB.CreateFromCenterAndExtents(transform.position, new float3(gridSize.x, 1, gridSize.y));

    RenderBuilderConfig renderBuilderConfig;
    RenderInstance renderInstance;


    void Awake()
    {
        renderBuilderConfig = RenderInstanceBuilder.Start()
            .WithMesh(mesh).WithMaterial(material)
            .WithTransformMatrix(false)
            .WithProperty<float3>(PositionProperty)
            .WithProperty<Color32>(ColorProperty);
    }

    void OnEnable()
    {
        var maxInstances = gridSize.x * gridSize.y;
        renderInstance = renderBuilderConfig.Build(maxInstances);
        renderInstance.SetVisibleCount(maxInstances);
    }

    void OnDisable()
    {
        renderInstance.Dispose();
        renderInstance = null;
    }

    void Update()
    {
        var matrix = transform.ToCompactMatrix();
        renderInstance.WriteSharedProperty(BatchRendererGroupUtility.ObjectToWorldID, matrix);
        renderInstance.WriteSharedProperty(BatchRendererGroupUtility.WorldToObjectID, matrix.Inverse());

        var waveEffects = new NativeArray<float>(gridSize.x * gridSize.y, Allocator.TempJob);
        if (wavesData.IsCreated)
        {
            var dataReader = wavesData.GetReader();
            renderInstance.Dependency = new ComputeWavesJob
            {
                Waves = dataReader.Data.AsDeferredJobArray(),
                WaveEffects = waveEffects,
                GridPosition = transform.position,
                GridSize = gridSize,
                GridPivot = gridPivot
            }.Schedule(dataReader.Data, 1, dataReader.Dependency);
        }

        renderInstance.Dependency = new ShowCellsJob
        {
            PositionWriter = renderInstance.GetPerInstanceWriter(PositionProperty),
            ColorWriter = renderInstance.GetPerInstanceWriter(ColorProperty),
            GridSize = gridSize,
            GridPivot = gridPivot,
            From = from, To = to,
            WaveEffects = waveEffects
        }.ScheduleParallel(gridSize.x * gridSize.y, math.max(gridSize.x, gridSize.y), renderInstance.Dependency);
        waveEffects.Dispose(renderInstance.Dependency);
    }

    [BurstCompile]
    struct ComputeWavesJob : IJobParallelForDefer
    {
        const float OscillationFactor = math.PI * 4;
        
        [ReadOnly] public NativeArray<Wave> Waves;
        public NativeArray<float> WaveEffects;
        public float3 GridPosition;
        public int2 GridSize;
        public float2 GridPivot;

        public void Execute(int waveIndex)
        {
            var wave = Waves[waveIndex];
            var localWavePos = wave.Position - GridPosition;
            var gridPos = new float2(localWavePos.x + GridSize.x * GridPivot.x, localWavePos.z + GridSize.y * GridPivot.y);

            var radius = wave.Radius;
            var radiusSq = radius * radius;
            
            var minCell = (int2)math.floor(gridPos - radius);
            var maxCell = (int2)math.ceil(gridPos + radius);
            minCell = math.clamp(minCell, 0, GridSize - 1);
            maxCell = math.clamp(maxCell, 0, GridSize - 1);

            var waveStrength = wave.Strength;
            var invRadiusSq = 1.0f / radiusSq;

            for (var j = minCell.y; j <= maxCell.y; j++)
            {
                float cellZ = j;
                var dz = cellZ - gridPos.y;
                var dzSq = dz * dz;
                
                for (var i = minCell.x; i <= maxCell.x; i++)
                {
                    float cellX = i;
                    var dx = cellX - gridPos.x;
                    var dxSq = dx * dx;
                    var distSq = dxSq + dzSq;

                    if (distSq > radiusSq) continue;
                    {
                        var normalizedDistance = 1 - distSq * invRadiusSq;
                        var oscillation = math.sin(normalizedDistance * OscillationFactor);
                        var effect = normalizedDistance * oscillation * waveStrength;

                        var index = j * GridSize.x + i;
                        var span = WaveEffects.AsSpan();
                        InterlockedAdd(ref span[index], effect);
                    }
                }
            }
        }
        
        static void InterlockedAdd(ref float location, float value)
        {
            float newCurrentValue = location;
            while (true)
            {
                float currentValue = newCurrentValue;
                float newValue = currentValue + value;
                newCurrentValue = System.Threading.Interlocked.CompareExchange(ref location, newValue, currentValue);
                if (newCurrentValue.Equals(currentValue)) return;
            }
        }
    }

    [BurstCompile]
    struct ShowCellsJob : IJobFor
    {
        [ReadOnly] public NativeArray<float> WaveEffects;
        
        public int2 GridSize;
        public float2 GridPivot;
        public Color From, To;
        
        public PerInstanceDataWriter PositionWriter;
        public PerInstanceDataWriter ColorWriter;

        public void Execute(int index)
        {
            var centerOffset = new float3(-GridSize.x * GridPivot.x, 0, -GridSize.y * GridPivot.y);
            var cell = new int2(index % GridSize.x, index / GridSize.x);
            var basePos = new float3(cell.x, 0, cell.y) + centerOffset;
            var totalEffect = WaveEffects[index];
            var pos = new float3(basePos.x, totalEffect, basePos.z);
            var invLerp = math.unlerp(-1, 1, totalEffect);
            var color = Color.Lerp(From, To, invLerp);

            ColorWriter.Write(index, (Color32)color.linear);
            PositionWriter.Write(index, pos);
        }
    }
}