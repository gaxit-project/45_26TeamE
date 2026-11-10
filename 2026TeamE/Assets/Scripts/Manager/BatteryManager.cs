using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BatteryManager : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private List<GameObject> battery = new List<GameObject>();

    [Header("UI設定")]
    [SerializeField] private float blinkInterval = 0.15f; 
    [SerializeField] private float consumeTimeout = 0.1f; 

    private float timer = 0f;
    private bool isBlinkVisible = true;
    private bool isConsuming = false;

    private float previousBattery;
    private float stopConsumeTimer = 0f;
    private int previousTargetIndex = -1;

    private List<Image> batteryImages = new List<Image>();

    void Start()
    {
        if (playerController != null)
        {
            previousBattery = playerController.currentBattery;
        }

        foreach (GameObject obj in battery)
        {
            if (obj != null)
            {
                batteryImages.Add(obj.GetComponent<Image>());
            }
            else
            {
                batteryImages.Add(null);
            }
        }
    }

    void Update()
    {
        if (playerController == null) return;

        CheckBatteryDecrease();

        int targetIndex = GetTargetIndex();

        if (isConsuming)
        {
            if (targetIndex != previousTargetIndex)
            {
                UpdateActiveStates();
                isBlinkVisible = true; 
                timer = 0f;
            }

            HandleBlinking(targetIndex);

            UpdateFillAmounts();
        }
        else
        {
            UpdateAppearance();

            if (targetIndex != -1 && battery[targetIndex] != null)
            {
                if (!battery[targetIndex].activeSelf) battery[targetIndex].SetActive(true);
            }
        }

        previousTargetIndex = targetIndex;
    }

    private void CheckBatteryDecrease()
    {
        if (playerController.currentBattery < previousBattery)
        {
            isConsuming = true;
            stopConsumeTimer = consumeTimeout; 
        }
        else
        {
            if (stopConsumeTimer > 0)
            {
                stopConsumeTimer -= Time.deltaTime;
            }
            else
            {
                isConsuming = false;
            }
        }

        previousBattery = playerController.currentBattery;
    }

    /// <summary>ゲージの本数。UIに並べた電池の数に合わせる。</summary>
    private int SegmentCount => Mathf.Min(battery.Count, batteryImages.Count);

    /// <summary>
    /// ゲージ1本あたりのバッテリー量。
    /// 以前は最大値1000・5本を前提に数値を直接書いていたため、最大値を変えると表示がずれていた。
    /// </summary>
    private float SegmentSize => SegmentCount > 0 ? playerController.MaxBattery / SegmentCount : 0f;

    /// <summary>いま減っている途中のゲージの番号。左（満タン側）から0始まり。空なら-1。</summary>
    private int GetTargetIndex()
    {
        float currentBat = playerController.currentBattery;
        if (currentBat <= 0f || SegmentCount == 0) return -1;

        float maxBat = playerController.MaxBattery;
        for (int i = 0; i < SegmentCount; i++)
        {
            if (currentBat > maxBat - (i + 1) * SegmentSize) return i;
        }
        return SegmentCount - 1;
    }

    private void HandleBlinking(int targetIndex)
    {
        if (targetIndex == -1 || battery[targetIndex] == null) return; 

        timer += Time.deltaTime;
        if (timer >= blinkInterval)
        {
            isBlinkVisible = !isBlinkVisible;
            timer = 0f;
        }

        battery[targetIndex].SetActive(isBlinkVisible);
    }

    private void UpdateActiveStates()
    {
        for (int i = 0; i < SegmentCount; i++)
        {
            if (batteryImages[i] != null && battery[i] != null)
            {
                bool shouldBeActive = batteryImages[i].fillAmount > 0f;
                if (battery[i].activeSelf != shouldBeActive)
                {
                    battery[i].SetActive(shouldBeActive);
                }
            }
        }
    }

    private void UpdateAppearance()
    {
        UpdateFillAmounts();
        UpdateActiveStates();
    }

    private void UpdateFillAmounts()
    {
        float currentBat = playerController.currentBattery;
        float maxBat = playerController.MaxBattery;
        float segmentSize = SegmentSize;

        for (int i = 0; i < SegmentCount; i++)
        {
            if (batteryImages[i] == null) continue;

            float segmentMin = maxBat - (i + 1) * segmentSize;
            float segmentMax = maxBat - i * segmentSize;

            float fill = 0f;
            if (currentBat >= segmentMax)
            {
                fill = 1f;
            }
            else if (currentBat <= segmentMin)
            {
                fill = 0f;
            }
            else
            {

                fill = (currentBat - segmentMin) / segmentSize;
            }

            if (!Mathf.Approximately(batteryImages[i].fillAmount, fill))
            {
                batteryImages[i].fillAmount = fill;
            }
        }
    }
}
