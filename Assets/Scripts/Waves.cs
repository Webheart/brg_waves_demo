using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;


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
        var currentAABB = MinMaxAABB.CreateFromCenterAndExtents(dropsCollisionAABB.Center + (float3)transform.position, dropsCollisionAABB.Extents);

        var writeBuffer = wavesData.GetWriter();
        var readBuffer = rainData.GetReader();
        var filter = new NativeList<int>(writeBuffer.Data.Length, Allocator.TempJob);

        writeBuffer.Dependency = new GenerateNewWavesJob
        {
            Waves = writeBuffer.Data,
            Drops = readBuffer.Data.AsDeferredJobArray(),
            AABB = currentAABB,
            BaseWaveStrength = baseWaveStrength
        }.Schedule(readBuffer.Dependency);

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
                else if (RaycastAABB(drop.PrevPosition, drop.Position, out float3 entryPoint))
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

        bool RaycastAABB(float3 start, float3 end, out float3 entryPoint)
        {
            entryPoint = float3.zero;
            var rayDir = end - start;
            var rayLength = math.length(rayDir);

            if (rayLength < 0.001f) return false;

            var invDir = 1.0f / rayDir;
            var t0 = (AABB.Min - start) * invDir;
            var t1 = (AABB.Max - start) * invDir;

            var tmin = math.min(t0, t1);
            var tmax = math.max(t0, t1);

            var tenter = math.cmax(tmin);
            var texit = math.cmin(tmax);

            if (tenter < texit && texit > 0 && tenter < rayLength)
            {
                entryPoint = start + math.normalize(rayDir) * math.max(0, tenter);
                return true;
            }

            return false;
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
            for (var index = DeadIndices.Length - 1; index >= 0; index--)
            {
                var deadIndex = DeadIndices[index];
                Waves.RemoveAt(deadIndex);
            }
        }
    }
}