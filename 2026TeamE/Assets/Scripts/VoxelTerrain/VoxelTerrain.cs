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

    [Header("このゾーンのアイテム設定")]
    [Tooltip("このゾーンに出現する宝箱系アイテムの総数（鍵3個を含む）")]
    public int itemsPerStage = 10;
    [Tooltip("このゾーンに出現する爆弾の数")]
    public int bombCount = 20;

    [Header("ゴールゾーン設定")]
    [Tooltip("trueにすると、このゾーンは鍵・爆弾・通常アイテムを一切生成せず、中央にゴールのお宝だけを最初から取得可能な状態で配置する")]
    public bool isGoalZone = false;
}

// フィールド定義、ライフサイクル
public partial class VoxelTerrain : MonoBehaviour
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
        Quartzite = 6,
        Boundary = 7
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
    [Tooltip("ステージ端（Z軸両端）の境界壁専用マテリアル。Bedrock（中継地点用）とは別に設定してください。")]
    [SerializeField] private Material boundaryMaterial;

    [Header("同期オプション")]
    [SerializeField] private bool useDeterministicSeed = true;
    [SerializeField] private int seed = 12345;

    [Header("チャンク設定")]
    [SerializeField] private int chunkSizeY = 16;
    [SerializeField] private GameObject chunkPrefab;

    [Header("アイテムPrefab設定")]
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private GameObject keyPrefab;
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private GameObject treasureBoxPrefab;
    [SerializeField] private GameObject oxygenPrefab;
    [SerializeField] private GameObject leatherBagPrefab;
    [SerializeField] private GameObject GoldleatherBagPrefab;

    [Header("中継地点設定")]
    [Tooltip("各ゾーンの最下部に自動配置される中継地点のプレハブ")]
    [SerializeField] private GameObject relayPointPrefab;

    [Header("ゴール設定")]
    [Tooltip("isGoalZoneがtrueのゾーンの中央に配置する、最初から取得可能なゴールのお宝プレハブ")]
    [SerializeField] private GameObject goalTreasurePrefab;
    [Tooltip("ゴールゾーンの中央に掘る開けた部屋の半径（ブロック数）")]
    [SerializeField] private float goalChamberRadius = 6f;

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
    public bool IsStageGenerated => mapData != null;

    /// <summary>
    /// ステージ生成中かどうか。生成は重いため複数フレームにまたがって実行される。
    /// 生成完了に依存する処理は、このフラグが false になるのを待つこと。
    /// </summary>
    public bool IsGenerating { get; private set; }

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
            zoneCollectedKeyCounts.Clear();
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
        // ステージ生成は複数フレームに分割して行われるため、
        // 地形が出来上がる前にプレイヤーを移動させてしまわないよう完了を待つ
        while (IsGenerating)
        {
            yield return null;
        }

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

}