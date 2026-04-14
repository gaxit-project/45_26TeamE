using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    Rigidbody rb;
    Animator animator;

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
    private float drillCDstarttime;

    [Header("ライト")]
    [SerializeField] Transform targetLight;

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
        if (!drillFlag)
        {
            rb.MovePosition(rb.position + moveDirection * Speed * Time.fixedDeltaTime);
        }

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

        DestractBlock();
        UpdateAnimation();
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

    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();

    public void OnDrill(InputAction.CallbackContext context)
    {
        if (context.performed) drillFlag = true;
        else if (context.canceled) drillFlag = false;
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

    
}
