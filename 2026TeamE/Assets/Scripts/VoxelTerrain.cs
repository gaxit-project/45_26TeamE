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

    [Header("プレイヤー開始位置設定")]
    [SerializeField] int startOffsetX = 0;
    [SerializeField] int startDepthFromSurface = 30;
    [SerializeField] float startHoleRadius = 8f;
    [SerializeField] float startShaftRadius = 3.0f;

    [Header("マテリアル")]
    [SerializeField] Material dirtMaterial;
    [SerializeField] Material oreMaterial;
    [SerializeField] Material bedrockMaterial;
    [SerializeField] Material stoneMaterial;
    [SerializeField] Material hardRockMaterial;
    [SerializeField] Material quartziteMaterial; // 珪岩(クォーツァイト)

    [Header("同期オプション")]
    [SerializeField] bool useDeterministicSeed = true;
    [SerializeField] int seed = 12345;

    [Header("チャンク設定")]
    [SerializeField] int chunkSizeY = 16;
    [SerializeField] GameObject chunkPrefab;

    [Header("宝石設定")]
    [SerializeField] GameObject treasurePrefab;
    [SerializeField] float baseTreasureChance = 1f;

    [Header("爆弾設定")]
    [SerializeField] GameObject bombPrefab;
    [SerializeField] float bombSpawnRatio = 0.2f;

    [Header("特殊セット設定")]
    [SerializeField] GameObject bombJewelSetPrefab;
    [SerializeField] float bombJewelSpawnChance = 0.5f;

    [Header("硬度設定")]
    [SerializeField] float hardnessScale = 0.5f;


    private Chunk[] chunks;
    private HashSet<int> chunksToUpdate = new HashSet<int>();
    private List<GameObject> spawnedTreasures = new List<GameObject>();
    byte[,,] mapData;

    public float BlockSize => blockSize;
    public int ChunkSizeY => chunkSizeY;

    // エリアごとの初期宝石合計額を保持する辞書
    public System.Collections.Generic.Dictionary<int, long> zoneInitialGemValues = new System.Collections.Generic.Dictionary<int, long>();
    private const long GEM_VALUE = 300000;

    // ブロックが変更されたときのイベント
    public event Action<int, int, byte> OnBlockChanged;
    public event Action<int, int> OnBlocksDestroyedByPlayer; // dirtCount, oreCount

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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // ← これ追加
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void OnEnable()
    {
        // シーン切り替えイベントを購読
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // オブジェクト破棄・無効化時にイベント購読を解除
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // アクティブなシーン名が "02_Main" の時だけ表示(true)、それ以外は非表示(false)
        if (scene.name == "02_Main")
        {
            SetActiveAllChildren(true);
        }
        else
        {
            SetActiveAllChildren(false);
        }
    }

    // 自身（コライダーなど）と子要素（チャンクや宝石など）の表示・非表示を一括切り替え
    private void SetActiveAllChildren(bool isActive)
    {
        // 自身のレンダラーやコライダーがあれば無効化/有効化
        if (TryGetComponent<Collider>(out var col)) col.enabled = isActive;

        // 子オブジェクト（Chunkや宝石）をすべてループで切り替え
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(isActive);
        }
    }

    void Start()
    {
        if (mapData == null)
        {
            CreateStage(widthZ, heightY, blockSize);
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
    public void ExecuteDig(int centerX, int centerY, int centerZ, float radius, Vector3 minLimit, Vector3 maxLimit, bool isPlayerDigging = true)
    {
        int r = Mathf.CeilToInt(radius);
        bool changed = false;
        HashSet<int> changedYRows = new HashSet<int>();
        int destroyedDirt = 0;
        int destroyedOre = 0;
        int emittedParticleCount = 0;

        for (int y = centerY - r; y <= centerY + r; y++)
        {
            for (int z = centerZ - r; z <= centerZ + r; z++)
            {
                // 2Dの距離判定
                float distSq = (centerY - y) * (centerY - y) + (centerZ - z) * (centerZ - z);
                if (distSq > radius * radius) continue;

                if (y < minLimit.y || y > maxLimit.y || z < minLimit.z || z > maxLimit.z) continue;

                // 画面上（Y,Z）の1マスにつき、エフェクトは1回だけ出すためのフラグ
                bool emittedForThisCell = false;

                // 奥行き（X方向）をまとめて壊す
                for (int x = 0; x < thicknessX; x++)
                {
                    if (!IsInside(x, y, z)) continue;
                    byte currentBlock = mapData[x, y, z];
                    if (currentBlock == (byte)BlockType.Air || currentBlock == (byte)BlockType.Bedrock) continue;

                    if (currentBlock == (byte)BlockType.Dirt)
                    {
                        destroyedDirt++;
                        if (!emittedForThisCell)
                        {
                            Vector3 localPos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize;
                            Vector3 worldPos = transform.TransformPoint(localPos);
                            if (BlockEffectManager.Instance != null)
                            {
                                BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.Dirt);
                            }
                            emittedForThisCell = true;
                            emittedParticleCount++;
                        }
                    }
                    else if (currentBlock == (byte)BlockType.Ore)
                    {
                        destroyedOre++;
                        if (!emittedForThisCell)
                        {
                            Vector3 localPos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize;
                            Vector3 worldPos = transform.TransformPoint(localPos);
                            if (BlockEffectManager.Instance != null)
                            {
                                BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.Ore);
                            }
                            emittedForThisCell = true;
                            emittedParticleCount++;
                        }
                    }
                    else if (currentBlock == (byte)BlockType.Stone)
                    {
                        // 石の場合
                        if (!emittedForThisCell)
                        {
                            Vector3 localPos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize;
                            Vector3 worldPos = transform.TransformPoint(localPos);
                            if (BlockEffectManager.Instance != null)
                            {
                                BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.Stone);
                            }
                            emittedForThisCell = true;
                            emittedParticleCount++;
                        }
                    }
                    else if (currentBlock == (byte)BlockType.HardRock)
                    {
                        // 岩の場合
                        if (!emittedForThisCell)
                        {
                            Vector3 localPos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize;
                            Vector3 worldPos = transform.TransformPoint(localPos);
                            if (BlockEffectManager.Instance != null)
                            {
                                BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.HardRock);
                            }
                            emittedForThisCell = true;
                            emittedParticleCount++;
                        }
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
        zoneInitialGemValues.Clear();

        var rnd = useDeterministicSeed ? new System.Random(seed) : new System.Random();
        float layerNoiseScale = 0.2f;
        float layerBumpyIntensity = 12f;

        int startX = (thicknessX / 2) + startOffsetX;
        int startY = heightY - startDepthFromSurface;
        int startZ = widthZ / 2;

        int goalThresholdY = 80;
        int relayThickness = 3;

        for (int y = 0; y < heightY; y++)
        {
            for (int x = 0; x < thicknessX; x++)
            {
                for (int z = 0; z < widthZ; z++)
                {
                    // --- A. スタート地点の小部屋と縦穴 ---
                    float dx = x - startX;
                    float dz = z - startZ;
                    float distXZ = Mathf.Sqrt(dx * dx + dz * dz);
                    float dy = y - startY;
                    float distSphere = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);

                    if (distSphere < startHoleRadius || (y > startY && distXZ < startShaftRadius))
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                        continue;
                    }

                    // 1. 地表付近の空気層
                    if (y > heightY - 3)
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                    }
                    // 2. ゴールエリアの空洞化
                    else if (y < goalThresholdY)
                    {
                        if (y <= 5) mapData[x, y, z] = (byte)BlockType.Bedrock;
                        else if (y == 6 && x == startX && z == startZ) mapData[x, y, z] = (byte)BlockType.Stone;
                        else mapData[x, y, z] = (byte)BlockType.Air;
                    }
                    // 3. 中継地点 (Bedrock) の判定
                    else if (y > 0 && IsRelayZone(y, relayThickness))
                    {
                        mapData[x, y, z] = (byte)BlockType.Bedrock;
                    }
                    // 4. 通常のブロック生成
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

                            if (depthRatio < -0.2f) mapData[x, y, z] = (byte)BlockType.Quartzite;
                            else if (depthRatio < 0.2f) mapData[x, y, z] = (byte)BlockType.HardRock;
                            else if (depthRatio < 0.6f) mapData[x, y, z] = (byte)BlockType.Stone;
                            else mapData[x, y, z] = (byte)BlockType.Dirt;
                        }
                    }
                }
            }
        }

        float offsetX = -(thicknessX * blockSize) / 2f;
        float offsetZ = -(widthZ * blockSize) / 2f;
        transform.position = new Vector3(offsetX, -(heightY * blockSize), offsetZ);

        GenerateChunks();
        TeleportPlayerToStart(startX, startY, startZ);
    }

    private bool IsRelayZone(int y, int thickness)
    {
        int interval = chunkSizeY * 10;
        for (int i = 1; i <= (heightY / interval); i++)
        {
            int targetY = interval * i;
            if (targetY >= heightY) continue;
            if (y >= targetY - thickness && y <= targetY + thickness) return true;
        }
        return false;
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

    private void TeleportPlayerToStart(int x, int y, int z)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Vector3 worldPos = transform.position + new Vector3(x * blockSize, y * blockSize + 1.5f, z * blockSize);
            player.transform.position = worldPos;
        }
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
            TrySpawnBombInChunk(i, bombSpawnRatio * depthFactor);
            // 最下5チャンクでは爆弾+宝石セットを生成しない
            if (i >= 5)
            {
                TrySpawnBombJewelSetInChunk(i, bombJewelSpawnChance * depthFactor);
            }
        }
    }

    public void UpdateChunkMesh(int index)
    {
        if (chunks == null || index < 0 || index >= chunks.Length) return;
        int startY = index * chunkSizeY;
        int endY = Mathf.Min(startY + chunkSizeY, heightY);
        chunks[index].RebuildMesh(mapData, startY, endY, thicknessX, heightY, widthZ, blockSize);
    }

    // チャンク内に宝石をスポーンさせる
    private void TrySpawnJewelsInChunk(int chunkIndex, float spawnChance)
    {
        int startY = chunkIndex * chunkSizeY;
        int endY = Mathf.Min(startY + chunkSizeY, heightY);
        
        int zoneIndex = chunkIndex / 10;
        if (!zoneInitialGemValues.ContainsKey(zoneIndex))
        {
            zoneInitialGemValues[zoneIndex] = 0;
        }

        for (int t = 0; t < 3; t++)
        {
            if (UnityEngine.Random.Range(0f, 100f) < spawnChance)
            {
                int rx = UnityEngine.Random.Range(0, thicknessX);
                int ry = UnityEngine.Random.Range(startY, endY);
                int rz = UnityEngine.Random.Range(0, widthZ);

                if (mapData[rx, ry, rz] == 1 || mapData[rx, ry, rz] == 4 || mapData[rx, ry, rz] == 5)
                {
                    Vector3 pos = transform.position + new Vector3(
                        rx * blockSize,
                        ry * blockSize + (blockSize / 2f),
                        rz * blockSize + (blockSize / 2f)
                    );
                    Quaternion rotation = Quaternion.Euler(0, -90f,0);

                    GameObject jewel = Instantiate(treasurePrefab, pos, rotation, transform);
                    spawnedTreasures.Add(jewel);
                    zoneInitialGemValues[zoneIndex] += GEM_VALUE;
                }
            }
        }
    }

    // チャンク内に爆弾をスポーンさせる
    private void TrySpawnBombInChunk(int chunkIndex, float spawnChance)
    {
        int startY = chunkIndex * chunkSizeY;
        int endY = Mathf.Min(startY + chunkSizeY, heightY);
        for (int t = 0; t < 2; t++)
        {
            if (UnityEngine.Random.Range(0f, 100f) < spawnChance)
            {
                int rx = UnityEngine.Random.Range(0, thicknessX);
                int ry = UnityEngine.Random.Range(startY, endY);
                int rz = UnityEngine.Random.Range(0, widthZ);
                if (mapData[rx, ry, rz] == 1 || mapData[rx, ry, rz] == 4 || mapData[rx, ry, rz] == 5)
                {
                    Vector3 pos = transform.position + new Vector3(
                        rx * blockSize,
                        ry * blockSize + (blockSize / 2f),
                        rz * blockSize + (blockSize / 2f)
                    );
                    Quaternion rotation = Quaternion.Euler(0, -90f, 0);
                    Instantiate(bombPrefab, pos, rotation, transform);
                }
            }
        }
    }

    private void TrySpawnBombJewelSetInChunk(int chunkIndex, float spawnChance)
    {
        int startY = chunkIndex * chunkSizeY;
        int endY = Mathf.Min(startY + chunkSizeY, heightY);

        for (int t = 0; t < 2; t++)
        {
            if (UnityEngine.Random.Range(0f, 100f) < spawnChance)
            {
                int rx = UnityEngine.Random.Range(0, thicknessX);
                int ry = UnityEngine.Random.Range(startY, endY);
                int rz = UnityEngine.Random.Range(0, widthZ);

                // 土・石・硬岩だけに出す（既存と同じ条件）
                if (mapData[rx, ry, rz] == 1 || mapData[rx, ry, rz] == 4 || mapData[rx, ry, rz] == 5)
                {
                    Vector3 pos = transform.position + new Vector3(
                        rx * blockSize,
                        ry * blockSize + (blockSize / 2f),
                        rz * blockSize + (blockSize / 2f)
                    );

                    Quaternion rotation = Quaternion.Euler(0, -90f, 0);

                    Instantiate(bombJewelSetPrefab, pos, rotation, transform);
                }
            }
        }
    }

    // プレイヤーのYブロック座標から、そのエリアの初期宝石合計額を取得する
    public long GetZoneInitialGemValue(int py)
    {
        int zoneIndex = py / (chunkSizeY * 10);
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
            case BlockType.Quartzite:
                baseHardness = 20.0f;
                break;
            default:
                baseHardness = 1.0f;
                break;
        }
        // 深さに応じて硬さを増加させる
        float depthFactor = (heightY - y) * hardnessScale * 0.05f;
        return baseHardness + depthFactor;
    }

    private int GetRelayID(int y)
    {
        int interval = chunkSizeY * 10;
        return y / interval;
    }
    public void OnPlayerReachRelayPoint(int y)
    {
        Debug.Log($"中継地点到達. 深度：{y}");

        if (CheckpointManager.Instance == null) return;

        int currentID = GetRelayID(y);

        // ❗ 同じチェックポイントならUI出さない
        if (currentID == CheckpointManager.Instance.GetUsedCheckpointID())
        {
            Debug.Log("同じチェックポイントのためスキップ");
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Vector3 checkpointPos = player.transform.position;
        checkpointPos.y -= (blockSize * 5f);

        // 保存（ID付き）
        CheckpointManager.Instance.SaveCheckpoint(checkpointPos, currentID);

        // UI表示
        if (TryGetComponent<SelectPoint>(out var selectPoint))
        {
            selectPoint.ShowButton();
        }
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

    public void RestartFromCheckpoint()
    {
        Vector3 lastPos = CheckpointManager.Instance.GetLastCheckpoint();
        GameObject player = GameObject.FindWithTag("Player");

        if (player != null)
        {
            player.transform.position = lastPos;

            ClearBlocksAroundPoint(lastPos, 4.0f);

            // ❗ここ重要
            CheckpointManager.Instance.MarkCheckpointAsUsed();
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

    // スケール（サイズ）を考慮して露出を判定するオーバーロード
    public bool IsJewelExposed(Vector3 worldPos, Vector3 scale)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int x = Mathf.RoundToInt(localPos.x / blockSize);
        int centerY = Mathf.FloorToInt(localPos.y / blockSize);
        int centerZ = Mathf.FloorToInt(localPos.z / blockSize);

        // スケールから半径（ブロック数）を計算。最小は1。
        int extentY = Mathf.CeilToInt(scale.y / 2f);
        int extentZ = Mathf.CeilToInt(scale.z / 2f);

        if (extentY < 1) extentY = 1;
        if (extentZ < 1) extentZ = 1;

        // 指定されたサイズの範囲内がすべてAirかチェック
        for (int y = centerY - extentY; y <= centerY + extentY; y++)
        {
            for (int z = centerZ - extentZ; z <= centerZ + extentZ; z++)
            {
                if (IsInside(x, y, z) && mapData[x, y, z] != (byte)BlockType.Air)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public bool IsJewelExposed(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int x = Mathf.RoundToInt(localPos.x / blockSize);
        int y = Mathf.FloorToInt(localPos.y / blockSize);
        int z = Mathf.FloorToInt(localPos.z / blockSize);

        bool allAir = true;

        // 宝石自身のブロックが土ならまだ露出していない
        if (IsInside(x, y, z) && mapData[x, y, z] != (byte)BlockType.Air) allAir = false;

        // 上下
        if (IsInside(x, y + 1, z) && mapData[x, y + 1, z] != (byte)BlockType.Air) allAir = false;
        if (IsInside(x, y - 1, z) && mapData[x, y - 1, z] != (byte)BlockType.Air) allAir = false;
        
        // 左右（ゲーム内Z軸）
        if (IsInside(x, y, z + 1) && mapData[x, y, z + 1] != (byte)BlockType.Air) allAir = false;
        if (IsInside(x, y, z - 1) && mapData[x, y, z - 1] != (byte)BlockType.Air) allAir = false;

        return allAir;
    }
}
