using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public abstract class NativeListData<T> : ScriptableObject, IDisposable where T : unmanaged
{
    public bool IsCreated => data.IsCreated;

    NativeList<T> data;
    Writer writer;

    public void Initialize(int capacity = 4)
    {
        data = new NativeList<T>(capacity, Allocator.Persistent);
        writer = new Writer(data);
    }

    public void Dispose()
    {
        data.Dispose();
        writer = null;
    }

    public Reader GetReader()
    {
        if (!IsCreated) throw new InvalidOperationException($"NativeListData<{typeof(T)}> is not initialized!");
        return new Reader(data, writer.Dependency);
    }

    public Writer GetWriter() => writer;

    public struct Reader
    {
        [ReadOnly] public readonly NativeList<T> Data;
        public readonly JobHandle Dependency;

        public Reader(in NativeList<T> data, in JobHandle dependency)
        {
            Data = data;
            Dependency = dependency;
        }
    }

    public class Writer
    {
        public readonly NativeList<T> Data;
        public JobHandle Dependency;

        public Writer(NativeList<T> data)
        {
            Data = data;
        }
    }
}