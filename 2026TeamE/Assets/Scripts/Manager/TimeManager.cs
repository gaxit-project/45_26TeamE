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
    [Tooltip("残り時間がこの秒数を切ったら警告状態に入る")]
    [SerializeField] private float warningThreshold = 30f;

    [Header("残り時間の警告（鼓動）")]
    [Tooltip("鼓動1回あたりの振動の強さ。0にすると振動なし")]
    [Range(0f, 1f)]
    [SerializeField] private float heartbeatRumble = 0.35f;
    [SerializeField] private float heartbeatDuration = 0.1f;
    [Tooltip("警告に入った直後の鼓動の間隔（秒）")]
    [SerializeField] private float heartbeatIntervalStart = 1.0f;
    [Tooltip("残り0秒に近づいた時の鼓動の間隔（秒）")]
    [SerializeField] private float heartbeatIntervalEnd = 0.35f;
    [Tooltip("鼓動のSE（空なら鳴らさない）")]
    [SerializeField] private string heartbeatSeName = "HeartBeat";

    // 次に鼓動を鳴らす時刻
    private float nextHeartbeatTime;

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

            if (totalTime > 0 && totalTime <= warningThreshold)
            {
                AnimateTimerText();
                UpdateHeartbeat();
            }
            else
            {
                timerText.transform.localScale = Vector3.one;
                timerText.color = Color.white;
            }
        }
    }

    /// <summary>
    /// 残り時間が少ない間、一定間隔で鼓動のような振動とSEを出す。
    /// 残り時間が減るほど間隔が短くなり、焦りが増していく。
    /// </summary>
    private void UpdateHeartbeat()
    {
        if (Time.time < nextHeartbeatTime) return;

        // 残り時間の割合（警告開始時が1、時間切れ間際が0）
        float remainingRatio = Mathf.Clamp01(totalTime / Mathf.Max(0.01f, warningThreshold));
        float interval = Mathf.Lerp(heartbeatIntervalEnd, heartbeatIntervalStart, remainingRatio);

        nextHeartbeatTime = Time.time + interval;

        if (heartbeatRumble > 0f && HapticsManager.Instance != null)
        {
            HapticsManager.Instance.PlayPulse(heartbeatRumble, heartbeatRumble * 0.5f, heartbeatDuration);
        }

        if (!string.IsNullOrEmpty(heartbeatSeName))
        {
            SoundManager.Instance?.PlaySE(heartbeatSeName);
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