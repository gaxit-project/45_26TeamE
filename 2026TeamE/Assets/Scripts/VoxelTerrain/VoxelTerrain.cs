using System;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class ZoneData
{
    public string zoneName = "第1層";
    public int widthZ = 35;
    public int heightChunks = 4;

    [Header("このゾーンのアイテム設定")]
    [Tooltip("このゾーンに出現する宝箱系アイテムの総数")]
    public int itemsPerStage = 10;
    [Tooltip("このゾーンに出現する爆弾の数")]
    public int bombCount = 20;

    [Header("ゴールゾーン設定")]
    [Tooltip("trueにすると、このゾーンは扉・爆弾・通常アイテムを一切生成せず、中央にゴールのお宝だけを最初から取得可能な状態で配置する")]
    public bool isGoalZone = false;
}


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
    [Tooltip("ステージ端（Z軸両端）の境界壁専用マテリアル。Bedrockとは別に設定してください。")]
    [SerializeField] private Material boundaryMaterial;

    [Header("同期オプション")]
    public static int LastUsedSeed { get; private set; }
    public static bool ForceUseSeed = false;
    [SerializeField] private bool useDeterministicSeed = true;
    [SerializeField] private int seed = 12345;

    [Header("チャンク設定")]
    [SerializeField] private int chunkSizeY = 16;
    [SerializeField] private GameObject chunkPrefab;

    [Header("アイテム配置設定")]
    [Tooltip("宝箱同士の最低距離（ブロック数）。狭い場所で置ききれない場合は無視されます。")]
    [SerializeField] private float treasureMinDistance = 10f;
    [Tooltip("爆弾が宝箱から離れるほど生成確率が下がる減衰率（大きいほど宝箱の近くに集中する）")]
    [SerializeField] private float bombWeightFalloff = 10f;
    [Tooltip("宝箱から爆弾を離す最低距離（ブロック数）。これより近い場所には爆弾は生成されません。")]
    [SerializeField] private float bombSafeDistanceFromTreasure = 2f;

    [Header("アイテムPrefab設定")]
    [SerializeField] private GameObject treasurePrefab;
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private GameObject treasureBoxPrefab;
    [SerializeField] private GameObject oxygenPrefab;
    [SerializeField] private GameObject leatherBagPrefab;
    [SerializeField] private GameObject GoldleatherBagPrefab;

    [Header("扉設定")]
    [Tooltip("地中にランダムに埋め込まれる扉のプレハブ。DoorBehaviourが付いている必要があります。")]
    [SerializeField] private GameObject doorPrefab;
    [Tooltip("扉をステージ左右の端から離す余白（ブロック数）。端に寄りすぎると壁に食い込んで見えるため。")]
    [SerializeField] private int doorEdgeMarginZ = 8;
    [Tooltip("扉を1つ置く深さの間隔（チャンク数）。15なら15チャンク掘るごとに扉が1つ現れる。")]
    [SerializeField] private int chunksPerDoor = 15;
    [Tooltip("1単位の中で扉が出る範囲を、単位の最深部から何チャンク分にするか")]
    [SerializeField] private int doorBandChunks = 3;
    [Tooltip("ゴールゾーンにも扉を配置する。通常はゴール層に寄り道は不要なのでオフ。")]
    [SerializeField] private bool allowDoorsInGoalZone = false;

    [Header("ゴール設定")]
    [Tooltip("isGoalZoneがtrueのゾーンの中央に配置する、最初から取得可能なゴールのお宝プレハブ")]
    [SerializeField] private GameObject goalTreasurePrefab;
    [Tooltip("ゴールゾーンの中央に掘る開けた部屋の半径（ブロック数）")]
    [SerializeField] private float goalChamberRadius = 6f;

    [Header("硬度設定")]
    [SerializeField] private float hardnessScale = 0.5f;

    
    private Chunk[] chunks;
    private HashSet<int> chunksToUpdate = new HashSet<int>();
    private List<GameObject> spawnedTreasures = new List<GameObject>();
    private byte[,,] mapData;
    private int heightY; 

    private HashSet<int> zoneUnlockedFlags = new HashSet<int>();
    private bool isRestoreRoutineRunning;
    public Dictionary<int, long> zoneInitialGemValues = new Dictionary<int, long>();
    private const long GEM_VALUE = 300000;

    public float BlockSize => blockSize;
    public int ChunkSizeY => chunkSizeY;
    public bool IsStageGenerated => mapData != null;

    
    
    
    
    public bool IsGenerating { get; private set; }

    
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
            StartCoroutine(RestoreAfterMainSceneLoadedRoutine());
        }
        else
        {
            SetActiveAllChildren(false);
        }
    }

    /// <summary>
    /// メインシーンが読み込まれた直後の復帰処理。
    /// ステージ生成（初回はこのフレームの後に始まる）が終わるのを待ってから、
    /// 扉からの復帰またはチェックポイントからの復帰を行う。
    /// </summary>
    private System.Collections.IEnumerator RestoreAfterMainSceneLoadedRoutine()
    {
        // sceneLoadedイベントとStart()の両方から呼ばれても一度しか走らせない。
        if (isRestoreRoutineRunning) yield break;
        isRestoreRoutineRunning = true;

        try
        {
            // このフレームの Start() で CreateStage が始まる場合があるため、1フレーム待つ。
            yield return null;

            while (IsGenerating)
            {
                yield return null;
            }

            if (!IsStageGenerated) yield break;

            // 扉から戻ってきた場合は、その扉の位置が最優先の復帰先。
            if (HasPendingDoorReturn)
            {
                RestorePlayerAtDoor();
                yield break;
            }

            if (CheckpointManager.Instance != null && CheckpointManager.Instance.HasCheckpoint())
            {
                RestartFromCheckpoint();
            }
        }
        finally
        {
            isRestoreRoutineRunning = false;
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

        // sceneLoadedイベントを取り逃した場合（実行中に生成された等）の保険。
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "02_Main")
        {
            StartCoroutine(RestoreAfterMainSceneLoadedRoutine());
        }
    }
}