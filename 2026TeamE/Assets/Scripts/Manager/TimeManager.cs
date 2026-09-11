using UnityEngine;
using TMPro;

public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI timerText;

    [SerializeField]private float totalTime = 120f;
    private bool isTimerEnded = false;
    private bool isTimerRunning = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [Header("演出用コンポーネント")]
    [SerializeField] private Animator canvasAnimator;
    [SerializeField] private Animator PlayerAnimator;

    [Header("タイマー強調設定")]
    [SerializeField] private float animationSpeed = 5f;
    [SerializeField] private float maxScale = 1.3f;

    private static bool firstLoad = true;

    private void Start()
    {
        if (firstLoad)
        {
            firstLoad = false;
        }
        else
        {
            StartTimer();
        }
    }

    
    public void StartTimer()
    {
        totalTime = totalTime;
        isTimerEnded = false;
        isTimerRunning = true;

        timerText.transform.localScale = Vector3.one;
        timerText.color = Color.white;
    }

    private void Update()
    {
        if (isTimerRunning && totalTime > 0)
        {
            totalTime -= Time.deltaTime;

            if (totalTime <= 0)
            {
                totalTime = 0;
                isTimerRunning = false;

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
            else
            {
                timerText.transform.localScale = Vector3.one;
                timerText.color = Color.white;
            }
        }
    }

    private void DisplayTime(float timeToDisplay)
    {
        int minutes = Mathf.FloorToInt(timeToDisplay / 60);
        int seconds = Mathf.FloorToInt(timeToDisplay % 60);

        timerText.text = string.Format("{0} : {1:00}", minutes, seconds);
    }

    private void AnimateTimerText()
    {
        float wave = (Mathf.Sin(Time.time * animationSpeed) + 1f) / 2f;

        float currentScale = Mathf.Lerp(1f, maxScale, wave);

        timerText.transform.localScale =
            new Vector3(currentScale, currentScale, 1f);

        timerText.color = Color.Lerp(Color.white, Color.red, wave);
    }

    [Header("タイムアップ後の演出待機時間")]
    [SerializeField] private float timeUpToTitleDelay = 2.0f;

    private void EndTimer()
    {
        if (canvasAnimator != null)
            canvasAnimator.SetBool("isTimeUp", true);

        if (PlayerAnimator != null)
            PlayerAnimator.SetBool("isTimeUp", true);

        // プレイヤーの操作を無効化
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.currentState = PlayerController.PlayerState.GameOver;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }

        
        StartCoroutine(ReturnToTitleAfterTimeUp());
    }

    private System.Collections.IEnumerator ReturnToTitleAfterTimeUp()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySE("つるはしで掘る1");

        yield return new WaitForSecondsRealtime(timeUpToTitleDelay);

        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("01_Title");
    }

    
    
    
    public void AddTime(float seconds)
    {
        if (isTimerEnded) return;

        totalTime += seconds;
        isTimerRunning = true;
    }

    
    
    
    public static void ResetFirstLoad()
    {
        firstLoad = true;
    }
}