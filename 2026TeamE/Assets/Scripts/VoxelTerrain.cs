using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 層ごとの設定
[System.Serializable]
public class ZoneData
{
    public string zoneName = "第1層";
    public int widthZ = 35;
    public int heightChunks = 4;
}

public class VoxelTerrain : MonoBehaviour
{
    public static VoxelTerrain Instance { get; private set; }

    public enum GenerationMode
    {
        Layered,
        Pattern
    }

    public enum BlockType : byte
    {
        Air = 0,
        Dirt = 1,
        Ore = 2,
        Bedrock = 3,
        Stone = 4,
        HardRock = 5,
        Quartzite = 6
    }

    private enum SpawnItemType
    {
        Key,
        Jewel,
        Oxygen,
        LeatherBag,
        GoldLeatherBag,
        Bomb
    }

    [Header("層ごとのサイズ設定")]
    [SerializeField]
    private List<ZoneData> zoneSettings = new List<ZoneData>()
    {
        new ZoneData { zoneName = "1層", widthZ = 35, heightChunks = 4 },
        new ZoneData { zoneName = "2層", widthZ = 55, heightChunks = 6 },
        new ZoneData { zoneName = "3層", widthZ = 75, heightChunks = 8 },
    };

    [Header("ステージ基本サイズ設定")]
    [SerializeField] private int thicknessX = 1;
    [SerializeField] private int maxStageWidthZ = 1000;
    [SerializeField] private float blockSize = 1f;

    [Header("生成設定")]
    [SerializeField] private GenerationMode generationMode = GenerationMode.Layered;
    [SerializeField] private float patternNoiseScale = 0.1f;
    [Range(0, 100)]
    [SerializeField] private float oreProbability = 5f;

    [Header("プレイヤー開始位置設定")]
    [SerializeField] private int startOffsetX = 0;
    [SerializeField] private int startDepthFromSurface = 5;
    [SerializeField] private float startHoleRadius = 8f;
    [SerializeField] private float startShaftRadius = 3.0f;

    [Header("マテリアル")]
    [SerializeField] private Material dirtMaterial;
    [SerializeField] private Material oreMaterial;
    [SerializeField] private Material bedrockMaterial;
    [SerializeField] private Material stoneMaterial;
    [SerializeField] private Material hardRockMaterial;
    [SerializeField] private Material quartziteMaterial;

    [Header("同期オプション")]
    [SerializeField] private bool useDeterministicSeed = true;
    [SerializeField] private int seed = 12345;

    [Header("チャンク設定")]
    [SerializeField] private int chunkSizeY = 16;
    [SerializeField] private GameObject chunkPrefab;

    [Header("1ステージあたりの出現アイテム数")]
    [SerializeField] private int itemsPerStage = 10;

    [Header("アイテムPrefab設定")]
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private GameObject keyPrefab;
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private int bombCount = 20;
    [SerializeField] private GameObject treasureBoxPrefab;
    [SerializeField] private GameObject oxygenPrefab;
    [SerializeField] private GameObject leatherBagPrefab;
    [SerializeField] private GameObject GoldleatherBagPrefab;

    [Header("硬度設定")]
    [SerializeField] private float hardnessScale = 0.5f;

    // 内部データ
    private Chunk[] chunks;
    private HashSet<int> chunksToUpdate = new HashSet<int>();
    private List<GameObject> spawnedTreasures = new List<GameObject>();
    private byte[,,] mapData;
    private int heightY; // 全ゾーンの高さの合計

    private Dictionary<int, int> zoneCollectedKeyCounts = new Dictionary<int, int>();
    private HashSet<int> zoneUnlockedFlags = new HashSet<int>();
    public Dictionary<int, long> zoneInitialGemValues = new Dictionary<int, long>();
    private const long GEM_VALUE = 300000;

    public float BlockSize => blockSize;
    public int ChunkSizeY => chunkSizeY;

