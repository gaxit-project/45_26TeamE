using UnityEngine;
using UnityEngine.InputSystem; // 必須

public class PlayerController : MonoBehaviour
{
    Rigidbody rb;
    Animator animator;

    [Header("プレイヤーパラメータ")]
    [SerializeField] float Speed = 5f;
    [SerializeField] float normalSpeed = 5f;
    [SerializeField] float dashSpeed = 10f;
    [SerializeField] float jumpPower = 5f;

    [Header("接地検知設定")]
    [SerializeField] LayerMask landLayer;
    [SerializeField] bool isGround = true;

    [Header("ドリル設定")]
    [SerializeField] bool drillFlag = false;

    private Vector2 moveInput; // Vector3からVector2に変更（入力値用）
    private Vector3 moveDirection;

    void Start()
    {
        Speed = normalSpeed;
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        // 実際の移動処理（例）
        rb.MovePosition(rb.position + moveDirection * Speed * Time.fixedDeltaTime);
    }

    private void CheckGround()
    {
        // 足元から少し高い位置(0.1f)から下向きに、距離0.2fだけレイを飛ばす
        // ※キャラの原点が足元にある前提です
        float rayDistance = 0.2f;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

        // レイを可視化（デバッグ用）
        Debug.DrawRay(rayOrigin, Vector3.down * rayDistance, Color.red);

        // 指定したレイヤー(landLayer)に当たれば接地とみなす
        isGround = Physics.Raycast(rayOrigin, Vector3.down, rayDistance, landLayer);
    }

    void Update()
    {
        CheckGround();

        // 入力の横方向(x)を、ワールド座標のZ軸の動きに変換
        float zMove = moveInput.x;
        moveDirection = new Vector3(0, 0, zMove).normalized;

        // 【追加】向きの回転処理
        if (moveInput.x > 0)
        {
            // 右(+Z)へ移動するとき：0度
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else if (moveInput.x < 0)
        {
            // 左(-Z)へ移動するとき：180度
            transform.rotation = Quaternion.Euler(0, 180, 0);
        }

        UpdateAnimation();
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
    }


    // Input Actionから呼ばれる移動入力用メソッド
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnDrill(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            drillFlag = true;
        }
        else if (context.canceled)
        {
            drillFlag = false;
        }
        animator.SetBool("drillFlag", drillFlag);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed && isGround)
        {
            rb.AddForce(transform.up * jumpPower, ForceMode.Impulse);
            isGround = false;
        }
    }
}
