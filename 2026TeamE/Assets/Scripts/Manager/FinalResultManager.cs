using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 

public class FinalResultManager : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI totalEarnedText; 

    [Header("演出設定")]
    [SerializeField] private float countDuration = 2.0f;
    [SerializeField] private string nextSceneName = "Title";

    private bool isAnimationFinished = false;
    private bool skipRequested = false;
    private int totalEarnedAmount; 

    void Start()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.StopBGM();
        MoneyManager mm = MoneyManager.Instance;
        if (mm != null)
        {
            totalEarnedAmount = mm.GetTotalEarnedMoney(); 
            if (totalEarnedText != null) totalEarnedText.text = "0"; 

            StartCoroutine(CountUpRoutine());
        }
    }

    void Update()
    {
        
        bool wasPressed = false;

        
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) wasPressed = true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) wasPressed = true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) wasPressed = true; 

        if (wasPressed)
        {
            if (!isAnimationFinished)
            {
                skipRequested = true;
            }
            else
            {
                
                if (SoundManager.Instance != null) SoundManager.Instance.StopBGM();

                SceneManager.LoadScene(nextSceneName);
            }
        }
    }

    private IEnumerator CountUpRoutine()
    {
        float elapsed = 0;
        while (elapsed < countDuration && !skipRequested)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / countDuration;
            int currentTotalValue = (int)(totalEarnedAmount * progress);
            
            if (totalEarnedText != null) totalEarnedText.text = currentTotalValue.ToString("N0");
            
            yield return null;
        }

        if (totalEarnedText != null) totalEarnedText.text = totalEarnedAmount.ToString("N0");
        
        yield return new WaitForSeconds(0.2f);
        isAnimationFinished = true;
    }
}
