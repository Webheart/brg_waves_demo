using Unity.Mathematics;

public struct NonUniformTransform
{
    public float3 Position;
    public quaternion Rotation;
    public float3 Scale;

    public NonUniformTransform(float3 position)
    {
        Position = position;
        Rotation = quaternion.identity;
        Scale = 1;
    }

    public NonUniformTransform(float3 position, quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
        Scale = 1;
    }

    public NonUniformTransform(float3 position, quaternion rotation, float3 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
}