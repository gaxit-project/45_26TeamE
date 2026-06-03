using UnityEngine;
using UnityEngine.Playables;

public class TimelineManager : MonoBehaviour
{
    [SerializeField] private PlayableDirector timelineDirector;

    private static bool hasPlayed = false;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (hasPlayed)
        {
            // 2回目以降のメインシーンの処理
            if (timelineDirector != null)
            {
                timelineDirector.enabled = false;
            }

            // --- 追加：2回目なので、強制的にプレイヤーを動動できるようにする ---
            // シーン内にある PlayerController を探して直接関数を呼ぶ
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.EnablePlayerControl();
            }
            // -----------------------------------------------------------------
        }
        else
        {
            // 1回目（初回プレイ時）
            hasPlayed = true;
        }
    }
}