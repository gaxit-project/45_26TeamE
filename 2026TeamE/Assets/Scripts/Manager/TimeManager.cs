using UnityEngine;
using TMPro;

public class TimerManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    private float totalTime = 120f;
    private bool isTimerEnded = false;
    private bool isTimerRunning = false; // タイマーが動いているかどうかのフラグ

    [Header("演出用コンポーネント")]
    [SerializeField] private Animator canvasAnimator;
    [SerializeField] private Animator PlayerAnimator;

    [Header("タイマー強調設定")]
    [SerializeField] private float animationSpeed = 5f;
    [SerializeField] private float maxScale = 1.3f;

    // タイムラインから呼び出すスタート関数
    public void StartTimer()
    {
        totalTime = 120f; // タイムをリセット（必要に応じて変更してください）
        isTimerEnded = false;
        isTimerRunning = true; // タイマーのカウントダウンを開始
    }

    void Update()
    {
        // タイマーが実行中、かつ時間が残っている場合のみカウントする
        if (isTimerRunning && totalTime > 0)
        {
            totalTime -= Time.deltaTime;

            if (totalTime <= 0)
            {
                totalTime = 0;
                isTimerRunning = false; // カウントをストップ

                if (!isTimerEnded)
                {
                    isTimerEnded = true;
                    EndTimer();
                }
            }

            DisplayTime(totalTime);

            if (totalTime > 0 && totalTime <= 30f)
            {
                AnimateTimerText();
            }
        }
    }

    void DisplayTime(float timeToDisplay)
    {
        int minutes = Mathf.FloorToInt(timeToDisplay / 60);
        int seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerText.text = string.Format("{0} : {1:00}", minutes, seconds);
    }

    void AnimateTimerText()
    {
        float wave = (Mathf.Sin(Time.time * animationSpeed) + 1f) / 2f;
        float currentScale = Mathf.Lerp(1f, maxScale, wave);
        timerText.transform.localScale = new Vector3(currentScale, currentScale, 1f);
        timerText.color = Color.Lerp(Color.white, Color.red, wave);
    }

    void EndTimer()
    {
        canvasAnimator.SetBool("isTimeUp", true);
        PlayerAnimator.SetBool("isTimeUp", true);
    }
}
