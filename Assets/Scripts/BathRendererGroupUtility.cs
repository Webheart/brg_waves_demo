using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

public readonly struct RenderParams
{
    public readonly int WindowsCount;
    public readonly int WindowSize;
    public readonly int InstancesPerWindow;
    public readonly int TotalBufferSize;
    public readonly int MaxInstancesCount;
    
    public RenderParams(int windowsCount, int windowSize, int instancesPerWindow, int totalBufferSize, int maxInstancesCount)
    {
        WindowsCount = windowsCount;
        WindowSize = windowSize;
        InstancesPerWindow = instancesPerWindow;
        TotalBufferSize = totalBufferSize;
        MaxInstancesCount = maxInstancesCount;
    }
}

internal static class BathRendererGroupUtility
{
    internal static readonly int ObjectToWorldID = Shader.PropertyToID("unity_ObjectToWorld");
    internal static readonly int WorldToObjectID = Shader.PropertyToID("unity_WorldToObject");
    internal static readonly int ColorID = Shader.PropertyToID("_BaseColor");

    internal static readonly bool IsConstantBuffer = BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer;
    
    internal const uint MetadataPerInstanceBit = 0x80000000;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignedTo16Bytes(this int value) => (value + 15) & -16;
    
    public static RenderParams CalculateRenderParams(int maxInstances, int instanceDataSize)
    {
        if (IsConstantBuffer)
        {
            int windowSize = BatchRendererGroup.GetConstantBufferMaxWindowSize();
            int instancesPerWindow = windowSize / instanceDataSize;
        
            int windowsCount = (maxInstances + instancesPerWindow - 1) / instancesPerWindow;
            int totalBufferSize = windowsCount * windowSize;

            return new RenderParams(windowsCount, windowSize, instancesPerWindow, totalBufferSize, maxInstances);
        }
        else
        {
            int totalBufferSize = maxInstances * instanceDataSize;
            totalBufferSize = totalBufferSize.AlignedTo16Bytes(); 
            return new RenderParams(1, totalBufferSize, maxInstances, totalBufferSize, maxInstances);
        }
    }

    public static GraphicsBuffer CreateGraphicsBuffer(int totalBufferSize)
    {
        if (IsConstantBuffer)
        {
            int elements = totalBufferSize / 16;
            return new GraphicsBuffer(GraphicsBuffer.Target.Constant, elements, 16);
        }
        else
        {
            int elements = totalBufferSize / 4;
            return new GraphicsBuffer(GraphicsBuffer.Target.Raw, elements, 4);
        }
    }
}