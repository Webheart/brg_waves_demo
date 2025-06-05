using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class Surface : MonoBehaviour
{
    public int2 Size;
    public float2 Gap;
    public Mesh Mesh;
    public Material Material;

    RenderInstance renderInstance;

    NativeArray<float3> positions;
    NativeArray<Color> colors;

    void Start()
    {
        Debug.Log($"{BatchRendererGroup.BufferTarget}");
        var maxInstances = Size.x * Size.y;
        positions = new NativeArray<float3>(maxInstances, Allocator.Persistent);
        colors = new NativeArray<Color>(maxInstances, Allocator.Persistent);
        renderInstance = RenderInstanceBuilder.Start()
            .WithMesh(Mesh).WithMaterial(Material)
            .WithTransformMatrix()
            .WithProperty<Color>(BathRendererGroupUtility.ColorID)
            .Build(maxInstances);
        BuildPositions();
        WriteToBuffer();
        renderInstance.UploadBuffer();
        // renderInstance.DebugDumpBuffer();
        var renderParams = renderInstance.RenderParams;
        Debug.Log($"WindowSize: {renderParams.WindowSize}");
        Debug.Log($"WindowsCount: {renderParams.WindowsCount}");
        Debug.Log($"InstancesPerWindow: {renderParams.InstancesPerWindow}");
        Debug.Log($"TotalBufferSize: {renderParams.TotalBufferSize}");
        Debug.Log($"MaxInstancesCount: {renderParams.MaxInstancesCount}");
    }

    void OnDestroy()
    {
        renderInstance.Dispose();
        positions.Dispose();
        colors.Dispose();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            BuildPositions();
            WriteToBuffer();
            renderInstance.UploadBuffer();
            renderInstance.DebugDumpBuffer();
        }
    }

    void BuildPositions()
    {
        // Вычисляем общий размер сетки
        float2 totalSize = new float2(
            (Size.x - 1) * Gap.x,
            (Size.y - 1) * Gap.y
        );

        // Центрируем сетку относительно позиции объекта
        float3 centerOffset = new float3(
            -totalSize.x * 0.5f,
            0f,
            -totalSize.y * 0.5f
        );

        var offset = (float3)transform.position;
        float time = Time.time;
        // Заполняем массив позиций
        for (int z = 0; z < Size.y; z++)
        {
            for (int x = 0; x < Size.x; x++)
            {
                float yPos = math.sin(time + x * 0.2f + z * 0.2f) * 2f;
                float3 pos = new float3(
                    x * Gap.x,
                    yPos,
                    z * Gap.y
                ) + centerOffset + offset;

                positions[z * Size.x + x] = pos;
            }
        }
    }

    void WriteToBuffer()
    {
        var buffer = renderInstance.GetBufferData();
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = new float4(1, 0, 0, 0);
        }

        var matrixWriter = renderInstance.GetWriter(BathRendererGroupUtility.ObjectToWorldID);
        var invmatrixWriter = renderInstance.GetWriter(BathRendererGroupUtility.WorldToObjectID);
        var colorWriter = renderInstance.GetWriter(BathRendererGroupUtility.ColorID);

        for (int i = 0; i < positions.Length; i++)
        {
            var pos = positions[i];

            // buffer[i * 3 + 0] = new float4(1, 0, 0, 0);
            // buffer[i * 3 + 1] = new float4(1, 0, 0, 0);
            // buffer[i * 3 + 2] = new float4(1, pos.x, pos.y, pos.z);

            
            // Debug.Log($"index in surface: {i}, {i * 3 + 0}");

            var color = new Color(0.5f, 0.25f, 0.25f).linear;
            // color.b = math.lerp(0, 1, pos.y);
            colorWriter.Write(i, color);
            
            matrixWriter.Write(i, new float4x3(new float4(1, 0, 0, 0), new float4(1, 0, 0, 0),new float4(1, pos.x, pos.y, pos.z)));
            invmatrixWriter.Write(i, new float4x3(new float4(1, 0, 0, 0),new float4(1, 0, 0, 0),new float4(1, 0, 0, 0)));
        }
    }
}