using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(menuName = "Create WavesData", fileName = "WavesData", order = 0)]
public class WavesData : NativeListData<Wave> { }

public struct Wave
{
    public float3 Position;
    public float Radius;
    public float Strength;
}