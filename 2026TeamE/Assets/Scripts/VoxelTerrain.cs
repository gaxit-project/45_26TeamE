using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
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
    [SerializeField] Material stoneMaterial;
    [SerializeField] Material hardRockMaterial;

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
    public int ChunkSizeY => chunkSizeY;

    // ブロックが変更されたときのイベント
    public event Action<int, int, byte> OnBlockChanged;

    public enum BlockType : byte
    {
        Air = 0,
        Dirt = 1,
        Ore = 2,
        Bedrock = 3,
        Stone = 4,
        HardRock = 5
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
        CreateStage(widthZ, heightY, blockSize);
    }

    /*void GenerateLevel()
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
    }*/

    void Update()
    {
        // デバッグ用：1キーが押されたら周囲の中継地点を消去
        if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            RemoveBedrockAroundPlayer();
        }
    }

    // ブロックを掘る
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

    public void CreateStage(int width, int height, float size)
    {
        widthZ = width;
        heightY = height;
        blockSize = size;
        mapData = new byte[thicknessX, heightY, widthZ];

        var rnd = useDeterministicSeed ? new System.Random(seed) : new System.Random();
        float layerNoiseScale = 0.2f; // 境界のガタガタ具合
        float layerBumpyIntensity = 12f; // 境界のガタガタの流れの強さ
        for (int y = 0; y < heightY; y++)
        {
            for (int x = 0; x < thicknessX; x++)
            {
                for (int z = 0; z < widthZ; z++)
                {
                    float scale = 0.8f;
                    float noise = Mathf.PerlinNoise(x * scale, y * scale + (seed * 0.1f));

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
                        if (rnd.NextDouble() * 100.0 < oreProbability)
                        {
                            mapData[x, y, z] = (byte)BlockType.Ore;
                        }
                        else
                        {
                            float bumpyNoise = Mathf.PerlinNoise(x * layerNoiseScale, z * layerNoiseScale + (seed * 0.1f));
                            float yOffset = (bumpyNoise - 0.5f) * layerBumpyIntensity;
                            float bumpyY = y + yOffset;
                            float depthRatio = bumpyY / heightY;

                            if (depthRatio < 0.2f)
                            {
                                mapData[x, y, z] = (byte)BlockType.HardRock;
                            }
                            else if (depthRatio < 0.6f)
                            {
                                mapData[x, y, z] = (byte)BlockType.Stone;
                            }
                            else
                            {
                                mapData[x, y, z] = (byte)BlockType.Dirt;
                            }
                        }
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
            chunks[i].Init(dirtMaterial, oreMaterial, bedrockMaterial, stoneMaterial, hardRockMaterial);

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

    public float GetHardnessAtPosition(int x, int y, int z)
    {
        if (!IsInside(x, y, z)) return 1.0f;
        byte blockType = mapData[x, y, z];
        float baseHardness = 1.0f;

        // ブロックに応じた硬度設定
        switch ((BlockType)blockType)
        {
            case BlockType.Dirt:
                baseHardness = 1.0f;
                break;
            case BlockType.Ore:
                baseHardness = 3.0f;
                break;
            case BlockType.Bedrock:
                baseHardness = float.MaxValue;
                break;
            case BlockType.Stone:
                baseHardness = 5.0f;
                break;
            case BlockType.HardRock:
                baseHardness = 10.0f;
                break;
            default:
                baseHardness = 1.0f;
                break;
        }
        // 深さに応じて硬さを増加させる
        float depthFactor = (heightY - y) * hardnessScale * 0.05f;
        return baseHardness + depthFactor;
    }

    public void OnPlayerReachRelayPoint(int y)
    {
        Debug.Log($"中継地点到達.深度：{y}");
    }

    // デバッグ用：プレイヤー周辺の中継地点を削除
    private void RemoveBedrockAroundPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        Vector3 localPos = transform.InverseTransformPoint(playerObj.transform.position);
        int px = Mathf.FloorToInt(localPos.x / blockSize);
        int py = Mathf.FloorToInt(localPos.y / blockSize);
        int pz = Mathf.FloorToInt(localPos.z / blockSize);

        bool changed = false;
        int searchRange = 5;

        for (int x = 0; x < thicknessX; x++)
        {
            for (int y = py - 2; y <= py + 2; y++)
            {
                for (int z = pz - searchRange; z <= pz + searchRange; z++)
                {
                    if (!IsInside(x, y, z)) continue;

                    if (mapData[x, y, z] == (byte)BlockType.Bedrock)
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                        changed = true;

                        int cIndex = y / chunkSizeY;
                        chunksToUpdate.Add(cIndex);
                        if (y % chunkSizeY == 0 && cIndex > 0) chunksToUpdate.Add(cIndex - 1);
                        if (y % chunkSizeY == chunkSizeY - 1 && cIndex < chunks.Length - 1) chunksToUpdate.Add(cIndex + 1);
                    }
                }
            }
        }

        if (changed)
        {
            Debug.Log("デバッグ：周囲の中継地点を削除しました。");
        }
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
