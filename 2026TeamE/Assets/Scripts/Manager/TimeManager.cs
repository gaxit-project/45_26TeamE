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
        }
    }

    void DisplayTime(float timeToDisplay)
    {
        int minutes = Mathf.FloorToInt(timeToDisplay / 60);
        int seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerText.text = string.Format("{0} : {1:00}", minutes, seconds);
    }

    void EndTimer() //Canvasのアニメーションを再生し，タイトルに戻る
    {
        canvasAnimator.SetBool("isTimeUp", true);
        PlayerAnimator.SetBool("isTimeUp", true);
    }


}
