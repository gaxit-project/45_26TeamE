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

        // 目標地点 = プレイヤーの現在地 + オフセット
        Vector3 targetPosition = player.position + offset;

        // 現在地から目標地点まで「のんびり」追従
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime
        );
    }
}
