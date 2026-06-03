using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class PlayerController : MonoBehaviour
{
    Rigidbody rb;
    Animator animator;

    private bool CanMove => PlayerPrefs.GetInt("CanMove", 0) == 1;

    public GameObject sonar;
    // --- Added: カメラ連携用の変数 ---
    [Header("カメラ連携")]
    [SerializeField] Animator cameraAnimator;
    // -------------------------------

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

    public int DrillLevel => drillLevel;
    public void UpgradeDrill() => drillLevel++;

    public bool IsDrilling => drillFlag;
    public bool HasBattery => currentBattery > 0f;
    public bool IsDashing => Time.time < dashEndTime;

    private Vector2 moveInput;
    private Vector3 moveDirection;
    private bool isJumpPressed;

    private PoseManager poseManager;

    void Awake()
    {
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        Time.timeScale = 1f;
        Speed = normalSpeed;
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        animator = GetComponent<Animator>();
        drillCDstarttime = Time.time;
        poseManager = GetComponent<PoseManager>();

        // --- Added: インスペクターで未設定の場合、メインカメラから取得を試みる ---
        if (cameraAnimator == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null) cameraAnimator = mainCam.GetComponent<Animator>();
        }
        // ------------------------------------------------------------------
    }

    void FixedUpdate()
    {
        if (!CanMove) return;
        if (poseManager != null && poseManager.IsPaused) return; // ポーズ中は処理をスキップ
        
        if (Time.time < dashEndTime)
        {
            wasDashing = true;
            // ダッシュ中は重力を無視して一定速度を代入
            rb.linearVelocity = dashDirection * dashDistance;
        }
        else
        {
            if (wasDashing)
            {
                wasDashing = false;
                // ダッシュ終了時に慣性を消してピタッと止める
                rb.linearVelocity = Vector3.zero;
            }

            Vector3 targetVelocity = moveDirection * Speed;
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
            ApplyCustomGravity();
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

    void Update()
    {
        if (!CanMove) return;
        if (poseManager != null && poseManager.IsPaused) return; // ポーズ中は処理をスキップ
        CheckGround();

        float zMove = moveInput.x;
        moveDirection = new Vector3(0, 0, zMove).normalized;

        if(drillFlag && HasBattery)
        {
            SoundManager.Instance.PlayLoopSE("ドリル");
        }
        else
        {
            SoundManager.Instance.StopLoopSE();
        }

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
            //currentBattery -= drillConsumption * Time.deltaTime;
            if (currentBattery < 0) currentBattery = 0;
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
    }

    private void HandleDrillRotation()
    {
        if (drillPivot == null) return;

        if (drillFlag)
        {
            // 上下入力(moveInput.y)に基づいた角度を計算
            // -1 〜 1 の入力を、指定した最大角度(例: 60度)に変換
            float targetAngle = moveInput.y * maxRotationAngle;

            // X軸を中心に回転させる（上下に振る）
            // ローカル回転を使うことで、プレイヤーが左右どちらを向いていても正しく動く
            drillPivot.localRotation = Quaternion.Euler(-targetAngle, 0, 0);
        }
        else
        {
            // 掘っていない時は正面(0度)にゆっくり戻す、または即座に戻す
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
        // ブロック地形（Voxel）の角に最適化するため、四角い箱（Box）の判定を使う
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

        // 即座に速度を代入（FixedUpdateでも継続して代入される）
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
        // --- Added: カメラのアニメーターにフラグを送信 ---
        //if (cameraAnimator != null)
        //{
            //cameraAnimator.SetBool("drillFlag", drillFlag);
        //}
        // --------------------------------------------
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!CanMove) return;
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnDrill(InputAction.CallbackContext context)
    {
        if (!CanMove) return;
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
        // 着地判定（CheckBox）の形をシーンビューに表示する
        // 地面についている時は緑、浮いている時は赤にする
        Gizmos.color = isGround ? new Color(0, 1, 0, 0.3f) : new Color(1, 0, 0, 0.3f);
        Vector3 boxCenter = transform.position + groundBoxOffset;
        Vector3 size = groundBoxExtents * 2f; // extentsを2倍にしてSizeにする
        
        // 半透明の箱を描画
        Gizmos.DrawCube(boxCenter, size);
        
        // はっきりとした枠線を描画
        Gizmos.color = isGround ? Color.green : Color.red;
        Gizmos.DrawWireCube(boxCenter, size);
    }

    public void EnablePlayerControl()
    {
        PlayerPrefs.SetInt("CanMove", 1);
        PlayerPrefs.Save();
    }
}