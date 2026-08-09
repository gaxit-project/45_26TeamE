using UnityEngine;

public class MainManager : MonoBehaviour
{
    public static MainManager Instance { get; private set; }

    [Header("ステージ設定")]
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
            SpawnNewLevel();
            SoundManager.Instance.PlayBGM("メインBGM");
        }
    }

    public void SpawnNewLevel()
    {
        VoxelTerrain vt = VoxelTerrain.Instance;
        if (vt != null)
        {
            vt.CreateStage(targetWidth, targetHeight, vt.BlockSize);
        }
    }
}
