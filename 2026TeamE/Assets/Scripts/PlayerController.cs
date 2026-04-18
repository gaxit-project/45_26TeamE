using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class PlayerController : MonoBehaviour
{
    Rigidbody rb;
    Animator animator;

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

    [Header("ドリルアクション")]
    [SerializeField] bool drillFlag = false;
    [SerializeField] float DrillDistance;
    [SerializeField] float DrillCD;
    [SerializeField] Transform miningZoneRoot;
    [SerializeField] private Transform drillPivot;
    [SerializeField] private float maxRotationAngle = 60f;
    private float drillCDstarttime;

    [Header("ライト")]
    [SerializeField] Transform targetLight;

    [Header("バッテリー")]
    [SerializeField] float maxBattery = 1000f;
    [SerializeField] public float currentBattery = 1000f;
    [SerializeField] float drillConsumption = 1f;
    [SerializeField] float SonarConsuption = 200f;


    public bool IsDrilling => drillFlag;
    public bool HasBattery => currentBattery > 0f;

    private Vector2 moveInput;
    private Vector3 moveDirection;
    private bool isJumpPressed;

    void Awake()
    {
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        Speed = normalSpeed;
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        drillCDstarttime = Time.time;

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
        // 「if (!drillFlag)」を削除し、常に移動入力を反映させる
        rb.MovePosition(rb.position + moveDirection * Speed * Time.fixedDeltaTime);

        ApplyCustomGravity();
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
            // 上下入力(moveInput.y)に基づいて角度を計算
            // -1 ～ 1 の入力を、指定した最大角度(例: 60度)に変換
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
        float rayDistance = 0.2f;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f + new Vector3(0, 0, -1);
        Vector3 rayOrigin2 = transform.position + Vector3.up * 0.1f + new Vector3(0, 0, 1);
        isGround = Physics.Raycast(rayOrigin, Vector3.down, rayDistance, landLayer) ||
                   Physics.Raycast(rayOrigin2, Vector3.down, rayDistance, landLayer);
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
        // --- Added: カメラのアニメーターにフラグを送信 ---（硬い岩実装したらフラグの名前変えて実装可能）
        //if (cameraAnimator != null)
        //{
            //cameraAnimator.SetBool("drillFlag", drillFlag);
        //}
        // --------------------------------------------
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnDrill(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            drillFlag = true;
            SoundManager.Instance.PlaySE("ドリル");
        }
        else if (context.canceled)
        {
            SoundManager.Instance.StopSE();
            drillFlag = false;
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
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

    public void OnSonar(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (currentBattery >= SonarConsuption)
            {
                Instantiate(sonar, transform.position, Quaternion.identity);
                currentBattery -= SonarConsuption;
            }
            
        }
    }
}
