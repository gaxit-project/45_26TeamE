using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    Mesh mesh;
    MeshFilter meshFilter;
    MeshCollider meshCollider;

    List<Vector3> vertices = new List<Vector3>();
    List<Vector2> uvs = new List<Vector2>();
    List<int> dirtTriangles = new List<int>();
    List<int> oreTriangles = new List<int>();

    public void Init(Material dirtMat, Material oreMat)
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshFilter.mesh = mesh;
        meshFilter.sharedMesh = mesh;

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        renderer.materials = new Material[] { dirtMat, oreMat };
    }

    public void RebuildMesh(byte[,,] mapData, int startY, int endY, int thicknessX, int heightY, int widthZ, float blockSize)
    {
        vertices.Clear();
        uvs.Clear();
        dirtTriangles.Clear();
        oreTriangles.Clear();

        for (int x = 0; x < thicknessX; x++)
        {
            for (int y = startY; y < endY; y++) // このチャンクの担当範囲だけループ
            {
                for (int z = 0; z < widthZ; z++)
                {
                    if (mapData[x, y, z] != 0)
                        AddCube(x, y, z, mapData[x, y, z], mapData, thicknessX, heightY, widthZ, blockSize);
                }
            }
        }

        UpdateMesh();
    }

    void AddCube(int x, int y, int z, byte blockType, byte[,,] mapData, int thicknessX, int heightY, int widthZ, float blockSize)
    {
        if (x != 0) return;
        float worldY = y * blockSize;
        Vector3 pos = new Vector3(0, worldY, z * blockSize);
        List<int> tris = (blockType == 2) ? oreTriangles : dirtTriangles;
        float s = blockSize;
        float totalThickness = thicknessX * s;

        Vector3 up = Vector3.up * s;
        Vector3 forward = Vector3.forward * s;
        Vector3 rightLong = Vector3.right * totalThickness;

        System.Func<int, int, int, bool> isTransparent = (tx, ty, tz) => {
            if (tx < 0 || tx >= thicknessX || ty < 0 || ty >= heightY || tz < 0 || tz >= widthZ) return true;
            return mapData[tx, ty, tz] == 0;
        };

        if (isTransparent(x, y + 1, z)) AddFace(pos + up, forward, rightLong, tris, s, totalThickness);
        if (isTransparent(x, y - 1, z)) AddFace(pos, rightLong, forward, tris, totalThickness, s);
        if (isTransparent(x, y, z + 1)) AddFace(pos + forward, rightLong, up, tris, s, s);
        if (isTransparent(x, y, z - 1)) AddFace(pos, up, rightLong, tris, s, s);
        AddFace(pos + rightLong, up, forward, tris, s, s);
        AddFace(pos, forward, up, tris, s, s);
    }

    void AddFace(Vector3 corner, Vector3 w, Vector3 h, List<int> tris, float width, float height)
    {
        int v = vertices.Count;
        vertices.Add(corner); vertices.Add(corner + w);
        vertices.Add(corner + h); vertices.Add(corner + w + h);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(width, 0));
        uvs.Add(new Vector2(0, height)); uvs.Add(new Vector2(width, height));
        tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
        tris.Add(v + 1); tris.Add(v + 3); tris.Add(v + 2);
    }

    // メッシュとコライダーを更新
    void UpdateMesh()
    {
        mesh.Clear();
        if (vertices.Count == 0)
        {
            mesh.Clear();
            meshCollider.sharedMesh = null;
            return;
        }
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.subMeshCount = 2;
        mesh.SetTriangles(dirtTriangles.ToArray(), 0);
        mesh.SetTriangles(oreTriangles.ToArray(), 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;

        Debug.Log($"{gameObject.name} UpdateMesh完了！頂点数: {vertices.Count}");
    }
}