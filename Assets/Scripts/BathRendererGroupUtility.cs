using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

public readonly struct RenderParams
{
    public readonly int MaxInstancesCount;
    public readonly int WindowsCount;
    public readonly int WindowSize;
    public readonly int InstancesPerWindow;
    public readonly int TotalBufferSize;
    
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignedTo4Bytes(this int value) => (value + 3) & -4;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GraphicsBuffer CreateGraphicsBuffer(RenderParams renderParams)
    {
        var bufferSize = renderParams.TotalBufferSize;
        var target = IsConstantBuffer ? GraphicsBuffer.Target.Constant : GraphicsBuffer.Target.Raw;
        var stride = IsConstantBuffer ? sizeof(float) * 4 : sizeof(float);
        var count = bufferSize / stride;
        return new GraphicsBuffer(target, count, stride);
    }

    public static RenderParams CalculateRenderParams(int instanceCount, int instanceSize)
    {
        int windowsCount;
        int windowsSize;
        int maxInstancesPerWindow;
        int totalBufferSize;

        if (IsConstantBuffer)
        {
            windowsSize = BatchRendererGroup.GetConstantBufferMaxWindowSize();
            maxInstancesPerWindow = windowsSize / instanceSize;
            windowsCount = (instanceCount + maxInstancesPerWindow - 1) / maxInstancesPerWindow;
            totalBufferSize = windowsCount * windowsSize;
        }
        else
        {
            windowsCount = 1;
            maxInstancesPerWindow = instanceCount;
            totalBufferSize = windowsSize = (instanceCount * instanceSize).AlignedTo16Bytes();
        }

        return new RenderParams(windowsCount, windowsSize, maxInstancesPerWindow, totalBufferSize, instanceCount);
    }
}