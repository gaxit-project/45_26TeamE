using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("追跡対象")]
    public Transform player;

    [Header("設定")]
    public float smoothTime = 1.0f; // 到着までの目安時間（大きいほど「のんびり」）
    public Vector3 offset = new Vector3(0, 0, -2f); // プレイヤーの少し手前(Zマイナス)に配置する場合

    private Vector3 currentVelocity = Vector3.zero;

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 targetPosition = player.position + offset;

        // 1. 全体的に「のんびり」追従させる
        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime
        );

        // 2. Y軸だけは「のんびり」を無視して、目標地点に即座に合わせる
        nextPosition.y = targetPosition.y;

        transform.position = nextPosition;
    }

}
