using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 

public class FinalResultManager : MonoBehaviour
{
    // --- グローバルスコア記録用（シーン間持ち越し） ---
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
    }
    // ------------------------------------------------

    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI totalEarnedText; 
    [SerializeField] private GameObject firstSelectedButton;

    [Header("詳細スコアUI（内訳）")]
    [SerializeField] private TextMeshProUGUI boxCountText;
    [SerializeField] private TextMeshProUGUI boxSubtotalText;
    [SerializeField] private TextMeshProUGUI bombCountText;
    [SerializeField] private TextMeshProUGUI bombSubtotalText;
    [SerializeField] private TextMeshProUGUI oxygenCountText;
    [SerializeField] private TextMeshProUGUI oxygenSubtotalText;

    [Header("スコア計算設定")]
    [Tooltip("宝箱1個あたりのスコア")]
    [SerializeField] private int pointsPerTreasureBox = 10000;
    [Tooltip("爆弾起爆1回あたりのスコア（マイナスにする場合は負の値）")]
    [SerializeField] private int pointsPerBomb = -5000;
    [Tooltip("残り酸素1秒あたりのスコア")]
    [SerializeField] private int pointsPerOxygenSecond = 100;
    [Tooltip("ゲーム中に稼いだ資金をスコアに合算するかどうか")]
    [SerializeField] private bool includeMoneyInScore = false;

    [Header("演出設定")]
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

        // 3つの要素のスコア計算
        float totalOxygen = 0f;
        foreach (var ox in OxygenRemainingPerFloor)
        {
            totalOxygen += ox;
        }

        int boxScore = CollectedTreasureBoxes * pointsPerTreasureBox;
        int bombScore = TriggeredBombs * pointsPerBomb;
        int oxygenScore = Mathf.FloorToInt(totalOxygen * pointsPerOxygenSecond);

        // UIにそれぞれの回数と小計を表示
        if (boxCountText != null) boxCountText.text = CollectedTreasureBoxes.ToString("N0");
        if (boxSubtotalText != null) boxSubtotalText.text = boxScore.ToString("N0");
        if (bombCountText != null) bombCountText.text = TriggeredBombs.ToString("N0");
        if (bombSubtotalText != null) bombSubtotalText.text = bombScore.ToString("N0");
        if (oxygenCountText != null) oxygenCountText.text = Mathf.FloorToInt(totalOxygen).ToString("N0");
        if (oxygenSubtotalText != null) oxygenSubtotalText.text = oxygenScore.ToString("N0");

        finalScoreAmount = boxScore + bombScore + oxygenScore;

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

        // アニメーション中のスキップのみ受け付ける。
        // （シーン遷移は専用のボタンから行うため、ここでの自動遷移は削除）
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

        // メインゲームシーンをロード (MainManagerのコードに合わせて "02_Main" を指定)
        SceneManager.LoadScene("02_Main");
    }
}
