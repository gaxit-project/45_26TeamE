using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("追跡対象")]
    public Transform player;

    [Header("設定")]
    public float smoothTime = 1.0f;
    public Vector3 offset = new Vector3(0, 0, -2f);

    [Header("画面揺れ")]
    [Tooltip("揺れの最大幅（ワールド単位）。0にすると揺れなし")]
    [SerializeField] private float maxShakeAmount = 0.25f;

    [Header("先読み（進行方向を少し多く映す）")]
    [Tooltip("進行方向にカメラを寄せる最大距離。0にすると先読みなし")]
    [SerializeField] private float lookAheadDistance = 2.0f;
    [Tooltip("この速度で移動している時に先読みが最大になる")]
    [SerializeField] private float lookAheadMaxSpeed = 8f;
    [Tooltip("先読みの寄り・戻りにかける時間。大きいほどゆっくり追従する")]
    [SerializeField] private float lookAheadSmoothTime = 0.4f;

    [Header("ゲームオーバー時のカメラ引き演出")]
    [Tooltip("タイムアップ後、カメラを引き始めるまでの待機時間（秒）")]
    [SerializeField] private float gameOverZoomDelay = 0.4f;
    [Tooltip("プレイヤーと最寄りターゲットが画面に収まるまでカメラを引いて動かす時間（秒）")]
    [SerializeField] private float gameOverZoomDuration = 1.6f;
    [Tooltip("カメラを引ききった後、リザルト画面へ遷移するまで見せる余韻の時間（秒）")]
    [SerializeField] private float gameOverHoldDuration = 1.5f;
    [Tooltip("画面端からプレイヤーやターゲットをどれだけ内側に収めるかの余白（ワールド単位）")]
    [SerializeField] private float gameOverScreenPadding = 5.0f;

    private Camera cam;
    private float initialOrthoSize = 15f;

    private bool isGameOverZooming;
    private float gameOverElapsed;
    private bool hasCapturedZoomStart;
    private Vector3 zoomStartCameraPos;
    private float zoomStartOrthoSize;
    private Vector3 gameOverTargetWorldPos;

    private Vector3 currentVelocity = Vector3.zero;

    // 先読みで今どれだけ寄っているか。急に飛ばないよう滑らかに補間する
    private Vector3 currentLookAhead = Vector3.zero;
    private Vector3 lookAheadVelocity = Vector3.zero;
    private Vector3 previousPlayerPosition;

    private float shakeRemainingTime;
    private float shakeTotalDuration;
    private float shakeStrength;

    private void Awake()
    {
        // shopCamera など player 未設定のカメラで Instance が上書きされるのを防ぐ
        if (Instance == null || player != null)
        {
            Instance = this;
        }

        cam = GetComponent<Camera>();
        if (cam != null && cam.orthographic)
        {
            initialOrthoSize = cam.orthographicSize;
        }
    }

    /// <summary>
    /// 画面を揺らす。掘削や爆発など、衝撃を伝えたい場面から呼ぶ。
    /// </summary>
    /// <param name="strength">揺れの強さ（0〜1想定）</param>
    /// <param name="duration">揺れが収まるまでの時間（秒）</param>
    public void AddShake(float strength, float duration)
    {
        if (strength <= 0f || duration <= 0f) return;

        strength = Mathf.Clamp01(strength);

        // まだ揺れている最中なら、強い方・長い方を採用する。
        // 弱い揺れで上書きされず、かつ掘り続けている間は揺れが途切れない。
        if (shakeRemainingTime > 0f)
        {
            strength = Mathf.Max(strength, shakeStrength);
            duration = Mathf.Max(duration, shakeRemainingTime);
        }

        shakeStrength = strength;
        shakeTotalDuration = duration;
        shakeRemainingTime = duration;
    }

    private void Start()
    {
        if (player != null) previousPlayerPosition = player.position;
    }

    /// <summary>
    /// 補間を挟まずに、カメラをプレイヤーの位置へ即座に合わせる。
    /// 扉から戻ってきた時やチェックポイント復帰時など、
    /// プレイヤーを瞬間移動させた直後に呼ぶ。呼ばないとカメラが遠くから追いかけてくる。
    /// </summary>
    public void SnapToPlayer()
    {
        if (player == null)
        {
            GameObject found = PlayerController.FindPlayerObject();
            if (found != null) player = found.transform;
        }
        if (player == null) return;

        isGameOverZooming = false;
        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = initialOrthoSize;
        }

        // 追従と先読みの慣性を捨てる。残っていると直後に流れてしまう。
        currentVelocity = Vector3.zero;
        currentLookAhead = Vector3.zero;
        lookAheadVelocity = Vector3.zero;
        previousPlayerPosition = player.position;

        transform.position = player.position + offset;
    }

    /// <summary>
    /// ゲームオーバー時に、プレイヤーと最寄りのゴール（または未使用の扉）が両方画面に収まるよう
    /// カメラを中心（中間地点）へ動かしつつズームアウトする演出を開始する。
    /// </summary>
    /// <returns>演出完了（余韻含む）までにかかる合計秒数。対象が見つからない場合は 0。</returns>
    public float StartGameOverZoom()
    {
        if (player == null)
        {
            GameObject found = PlayerController.FindPlayerObject();
            if (found != null) player = found.transform;
        }
        if (cam == null) cam = GetComponent<Camera>();
        if (player == null || cam == null || !cam.orthographic) return 0f;

        if (!TryFindClosestRevealTarget(player.position, cam.aspect, out Vector3 targetPos))
        {
            return 0f;
        }

        gameOverTargetWorldPos = targetPos;
        gameOverElapsed = 0f;
        hasCapturedZoomStart = false;
        isGameOverZooming = true;

        return Mathf.Max(0f, gameOverZoomDelay)
             + Mathf.Max(0f, gameOverZoomDuration)
             + Mathf.Max(0f, gameOverHoldDuration);
    }

    /// <summary>
    /// ゴールまたは未使用の扉のうち、プレイヤーと一緒に画面へ収めるのに必要な
    /// カメラサイズ（orthographicSize）が最も小さくて済むターゲットを探す。
    /// </summary>
    private bool TryFindClosestRevealTarget(Vector3 playerPos, float aspect, out Vector3 bestTargetPos)
    {
        bestTargetPos = Vector3.zero;
        float bestRequiredSize = float.MaxValue;
        bool found = false;

        // 1. ゴール（GoalJewelry）を候補として調べる
        GoalJewelry[] goals = FindObjectsByType<GoalJewelry>();
        for (int i = 0; i < goals.Length; i++)
        {
            if (goals[i] == null || !goals[i].gameObject.activeInHierarchy) continue;

            Vector3 pos = goals[i].transform.position;
            float required = CalculateRequiredOrthoSize(playerPos, pos, aspect);
            if (required < bestRequiredSize)
            {
                bestRequiredSize = required;
                bestTargetPos = pos;
                found = true;
            }
        }

        // 2. 未使用の中継地点（DoorBehaviour）を候補として調べる
        DoorBehaviour[] doors = FindObjectsByType<DoorBehaviour>();
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null || !doors[i].gameObject.activeInHierarchy) continue;
            if (doors[i].IsBroken) continue; // 既に入った扉は除外

            Vector3 pos = doors[i].transform.position;
            float required = CalculateRequiredOrthoSize(playerPos, pos, aspect);
            if (required < bestRequiredSize)
            {
                bestRequiredSize = required;
                bestTargetPos = pos;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// カメラをプレイヤーとターゲットの中間地点に置いたとき、
    /// 両方が画面端から gameOverScreenPadding 分だけ内側に収まるために必要な orthographicSize を求める。
    /// </summary>
    private float CalculateRequiredOrthoSize(Vector3 playerPos, Vector3 targetPos, float aspect)
    {
        float safeAspect = Mathf.Max(0.01f, aspect);
        float halfSpanY = Mathf.Abs(targetPos.y - playerPos.y) * 0.5f + gameOverScreenPadding;
        float halfSpanZ = (Mathf.Abs(targetPos.z - playerPos.z) * 0.5f + gameOverScreenPadding) / safeAspect;
        return Mathf.Max(halfSpanY, halfSpanZ);
    }

    void LateUpdate()
    {
        if (player == null) return;

        if (isGameOverZooming && cam != null && cam.orthographic)
        {
            UpdateGameOverZoom();
            return;
        }

        Vector3 targetPosition = player.position + offset + UpdateLookAhead();

        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime
        );

        nextPosition.y = targetPosition.y;

        transform.position = nextPosition + CalculateShakeOffset();
    }

    private void UpdateGameOverZoom()
    {
        gameOverElapsed += Time.unscaledDeltaTime;

        // 引き始めるまでの待機時間中は、通常どおりプレイヤーを追従する
        if (gameOverElapsed < gameOverZoomDelay)
        {
            Vector3 normalTarget = player.position + offset;
            Vector3 nextPos = Vector3.SmoothDamp(transform.position, normalTarget, ref currentVelocity, smoothTime);
            nextPos.y = normalTarget.y;
            transform.position = nextPos + CalculateShakeOffset();
            return;
        }

        if (!hasCapturedZoomStart)
        {
            hasCapturedZoomStart = true;
            zoomStartCameraPos = transform.position;
            zoomStartOrthoSize = cam.orthographicSize;
        }

        Vector3 playerPos = player.position;

        // カメラの中心（YZ平面）をプレイヤーとターゲットの中間地点にする
        float midY = (playerPos.y + gameOverTargetWorldPos.y) * 0.5f;
        float midZ = (playerPos.z + gameOverTargetWorldPos.z) * 0.5f;
        Vector3 desiredCameraPos = new Vector3(playerPos.x + offset.x, midY, midZ);

        // 両方が余白つきで画面に収まる orthographicSize（現在の画角より狭くならないよう下限は initialOrthoSize）
        float desiredOrthoSize = Mathf.Max(
            initialOrthoSize,
            CalculateRequiredOrthoSize(playerPos, gameOverTargetWorldPos, cam.aspect)
        );

        float duration = Mathf.Max(0.01f, gameOverZoomDuration);
        float t = Mathf.Clamp01((gameOverElapsed - gameOverZoomDelay) / duration);
        float easedT = Mathf.SmoothStep(0f, 1f, t);

        transform.position = Vector3.Lerp(zoomStartCameraPos, desiredCameraPos, easedT) + CalculateShakeOffset();
        cam.orthographicSize = Mathf.Lerp(zoomStartOrthoSize, desiredOrthoSize, easedT);
    }

    /// <summary>
    /// プレイヤーの移動方向にカメラを少し寄せ、進行方向を広く映すためのオフセットを求める。
    /// 速く動いているほど大きく寄り、止まると中央に戻る。
    /// </summary>
    private Vector3 UpdateLookAhead()
    {
        if (lookAheadDistance <= 0f)
        {
            previousPlayerPosition = player.position;
            return Vector3.zero;
        }

        // Rigidbodyの速度ではなく実際の移動量から求めることで、
        // 壁に押し付けて動けていない時に先読みが働くのを防ぐ
        Vector3 movement = player.position - previousPlayerPosition;
        previousPlayerPosition = player.position;

        Vector3 targetLookAhead = Vector3.zero;

        if (Time.deltaTime > 0f)
        {
            Vector3 velocity = movement / Time.deltaTime;

            // 画面はYZ平面なので、奥行きにあたるXは先読みしない
            velocity.x = 0f;

            float speedRatio = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.01f, lookAheadMaxSpeed));
            targetLookAhead = velocity.normalized * (lookAheadDistance * speedRatio);
        }

        currentLookAhead = Vector3.SmoothDamp(
            currentLookAhead,
            targetLookAhead,
            ref lookAheadVelocity,
            lookAheadSmoothTime
        );

        return currentLookAhead;
    }

    /// <summary>
    /// このフレームの揺れ幅を求める。時間が経つほど揺れが収まっていく。
    /// </summary>
    private Vector3 CalculateShakeOffset()
    {
        if (Time.timeScale == 0f) return Vector3.zero;
        if (shakeRemainingTime <= 0f || shakeTotalDuration <= 0f) return Vector3.zero;

        shakeRemainingTime = Mathf.Max(0f, shakeRemainingTime - Time.deltaTime);

        // 残り時間の割合をそのまま減衰に使う（1→0）
        float decay = shakeRemainingTime / shakeTotalDuration;
        float amount = maxShakeAmount * shakeStrength * decay;

        // 画面はYZ平面なので、その2軸だけを揺らす
        return new Vector3(
            0f,
            Random.Range(-amount, amount),
            Random.Range(-amount, amount)
        );
    }
}
