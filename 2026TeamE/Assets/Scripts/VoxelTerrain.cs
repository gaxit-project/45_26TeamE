using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class VoxelTerrain : MonoBehaviour
{
    public static VoxelTerrain Instance { get; private set; }

    [Header("生成設定")]
    [SerializeField] int width = 50;
    [SerializeField] int height = 50;
    [SerializeField] float blockThickness = 2.0f;
    [Range(0, 100)]
    [SerializeField] float oreProbability = 5f;

    [Header("同期オプション")]
    [SerializeField] bool useDeterministicSeed = true;
    [SerializeField] int seed = 12345;

    byte[,] mapData;
    Mesh mesh;
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();

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
            // マウス位置の取得
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            // カメラからRayを飛ばす
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    // 少し奥を判定するコツ
                    Vector3 localPos = transform.InverseTransformPoint(hit.point + ray.direction * 0.1f);
                    int x = Mathf.FloorToInt(localPos.z);
                    int y = Mathf.FloorToInt(localPos.y);

                    DigResult result = ExecuteDig(x, y);
                    Debug.Log($"掘削テスト: ({x}, {y}) 結果: {result}");
                }
            }
        }
    }

    void GenerateLevel()
    {
        mapData = new byte[width, height];
        var rnd = useDeterministicSeed ? new System.Random(seed) : new System.Random();
        for (int y = 0; y < height; y++)
        {
            for(int x = 0; x < width; x++)
            {
                mapData[x, y] = (rnd.NextDouble() * 100.0 < oreProbability) ? (byte)2 : (byte)1;
            }
        }
    }

    // ローカル入力
    void Dig(int x, int y)
    {
        if(!IsInside(x, y)) return;
        if(mapData[x, y] == 0) return;

        mapData[x, y] = 0;
        OnBlockChanged?.Invoke(x, y, 0);
        ConstructMesh();
    }

    // ブロックを掘る（結果を返す）
    public DigResult ExecuteDig(int x, int y)
    {
        if (!IsInside(x, y)) return DigResult.Empty;
        byte blockType = mapData[x, y];

        if(blockType == 0) return DigResult.Empty; // 空の場合
        if (blockType == 2) return DigResult.Ore; // 鉱石の場合

        // 土の場合
        mapData[x, y] = 0;
        OnBlockChanged?.Invoke(x, y, 0);
        ConstructMesh();
        return DigResult.Dirt;
    }

    // 外部からの変更を適用（例：ネットワーク同期）
    public void ApplyRemoteBlockChange(int x, int y, byte blockType, bool suppressEvent = true)
    {
        if (!IsInside(x, y)) return;
        if (mapData[x, y] == blockType) return;

        mapData[x, y] = blockType;
        if(!suppressEvent)
        {
            OnBlockChanged?.Invoke(x, y, blockType);
        }
        ConstructMesh();
    }

    // ブロックを強制的に削除
    public void RemoveBlockForced(int x, int y)
    {
        if(!IsInside(x, y)) return;
        if(mapData[x, y] == 0) return;

        mapData[x, y] = 0;
        OnBlockChanged?.Invoke(x, y, 0);
        ConstructMesh();
    }

    // ブロックを置く
    public void SetBlock(int x, int y, byte blockType)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        if (mapData[x, y] != blockType)
        {
            mapData[x, y] = blockType;
            OnBlockChanged?.Invoke(x, y, blockType);
            ConstructMesh();
        }
    }

    // 座標がマップ内にあるかどうかをチェック
    bool IsInside(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // メッシュの構築
    void ConstructMesh()
    {
        vertices.Clear();
        triangles.Clear();

        for(int y = 0; y < height; y++)
        {
            for(int x = 0; x < width; x++)
            {
                if(mapData[x, y] != 0)
                {
                    AddVoxelFace(x, y);
                }
            }
        }

        UpdateMesh();
    }

    // ボクセルの面を追加
    void AddVoxelFace(int x, int y)
    {
        float depthX = 0f;
        float size = 1f;

        int vIndex = vertices.Count;

        // 四角形の頂点を追加
        vertices.Add(new Vector3(depthX, y, x * size)); // 左下
        vertices.Add(new Vector3(depthX + size, y, (x + 1) * size)); // 右下
        vertices.Add(new Vector3(depthX, y + size, x * size)); // 左上
        vertices.Add(new Vector3(depthX + size, y + size, (x + 1) * size)); // 右上
         
        // 四角形を構成する2つの三角形のインデックスを追加
        triangles.Add(vIndex + 0);
        triangles.Add(vIndex + 2);
        triangles.Add(vIndex + 1);

        triangles.Add(vIndex + 1);
        triangles.Add(vIndex + 2);
        triangles.Add(vIndex + 3);
    }

    // メッシュの更新
    void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices.ToArray(); // 頂点配列をメッシュに設定
        mesh.triangles = triangles.ToArray(); // 三角形インデックス配列をメッシュに設定
        mesh.RecalculateNormals(); // 法線を再計算して光の当たり方を正しくする

        GetComponent<MeshCollider>().sharedMesh = mesh;
    }
}
