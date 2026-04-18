using UnityEngine;

// LineRendererとSphereColliderが自動で追加されるようにする
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(SphereCollider))]
public class Sonar : MonoBehaviour
{
    public float currentRadius = 1.0f; // 現在の半径
    public float expansionSpeed = 5.0f; // 広がるスピード
    public float maxRadius = 10.0f; // 消えるまでの最大半径
    public int segments = 36; // 円を構成する点の数

    // 【追加】最大サイズで止めておく時間
    public float holdTime = 0.3f;
    // 【追加】現在止まっている（ホールド中）かどうかのフラグ
    private bool isHolding = false;

    private LineRenderer lineRenderer;
    private SphereCollider sphereCollider;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;

        // コライダーの取得と初期設定
        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = currentRadius; // 初期サイズを視覚と合わせる

        Debug.Log("ソナー発射");
    }

    void Update()
    {
        // 1. まだ止まっていない場合のみ、半径を広げる
        if (!isHolding)
        {
            currentRadius += expansionSpeed * Time.deltaTime;

            // 最大サイズに到達した瞬間の処理
            if (currentRadius >= maxRadius)
            {
                currentRadius = maxRadius; // サイズを最大値にピタッと固定する
                isHolding = true;          // 「ホールド中」状態にする

                // 【ここがポイント！】Destroyの第2引数に秒数を指定すると、その時間待機してから消去してくれます
                Destroy(gameObject, holdTime);
            }
        }

        // 2. 視覚的な円を描画（ホールド中も描画を維持する）
        DrawCircle();

        // 3. 当たり判定のコライダーの大きさも連動させる
        sphereCollider.radius = currentRadius;
    }

    void DrawCircle()
    {
        lineRenderer.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;

            float z = Mathf.Cos(angle) * currentRadius;
            float y = Mathf.Sin(angle) * currentRadius;

            // X座標を0ではなく、少しだけカメラ側（手前）にずらす
            float xOffset = 5.0f;

            lineRenderer.SetPosition(i, new Vector3(xOffset, y, z));
        }
    }

    // --- ここから検知ロジック ---

    // ソナー（のコライダー）が何かに触れた瞬間に呼ばれる関数
    void OnTriggerEnter(Collider other)
    {
        // ⚠️ デバッグ用：タグに関係なく、触れたもの全てをログに出す！
        //Debug.Log("💥ソナーが衝突！ 相手の名前: " + other.gameObject.name + " / タグ: " + other.tag);

        // 触れた相手のTagが "jewelry" だった場合
        if (other.CompareTag("jewelry"))
        {
            Debug.Log("宝石を検知しました！: " + other.gameObject.name);
        }
    }
}