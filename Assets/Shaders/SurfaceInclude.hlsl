#ifndef UNPACK_COLOR_INCLUDED
#define UNPACK_COLOR_INCLUDED

void UnpackColor_float(float packed, out float4 unpacked)
{
    uint value = asuint(packed);
    uint r = (value >> 0) & 0xFF;
    uint g = (value >> 8) & 0xFF;
    uint b = (value >> 16) & 0xFF;
    uint a = (value >> 24) & 0xFF;
    unpacked = float4(r, g, b, a) / 255.0;
}

#endif

#ifndef SET_WORLD_POSITION_INCLUDED
#define SET_WORLD_POSITION_INCLUDED

void SetWorldPosition_float(float3 ObjectPosition, float3 TargetWorldPos, out float3 WorldPosition)
{
    WorldPosition = TargetWorldPos + ObjectPosition;
}

#endif
