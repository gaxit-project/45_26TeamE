using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.EventSystems.EventTrigger;

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
    List<Vector2> uvs = new List<Vector2>();
    List<int> dirtTriangles = new List<int>();
    List<int> oreTriangles = new List<int>();

    public float BlockSize => blockSize;

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
    public void ExecuteDig(int centerX, int centerY, int centerZ, float radius, Vector3 minLimit, Vector3 maxLimit)
    {
        // 半径をセル数に換算
        int r = Mathf.CeilToInt(radius);
        bool changed = false;

        // 球体の影響範囲（中心から半径rの立方体範囲）をすべてループ
        for (int x = 0; x < thicknessX; x++)
        {
            for (int y = centerY - r; y <= centerY + r; y++)
            {
                for (int z = centerZ - r; z <= centerZ + r; z++)
                {
                    if (!IsInside(x, y, z)) continue;

                    if (mapData[x, y, z] == 0) continue;

                    float distSq = (centerY - y) * (centerY - y) + (centerZ - z) * (centerZ - z);
                    if (distSq <= radius * radius)
                    {
                        if (y >= minLimit.y && y <= maxLimit.y &&
                            z >= minLimit.z && z <= maxLimit.z)
                        {
                            mapData[x, y, z] = 0;
                            changed = true;
                        }
                    }
                }
            }
        }

        if (changed)
        {
            ConstructMesh();
        }
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
        uvs.Clear();
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
        if (x != 0) return;

        Vector3 pos = new Vector3(0, y, z) * blockSize;
        List<int> tris = (blockType == 3) ? oreTriangles : dirtTriangles;
        float s = blockSize;

        // 長さ（厚み）を計算
        float totalThickness = thicknessX * s;

        // 各方向のベクトル
        Vector3 up = Vector3.up * s;
        Vector3 forward = Vector3.forward * s;
        Vector3 rightLong = Vector3.right * totalThickness;

        // 【重要】各面のサイズを指定してAddFaceを呼ぶ

        // 上面（サイズ: 1 x totalThickness）
        AddFace(pos + up, forward, rightLong, tris, s, totalThickness);
        // 下面（サイズ: totalThickness x 1）
        AddFace(pos, rightLong, forward, tris, totalThickness, s);
        // 正面（サイズ: 1 x 1）
        AddFace(pos + forward, rightLong, up, tris, s, s);
        // 背面（サイズ: 1 x 1）
        AddFace(pos, up, rightLong, tris, s, s);
        // 右端面（サイズ: 1 x 1）
        AddFace(pos + rightLong, up, forward, tris, s, s);
        // 左端面（サイズ: 1 x 1）
        AddFace(pos, forward, up, tris, s, s);
    }

    // 面を追加する関数
    void AddFace(Vector3 corner, Vector3 w, Vector3 h, List<int> tris, float width, float height)
    {
        int v = vertices.Count;
        vertices.Add(corner);
        vertices.Add(corner + w);
        vertices.Add(corner + h);
        vertices.Add(corner + w + h);

        // --- 【重要】UV座標（テクスチャの地図）を計算 ---
        // 面のサイズ（width, height）に合わせて、UVを(0,0)から(width, height)まで割り当てる
        // これにより、テクスチャがリピート（タイル状に並ぶ）されます
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(width, 0));
        uvs.Add(new Vector2(0, height));
        uvs.Add(new Vector2(width, height));

        // 三角形の生成（表裏反転対応済み）
        tris.Add(v);
        tris.Add(v + 1);
        tris.Add(v + 2);

        tris.Add(v + 1);
        tris.Add(v + 3);
        tris.Add(v + 2);
    }

    // メッシュの更新
    void UpdateMesh()
    {
        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();

        // --- 【重要】UVをメッシュにセット ---
        mesh.uv = uvs.ToArray();

        mesh.subMeshCount = 2;
        mesh.SetTriangles(dirtTriangles.ToArray(), 0);
        mesh.SetTriangles(oreTriangles.ToArray(), 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // 物理判定の更新
        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.cookingOptions = MeshColliderCookingOptions.UseFastMidphase |
                                MeshColliderCookingOptions.WeldColocatedVertices;
            mc.sharedMesh = null;
            mc.sharedMesh = mesh;
        }
    }

    // ステージの生成（外部から呼び出す用）
    public void CreateStage(int width, int height, float size)
    {
        widthZ = width;
        heightY = height;
        blockSize = size;

        mapData = new byte[thicknessX, heightY, widthZ];

        var rnd = useDeterministicSeed ? new System.Random(seed) : new System.Random();
        for (int x = 0; x < thicknessX; x++)
        {
            for (int y = 0; y < heightY; y++)
            {
                for (int z = 0; z < widthZ; z++)
                {
                    if (y > heightY - 3)
                        mapData[x, y, z] = 0;
                    else
                        mapData[x, y, z] = (rnd.NextDouble() * 100.0 < oreProbability) ? (byte)2 : (byte)1;
                }
            }
        }

        ConstructMesh();
        transform.position = new Vector3(0, -(heightY * blockSize), 0);
    }
}