    // イベント
    public event Action<int, int, byte> OnBlockChanged;
    public event Action<int, int> OnBlocksDestroyedByPlayer;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name == "02_Main")
        {
            SetActiveAllChildren(true);
        }
        else
        {
            SetActiveAllChildren(false);
        }
    }

    private void SetActiveAllChildren(bool isActive)
    {
        if (TryGetComponent<Collider>(out var col)) col.enabled = isActive;

        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(isActive);
        }
    }

    void Start()
    {
        if (mapData == null)
        {
            CreateStage(maxStageWidthZ, GetTotalHeight(), blockSize);
        }
        StartCoroutine(RestartRoutine());
    }

    private System.Collections.IEnumerator RestartRoutine()
    {
        yield return null;

        if (CheckpointManager.Instance != null && CheckpointManager.Instance.HasCheckpoint())
        {
            RestartFromCheckpoint();
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            RemoveBedrockAroundPlayer();
        }
    }

    #region --- ゾーン & 範囲判定 ---

    /// <summary>
    /// 全ゾーンの深さ（Yブロック数）を合計して算出
    /// </summary>
    public int GetTotalHeight()
    {
        int total = 0;
        foreach (var zone in zoneSettings)
        {
            total += zone.heightChunks * chunkSizeY;
        }
        return total;
    }

    /// <summary>
    /// Y座標から現在の層のインデックスを取得
    /// </summary>
    public int GetRelayID(int y)
    {
        int currentY = heightY;

        for (int i = 0; i < zoneSettings.Count; i++)
        {
            int zoneHeight = zoneSettings[i].heightChunks * chunkSizeY;
            int nextY = currentY - zoneHeight;

            if (y < currentY && y >= nextY)
            {
                return i;
            }
            currentY = nextY;
        }
        return zoneSettings.Count - 1;
    }

    /// <summary>
    /// ブロックが層ごとの有効領域内にあるかチェック
    /// </summary>
    public bool IsInside(int x, int y, int z)
    {
        if (x < 0 || x >= thicknessX) return false;
        if (y < 0 || y >= heightY) return false;

        int zoneIndex = GetRelayID(y);
        ZoneData currentZone = zoneSettings[zoneIndex];

        int centerZ = maxStageWidthZ / 2;
        int halfWidth = currentZone.widthZ / 2;

        int minZ = centerZ - halfWidth;
        int maxZ = centerZ + halfWidth;

        return (z >= minZ && z <= maxZ);
    }

    #endregion

    #region --- ステージ生成 ---

    public void CreateStage(int width, int height, float size)
    {
        heightY = GetTotalHeight();
        maxStageWidthZ = width;
        blockSize = size;
        int totalChunksY = 0;
        foreach (var zone in zoneSettings)
        {
            totalChunksY += zone.heightChunks;
        }
        heightY = totalChunksY * chunkSizeY;
        mapData = new byte[thicknessX, heightY, maxStageWidthZ];
        zoneInitialGemValues.Clear();

        var rnd = useDeterministicSeed ? new System.Random(seed) : new System.Random();
        float layerNoiseScale = 0.2f;
        float layerBumpyIntensity = 12f;

        int startX = 0;
        int startY = heightY - startDepthFromSurface;
        int startZ = maxStageWidthZ / 2;

        int goalThresholdY = 80;

        for (int y = 0; y < heightY; y++)
        {
            for (int x = 0; x < thicknessX; x++)
            {
                for (int z = 0; z < maxStageWidthZ; z++)
                {
                    if (!IsInside(x, y, z))
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                        continue;
                    }

                    float dz = z - startZ;
                    float dy = y - startY;
                    float distSphere = Mathf.Sqrt(dy * dy + dz * dz);

                    if (distSphere < startHoleRadius || (y > startY && Mathf.Abs(dz) < startShaftRadius))
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                        continue;
                    }
                    if (y > heightY - 3)
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                    }
                    else if (y < goalThresholdY)
                    {
                        if (y <= 5) mapData[x, y, z] = (byte)BlockType.Bedrock;
                        else if (y == 6 && z == startZ) mapData[x, y, z] = (byte)BlockType.Stone;
                        else mapData[x, y, z] = (byte)BlockType.Air;
                    }
                    else if (IsRelayZoneBottom(y))
                    {
                        mapData[x, y, z] = (byte)BlockType.Bedrock;
                    }
                    else
                    {
                        if (generationMode == GenerationMode.Layered && rnd.NextDouble() * 100.0 < oreProbability)
                        {
                            mapData[x, y, z] = (byte)BlockType.Ore;
                        }
                        else
                        {
                            float bumpyNoise = Mathf.PerlinNoise(x * layerNoiseScale, z * layerNoiseScale + (seed * 0.1f));
                            float yOffset = (bumpyNoise - 0.5f) * layerBumpyIntensity;
                            float bumpyY = y + yOffset;
                            float depthRatio = bumpyY / heightY;

                            if (generationMode == GenerationMode.Layered)
                            {
                                if (depthRatio < -0.2f) mapData[x, y, z] = (byte)BlockType.Quartzite;
                                else if (depthRatio < 0.2f) mapData[x, y, z] = (byte)BlockType.HardRock;
                                else if (depthRatio < 0.6f) mapData[x, y, z] = (byte)BlockType.Stone;
                                else mapData[x, y, z] = (byte)BlockType.Dirt;
                            }
                            else
                            {
                                float noiseVal = Mathf.PerlinNoise(z * patternNoiseScale + seed * 0.1f, y * patternNoiseScale + seed * 0.1f);
                                if (depthRatio >= 0.6f)
                                {
                                    mapData[x, y, z] = noiseVal > 0.6f ? (byte)BlockType.Stone : (byte)BlockType.Dirt;
                                }
                                else if (depthRatio >= 0.2f)
                                {
                                    if (noiseVal > 0.7f) mapData[x, y, z] = (byte)BlockType.HardRock;
                                    else if (noiseVal > 0.3f) mapData[x, y, z] = (byte)BlockType.Stone;
                                    else mapData[x, y, z] = (byte)BlockType.Dirt;
                                }
                                else if (depthRatio >= -0.2f)
                                {
                                    if (noiseVal > 0.7f) mapData[x, y, z] = (byte)BlockType.Quartzite;
                                    else if (noiseVal > 0.3f) mapData[x, y, z] = (byte)BlockType.HardRock;
                                    else mapData[x, y, z] = (byte)BlockType.Stone;
                                }
                                else
                                {
                                    mapData[x, y, z] = noiseVal < 0.4f ? (byte)BlockType.HardRock : (byte)BlockType.Quartzite;
                                }
                            }
                        }
                    }
                }
            }
        }

        CleanupIsolatedDirtBlocks();

        float offsetX = -(thicknessX * blockSize) / 2f;
        float offsetZ = -(maxStageWidthZ * blockSize) / 2f;
        transform.position = new Vector3(offsetX, -(heightY * blockSize), offsetZ);

        GenerateChunksAndItems(rnd);
        TeleportPlayerToStart(startX, startY, startZ);

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            ClearBlocksAroundPoint(playerObj.transform.position, 4.0f);
        }
    }

    /// <summary>
    /// 最下層かどうか判定する
    /// </summary>
    private bool IsRelayZoneBottom(int y)
    {
        int currentY = heightY;
        for (int i = 0; i < zoneSettings.Count - 1; i++)
        {
            currentY -= zoneSettings[i].heightChunks * chunkSizeY;
            if (y == currentY || y == currentY - 1)
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region --- アイテム & チャンク生成 ---

    private void GenerateChunksAndItems(System.Random rnd)
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        int numChunks = Mathf.CeilToInt((float)heightY / chunkSizeY);
        chunks = new Chunk[numChunks];

        for (int i = 0; i < numChunks; i++)
        {
            GameObject go = Instantiate(chunkPrefab, transform);
            go.name = $"Chunk_{i}";
            go.transform.localPosition = Vector3.zero;
            chunks[i] = go.GetComponent<Chunk>();
            chunks[i].Init(dirtMaterial, oreMaterial, bedrockMaterial, stoneMaterial, hardRockMaterial, quartziteMaterial);
            UpdateChunkMesh(i);
        }

        int currentStageTopY = heightY;
        for (int zIdx = 0; zIdx < zoneSettings.Count; zIdx++)
        {
            int zoneHeight = zoneSettings[zIdx].heightChunks * chunkSizeY;
            int currentStageBottomY = currentStageTopY - zoneHeight;

            if (!zoneInitialGemValues.ContainsKey(zIdx))
            {
                zoneInitialGemValues[zIdx] = 0;
            }

            List<Vector3Int> validPositions = new List<Vector3Int>();
            for (int y = currentStageBottomY; y < currentStageTopY; y++)
            {
                for (int x = 0; x < thicknessX; x++)
                {
                    for (int z = 0; z < maxStageWidthZ; z++)
                    {
                        if (!IsInside(x, y, z)) continue;

                        byte b = mapData[x, y, z];
                        if (b == (byte)BlockType.Dirt || b == (byte)BlockType.Ore || b == (byte)BlockType.Stone || b == (byte)BlockType.HardRock)
                        {
                            validPositions.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }

            if (validPositions.Count > 0)
            {
                for (int i = validPositions.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    var temp = validPositions[i];
                    validPositions[i] = validPositions[j];
                    validPositions[j] = temp;
                }

                int currentValidIndex = 0;
                List<SpawnItemType> tresureSequence = new List<SpawnItemType>();

                for (int i = 0; i < 3; i++) tresureSequence.Add(SpawnItemType.Key);

                SpawnItemType[] normalPool = {
                    SpawnItemType.Oxygen,
                    SpawnItemType.LeatherBag,
                    SpawnItemType.GoldLeatherBag,
                };

                int remainingTresureCount = itemsPerStage - tresureSequence.Count;
                for (int i = 0; i < remainingTresureCount; i++)
                {
                    tresureSequence.Add(normalPool[rnd.Next(normalPool.Length)]);
                }

                int tresureSpawnCount = Mathf.Min(tresureSequence.Count, validPositions.Count);
                for (int i = 0; i < tresureSpawnCount; i++)
                {
                    if (currentValidIndex >= validPositions.Count) break;
                    SpawnItemAt(tresureSequence[i], validPositions[currentValidIndex], zIdx);
                    currentValidIndex++;
                }

                int bombSpawnCount = Mathf.Min(bombCount, validPositions.Count - currentValidIndex);
                for (int i = 0; i < bombSpawnCount; i++)
                {
                    if (currentValidIndex >= validPositions.Count) break;
                    SpawnItemAt(SpawnItemType.Bomb, validPositions[currentValidIndex], zIdx);
                    currentValidIndex++;
                }
            }

            currentStageTopY = currentStageBottomY; ;
        }
    }

    private void SpawnItemAt(SpawnItemType type, Vector3Int coord, int zoneIndex)
    {
        Vector3 pos = transform.position + new Vector3(
            coord.x * blockSize,
            coord.y * blockSize + (blockSize / 2f),
            coord.z * blockSize + (blockSize / 2f)
        );
        Quaternion rotation = Quaternion.Euler(0, -90f, 0);

        if (type == SpawnItemType.Bomb)
        {
            if (bombPrefab != null)
            {
                Instantiate(bombPrefab, pos, rotation, transform);
            }
            return;
        }

        if (treasureBoxPrefab == null) return;

        GameObject box = Instantiate(treasureBoxPrefab, pos, rotation, transform);
        if (box.TryGetComponent<TreasureBoxBehaviour>(out var tb))
        {
            tb.zoneIndex = zoneIndex;
            switch (type)
            {
                case SpawnItemType.Key:
                    tb.contentPrefab = keyPrefab;
                    break;
                case SpawnItemType.Jewel:
                    tb.contentPrefab = treasurePrefab;
                    spawnedTreasures.Add(box);
                    zoneInitialGemValues[zoneIndex] += GEM_VALUE;
                    break;
                case SpawnItemType.Oxygen:
                    tb.contentPrefab = oxygenPrefab;
                    break;
                case SpawnItemType.LeatherBag:
                    tb.contentPrefab = leatherBagPrefab;
                    break;
                case SpawnItemType.GoldLeatherBag:
                    tb.contentPrefab = GoldleatherBagPrefab;
                    break;
            }
        }
    }

    #endregion

    #region --- 掘削 & 操作処理 ---

    public void ExecuteDig(int centerX, int centerY, int centerZ, float radius, Vector3 minLimit, Vector3 maxLimit, bool isPlayerDigging = true)
    {
        int r = Mathf.CeilToInt(radius);
        bool changed = false;
        HashSet<int> changedYRows = new HashSet<int>();
        int destroyedDirt = 0;
        int destroyedOre = 0;

        for (int y = centerY - r; y <= centerY + r; y++)
        {
            for (int z = centerZ - r; z <= centerZ + r; z++)
            {
                float distSq = (centerY - y) * (centerY - y) + (centerZ - z) * (centerZ - z);
                if (distSq > radius * radius) continue;
                if (y < minLimit.y || y > maxLimit.y || z < minLimit.z || z > maxLimit.z) continue;

                bool emittedForThisCell = false;

                for (int x = 0; x < thicknessX; x++)
                {
                    if (!IsInside(x, y, z)) continue;
                    byte currentBlock = mapData[x, y, z];
                    if (currentBlock == (byte)BlockType.Air || currentBlock == (byte)BlockType.Bedrock) continue;

                    if (currentBlock == (byte)BlockType.Dirt)
                    {
                        destroyedDirt++;
                        if (!emittedForThisCell && BlockEffectManager.Instance != null)
                        {
                            Vector3 worldPos = transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize);
                            BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.Dirt);
                            emittedForThisCell = true;
                        }
                    }
                    else if (currentBlock == (byte)BlockType.Ore)
                    {
                        destroyedOre++;
                        if (!emittedForThisCell && BlockEffectManager.Instance != null)
                        {
                            Vector3 worldPos = transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize);
                            BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.Ore);
                            emittedForThisCell = true;
                        }
                    }
                    else if (currentBlock == (byte)BlockType.Stone && !emittedForThisCell && BlockEffectManager.Instance != null)
                    {
                        Vector3 worldPos = transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize);
                        BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.Stone);
                        emittedForThisCell = true;
                    }
                    else if (currentBlock == (byte)BlockType.HardRock && !emittedForThisCell && BlockEffectManager.Instance != null)
                    {
                        Vector3 worldPos = transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize);
                        BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.HardRock);
                        emittedForThisCell = true;
                    }

                    mapData[x, y, z] = 0;
                    changed = true;
                    changedYRows.Add(y);
                }
            }
        }

        if (isPlayerDigging && (destroyedDirt > 0 || destroyedOre > 0))
        {
            OnBlocksDestroyedByPlayer?.Invoke(destroyedDirt, destroyedOre);
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

    public void RemoveBedrockAroundPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        Vector3 localPos = transform.InverseTransformPoint(playerObj.transform.position);
        int py = Mathf.FloorToInt(localPos.y / blockSize);
        int pz = Mathf.FloorToInt(localPos.z / blockSize);
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
                        int cIndex = y / chunkSizeY;
                        chunksToUpdate.Add(cIndex);

                        if (y % chunkSizeY == 0 && cIndex > 0) chunksToUpdate.Add(cIndex - 1);
                        if (y % chunkSizeY == chunkSizeY - 1 && cIndex < chunks.Length - 1) chunksToUpdate.Add(cIndex + 1);
                    }
                }
            }
        }
    }

    public void ClearBlocksAroundPoint(Vector3 worldCenter, float radius)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldCenter);
        int centerX = Mathf.RoundToInt(localPos.x / blockSize);
        int centerY = Mathf.RoundToInt(localPos.y / blockSize);
        int centerZ = Mathf.RoundToInt(localPos.z / blockSize);

        int r = Mathf.CeilToInt(radius / blockSize);

        for (int x = centerX - r; x <= centerX + r; x++)
        {
            for (int y = centerY - r; y <= centerY + r; y++)
            {
                for (int z = centerZ - r; z <= centerZ + r; z++)
                {
                    if (!IsInside(x, y, z)) continue;
                    float distSq = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) + (z - centerZ) * (z - centerZ);
                    if (distSq <= (radius / blockSize) * (radius / blockSize))
                    {
                        if (mapData[x, y, z] != (byte)BlockType.Air)
                        {
                            mapData[x, y, z] = (byte)BlockType.Air;
                            int cIndex = y / chunkSizeY;
                            chunksToUpdate.Add(cIndex);
                            if (y % chunkSizeY == 0 && cIndex > 0) chunksToUpdate.Add(cIndex - 1);
                            if (y % chunkSizeY == chunkSizeY - 1 && cIndex < chunks.Length - 1) chunksToUpdate.Add(cIndex + 1);
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region --- 宝石・アイテム露出判定 ---

    public bool IsJewelExposed(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int x = Mathf.RoundToInt(localPos.x / blockSize);
        int y = Mathf.FloorToInt(localPos.y / blockSize);
        int z = Mathf.FloorToInt(localPos.z / blockSize);

        bool allAir = true;

        if (IsInside(x, y, z) && mapData[x, y, z] != (byte)BlockType.Air && mapData[x, y, z] != (byte)BlockType.Bedrock) allAir = false;

        if (IsInside(x, y + 1, z) && mapData[x, y + 1, z] != (byte)BlockType.Air && mapData[x, y + 1, z] != (byte)BlockType.Bedrock) allAir = false;
        if (IsInside(x, y - 1, z) && mapData[x, y - 1, z] != (byte)BlockType.Air && mapData[x, y - 1, z] != (byte)BlockType.Bedrock) allAir = false;
        if (IsInside(x, y, z + 1) && mapData[x, y, z + 1] != (byte)BlockType.Air && mapData[x, y, z + 1] != (byte)BlockType.Bedrock) allAir = false;
        if (IsInside(x, y, z - 1) && mapData[x, y, z - 1] != (byte)BlockType.Air && mapData[x, y, z - 1] != (byte)BlockType.Bedrock) allAir = false;

        return allAir;
    }

    public bool IsJewelExposed(Vector3 worldPos, Vector3 scale)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int x = Mathf.RoundToInt(localPos.x / blockSize);
        int centerY = Mathf.FloorToInt(localPos.y / blockSize);
        int centerZ = Mathf.FloorToInt(localPos.z / blockSize);

        int extentY = Mathf.CeilToInt(scale.y / 2f);
        int extentZ = Mathf.CeilToInt(scale.z / 2f);

        if (extentY < 1) extentY = 1;
        if (extentZ < 1) extentZ = 1;

        for (int y = centerY - extentY; y <= centerY + extentY; y++)
        {
            for (int z = centerZ - extentZ; z <= centerZ + extentZ; z++)
            {
                if (IsInside(x, y, z) && mapData[x, y, z] != (byte)BlockType.Air && mapData[x, y, z] != (byte)BlockType.Bedrock)
                {
                    return false;
                }
            }
        }
        return true;
    }

    #endregion

    #region --- チェックポイント & ゲーム進行 ---

    public void OnPlayerReachRelayPoint(int y)
    {
        Debug.Log($"中継地点到達. 深度：{y}");
        if (CheckpointManager.Instance == null) return;

        int currentID = GetRelayID(y);

        if (currentID == CheckpointManager.Instance.GetUsedCheckpointID())
        {
            Debug.Log("同じチェックポイントのためスキップ");
            return;
        }

        if (!IsZoneCleared(currentID))
        {
            int currentCount = zoneCollectedKeyCounts.ContainsKey(currentID) ? zoneCollectedKeyCounts[currentID] : 0;
            Debug.Log($"アクセス拒否：ゾーン {currentID} の鍵が足りません ({currentCount}/3)");
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Vector3 checkpointPos = player.transform.position;
        checkpointPos.y -= (blockSize * 5f);

        CheckpointManager.Instance.SaveCheckpoint(checkpointPos, currentID);

        if (TryGetComponent<SelectPoint>(out var selectPoint))
        {
            selectPoint.ShowButton();
        }
    }

    public void CollectedKey(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int blockY = Mathf.Clamp(Mathf.FloorToInt(localPos.y / blockSize), 0, heightY - 1);
        int zoneIndex = GetRelayID(blockY);

        CollectedKeyDirect(zoneIndex, worldPos);
        Debug.Log($"<color=yellow>[鍵獲得]</color> 深度: {blockY} (ゾーン: {zoneIndex}) | 現在の鍵: {zoneCollectedKeyCounts[zoneIndex]} / 3個");
    }

    public void CollectedKeyDirect(int zoneIndex, Vector3 worldPos)
    {
        if (!zoneCollectedKeyCounts.ContainsKey(zoneIndex))
        {
            zoneCollectedKeyCounts[zoneIndex] = 0;
        }
        zoneCollectedKeyCounts[zoneIndex]++;

        if (KeyUIController.Instance != null)
        {
            KeyUIController.Instance.FlyAndUpdateKeyUI(zoneCollectedKeyCounts[zoneIndex], worldPos);
        }
    }

    public bool IsZoneCleared(int zoneIndex)
    {
        const int REQUIRED_KEYS = 3;
        if (zoneCollectedKeyCounts.TryGetValue(zoneIndex, out int count))
        {
            return count >= REQUIRED_KEYS;
        }
        return false;
    }

    public void RestartFromCheckpoint()
    {
        if (CheckpointManager.Instance == null) return;

        Vector3 lastPos = CheckpointManager.Instance.GetLastCheckpoint();
        GameObject player = GameObject.FindWithTag("Player");

        if (player != null)
        {
            player.transform.position = lastPos;
            ClearBlocksAroundPoint(lastPos, 4.0f);

            CheckpointManager.Instance.MarkCheckpointAsUsed();
        }
    }

    public void ResetRuntime(bool regenerateStage = false)
    {
        chunksToUpdate?.Clear();
        zoneCollectedKeyCounts?.Clear();
        zoneUnlockedFlags?.Clear();
        zoneInitialGemValues?.Clear();
        if (spawnedTreasures != null)
        {
            for (int i = spawnedTreasures.Count - 1; i >= 0; i--)
            {
                var go = spawnedTreasures[i];
                if (go != null) Destroy(go);
            }
            spawnedTreasures.Clear();
        }
        if (regenerateStage)
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            CreateStage(maxStageWidthZ, GetTotalHeight(), blockSize);
        }
        else
        {
            if (chunks != null)
            {
                for (int i = 0; i < chunks.Length; i++)
                {
                    UpdateChunkMesh(i);
                }
            }
        }
    }

    private void TeleportPlayerToStart(int x, int y, int z)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Vector3 worldPos = transform.position + new Vector3(x * blockSize, y * blockSize + 1.5f, z * blockSize);
            player.transform.position = worldPos;
        }
    }

    #endregion

    #region --- 補助機能 & メッシュ更新 ---

    public void UpdateChunkMesh(int index)
    {
        if (chunks == null || index < 0 || index >= chunks.Length) return;
        int startY = index * chunkSizeY;
        int endY = Mathf.Min(startY + chunkSizeY, heightY);
        chunks[index].RebuildMesh(mapData, startY, endY, thicknessX, heightY, maxStageWidthZ, blockSize);
    }

    private void LateUpdate()
    {
        if (chunksToUpdate.Count > 0)
        {
            foreach (int index in chunksToUpdate)
            {
                UpdateChunkMesh(index);
            }
            chunksToUpdate.Clear();
        }
    }

    public long GetZoneInitialGemValue(int py)
    {
        int zoneIndex = GetRelayID(py);
        if (zoneInitialGemValues.ContainsKey(zoneIndex))
        {
            return zoneInitialGemValues[zoneIndex];
        }
        return 0;
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

        if ((BlockType)blockType == BlockType.Bedrock) return float.MaxValue;

        float baseHardness = (BlockType)blockType switch
        {
            BlockType.Dirt => 1.0f,
            BlockType.Ore => 6.0f,
            BlockType.Stone => 10.0f,
            BlockType.HardRock => 20.0f,
            BlockType.Quartzite => 40.0f,
            _ => 1.0f
        };

        float depthFactor = (heightY - y) * hardnessScale * 0.05f;
        return baseHardness + depthFactor;
    }

    private static readonly int[] dx = { 1, -1, 0, 0, 0, 0 };
    private static readonly int[] dy = { 0, 0, 1, -1, 0, 0 };
    private static readonly int[] dz = { 0, 0, 0, 0, 1, -1 };

    private void CleanupIsolatedDirtBlocks()
    {
        if (mapData == null) return;

        int sizeX = mapData.GetLength(0);
        int sizeY = mapData.GetLength(1);
        int sizeZ = mapData.GetLength(2);

        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (mapData[x, y, z] == (byte)BlockType.Air) continue;

                    bool hasNeighbor = false;

                    int[] dx = { 1, -1, 0, 0, 0, 0 };
                    int[] dy = { 0, 0, 1, -1, 0, 0 };
                    int[] dz = { 0, 0, 0, 0, 1, -1 };

                    for (int i = 0; i < 6; i++)
                    {
                        int nx = x + dx[i];
                        int ny = y + dy[i];
                        int nz = z + dz[i];

                        if (nx >= 0 && nx < sizeX && ny >= 0 && ny < sizeY && nz >= 0 && nz < sizeZ)
                        {
                            if (mapData[nx, ny, nz] != (byte)BlockType.Air)
                            {
                                hasNeighbor = true;
                                break;
                            }
                        }
                    }
                    if (!hasNeighbor)
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 鍵宝箱が未獲得で破壊された場合に、同じゾーンの別の土ブロックに再生成する
    /// </summary>
    public void RespawnKeyTreasureBox(Vector3 destroyedPos, int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= zoneSettings.Count) return;

        int zoneHeight = zoneSettings[zoneIndex].heightChunks * chunkSizeY;
        int currentStageBottomY = 0;
        for (int i = 0; i < zoneIndex; i++)
        {
            currentStageBottomY += zoneSettings[i].heightChunks * chunkSizeY;
        }
        int currentStageTopY = currentStageBottomY + zoneHeight;

        List<Vector3Int> validPositions = new List<Vector3Int>();
        for (int y = currentStageBottomY; y < currentStageTopY; y++)
        {
            for (int x = 0; x < thicknessX; x++)
            {
                for (int z = 0; z < maxStageWidthZ; z++)
                {
                    if (!IsInside(x, y, z)) continue;

                    byte b = mapData[x, y, z];
                    if (b == (byte)BlockType.Dirt || b == (byte)BlockType.Ore || b == (byte)BlockType.Stone || b == (byte)BlockType.HardRock)
                    {
                        validPositions.Add(new Vector3Int(x, y, z));
                    }
                }
            }
        }

        if (validPositions.Count > 0)
        {
            var rnd = new System.Random();
            Vector3Int targetCoord = validPositions[rnd.Next(validPositions.Count)];

            // 鍵のタイプで宝箱を再生成
            SpawnItemAt(SpawnItemType.Key, targetCoord, zoneIndex);
            Debug.Log($"<color=orange>[鍵リスポーン]</color> ゾーン {zoneIndex} の空きブロック ({targetCoord.x}, {targetCoord.y}, {targetCoord.z}) に再配置しました。");
        }
    }

    #endregion
}