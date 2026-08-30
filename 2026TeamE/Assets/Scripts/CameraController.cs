using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("�ǐՑΏ�")]
    public Transform player;

    [Header("�ݒ�")]
    public float smoothTime = 1.0f; 
    public Vector3 offset = new Vector3(0, 0, -2f); 

    private Vector3 currentVelocity = Vector3.zero;

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 targetPosition = player.position + offset;

        
        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime
        );

        
        nextPosition.y = targetPosition.y;

        transform.position = nextPosition;
    }

}
