using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// メインシーンの導入演出（カメラ移動のタイムライン）を管理する。
/// 初回だけ演出を流し、2回目以降はスキップしてすぐ操作できるようにする。
/// </summary>
/// <remarks>
/// このコンポーネントは DontDestroyOnLoad で生き残るため、Start() はゲーム中に一度しか走らない。
/// シーンを読み込み直すたびに判定し直す必要があるので、sceneLoaded を使っている。
/// </remarks>
public class TimelineManager : MonoBehaviour
{
    [SerializeField] private PlayableDirector timelineDirector;

    [Tooltip("導入演出を管理する対象のシーン名")]
    [SerializeField] private string targetSceneName = "02_Main";

    public static TimelineManager Instance { get; private set; }

    private static bool hasPlayed = false;
    public static bool HasPlayed => hasPlayed;

    /// <summary>判定処理が二重に走らないようにするためのフラグ。</summary>
    private bool isApplyingIntroState;

    public static bool IsPlaying
    {
        get
        {
            if (Instance != null && Instance.timelineDirector != null)
            {
                return Instance.timelineDirector.state == PlayState.Playing;
            }
            return false;
        }
    }

    public static void ResetTimeline()
    {
        hasPlayed = false;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            // シーンを読み込み直すと新しいタイムラインが来るので、参照を差し替えてから自分を消す。
            if (timelineDirector != null)
            {
                Instance.timelineDirector = timelineDirector;
            }
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != targetSceneName) return;
        if (isApplyingIntroState) return;

        StartCoroutine(ApplyIntroStateRoutine());
    }

    /// <summary>
    /// 導入演出を流すかスキップするかを決める。
    /// </summary>
    /// <remarks>
    /// シーン内のオブジェクトが揃ってから判定したいので1フレーム待つ。
    /// Awake のタイミングで PlayerController や TextManager を触ると、
    /// まだ初期化されておらず例外になることがある。
    /// </remarks>
    private IEnumerator ApplyIntroStateRoutine()
    {
        isApplyingIntroState = true;

        try
        {
            yield return null;

            if (!hasPlayed)
            {
                // 初回はタイムライン側の演出に任せ、その中で操作が解禁される。
                hasPlayed = true;
                yield break;
            }

            if (timelineDirector != null)
            {
                timelineDirector.Stop();
                timelineDirector.enabled = false;
            }

            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.EnablePlayerControl();
            }
            else
            {
                Debug.LogWarning("[TimelineManager] PlayerController が見つからないため、操作を解禁できませんでした。");
            }
        }
        finally
        {
            isApplyingIntroState = false;
        }
    }
}
