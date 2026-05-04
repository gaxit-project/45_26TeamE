using System;
using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] Material bedrockMaterial;

    [Header("同期オプション")]
    [SerializeField] bool useDeterministicSeed = true;
    [SerializeField] int seed = 12345;

    [Header("チャンク設定")]
    [SerializeField] int chunkSizeY = 16;
    [SerializeField] GameObject chunkPrefab;

    [Header("宝石設定")]
    [SerializeField] GameObject treasurePrefab;
    [SerializeField] float baseTreasureChance = 1f;

    [Header("硬度設定")]
    [SerializeField] float hardnessScale = 0.5f;


    private Chunk[] chunks;
    private HashSet<int> chunksToUpdate = new HashSet<int>();
    private List<GameObject> spawnedTreasures = new List<GameObject>();
    byte[,,] mapData;

    public float BlockSize => blockSize;

    // ブロックが変更されたときのイベント
    public event Action<int, int, byte> OnBlockChanged;

    public enum BlockType : byte
    {
        Air = 0,
        Dirt = 1,
        Ore = 2,
        Bedrock = 3
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
        GenerateLevel();
        CreateStage(widthZ, heightY, blockSize);
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

    // ブロックを掘る（結果を返す）
    public void ExecuteDig(int centerX, int centerY, int centerZ, float radius, Vector3 minLimit, Vector3 maxLimit)
    {
        int r = Mathf.CeilToInt(radius);
        bool changed = false;
        HashSet<int> changedYRows = new HashSet<int>();

        for (int x = 0; x < thicknessX; x++)
        {
            for (int y = centerY - r; y <= centerY + r; y++)
            {
                for (int z = centerZ - r; z <= centerZ + r; z++)
                {
                    if (!IsInside(x, y, z)) continue;
                    if (mapData[x, y, z] == (byte)BlockType.Air || mapData[x, y, z] == (byte)BlockType.Bedrock) continue;

                    float distSq = (centerY - y) * (centerY - y) + (centerZ - z) * (centerZ - z);
                    if (distSq <= radius * radius)
                    {
                        if (y >= minLimit.y && y <= maxLimit.y &&
                            z >= minLimit.z && z <= maxLimit.z)
                        {
                            mapData[x, y, z] = 0;
                            changed = true;
                            changedYRows.Add(y);
                        }
                    }
                }
            }
        }

        if (changed)
        {
            foreach (var yIndex in changedYRows)
            {
                int cIndex = yIndex / chunkSizeY;
                if (chunks == null || cIndex < 0 || cIndex >= chunks.Length) continue;
                chunksToUpdate.Add(cIndex);

                if (yIndex % chunkSizeY == 0 && cIndex > 0)
                    chunksToUpdate.Add(cIndex - 1);
                if (yIndex % chunkSizeY == chunkSizeY - 1 && cIndex < chunks.Length - 1)
                    chunksToUpdate.Add(cIndex + 1);
            }
        }
    }

    // ブロックを強制的に削除
    public void RemoveBlockForced(int x, int y, int z)
    {
        if(!IsInside(x, y, z)) return;
        if(mapData[x, y, z] == 0) return;
        mapData[x, y, z] = 0;
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
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                    }
                    else if (y > 0 && y % (chunkSizeY * 10) == 0)
                    {
                        mapData[x, y, z] = (byte)BlockType.Bedrock;
                    }
                    else
                    {
                        mapData[x, y, z] = (rnd.NextDouble() * 100.0 < oreProbability) ? (byte)BlockType.Ore : (byte)BlockType.Dirt;
                    }
                }
            }
        }

        float offsetX = -(thicknessX * blockSize) / 2f;
        float offsetZ = -(widthZ * blockSize) / 2f;

        transform.position = new Vector3(offsetX, -(heightY * blockSize), offsetZ);
        GenerateChunks();
    }

    void GenerateChunks()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);

        int numChunks = Mathf.CeilToInt((float)heightY / chunkSizeY);
        chunks = new Chunk[numChunks];

        for (int i = 0; i < numChunks; i++)
        {
            GameObject go = Instantiate(chunkPrefab, transform);
            go.name = $"Chunk_{i}";
            go.transform.localPosition = new Vector3(0, 0, 0);
            chunks[i] = go.GetComponent<Chunk>();
            chunks[i].Init(dirtMaterial, oreMaterial, bedrockMaterial);

            UpdateChunkMesh(i);
            float depthFactor = (float)(numChunks - i);
            float finalProbability = baseTreasureChance * depthFactor;

            TrySpawnJewelsInChunk(i, finalProbability);
        }
    }

    public void UpdateChunkMesh(int index)
    {
        if (chunks == null || index < 0 || index >= chunks.Length) return;
        int startY = index * chunkSizeY;
        int endY = Mathf.Min(startY + chunkSizeY, heightY);
        chunks[index].RebuildMesh(mapData, startY, endY, thicknessX, heightY, widthZ, blockSize);
    }

    private void TrySpawnJewelsInChunk(int chunkIndex, float spawnChance)
    {
        int startY = chunkIndex * chunkSizeY;
        int endY = Mathf.Min(startY + chunkSizeY, heightY);

        for (int t = 0; t < 3; t++)
        {
            if (UnityEngine.Random.Range(0f, 100f) < spawnChance)
            {
                int rx = UnityEngine.Random.Range(0, thicknessX);
                int ry = UnityEngine.Random.Range(startY, endY);
                int rz = UnityEngine.Random.Range(0, widthZ);

                if (mapData[rx, ry, rz] == 1)
                {
                    Vector3 pos = transform.position + new Vector3(
                        rx * blockSize,
                        ry * blockSize + (blockSize / 2f),
                        rz * blockSize + (blockSize / 2f)
                    );
                    Quaternion rotation = Quaternion.Euler(0, 90f,0);

                    GameObject jewel = Instantiate(treasurePrefab, pos, rotation, transform);
                    spawnedTreasures.Add(jewel);
                }
            }
        }
    }

    public float GetHardnessAtDepth(int y)
    {
        float depth = heightY - y;
        return 1.0f + Mathf.Max(0, depth * hardnessScale * 0.1f);
    }

    public void OnPlayerReachRelayPoint(int y)
    {
        Debug.Log($"中継地点到達.深度：{y}");
    }

    void LateUpdate()
    {
        if (chunksToUpdate.Count > 0)
        {
            foreach (int index in chunksToUpdate) UpdateChunkMesh(index);
            chunksToUpdate.Clear();
        }
    }

    bool IsInside(int x, int y, int z) => x >= 0 && x < thicknessX && y >= 0 && y < heightY && z >= 0 && z < widthZ;
}
