using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace RendererTools
{
    public static class BatchRendererGroupUtility
    {
        public static readonly int ObjectToWorldID = Shader.PropertyToID("unity_ObjectToWorld");
        public static readonly int WorldToObjectID = Shader.PropertyToID("unity_WorldToObject");
        public static readonly int ColorID = Shader.PropertyToID("_BaseColor");

        internal static readonly bool IsConstantBuffer = BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer;
    
        internal const uint MetadataPerInstanceBit = 0x80000000;
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int AlignedTo16Bytes(this int value) => (value + 15) & -16;
        internal static int AlignToBytes(this int value, int bytes) => (value + bytes - 1) & -bytes;
    
        internal static RenderParams CalculateRenderParams(int maxInstances, int instanceDataSize, int bufferDataSize)
        {
            if (IsConstantBuffer)
            {
                var windowSize = BatchRendererGroup.GetConstantBufferMaxWindowSize();
                var instancesPerWindow = windowSize / (instanceDataSize + bufferDataSize);
        
                var windowsCount = (maxInstances + instancesPerWindow - 1) / instancesPerWindow;
                // bufferDataSize = bufferDataSize.AlignToBytes(BatchRendererGroup.GetConstantBufferOffsetAlignment());
                var totalBufferSize = windowsCount * windowSize;

                return new RenderParams(windowsCount, windowSize, instancesPerWindow, totalBufferSize, maxInstances, bufferDataSize);
            }
            else
            {
                var totalBufferSize = maxInstances * instanceDataSize + bufferDataSize;
                totalBufferSize = totalBufferSize.AlignedTo16Bytes();
                return new RenderParams(1, totalBufferSize, maxInstances, totalBufferSize, maxInstances, bufferDataSize);
            }
        }

        internal static GraphicsBuffer CreateGraphicsBuffer(int totalBufferSize)
        {
            if (IsConstantBuffer)
            {
                var elements = totalBufferSize / 4;
                return new GraphicsBuffer(GraphicsBuffer.Target.Constant, elements, 4);
            }
            else
            {
                var elements = totalBufferSize / 4;
                return new GraphicsBuffer(GraphicsBuffer.Target.Raw, elements, 4);
            }
        }
    }
}