using System.Collections.Generic;
using UnityEngine;

public class BatteryManager : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private List<GameObject> battery = new List<GameObject>();

    [Header("点滅設定")]
    [SerializeField] private float blinkInterval = 0.15f; // 点滅スピード
    [SerializeField] private float consumeTimeout = 0.1f; // 減らなくなってから消費判定を切るまでの猶予時間（秒）

    private float timer = 0f;
    private bool isBlinkVisible = true;
    private bool isConsuming = false;

    // バッテリー減少判定用の変数
    private float previousBattery;
    private float stopConsumeTimer = 0f;
    private int previousTargetIndex = -1;

    void Start()
    {
        // 最初は現在のバッテリー量からスタート
        if (playerController != null)
        {
            previousBattery = playerController.currentBattery;
        }
    }

    void Update()
    {
        // 1. バッテリーが「減っているか」を自動チェック
        CheckBatteryDecrease();

        // 2. 現在点滅させるべきアイコンのインデックスを取得
        int targetIndex = GetTargetIndex();

        // 3. 点滅時とそうでない時で処理を完全に分ける
        if (isConsuming)
        {
            // 【重要】点滅対象のアイコンが隣に移った（例:81から80に減った）瞬間だけは
            // 空になったアイコンを完全に消すために1回だけ表示処理を呼ぶ
            if (targetIndex != previousTargetIndex)
            {
                UpdateAppearance();
                isBlinkVisible = true; // 新しい点滅アイコンを必ず表示状態から開始
                timer = 0f;
            }

            // 点滅中は毎フレームの UpdateAppearance() は呼ばず、点滅ロジックのみ実行
            HandleBlinking(targetIndex);
        }
        else
        {
            // 点滅していない時は通常の表示処理を行う
            UpdateAppearance();

            // 点滅が途中で終わった時に、アイコンが透明のまま残るのを防ぐ
            if (targetIndex != -1)
            {
                battery[targetIndex].SetActive(true);
            }
        }

        // 次のフレームの比較用に現在のインデックスを保存
        previousTargetIndex = targetIndex;
    }

    // ========== 追加：バッテリー減少の自動判定 ==========
    private void CheckBatteryDecrease()
    {
        // 前回のフレームよりバッテリーが少なくなっていたら「消費中」
        if (playerController.currentBattery < previousBattery)
        {
            isConsuming = true;
            stopConsumeTimer = consumeTimeout; // タイマーをリセット
        }
        else
        {
            // 減らなくなった場合、少しだけ待ってから消費状態を解除する
            // （単発で減った時に1フレームだけしか点滅しないのを防ぎ、自然に見せるため）
            if (stopConsumeTimer > 0)
            {
                stopConsumeTimer -= Time.deltaTime;
            }
            else
            {
                isConsuming = false;
            }
        }

        // 次のフレームの比較用に記憶
        previousBattery = playerController.currentBattery;
    }

    // ========== 追加：現在の点滅対象を数値から取得 ==========
    private int GetTargetIndex()
    {
        if (playerController.currentBattery > 80) return 0;
        if (playerController.currentBattery > 60) return 1;
        if (playerController.currentBattery > 40) return 2;
        if (playerController.currentBattery > 20) return 3;
        if (playerController.currentBattery > 0) return 4;
        return -1; // バッテリー0の時は -1
    }

    // ========== 変更：点滅処理（対象のみ操作） ==========
    private void HandleBlinking(int targetIndex)
    {
        if (targetIndex == -1) return; // バッテリーが0なら何もしない

        timer += Time.deltaTime;
        if (timer >= blinkInterval)
        {
            isBlinkVisible = !isBlinkVisible;
            timer = 0f;
        }

        // 対象のアイコンだけを点滅させる
        battery[targetIndex].SetActive(isBlinkVisible);
    }

    // ========== 元の表示ロジック（そのまま） ==========
    private void UpdateAppearance()
    {
        if (playerController.currentBattery <= 0)
        {
            battery[4].SetActive(false);
            battery[3].SetActive(false);
            battery[2].SetActive(false);
            battery[1].SetActive(false);
            battery[0].SetActive(false);
        }
        else if (playerController.currentBattery <= 20)
        {
            battery[4].SetActive(true);
            battery[3].SetActive(false);
            battery[2].SetActive(false);
            battery[1].SetActive(false);
            battery[0].SetActive(false);
        }
        else if (playerController.currentBattery <= 40)
        {
            battery[4].SetActive(true);
            battery[3].SetActive(true);
            battery[2].SetActive(false);
            battery[1].SetActive(false);
            battery[0].SetActive(false);
        }
        else if (playerController.currentBattery <= 60)
        {
            battery[4].SetActive(true);
            battery[3].SetActive(true);
            battery[2].SetActive(true);
            battery[1].SetActive(false);
            battery[0].SetActive(false);
        }
        else if (playerController.currentBattery <= 80)
        {
            battery[4].SetActive(true);
            battery[3].SetActive(true);
            battery[2].SetActive(true);
            battery[1].SetActive(true);
            battery[0].SetActive(false);
        }
        else if (playerController.currentBattery <= 100)
        {
            battery[4].SetActive(true);
            battery[3].SetActive(true);
            battery[2].SetActive(true);
            battery[1].SetActive(true);
            battery[0].SetActive(true);
        }
    }
}