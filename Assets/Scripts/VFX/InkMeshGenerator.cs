using System.Collections.Generic;
using UnityEngine;

// Builds a box with many segments along X so the ink shader has vertices to displace.
// Keep the transform scale at (1,1,1) and set the size here, otherwise the wave amplitude gets scaled too.
[ExecuteAlways, RequireComponent(typeof(MeshFilter))]
public class InkMeshGenerator : MonoBehaviour
{
    [Min(1)] public int segments = 128;
    public Vector3 size = new Vector3(20f, 6f, 4f);

    Mesh mesh;

    void OnEnable() => Build();
    void OnValidate() => Build();

    void Build()
    {
        if (mesh == null) mesh = new Mesh { name = "InkBox" };
        mesh.Clear();

        var v = new List<Vector3>();
        var t = new List<int>();
        Vector3 h = size * 0.5f;
        Vector3 X = Vector3.right * size.x;
        Vector3 Y = Vector3.up * size.y;
        Vector3 Z = Vector3.forward * size.z;

        Face(v, t, new Vector3(-h.x, -h.y, -h.z), X, Y, segments, 1); // front
        Face(v, t, new Vector3(-h.x, -h.y,  h.z), X, Y, segments, 1); // back
        Face(v, t, new Vector3(-h.x,  h.y, -h.z), X, Z, segments, 1); // top
        Face(v, t, new Vector3(-h.x, -h.y, -h.z), X, Z, segments, 1); // bottom
        Face(v, t, new Vector3(-h.x, -h.y, -h.z), Z, Y, 1, 1);        // left
        Face(v, t, new Vector3( h.x, -h.y, -h.z), Z, Y, 1, 1);        // right

        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    static void Face(List<Vector3> v, List<int> t, Vector3 o, Vector3 u, Vector3 w, int us, int ws)
    {
        int start = v.Count;
        for (int j = 0; j <= ws; j++)
            for (int i = 0; i <= us; i++)
                v.Add(o + u * (i / (float)us) + w * (j / (float)ws));

        for (int j = 0; j < ws; j++)
            for (int i = 0; i < us; i++)
            {
                int a = start + j * (us + 1) + i;
                int b = a + 1;
                int c = a + us + 1;
                int d = c + 1;
                t.Add(a); t.Add(c); t.Add(b);
                t.Add(b); t.Add(c); t.Add(d);
            }
    }
}
