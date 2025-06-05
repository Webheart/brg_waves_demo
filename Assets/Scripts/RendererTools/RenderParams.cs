namespace RendererTools
{
    internal readonly struct RenderParams
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
}