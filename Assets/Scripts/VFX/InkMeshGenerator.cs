using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a procedural 3D box with customizable subdivisions along X and Z.
/// Gives the Ink Lake shader ample vertices for physical 3D wave displacement,
/// while providing proper normals, UVs, tangents, and BoxCollider synchronization.
/// Keep transform scale at (1,1,1) and configure size here to preserve true wave amplitude.
/// </summary>
[ExecuteAlways, RequireComponent(typeof(MeshFilter))]
public class InkMeshGenerator : MonoBehaviour
{
    [Header("Subdivisions")]
    [Tooltip("Number of segments along X (primary direction for 2D platformer camera).")]
    [Min(1)] public int segmentsX = 128;

    [Tooltip("Number of segments along Z (depth direction for 3D first-person camera).")]
    [Min(1)] public int segmentsZ = 16;

    [Header("Dimensions")]
    public Vector3 size = new Vector3(20f, 6f, 4f);

    [Header("Collider Sync")]
    [Tooltip("Automatically match attached BoxCollider size and center to this mesh volume.")]
    public bool syncBoxCollider = true;

    // Backward compatibility for existing code/inspector references
    public int segments
    {
        get => segmentsX;
        set => segmentsX = value;
    }

    private Mesh mesh;

    private void OnEnable() => Build();
    private void OnValidate() => Build();

    [ContextMenu("Rebuild Mesh")]
    public void Build()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "InkBox_Subdivided" };
        }
        mesh.Clear();

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();

        Vector3 h = size * 0.5f;
        Vector3 X = Vector3.right * size.x;
        Vector3 Y = Vector3.up * size.y;
        Vector3 Z = Vector3.forward * size.z;

        int sx = Mathf.Max(1, segmentsX);
        int sz = Mathf.Max(1, segmentsZ);

        // Front face (+XY)
        AddFace(vertices, triangles, uvs, new Vector3(-h.x, -h.y, -h.z), X, Y, sx, 1);
        // Back face (-XY)
        AddFace(vertices, triangles, uvs, new Vector3(-h.x, -h.y,  h.z), X, Y, sx, 1);
        // Top surface (+XZ - Main Water Surface)
        AddFace(vertices, triangles, uvs, new Vector3(-h.x,  h.y, -h.z), X, Z, sx, sz);
        // Bottom face (-XZ)
        AddFace(vertices, triangles, uvs, new Vector3(-h.x, -h.y, -h.z), X, Z, sx, 1);
        // Left side face (+ZY)
        AddFace(vertices, triangles, uvs, new Vector3(-h.x, -h.y, -h.z), Z, Y, sz, 1);
        // Right side face (+ZY)
        AddFace(vertices, triangles, uvs, new Vector3( h.x, -h.y, -h.z), Z, Y, sz, 1);

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var mf = GetComponent<MeshFilter>();
        if (mf != null)
        {
            mf.sharedMesh = mesh;
        }

        if (syncBoxCollider)
        {
            var boxCol = GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                boxCol.size = size;
                boxCol.center = Vector3.zero;
            }
        }
    }

    private static void AddFace(List<Vector3> v, List<int> t, List<Vector2> uvs,
                                Vector3 origin, Vector3 axisU, Vector3 axisW, int segmentsU, int segmentsW)
    {
        int startIndex = v.Count;
        for (int j = 0; j <= segmentsW; j++)
        {
            float vCoord = j / (float)segmentsW;
            for (int i = 0; i <= segmentsU; i++)
            {
                float uCoord = i / (float)segmentsU;
                v.Add(origin + axisU * uCoord + axisW * vCoord);
                uvs.Add(new Vector2(uCoord, vCoord));
            }
        }

        for (int j = 0; j < segmentsW; j++)
        {
            for (int i = 0; i < segmentsU; i++)
            {
                int a = startIndex + j * (segmentsU + 1) + i;
                int b = a + 1;
                int c = a + segmentsU + 1;
                int d = c + 1;

                t.Add(a); t.Add(c); t.Add(b);
                t.Add(b); t.Add(c); t.Add(d);
            }
        }
    }
}
