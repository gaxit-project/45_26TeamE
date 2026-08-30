using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BatteryManager : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private List<GameObject> battery = new List<GameObject>();

    [Header("点滅設定")]
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
        CheckBatteryDecrease();

        int targetIndex = GetTargetIndex();

        if (isConsuming)
        {
            if (targetIndex != previousTargetIndex)
            {
                UpdateAppearance();
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
                battery[targetIndex].SetActive(true);
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

    private int GetTargetIndex()
    {
        if (playerController.currentBattery > 800) return 0;
        if (playerController.currentBattery > 600) return 1;
        if (playerController.currentBattery > 400) return 2;
        if (playerController.currentBattery > 200) return 3;
        if (playerController.currentBattery > 0) return 4;
        return -1; 
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

    
    private void UpdateAppearance()
    {
        UpdateFillAmounts();

        
        for (int i = 0; i < 5; i++)
        {
            if (batteryImages[i] != null && battery[i] != null)
            {
                if (batteryImages[i].fillAmount <= 0f)
                {
                    battery[i].SetActive(false);
                }
                else
                {
                    battery[i].SetActive(true);
                }
            }
        }
    }

    
    private void UpdateFillAmounts()
    {
        float currentBat = playerController.currentBattery;

        for (int i = 0; i < 5; i++)
        {
            if (batteryImages[i] == null) continue;

            float segmentMin = 1000f - (i + 1) * 200f; 
            float segmentMax = 1000f - i * 200f;       

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
                
                fill = (currentBat - segmentMin) / 200f;
            }

            batteryImages[i].fillAmount = fill;
        }
    }
}
