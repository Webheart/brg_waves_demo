using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace RendererTools
{
    internal static class BatchRendererGroupUtility
    {
        internal static readonly bool IsConstantBuffer = BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer;
        internal const uint MetadataPerInstanceBit = 0x80000000;
        internal const int IntSize = sizeof(int);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int AlignedTo16Bytes(this int value) => (value + 15) & -16;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int AlignToBytes(this int value, int bytes) => (value + bytes - 1) & -bytes;

        internal static RenderParams CalculateRenderParams(int maxInstances, int instanceDataSize, int bufferDataSize)
        {
            if (IsConstantBuffer)
            {
                var windowSize = BatchRendererGroup.GetConstantBufferMaxWindowSize();
                bufferDataSize = bufferDataSize.AlignToBytes(BatchRendererGroup.GetConstantBufferOffsetAlignment());
                var instancesPerWindow = windowSize / (instanceDataSize + bufferDataSize);

                var windowsCount = (maxInstances + instancesPerWindow - 1) / instancesPerWindow;
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
            return new GraphicsBuffer(GraphicsBuffer.Target.Raw, totalBufferSize / IntSize, IntSize);
        }
    }
}