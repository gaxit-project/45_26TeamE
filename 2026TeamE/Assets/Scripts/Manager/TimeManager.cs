using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; // シーン遷移に必要

public class TimerManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;
    private float totalTime = 120f;
    private bool isTimerEnded = false; // 終了判定フラグ

    [SerializeField] private string resultSceneName = "Result"; // リザルトシーンの名前

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

    void EndTimer()
    {
        SoundManager.Instance.PlayBGM("メインBGM");
        // リザルトシーンに遷移する
        SceneManager.LoadScene(resultSceneName);
    }
}
