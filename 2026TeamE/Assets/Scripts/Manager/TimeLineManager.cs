using UnityEngine;
using UnityEngine.Playables;

public class TimelineManager : MonoBehaviour
{
    [SerializeField] private PlayableDirector timelineDirector;

    public static TimelineManager Instance { get; private set; }

    private static bool hasPlayed = false;
    public static bool HasPlayed => hasPlayed;

    public static bool IsPlaying
    {
        get
        {
            if (Instance != null && Instance.timelineDirector != null)
            {
                return Instance.timelineDirector.state == PlayState.Playing;
            }
            return false;
        }
    }

    public static void ResetTimeline()
    {
        hasPlayed = false;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            if (timelineDirector != null)
            {
                Instance.timelineDirector = timelineDirector;
            }
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (hasPlayed)
        {
            if (timelineDirector != null)
            {
                timelineDirector.enabled = false;
            }

            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.EnablePlayerControl();
            }
        }
        else
        {
            hasPlayed = true;
        }
    }
}