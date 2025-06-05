using UnityEngine;

public static class PrimitiveMeshes
{
    public static Mesh GenerateCube()
    {
        var mesh = new Mesh();
        Vector3[] vertices = {
            new (0,0,0), new (1,0,0), new (1,1,0), new (0,1,0),
            new (0,1,1), new (1,1,1), new (1,0,1), new (0,0,1)
        };

        int[] triangles = {
            0,2,1, 0,3,2, // Front
            2,3,4, 2,4,5, // Top
            1,2,5, 1,5,6, // Right
            0,7,4, 0,4,3, // Left
            5,4,7, 5,7,6, // Back
            0,6,7, 0,1,6  // Bottom
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}