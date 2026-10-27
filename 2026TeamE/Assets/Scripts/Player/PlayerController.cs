using System.Collections;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class PlayerController : MonoBehaviour
{
    Rigidbody rb;
    Animator animator;

    private bool CanMove = false;

    [Header("操作解禁の保険")]
    [Tooltip("導入演出が何らかの理由で終わらなかった場合に、強制的に操作を解禁するまでの秒数。0以下で無効。")]
    [SerializeField] private float controlEnableTimeout = 20f;

    public enum PlayerState
    {
        Normal,
        TakingDamage,
        GameOver,
        GameClear
    }
    public PlayerState currentState = PlayerState.Normal;

    [Header("ダメージ演出")]
    [SerializeField] private Renderer[] characterRenderers;
    [SerializeField] private float blinkInterval = 0.1f;
    [SerializeField] private Transform dropPoint;
    [SerializeField] private float dropForce = 5f;

    public GameObject sonar;

    [Header("プレイヤーパラメータ")]
    [SerializeField] float Speed = 5f;
    [SerializeField] float normalSpeed = 5f;
    [SerializeField] float dashSpeed = 10f;
    [SerializeField] float jumpPower = 5f;

    [Header("重力パラメータ")]
    [SerializeField] float fallMultiplier = 4f;
    [SerializeField] float lowJumpMultiplier = 3f;

    [Header("接地判定")]
    [SerializeField] LayerMask landLayer;
    [SerializeField] bool isGround = true;
    [SerializeField] Vector3 groundBoxExtents = new Vector3(0.35f, 0.15f, 0.35f);
    [SerializeField] Vector3 groundBoxOffset = new Vector3(0, 0.1f, 0);

    [Header("ドリルアクション")]
    [SerializeField] bool drillFlag = false;
    [SerializeField] float DrillDistance;
    [SerializeField] float DrillCD;
    [SerializeField] Transform miningZoneRoot;
    [SerializeField] private Transform drillPivot;
    [SerializeField] private float maxRotationAngle = 60f;
    [SerializeField] int drillLevel = 1;
    private float drillCDstarttime;

    [Header("Dash Settings")]
    [SerializeField] private int dashPower = 5;
    [SerializeField] private float dashCooldown = 1.0f;
    [SerializeField] private float dashDistance = 10.0f;
    [SerializeField] private float dashDuration = 0.3f;
    private float lastDashTime = -100f;
    private float dashEndTime = -100f;
    private Vector3 dashDirection;
    private bool wasDashing = false;

    [Header("ライト")]
    [SerializeField] Transform targetLight;

    [Header("バッテリー")]
    [SerializeField] float maxBattery = 1000f;
    [SerializeField] public float currentBattery = 1000f;
    [SerializeField] float drillConsumption = 1f;
    [SerializeField] float SonarConsuption = 200f;

    [Header("JetPack")]
    [SerializeField] float baseJetpackForce = 15f;
    [SerializeField] float baseJetpackMaxSpeed = 6f;

    [Header("JetPack Effect")]
    [SerializeField] private ParticleSystem jetpackEffectLeft;
    [SerializeField] private ParticleSystem jetpackEffectRight;
    public int DrillLevel => drillLevel;
    public void UpgradeDrill() => drillLevel++;

    public static PlayerController Instance { get; private set; }

    public bool IsDrilling => IsControlEnabled && drillFlag;

    /// <summary>プレイヤーの操作が解禁されており、かつ行動可能な状態か。ムービー中や被弾・ポーズ中はfalse。</summary>
    public bool IsControlEnabled => CanMove && currentState == PlayerState.Normal && (poseManager == null || !poseManager.IsInputBlocked) && Time.timeScale > 0f && !TimelineManager.IsPlaying;
    public bool HasBattery => currentBattery > 0f;

    /// <summary>
    /// バッテリーを満タンに戻す。換金所・強化画面から戻ったときに呼ぶ。
    /// </summary>
    public void RefillBattery()
    {
        currentBattery = maxBattery;
    }
    public bool IsDashing => Time.time < dashEndTime;

    private Vector2 moveInput;
    private Vector3 moveDirection;
    private bool isJumpPressed;

    private PoseManager poseManager;
    private DrillTip drillTip;
    private bool isRumbling = false;

    void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        drillFlag = false;
        if (isRumbling && HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Stop();
            isRumbling = false;
        }
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopLoopSE();
        }
    }

    private void UpdateRumbleState()
    {
        if (HapticsManager.Instance == null) return;

        bool contacting = drillTip != null && drillTip.IsContactingDiggableSurface;
        bool shouldRumble = IsControlEnabled && drillFlag && contacting;

        if (shouldRumble)
        {
            if (!isRumbling)
            {
                HapticsManager.Instance.PlayContinuous(0.2f, 0.4f);
                isRumbling = true;
            }
        }
        else
        {
            if (isRumbling)
            {
                HapticsManager.Instance.Stop();
                isRumbling = false;
            }
        }
    }

    /// <summary>
    /// 導入演出が終わらず操作が解禁されないまま進行不能になるのを防ぐための保険。
    /// 一定時間が過ぎても操作できない場合は強制的に解禁する。
    /// </summary>
    private IEnumerator ControlEnableWatchdog()
    {
        if (controlEnableTimeout <= 0f) yield break;

        float elapsed = 0f;
        while (!CanMove && elapsed < controlEnableTimeout)
        {
            // ロード中はtimeScaleが0になるため、実時間で計測する
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!CanMove)
        {
            Debug.LogWarning($"[PlayerController] {controlEnableTimeout}秒経っても操作が解禁されなかったため、強制的に解禁しました。導入演出の設定を確認してください。");
            EnablePlayerControl();
        }
    }

    void Start()
    {
        currentState = PlayerState.Normal;
        StartCoroutine(ControlEnableWatchdog());

        // メインシーンに入るたびにバッテリーを満タンにする。
        // これまではシーン再読込でSerializeFieldの初期値に戻るという副作用に頼っていたため、
        // 読み込み方や初期値の変更で簡単に壊れる状態だった。
        currentBattery = maxBattery;

        if (characterRenderers == null || characterRenderers.Length == 0)
        {
            characterRenderers = GetComponentsInChildren<Renderer>()
                .Where(r => !(r is ParticleSystemRenderer) && r.enabled)
                .ToArray();
        }

        bool isLoading = SceneLoader.Instance != null && SceneLoader.Instance.IsLoading;
        if (!isLoading)
        {
            Time.timeScale = 1f;
        }

        Speed = normalSpeed;
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        animator = GetComponent<Animator>();
        drillCDstarttime = Time.time;
        poseManager = GetComponent<PoseManager>();

        if (jetpackEffectLeft != null)
            jetpackEffectLeft.Stop();

        if (jetpackEffectRight != null)
            jetpackEffectRight.Stop();

        drillTip = GetComponentInChildren<DrillTip>();
    }

    void FixedUpdate()
    {
        if (!IsControlEnabled) return;

        if (Time.time < dashEndTime)
        {
            wasDashing = true;
            rb.linearVelocity = dashDirection * dashDistance;
        }
        else
        {
            if (wasDashing)
            {
                wasDashing = false;
                rb.linearVelocity = Vector3.zero;
            }

            Vector3 targetVelocity = moveDirection * Speed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

            if (!isGround && isJumpPressed)
            {
                if (rb.linearVelocity.y < CurrentJetpackMaxSpeed)
                {
                    rb.AddForce(
                        Vector3.up * CurrentJetpackForce,
                        ForceMode.Acceleration
                    );
                }
            }

            ApplyCustomGravity();
        }
    }

    private float CurrentJetpackForce
    {
        get
        {
            int level = UpgradeManager.GetLevel(UpgradeManager.JET);
            return baseJetpackForce + (level - 1) * 5f;
        }
    }

    private float CurrentJetpackMaxSpeed
    {
        get
        {
            int level = UpgradeManager.GetLevel(UpgradeManager.JET);
            return baseJetpackMaxSpeed + (level - 1) * 2f;
        }
    }

    private bool IsJetpacking
    {
        get
        {
            return !isGround && isJumpPressed;
        }
    }

    private void ApplyCustomGravity()
    {
        if (rb.linearVelocity.y < 0 && !isJumpPressed)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !isJumpPressed)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void Update()
    {
        if (!IsControlEnabled)
        {
            drillFlag = false;
            isJumpPressed = false;
            moveInput = Vector2.zero;
            if (animator != null)
            {
                animator.SetBool("drillFlag", false);
                animator.SetFloat("Move_X", 0f);
            }
            UpdateRumbleState();
            return;
        }
        CheckGround();

        float zMove = moveInput.x;
        moveDirection = new Vector3(0, 0, zMove).normalized;

        if (moveInput.x > 0)
        {
            if (transform.rotation.eulerAngles.y != 0)
            {
                transform.rotation = Quaternion.Euler(0, 0, 0);
                FlipLightX();
            }
        }
        else if (moveInput.x < 0)
        {
            if (transform.rotation.eulerAngles.y != 180)
            {
                transform.rotation = Quaternion.Euler(0, 180, 0);
                FlipLightX();
            }
        }

        if (drillFlag)
        {
            float angle = Mathf.Atan2(moveInput.y, Mathf.Abs(moveInput.x)) * Mathf.Rad2Deg;
            miningZoneRoot.localRotation = Quaternion.Euler(angle, 0, 0);
        }
        else
        {
            miningZoneRoot.localRotation = Quaternion.Euler(0, 0, 0);
        }

        UpdateAnimation();
        HandleDrillRotation();
        UpdateJetpackEffects();
        UpdateRumbleState();
    }

    private void UpdateJetpackEffects()
    {
        if (jetpackEffectLeft == null || jetpackEffectRight == null)
            return;

        if (IsJetpacking)
        {
            if (!jetpackEffectLeft.isPlaying)
                jetpackEffectLeft.Play();

            if (!jetpackEffectRight.isPlaying)
                jetpackEffectRight.Play();
        }
        else
        {
            if (jetpackEffectLeft.isPlaying)
                jetpackEffectLeft.Stop();

            if (jetpackEffectRight.isPlaying)
                jetpackEffectRight.Stop();
        }
    }

    private void HandleDrillRotation()
    {
        if (drillPivot == null) return;

        if (drillFlag)
        {
            float targetAngle = moveInput.y * maxRotationAngle;
            drillPivot.localRotation = Quaternion.Euler(-targetAngle, 0, 0);
        }
        else
        {
            drillPivot.localRotation = Quaternion.Slerp(drillPivot.localRotation, Quaternion.identity, Time.deltaTime * 10f);
        }
    }

    private void FlipLightX()
    {
        if (targetLight != null)
        {
            Vector3 pos = targetLight.localPosition;
            pos.x *= -1;
            targetLight.localPosition = pos;
        }
    }

    private void CheckGround()
    {
        Vector3 boxCenter = transform.position + groundBoxOffset;
        isGround = Physics.CheckBox(boxCenter, groundBoxExtents, Quaternion.identity, landLayer);
    }

    private void PerformDash()
    {
        if (!CanMove) return;
        dashEndTime = Time.time + dashDuration;

        dashDirection = moveInput.magnitude > 0.1f ?
            new Vector3(0, moveInput.y, moveInput.x).normalized : transform.forward;

        rb.linearVelocity = dashDirection * dashDistance;

        Ray ray = new Ray(transform.position + new Vector3(0, 2, 0), dashDirection);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, DrillDistance))
        {
            if (hit.collider.CompareTag("Block_dirt"))
            {
                Block_dirt targetBlock = hit.collider.GetComponent<Block_dirt>();
                if (targetBlock != null)
                {
                    targetBlock.TakeDamage(dashPower);
                }
            }
        }
    }

    private void UpdateAnimation()
    {
        animator.SetFloat("Move_X", Mathf.Abs(moveInput.x));
        if (drillFlag)
        {
            animator.SetFloat("Drill_Z", moveInput.x);
            animator.SetFloat("Drill_Y", moveInput.y);
        }
        animator.SetBool("isGround", isGround);
        animator.SetBool("drillFlag", drillFlag);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsControlEnabled)
        {
            moveInput = Vector2.zero;
            return;
        }
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnDrill(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (!IsControlEnabled)
            {
                drillFlag = false;
                return;
            }

            drillFlag = true;

            if (Time.time >= lastDashTime + dashCooldown)
            {
                lastDashTime = Time.time;
                PerformDash();
            }
        }
        else if (context.canceled)
        {
            drillFlag = false;
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (!IsControlEnabled)
            {
                isJumpPressed = false;
                return;
            }

            isJumpPressed = true;

            if (isGround)
            {
                rb.AddForce(transform.up * jumpPower, ForceMode.Impulse);
                isGround = false;
            }
            else
            {
                // 空中でジェットパックを作動させた瞬間、落下中なら速度をリセットして即座に浮上できるようにする
                if (rb.linearVelocity.y < 0)
                {
                    rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                }
            }
        }
        else if (context.canceled)
        {
            isJumpPressed = false;
        }
    }

    public void SetDrillLevel(int newLevel)
    {
        drillLevel = newLevel;
    }

    public void OnSonar(InputAction.CallbackContext context)
    {
        if (!IsControlEnabled) return;

        if (context.performed)
        {
            int sonarLevel = UpgradeManager.GetLevel(UpgradeManager.SONAR);
            if (sonarLevel >= 3)
            {
                SonarConsuption = 100;
            }
            else
            {
                SonarConsuption = 200;
            }
            if (currentBattery >= SonarConsuption)
            {
                Instantiate(sonar, transform.position, Quaternion.identity);
                currentBattery -= SonarConsuption;
                GameLogger.Event("Sonar", $"battery={currentBattery:F0}");
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isGround ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);
        Vector3 boxCenter = transform.position + groundBoxOffset;
        Vector3 size = groundBoxExtents * 2f;

        Gizmos.DrawCube(boxCenter, size);

        Gizmos.color = isGround ? Color.green : Color.red;
        Gizmos.DrawWireCube(boxCenter, size);
    }

    public void EnablePlayerControl()
    {
        CanMove = true;
        GameLogger.Event("ControlEnabled");
        if (TextManager.Instance != null)
        {
            TextManager.Instance.ShowText("お宝を掘り当てて下へ進もう！");
        }
    }

    public void DisablePlayerControl()
    {
        CanMove = false;
        drillFlag = false;
        isJumpPressed = false;
        moveInput = Vector2.zero;
        if (animator != null)
        {
            animator.SetBool("drillFlag", false);
            animator.SetFloat("Move_X", 0f);
        }
        if (isRumbling && HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Stop();
            isRumbling = false;
        }
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopLoopSE();
        }
    }

    /// <summary>
    /// プレイヤーの向きを右向き（Y軸0度）にする。
    /// ダンスアニメーションなどの右向き前提演出で使用。
    /// </summary>
    public void FaceRight()
    {
        if (Mathf.Abs(Mathf.DeltaAngle(transform.rotation.eulerAngles.y, 0f)) > 1f)
        {
            transform.rotation = Quaternion.Euler(0, 0, 0);
            FlipLightX();
        }

        if (miningZoneRoot != null)
        {
            miningZoneRoot.localRotation = Quaternion.Euler(0, 0, 0);
        }
        if (drillPivot != null)
        {
            drillPivot.localRotation = Quaternion.identity;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ICollectible collectible = other.GetComponent<ICollectible>();
        if (collectible != null)
        {
            collectible.Collect();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        ICollectible collectible = other.GetComponent<ICollectible>();
        if (collectible != null)
        {
            collectible.Collect();
        }
    }

    // アニメーションイベント用
    public void DamageAnim()
    {
    }

    public void TakeDamageWithMoney(Sprite dropIcon = null)
    {
        if (currentState == PlayerState.Normal)
        {
            GameLogger.Event("Damage", $"battery={currentBattery:F0}");
            StartCoroutine(DamageRoutine(dropIcon));
        }
    }

    private System.Collections.IEnumerator DamageRoutine(Sprite dropIcon)
    {
        currentState = PlayerState.TakingDamage;

        // 被弾時にすべての入力状態をリセットし、直立（待機）状態に戻す
        drillFlag = false;
        isJumpPressed = false;
        moveInput = Vector2.zero;
        if (isRumbling && HapticsManager.Instance != null)
        {
            HapticsManager.Instance.Stop();
            isRumbling = false;
        }
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopLoopSE();
        }
        UpdateAnimation();

        animator.SetTrigger("Damage");

        // 速度をゼロにして慣性で滑るのを防ぐ
        rb.linearVelocity = Vector3.zero;

        if (dropIcon != null)
        {
            int dropCount = 3; // 演出用に適当な数を散らす
            for (int i = 0; i < dropCount; i++)
            {
                DropVisualItem(dropIcon);
            }
        }

        float duration = 1.0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            SetRenderersEnabled(false);
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;

            SetRenderersEnabled(true);
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        SetRenderersEnabled(true); // 確実に表示状態に戻す
        animator.Play("Walk Tree"); // 入力はリセットされているので、Walk Tree内の「待機アニメーション」が再生されます
        currentState = PlayerState.Normal;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        if (characterRenderers == null) return;
        foreach (var r in characterRenderers)
        {
            if (r != null) r.enabled = enabled;
        }
    }

    private void DropVisualItem(Sprite icon)
    {
        if (icon == null) return;

        // 物理計算用の親オブジェクト（コライダーなしで床をすり抜けさせる＝引っかからない）
        GameObject physicsObj = new GameObject("DroppedItemPhysics");
        Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position + Vector3.up;
        physicsObj.transform.position = spawnPos;

        Rigidbody rb = physicsObj.AddComponent<Rigidbody>();
        // X座標の固定と、Y,Z回転の固定（X軸周りのみ回転＝画面上でクルクル回る）
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        Vector3 randomDirection = new Vector3(0f, 1.0f, Random.Range(-1.0f, 1.0f)).normalized;
        rb.AddForce(randomDirection * dropForce, ForceMode.Impulse);
        rb.AddTorque(new Vector3(Random.Range(-15f, 15f), 0f, 0f), ForceMode.Impulse);

        GameObject visualObj = new GameObject("Visual");
        visualObj.transform.SetParent(physicsObj.transform);
        visualObj.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
        sr.sprite = icon;

        // カメラの方を向かせる（1回だけ設定し、あとは親の回転に任せる）
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // カメラのforwardと同じ方向を向かせる（SpriteRendererは-Zが表面なので、これでカメラと正対する）
            visualObj.transform.rotation = Quaternion.LookRotation(mainCam.transform.forward, mainCam.transform.up);
        }

        // 2秒後に自動的に消滅（フェイク演出なので拾えない）
        Destroy(physicsObj, 2.0f);
    }
}