using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // 追加

public class ResultManager : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI onHandResultText;
    [SerializeField] private TextMeshProUGUI targetResultText;
    [SerializeField] private TextMeshProUGUI resultStatusText;

    [Header("演出設定")]
    [SerializeField] private float countDuration = 2.0f;
    [SerializeField] private string nextSceneName = "Title";

    private bool isAnimationFinished = false;
    private bool skipRequested = false;
    private int finalAmount;
    private int targetAmount;

    void Start()
    {
        MoneyManager mm = MoneyManager.Instance;
        if (mm != null)
        {
            finalAmount = mm.GetMoneyOnHand();
            targetAmount = mm.GetTargetAmountOnPart();
            onHandResultText.text = "0";
            targetResultText.text = targetAmount.ToString("0,000,000,000");
            resultStatusText.text = "";
            StartCoroutine(CountUpRoutine());
        }
    }

    void Update()
    {
        // 新しい Input System での「どれか押した」判定
        bool wasPressed = false;

        // キーボードかマウスのクリックがあったか
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) wasPressed = true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) wasPressed = true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) wasPressed = true; // Aボタン/×ボタン等

        if (wasPressed)
        {
            if (!isAnimationFinished)
            {
                skipRequested = true;
            }
            else
            {
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
            int currentDisplayValue = (int)(finalAmount * progress);
            onHandResultText.text = currentDisplayValue.ToString("0,000,000,000");
            yield return null;
        }

        onHandResultText.text = finalAmount.ToString("0,000,000,000");
        CheckSuccess(finalAmount, targetAmount);

        yield return new WaitForSeconds(0.2f);
        isAnimationFinished = true;
    }

    void CheckSuccess(int onHand, int target)
    {
        if (onHand >= target)
        {
            resultStatusText.text = "SUCCESS";
            resultStatusText.color = Color.green;
        }
        else
        {
            resultStatusText.text = "FAILED";
            resultStatusText.color = Color.red;
        }
    }
}
