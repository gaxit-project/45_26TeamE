using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 

public class FinalResultManager : MonoBehaviour
{
    // --- グローバルスコア記録用（シーンをまたいで持ち越す） ---
    public static int CollectedTreasureBoxes { get; private set; } = 0;
    public static int TriggeredBombs { get; private set; } = 0;
    public static List<float> OxygenRemainingPerFloor { get; private set; } = new List<float>();

    public static void AddCollectedTreasureBox() { CollectedTreasureBoxes++; }
    public static void AddTriggeredBomb() { TriggeredBombs++; }
    public static void RecordOxygenRemaining(float oxygen) { OxygenRemainingPerFloor.Add(oxygen); }

    public static void ResetStats()
    {
        CollectedTreasureBoxes = 0;
        TriggeredBombs = 0;
        OxygenRemainingPerFloor.Clear();
        GoalJewelry.isGoalReached = false;
    }
    // ------------------------------------------------

    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI totalEarnedText; 
    [SerializeField] private GameObject firstSelectedButton;

    [Header("")]
    [SerializeField] private TextMeshProUGUI boxCountText;
    [SerializeField] private TextMeshProUGUI boxSubtotalText;
    [SerializeField] private TextMeshProUGUI bombCountText;
    [SerializeField] private TextMeshProUGUI bombSubtotalText;
    [SerializeField] private TextMeshProUGUI oxygenCountText;
    [SerializeField] private TextMeshProUGUI oxygenSubtotalText;
    [SerializeField] private TextMeshProUGUI clearCountText;
    [SerializeField] private TextMeshProUGUI clearSubtotalText;

    [Header("")]
    [Tooltip("宝箱1個あたりのスコア")]
    [SerializeField] private int pointsPerTreasureBox = 10000;
    [Tooltip("")]
    [SerializeField] private int pointsPerBomb = -5000;
    [Tooltip("残り酸素1秒あたりのスコア")]
    [SerializeField] private int pointsPerOxygenSecond = 100;
    [Tooltip("クリアした際のボーナススコア")]
    [SerializeField] private int pointsForClear = 50000;
    [Tooltip("ゲーム中に稼いだお金をスコアに合算するかどうか")]
    [SerializeField] private bool includeMoneyInScore = false;

    [Header("")]
    [SerializeField] private float countDuration = 2.0f;
    [SerializeField] private string nextSceneName = "Title";

    private bool isAnimationFinished = false;
    private bool skipRequested = false;
    private int finalScoreAmount; 

    void Start()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayBGM("Virtual_Adventure_2");

        if (firstSelectedButton != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }

        float totalOxygen = 0f;
        foreach (var ox in OxygenRemainingPerFloor)
        {
            totalOxygen += ox;
        }

        int boxScore = CollectedTreasureBoxes * pointsPerTreasureBox;
        int bombScore = TriggeredBombs * pointsPerBomb;
        int oxygenScore = Mathf.FloorToInt(totalOxygen * pointsPerOxygenSecond);
        int isCleared = GoalJewelry.isGoalReached ? 1 : 0;
        int clearScore = isCleared * pointsForClear;

        if (boxCountText != null) boxCountText.text = CollectedTreasureBoxes.ToString("N0");
        if (boxSubtotalText != null) boxSubtotalText.text = boxScore.ToString("N0");
        if (bombCountText != null) bombCountText.text = TriggeredBombs.ToString("N0");
        if (bombSubtotalText != null) bombSubtotalText.text = bombScore.ToString("N0");
        if (oxygenCountText != null) oxygenCountText.text = Mathf.FloorToInt(totalOxygen).ToString("N0");
        if (oxygenSubtotalText != null) oxygenSubtotalText.text = oxygenScore.ToString("N0");
        if (clearCountText != null) clearCountText.text = isCleared.ToString();
        if (clearSubtotalText != null) clearSubtotalText.text = clearScore.ToString("N0");

        finalScoreAmount = boxScore + bombScore + oxygenScore + clearScore;

        // 稼いだお金を合算するかどうか
        if (includeMoneyInScore && MoneyManager.Instance != null)
        {
            finalScoreAmount += MoneyManager.Instance.GetTotalEarnedMoney(); 
        }

        if (totalEarnedText != null) totalEarnedText.text = "0"; 

        StartCoroutine(CountUpRoutine());
    }

    void Update()
    {
        bool wasPressed = false;

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) wasPressed = true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) wasPressed = true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) wasPressed = true; 

        // アニメーション中のスキップのみ受け付ける
        if (wasPressed && !isAnimationFinished)
        {
            skipRequested = true;
        }
    }

    private IEnumerator CountUpRoutine()
    {
        float elapsed = 0;
        while (elapsed < countDuration && !skipRequested)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / countDuration;
            int currentTotalValue = (int)(finalScoreAmount * progress);

            if (totalEarnedText != null) totalEarnedText.text = currentTotalValue.ToString("N0");

            yield return null;
        }

        if (totalEarnedText != null) totalEarnedText.text = finalScoreAmount.ToString("N0");

        // 今回の最終スコアをランキングに登録
        RankingManager.SaveScore(finalScoreAmount);

        yield return new WaitForSeconds(0.2f);
        isAnimationFinished = true;
    }

    // --- ボタンから呼び出される処理 ---

    public void ReturnToTitle()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.StopBGM();
        SceneManager.LoadScene(nextSceneName);
    }

    public void RetrySameSeed()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.StopBGM();

        // 次の生成で同じシードを使うように指示
        VoxelTerrain.ForceUseSeed = true;

        // 全てリセットして再スタート（DontDestroyOnLoadのマネージャ群を破棄する）
        ResetManager.ResetAll();

        // メインゲームシーンをロード（MainManagerのコードに合わせて "02_Main" を指定）
        StartCoroutine(LoadMainAfterResetRoutine());
    }

    [Header("ランキング機能")]
    [SerializeField] private GameObject rankingPanel;

    /// <summary>
    /// リセット後にメインシーンを読み込む。
    /// ResetManager は DontDestroyOnLoad のマネージャ群を Destroy するが、
    /// Destroy の実行はフレーム末なので、同じフレームでシーンを読み込むと
    /// 古い VoxelTerrain が生き残ったままになり、新しいシーンの VoxelTerrain が
    /// 「Instance が既にある」と判断して自分を消してしまう。
    /// その結果、前回のステージがそのまま残る。1フレーム待って破棄を確定させる。
    /// </summary>
    private IEnumerator LoadMainAfterResetRoutine()
    {
        yield return null;

        // ローディング画面を挟むことで、ステージ生成中の画面を見せないようにする。
        // SceneLoader は内部で生成完了（SceneInitializationGate）を待ってから切り替える。
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene("02_Main");
        }
        else
        {
            SceneManager.LoadScene("02_Main");
        }
    }

    public void ToggleRankingPanel()
    {
        if (rankingPanel != null)
        {
            rankingPanel.SetActive(!rankingPanel.activeSelf);
        }
    }
}
