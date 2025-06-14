using UnityEngine;

namespace Utils
{
    public static class ShaderProperties
    {
        public static readonly int ObjectToWorld = Shader.PropertyToID("unity_ObjectToWorld");
        public static readonly int WorldToObject = Shader.PropertyToID("unity_WorldToObject");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int Color32 = Shader.PropertyToID("_Color32");
        public static readonly int CellOffset = Shader.PropertyToID("_CellOffset");
    }
}