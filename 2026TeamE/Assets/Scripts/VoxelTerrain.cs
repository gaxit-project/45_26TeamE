using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;
using Unity.VisualScripting;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class VoxelTerrain : MonoBehaviour
{
    public static VoxelTerrain Instance { get; private set; }

    [Header("生成設定")]
    [SerializeField] int thicknessX = 5;
    [SerializeField] int heightY = 20;
    [SerializeField] int widthZ = 30;
    [SerializeField] float blockSize = 1f;
    [Range(0, 100)]
    [SerializeField] float oreProbability = 5f;

    [Header("マテリアル")]
    [SerializeField] Material dirtMaterial;
    [SerializeField] Material oreMaterial;

    [Header("同期オプション")]
    [SerializeField] bool useDeterministicSeed = true;
    [SerializeField] int seed = 12345;

    byte[,,] mapData;
    Mesh mesh;
    List<Vector3> vertices = new List<Vector3>();
    List<int> dirtTriangles = new List<int>();
    List<int> oreTriangles = new List<int>();

    // ブロックが変更されたときのイベント
    public event Action<int, int, byte> OnBlockChanged;

    public enum DigResult
    {
        Empty,
        Dirt,
        Ore
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        GenerateLevel();
        ConstructMesh();
    }

    // --- VoxelTerrain.cs の Update メソッドとして追加 ---
    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    // 判定を 0.1f から 0.5f (ブロックの半分) に変更
                    Vector3 targetPos = hit.point + ray.direction * 0.5f;
                    Vector3 localPos = transform.InverseTransformPoint(targetPos);

                    int x = Mathf.FloorToInt(localPos.x / blockSize);
                    int y = Mathf.FloorToInt(localPos.y / blockSize);
                    int z = Mathf.FloorToInt(localPos.z / blockSize);

                    // ログを出して、どこを叩いているか確認できるようにする
                    Debug.Log($"Hit! 配列座標: ({x}, {y}, {z}) ブロック値: {GetBlock(x, y, z)}");

                    ExecuteDig(x, y, z);
                }
            }
        }
    }

    void GenerateLevel()
    {
        mapData = new byte[thicknessX, heightY, widthZ];
        var rnd = useDeterministicSeed ? new System.Random(seed) : new System.Random();
        for (int x = 0; x < thicknessX; x++)
        {
                for (int y = 0; y < heightY; y++)
                {
                    for(int z = 0; z < widthZ; z++)
                    {
                        mapData[x, y, z] = (rnd.NextDouble() * 100.0 < oreProbability) ? (byte)2 : (byte)1;
                    }
            }
        }
    }
    
    // ローカル入力
    void Dig(int x, int y, int z)
    {
        if(!IsInside(x, y, z)) return;

        if(mapData[x, y, z] == 0) return;

        mapData[x, y, z] = 0;
        OnBlockChanged?.Invoke(x, y, 0);
        ConstructMesh();
    }

    // ブロックを掘る（結果を返す）
    public void ExecuteDig(int x, int y, int z)
    {
        if (!IsInside(x, y, z)) return;
        byte blockType = mapData[x, y, z];
        if (blockType == 0) return; // 空の場合
        if (blockType == 2) { 
            mapData[x, y, z] = 3; 
            OnBlockChanged?.Invoke(x, y, 3);
        }
        else if (blockType == 3) { return; } // 既に掘られた鉱石は無効
        else { 
            mapData[x, y, z] = 0;
            OnBlockChanged?.Invoke(x, y, 0);
        }
        ConstructMesh();
    }

    // 外部からの変更を適用（例：ネットワーク同期）
    public void ApplyRemoteBlockChange(int x, int y, int z, byte blockType, bool suppressEvent = true)
    {
        if (!IsInside(x, y, z)) return;
        if (mapData[x, y, z] == blockType) return;

        mapData[x, y, z] = blockType;
        if(!suppressEvent)
        {
            OnBlockChanged?.Invoke(x, y, blockType);
        }
        ConstructMesh();
    }

    // ブロックを強制的に削除
    public void RemoveBlockForced(int x, int y, int z)
    {
        if(!IsInside(x, y, z)) return;
        if(mapData[x, y, z] == 0) return;
        mapData[x, y, z] = 0;
        ConstructMesh();
    }

    // ブロックを置く
    public void SetBlock(int x, int y, int z, byte blockType)
    {
        if (x < 0 || x >= thicknessX || y < 0 || y >= heightY || z < 0 || z >= widthZ) return;
        if (mapData[x, y, z] != blockType)
        {
            mapData[x, y, z] = blockType;
            ConstructMesh();
        }
    }

    bool IsInside(int x, int y, int z) => x >= 0 && x < thicknessX && y >= 0 && y < heightY && z >= 0 && z < widthZ;

    byte GetBlock(int x, int y, int z) => IsInside(x, y, z) ? mapData[x, y, z] : (byte)0;

    // メッシュの構築
    void ConstructMesh()
    {
        vertices.Clear();
        dirtTriangles.Clear();
        oreTriangles.Clear();

        for (int x = 0; x < thicknessX; x++)
        {
            for (int y = 0; y < heightY; y++)
            {
                for (int z = 0; z < widthZ; z++)
                {
                    if(mapData[x, y, z] != 0)
                    {
                        AddCube(x, y, z, mapData[x, y, z]);
                    }
                }
            }
        }

        UpdateMesh();
    }

    // ブロックの種類に応じて面を追加
    void AddCube(int x, int y, int z, byte blockType)
    {
        Vector3 pos = new Vector3(x, y, z) * blockSize;
        List<int> tris = (blockType == 3) ? oreTriangles : dirtTriangles;

        // 隣が空気なら面を貼る（最適化）
        if (GetBlock(x, y + 1, z) == 0) AddFace(pos + Vector3.up, Vector3.forward, Vector3.right, tris); // 上
        if (GetBlock(x, y - 1, z) == 0) AddFace(pos, Vector3.right, Vector3.forward, tris); // 下
        if (GetBlock(x, y, z + 1) == 0) AddFace(pos + Vector3.forward, Vector3.right, Vector3.up, tris); // 前
        if (GetBlock(x, y, z - 1) == 0) AddFace(pos, Vector3.up, Vector3.right, tris); // 後
        if (GetBlock(x + 1, y, z) == 0) AddFace(pos + Vector3.right, Vector3.up, Vector3.forward, tris); // 右
        if (GetBlock(x - 1, y, z) == 0) AddFace(pos, Vector3.forward, Vector3.up, tris); // 左
    }

    // 面を追加する関数
    void AddFace(Vector3 corner, Vector3 w, Vector3 h, List<int> tris)
    {
        int v = vertices.Count;
        vertices.Add(corner);
        vertices.Add(corner + w);
        vertices.Add(corner + h);
        vertices.Add(corner + w + h);
        tris.Add(v);
        tris.Add(v + 2);
        tris.Add(v + 1);
        tris.Add(v + 1);
        tris.Add(v + 2);
        tris.Add(v + 3);
    }

    // メッシュの更新
    void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.subMeshCount = 2;
        mesh.SetTriangles(dirtTriangles.ToArray(), 0);
        mesh.SetTriangles(oreTriangles.ToArray(), 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshCollider>().sharedMesh = null;
        GetComponent<MeshCollider>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().materials = new Material[] { dirtMaterial, oreMaterial };
    }
}
