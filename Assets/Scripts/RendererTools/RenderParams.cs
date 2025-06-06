namespace RendererTools
{
    internal struct RenderParams
    {
        internal int WindowsCount;
        internal int WindowSize;
        internal int InstancesPerWindow;
        internal int TotalBufferSize;
        internal int MaxInstancesCount;
        internal int SharedDataSize;
        
        internal RenderParams(int windowsCount, int windowSize, int instancesPerWindow, int totalBufferSize, int maxInstancesCount, int sharedDataSize)
        {
            WindowsCount = windowsCount;
            WindowSize = windowSize;
            InstancesPerWindow = instancesPerWindow;
            TotalBufferSize = totalBufferSize;
            MaxInstancesCount = maxInstancesCount;
            SharedDataSize = sharedDataSize;
        }
    }
}