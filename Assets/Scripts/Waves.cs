using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;
using Utils;

[DefaultExecutionOrder(JobOrder.DataPostProcessor)]
public class Waves : MonoBehaviour
{
    [Header("Bounds Properties")]
    [SerializeField] MinMaxAABB dropsCollisionAABB;

    [Header("Waves Properties")]
    [SerializeField] float baseWaveStrength = 2;
    [SerializeField] float expansionSpeed = 5f;
    [SerializeField] float decaySpeed = 0.8f;

    [Header("Write Data Properties")]
    [RuntimeReadOnly, SerializeField] WavesData wavesData;

    [Header("ReadOnly Data Properties")]
    [RuntimeReadOnly, SerializeField] RainDropsData rainData;

    MinMaxAABB currentAABB;
    public void RaycastNewWave(float3 start, float3 end, float strength)
    {
        if (!currentAABB.RaycastAABB(start, end, out var entryPoint)) return;
        wavesData.GetWriter().Data.Add(new Wave
        {
            Position = entryPoint,
            Strength = baseWaveStrength * strength,
            Radius = 1
        });
    }

    void OnEnable()
    {
        wavesData.Initialize();
    }

    void OnDisable()
    {
        wavesData.Dispose();
    }

    void Update()
    {
        currentAABB = MinMaxAABB.CreateFromCenterAndExtents(dropsCollisionAABB.Center + (float3)transform.position, dropsCollisionAABB.Extents);

        var writeBuffer = wavesData.GetWriter();
        var readBuffer = rainData.GetReader();
        var filter = new NativeList<int>(writeBuffer.Data.Length, Allocator.TempJob);

        if (readBuffer.Data.IsCreated)
        {
            writeBuffer.Dependency = new GenerateNewWavesJob
            {
                Waves = writeBuffer.Data,
                Drops = readBuffer.Data.AsDeferredJobArray(),
                AABB = currentAABB,
                BaseWaveStrength = baseWaveStrength
            }.Schedule(readBuffer.Dependency);
        }

        writeBuffer.Dependency = new UpdateWavesJob
        {
            Waves = writeBuffer.Data.AsDeferredJobArray(),
            DeadIndices = filter.AsParallelWriter(),
            DeltaTime = Time.deltaTime,
            ExpansionSpeed = expansionSpeed,
            DecaySpeed = decaySpeed,
        }.Schedule(writeBuffer.Data, 10, writeBuffer.Dependency);

        writeBuffer.Dependency = new RemoveDeadWavesJob
        {
            Waves = writeBuffer.Data,
            DeadIndices = filter
        }.Schedule(writeBuffer.Dependency);
        filter.Dispose(writeBuffer.Dependency);
    }

    [BurstCompile]
    struct GenerateNewWavesJob : IJob
    {
        [ReadOnly] public NativeArray<RainDrop> Drops;
        public NativeList<Wave> Waves;
        public MinMaxAABB AABB;
        public float BaseWaveStrength;

        public void Execute()
        {
            for (var index = 0; index < Drops.Length; index++)
            {
                var drop = Drops[index];
                if (AABB.Contains(drop.Position)) continue;

                if (AABB.Contains(drop.PrevPosition))
                {
                    CreateWave(drop.Position, drop.Mass);
                }
                else if (AABB.RaycastAABB(drop.PrevPosition, drop.Position, out var entryPoint))
                {
                    CreateWave(entryPoint, drop.Mass);
                }
            }
        }

        void CreateWave(float3 position, float mass)
        {
            Waves.Add(new Wave
            {
                Position = position,
                Strength = BaseWaveStrength * mass
            });
        }
    }

    [BurstCompile]
    struct UpdateWavesJob : IJobParallelForDefer
    {
        public NativeArray<Wave> Waves;

        public NativeList<int>.ParallelWriter DeadIndices;

        public float DeltaTime;
        public float ExpansionSpeed;
        public float DecaySpeed;

        public void Execute(int index)
        {
            var wave = Waves[index];
            wave.Radius += ExpansionSpeed * DeltaTime;
            wave.Strength -= DecaySpeed * DeltaTime;
            Waves[index] = wave;

            if (wave.Strength <= 0) DeadIndices.AddNoResize(index);
        }
    }

    [BurstCompile]
    struct RemoveDeadWavesJob : IJob
    {
        public NativeList<Wave> Waves;
        public NativeList<int> DeadIndices;

        public void Execute()
        {
            DeadIndices.Sort();
            for (var index = DeadIndices.Length - 1; index >= 0; index--)
            {
                var deadIndex = DeadIndices[index];
                Waves.RemoveAt(deadIndex);
            }
        }
    }
}