using UnityEngine;
using TMPro;

public class TimerManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    private float totalTime = 120f;
    private bool isTimerEnded = false; // 終了判定フラグ

    [Header("演出用コンポーネント")]
    [SerializeField] private Animator canvasAnimator; // CanvasのAnimatorをインスペクターから割り当て
    [SerializeField] private Animator PlayerAnimator; // タイトルのAnimatorをインスペクターから割り当て

    [Header("タイマー強調設定")]
    [SerializeField] private float animationSpeed = 5f;
    [SerializeField] private float maxScale = 1.3f;

    void Update()
    {
        if (totalTime > 0)
        {
            totalTime -= Time.deltaTime;

            if (totalTime <= 0)
            {
                totalTime = 0;
                if (!isTimerEnded)
                {
                    isTimerEnded = true;
                    EndTimer();
                }
            }

            DisplayTime(totalTime);
            if(totalTime > 0 && totalTime <= 30f)
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

    void EndTimer() //Canvasのアニメーションを再生し，タイトルに戻る
    {
        canvasAnimator.SetBool("isTimeUp", true);
        PlayerAnimator.SetBool("isTimeUp", true);
    }


}
