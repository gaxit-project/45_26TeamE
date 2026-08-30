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

    public bool onDamaged = false;

    public GameObject sonar;
    [Header("カメラ連携")]
    [SerializeField] Animator cameraAnimator;

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

    public bool IsDrilling => drillFlag;
    public bool HasBattery => currentBattery > 0f;
    public bool IsDashing => Time.time < dashEndTime;

    private Vector2 moveInput;
    private Vector3 moveDirection;
    private bool isJumpPressed;

    private PoseManager poseManager;
    private DrillTip drillTip;
    private bool isRumbling = false;

    void Awake()
    {
        Application.targetFrameRate = 60;
    }

    private void UpdateRumbleState()
    {
        if (HapticsManager.Instance == null) return;

        bool contacting = drillTip != null && drillTip.IsContactingDiggableSurface;
        bool shouldRumble = drillFlag && contacting;

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

    void Start()
    {
        onDamaged = false;

        
        
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

        if (cameraAnimator == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null) cameraAnimator = mainCam.GetComponent<Animator>();
        }

        drillTip = GetComponentInChildren<DrillTip>();
    }

    void FixedUpdate()
    {
        if (!CanMove) return;
        if (poseManager != null && poseManager.IsPaused) return;

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
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !isJumpPressed)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private bool IsStandingOnRelayZone()
    {
        if (VoxelTerrain.Instance == null) return false;

        Vector3 lp = VoxelTerrain.Instance.transform.InverseTransformPoint(transform.position + Vector3.down * 0.2f);
        int ty = Mathf.FloorToInt(lp.y / VoxelTerrain.Instance.BlockSize);

        return VoxelTerrain.Instance.IsRelayZoneBottom(ty);
    }

    void Update()
    {
        if (!CanMove) return;
        if (poseManager != null && poseManager.IsPaused) return;
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

        if (drillFlag && HasBattery)
        {
            if (currentBattery < 0) currentBattery = 0;
        }

        bool isDownwards = moveInput.y < -0.1f;
        bool isRelayZone = IsStandingOnRelayZone();

        if (drillFlag)
        {
            float effectiveMoveY = (isRelayZone && isDownwards) ? 0f : moveInput.y;
            float angle = Mathf.Atan2(effectiveMoveY, Mathf.Abs(moveInput.x)) * Mathf.Rad2Deg;
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
            bool isDownwards = moveInput.y < -0.1f;
            bool isRelayZone = IsStandingOnRelayZone();

            float effectiveMoveY = (isRelayZone && isDownwards) ? 0f : moveInput.y;

            float targetAngle = effectiveMoveY * maxRotationAngle;
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

    private void DestractBlock()
    {
        if (drillFlag)
        {
            Vector3 drillDirection = moveInput.magnitude > 0.1f ?
                new Vector3(0, moveInput.y, moveInput.x).normalized : transform.forward;

            Ray ray = new Ray(transform.position + new Vector3(0, 2, 0), drillDirection);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, DrillDistance) && Time.time >= drillCDstarttime + DrillCD)
            {
                if (hit.collider.CompareTag("Block_dirt"))
                {
                    Block_dirt targetBlock = hit.collider.GetComponent<Block_dirt>();
                    if (targetBlock != null)
                    {
                        targetBlock.TakeDamage(1);
                        drillCDstarttime = Time.time;
                    }
                }
            }
        }
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
        if (!CanMove) return;
        if (onDamaged) return;
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnDrill(InputAction.CallbackContext context)
    {
        if (!CanMove) return;
        if (onDamaged) return;
        if (poseManager != null && poseManager.IsInputBlocked) return;

        if (context.performed)
        {
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
        if (!CanMove) return;
        if (onDamaged) return;
        if (poseManager != null && poseManager.IsInputBlocked) return;

        if (context.performed)
        {
            isJumpPressed = true;
            if (isGround)
            {
                rb.AddForce(transform.up * jumpPower, ForceMode.Impulse);
                isGround = false;
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
        if (!CanMove) return;
        if (onDamaged) return;
        if (poseManager != null && poseManager.IsInputBlocked) return;

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
        TextManager.Instance.ShowText("鍵を集めて下へ進もう！");
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

    public void DamageAnim()
    {
        if (onDamaged == false)
        {
            onDamaged = true;
            animator.SetTrigger("Damage");
        }
        if (onDamaged == true)
        {
            animator.SetTrigger("Damage");
            onDamaged = false;
        }
    }
}