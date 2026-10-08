using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 

public class FinalResultManager : MonoBehaviour
{
    // --- 繧ｰ繝ｭ繝ｼ繝舌Ν繧ｹ繧ｳ繧｢險倬鹸逕ｨ・医す繝ｼ繝ｳ髢捺戟縺｡雜翫＠・・---
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
        PlayerPrefs.SetInt("GoalReached", 0);
        PlayerPrefs.Save();
    }
    // ------------------------------------------------

    [Header("UI蜿ら・")]
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
    [Tooltip("螳晉ｮｱ1蛟九≠縺溘ｊ縺ｮ繧ｹ繧ｳ繧｢")]
    [SerializeField] private int pointsPerTreasureBox = 10000;
    [Tooltip("")]
    [SerializeField] private int pointsPerBomb = -5000;
    [Tooltip("谿九ｊ驟ｸ邏1遘偵≠縺溘ｊ縺ｮ繧ｹ繧ｳ繧｢")]
    [SerializeField] private int pointsPerOxygenSecond = 100;
    [Tooltip("繧ｯ繝ｪ繧｢縺励◆髫帙・繝懊・繝翫せ繧ｹ繧ｳ繧｢")]
    [SerializeField] private int pointsForClear = 50000;
    [Tooltip("繧ｲ繝ｼ繝荳ｭ縺ｫ遞ｼ縺・□縺企≡繧偵せ繧ｳ繧｢縺ｫ蜷育ｮ励☆繧九°縺ｩ縺・°")]
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
        int isCleared = (GoalJewelry.isGoalReached || PlayerPrefs.GetInt("GoalReached", 0) == 1) ? 1 : 0;
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

        // 遞ｼ縺・□縺企≡繧貞粋邂励☆繧九°縺ｩ縺・°
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

        // 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ荳ｭ縺ｮ繧ｹ繧ｭ繝・・縺ｮ縺ｿ蜿励￠莉倥￠繧九・        // ・医す繝ｼ繝ｳ驕ｷ遘ｻ縺ｯ蟆ら畑縺ｮ繝懊ち繝ｳ縺九ｉ陦後≧縺溘ａ縲√％縺薙〒縺ｮ閾ｪ蜍暮・遘ｻ縺ｯ蜑企勁・・        if (wasPressed && !isAnimationFinished)
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
        
        // 莉雁屓縺ｮ譛邨ゅせ繧ｳ繧｢繧偵Λ繝ｳ繧ｭ繝ｳ繧ｰ縺ｫ逋ｻ骭ｲ
        RankingManager.SaveScore(finalScoreAmount);

        yield return new WaitForSeconds(0.2f);
        isAnimationFinished = true;
    }

    // --- 繝懊ち繝ｳ縺九ｉ蜻ｼ縺ｳ蜃ｺ縺輔ｌ繧句・逅・---

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
        SceneManager.LoadScene("02_Main");
    }

    // ランキングの表示・非表示を切り替えるメソッド
    public void ToggleRankingPanel()
    {
        if (rankingPanel != null)
        {
            rankingPanel.SetActive(!rankingPanel.activeSelf);
        }
    }
}

