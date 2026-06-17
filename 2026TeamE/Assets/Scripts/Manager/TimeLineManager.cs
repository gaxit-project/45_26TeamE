using UnityEngine;
using UnityEngine.Playables;

public class TimelineManager : MonoBehaviour
{
    [SerializeField] private PlayableDirector timelineDirector;

    private static bool hasPlayed = false;
    public static bool HasPlayed => hasPlayed;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
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