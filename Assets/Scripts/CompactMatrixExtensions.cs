using Unity.Mathematics;
using UnityEngine;

public static class CompactMatrixExtensions
{
    public static float3x4 ToCompactMatrix(this NonUniformTransform transform)
    {
        return new float3x4(
            math.mul(transform.Rotation, new float3(transform.Scale.x, 0.0f, 0.0f)),
            math.mul(transform.Rotation, new float3(0.0f, transform.Scale.y, 0.0f)),
            math.mul(transform.Rotation, new float3(0.0f, 0.0f, transform.Scale.z)),
            transform.Position);
    }

    public static float3x4 ToCompactMatrix(this RigidTransform transform, float3 scale)
    {
        return new float3x4(
            math.mul(transform.rot, new float3(scale.x, 0.0f, 0.0f)),
            math.mul(transform.rot, new float3(0.0f, scale.y, 0.0f)),
            math.mul(transform.rot, new float3(0.0f, 0.0f, scale.z)),
            transform.pos);
    }

    public static float3x4 ToCompactMatrix(this float3 position, quaternion rotation, float3 scale)
    {
        return new float3x4(
            math.mul(rotation, new float3(scale.x, 0.0f, 0.0f)),
            math.mul(rotation, new float3(0.0f, scale.y, 0.0f)),
            math.mul(rotation, new float3(0.0f, 0.0f, scale.z)),
            position);
    }

    public static float3x4 ToCompactMatrix(this Transform transform)
    {
        return new float3x4(
            math.mul(transform.rotation, new float3(transform.lossyScale.x, 0.0f, 0.0f)),
            math.mul(transform.rotation, new float3(0.0f, transform.lossyScale.y, 0.0f)),
            math.mul(transform.rotation, new float3(0.0f, 0.0f, transform.lossyScale.z)),
            transform.position);
    }

    public static float3x4 Inverse(this float3x4 compactMatrix)
    {
        return math.inverse(compactMatrix.ToFullMatrix()).ToCompactMatrix();
    }

    public static float4x4 ToFullMatrix(this float3x4 matrix)
    {
        return new float4x4(
            new float4(matrix.c0.x, matrix.c0.y, matrix.c0.z, 0.0f),
            new float4(matrix.c1.x, matrix.c1.y, matrix.c1.z, 0.0f),
            new float4(matrix.c2.x, matrix.c2.y, matrix.c2.z, 0.0f),
            new float4(matrix.c3.x, matrix.c3.y, matrix.c3.z, 1.0f));
    }

    public static float3x4 ToCompactMatrix(this float4x4 matrix)
    {
        return new float3x4(
            new float3(matrix.c0.x, matrix.c0.y, matrix.c0.z),
            new float3(matrix.c1.x, matrix.c1.y, matrix.c1.z),
            new float3(matrix.c2.x, matrix.c2.y, matrix.c2.z),
            new float3(matrix.c3.x, matrix.c3.y, matrix.c3.z));
    }
}