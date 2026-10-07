using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// 扉への入場操作と、リザルトシーンへの遷移コンポーネント。
/// メインシーンのUI側に1つだけ置いておけばよい。
/// </summary>
/// <remarks>
/// 以前は層ごとの中継地点と鍵の所持数を判定するクラスだったが、
/// 「掘っていると現れる扉にYボタンで入る」仕様へ変更した。
/// 入力の受付を扉ごとに持たせると扉の数だけ InputAction が有効になってしまうため、
/// 受付はここに一本化し、どの扉が範囲内かは VoxelTerrain が持つ。
/// 地形側は入場の記録だけを行う。
/// </remarks>
public class SelectPoint : MonoBehaviour
{
    /// <summary>メインシーンに存在する唯一のインスタンス。シーンを抜けると null になる。</summary>
    public static SelectPoint Instance { get; private set; }

    [Header("扉に入るボタン")]
    [Tooltip("扉に入るための入力（Yボタンなど）")]
    [SerializeField] private InputAction enterDoorAction;

    [Header("遷移先")]
    [SerializeField] private string resultSceneName = "Result";

    [Header("演出")]
    [Tooltip("遷移直前に鳴らすSE名。空なら鳴らしません。")]
    [SerializeField] private string transitionSeName = "つるはしで掘る1";
    [Tooltip("SEを聞かせるための待ち時間（秒・実時間）")]
    [SerializeField] private float transitionDelay = 0.1f;

    private bool isTransitioning;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        enterDoorAction?.Enable();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        enterDoorAction?.Disable();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// このコンポーネントは VoxelTerrain と一緒にシーンをまたいで生き残るため、
    /// メインシーンに戻るたびに遷移中フラグを戻さないと2つ目以降の扉に入れなくなる。
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "02_Main")
        {
            isTransitioning = false;
        }
    }

    private void Update()
    {
        if (isTransitioning) return;
        if (enterDoorAction == null || !enterDoorAction.WasPressedThisFrame()) return;
        if (VoxelTerrain.Instance == null) return;

        // 扉に入れたときだけ画面を切り替える。
        if (VoxelTerrain.Instance.TryEnterDoorInRange())
        {
            GoToResult();
        }
    }

    /// <summary>リザルト（お金の両替・強化）シーンへ移行する。</summary>
    public void GoToResult()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        StartCoroutine(GoToResultRoutine());
    }

    private IEnumerator GoToResultRoutine()
    {
        if (SoundManager.Instance != null)
        {
            if (!string.IsNullOrEmpty(transitionSeName))
            {
                SoundManager.Instance.PlaySE(transitionSeName);
            }
            SoundManager.Instance.StopLoopSE();
            SoundManager.Instance.StopBGM();
        }

        yield return new WaitForSecondsRealtime(transitionDelay);

        // ロード中にtimeScaleを0にしている場合があるため、遷移前に必ず戻す。
        Time.timeScale = 1f;

        if (TimerManager.Instance != null)
        {
            FinalResultManager.RecordOxygenRemaining(TimerManager.Instance.TotalTime);
            Debug.Log($"[スコア] リザルトへ帰還。残り酸素（時間）: {TimerManager.Instance.TotalTime} を記録しました。");
        }

        SceneManager.LoadScene(resultSceneName);
    }
}
