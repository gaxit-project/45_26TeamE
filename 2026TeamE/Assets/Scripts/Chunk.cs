using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    Mesh mesh;
    MeshFilter meshFilter;
    MeshCollider meshCollider;
    MeshRenderer meshRenderer;

    List<Vector3> vertices = new List<Vector3>();
    List<Vector2> uvs = new List<Vector2>();
    List<int> dirtTriangles = new List<int>();
    List<int> oreTriangles = new List<int>();
    List<int> bedrockTriangles = new List<int>();
    List<int> stoneTriangles = new List<int>();
    List<int> hardRockTriangles = new List<int>();
    List<int> quartziteTriangles = new List<int>();
    List<int> boundaryTriangles = new List<int>();
    const int SUBMESH_COUNT = 7;

    public void Init(Material dirtMat, Material oreMat, Material bedrockMat, Material stoneMat, Material hardRockMat, Material quartziteMat, Material boundaryMat)
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshFilter.mesh = mesh;
        meshRenderer.materials = new Material[] { dirtMat, oreMat, bedrockMat, stoneMat, hardRockMat, quartziteMat, boundaryMat };
    }

    public void RebuildMesh(byte[,,] mapData, int startY, int endY, int thicknessX, int heightY, int widthZ, float blockSize)
    {
        vertices.Clear();
        uvs.Clear();
        dirtTriangles.Clear();
        oreTriangles.Clear();
        bedrockTriangles.Clear();
        stoneTriangles.Clear();
        hardRockTriangles.Clear();
        quartziteTriangles.Clear();
        boundaryTriangles.Clear();

        for (int x = 0; x < thicknessX; x++)
        {
            for (int y = startY; y < endY; y++)
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
        float worldY = y * blockSize;
        Vector3 pos = new Vector3(x * blockSize, worldY, z * blockSize);

        List<int> tris;
        switch ((VoxelTerrain.BlockType)blockType)
        {
            case VoxelTerrain.BlockType.Bedrock:
                tris = bedrockTriangles;
                break;
            case VoxelTerrain.BlockType.Ore:
                tris = oreTriangles;
                break;
            case VoxelTerrain.BlockType.Stone:
                tris = stoneTriangles;
                break;
            case VoxelTerrain.BlockType.HardRock:
                tris = hardRockTriangles;
                break;
            case VoxelTerrain.BlockType.Quartzite:
                tris = quartziteTriangles;
                break;
            case VoxelTerrain.BlockType.Boundary:
                tris = boundaryTriangles;
                break;
            case VoxelTerrain.BlockType.Dirt:
            default:
                tris = dirtTriangles;
                break;
        }

        float s = blockSize;
        float totalThickness = s;

        Vector3 up = Vector3.up * s;
        Vector3 forward = Vector3.forward * s;
        Vector3 right = Vector3.right * s;

        bool IsTransparent(int tx, int ty, int tz) {
            if (tx < 0 || tx >= thicknessX || ty < 0 || ty >= heightY || tz < 0 || tz >= widthZ) return true;
            return mapData[tx, ty, tz] == 0;
        }

        if (IsTransparent(x, y + 1, z)) AddFace(pos + up, forward, right, tris, s, s);
        if (IsTransparent(x, y - 1, z)) AddFace(pos, right, forward, tris, s, s);
        if (IsTransparent(x, y, z + 1)) AddFace(pos + forward, right, up, tris, s, s);
        if (IsTransparent(x, y, z - 1)) AddFace(pos, up, right, tris, s, s);
        if (IsTransparent(x + 1, y, z)) AddFace(pos + right, up, forward, tris, s, s);
        if (IsTransparent(x - 1, y, z)) AddFace(pos, forward, up, tris, s, s);
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

    void UpdateMesh()
    {
        mesh.Clear();
        if (vertices.Count == 0)
        {
            mesh.Clear();
            meshCollider.sharedMesh = null;
            return;
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);

        mesh.subMeshCount = SUBMESH_COUNT; // マテリアルの数を変えたらここも忘れずに変える！

        mesh.SetTriangles(dirtTriangles, 0);
        mesh.SetTriangles(oreTriangles, 1);
        mesh.SetTriangles(bedrockTriangles, 2);
        mesh.SetTriangles(stoneTriangles, 3);
        mesh.SetTriangles(hardRockTriangles, 4);
        mesh.SetTriangles(quartziteTriangles, 5);
        mesh.SetTriangles(boundaryTriangles, 6);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }
}