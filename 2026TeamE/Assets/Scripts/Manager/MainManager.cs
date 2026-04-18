using UnityEngine;

public class MainManager : MonoBehaviour
{
    public static MainManager Instance { get; private set; }

    [Header("お金")]
    [SerializeField] private long currentMoney = 0;
    [SerializeField] private int oreValue = 0;

    [Header("ステージ")]
    [SerializeField] private int targetHeight = 500;
    [SerializeField] private int targetWidth = 80;
    [SerializeField] private float blockSize = 0.2f;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SpawnNewLevel();
        SoundManager.Instance.PlayBGM("メインBGM");
    }

    public void SpawnNewLevel()
    {
        VoxelTerrain vt = VoxelTerrain.Instance;
        vt.CreateStage(targetWidth, targetHeight, vt.BlockSize);
    }
}
