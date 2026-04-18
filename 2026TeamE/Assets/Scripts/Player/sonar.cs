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
        // 1. 半径を広げる
        currentRadius += expansionSpeed * Time.deltaTime;

        if (currentRadius > maxRadius)
        {
            Destroy(gameObject);
            return;
        }

        // 2. 視覚的な円を描画
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

            // 【変更点】X座標を0ではなく、少しだけカメラ側（手前）にずらす
            // ※カメラの位置によってマイナスかプラスか変わります。まずは -1.0f などを試してください。
            float xOffset = 5.0f;

            lineRenderer.SetPosition(i, new Vector3(xOffset, y, z));
        }
    }

    // --- ここから検知ロジック ---

    // ソナー（のコライダー）が何かに触れた瞬間に呼ばれる関数
    void OnTriggerEnter(Collider other)
    {
        // ⚠️ デバッグ用：タグに関係なく、触れたもの全てをログに出す！
        Debug.Log("💥ソナーが衝突！ 相手の名前: " + other.gameObject.name + " / タグ: " + other.tag);
        // 触れた相手のTagが "Enemy" だった場合
        if (other.CompareTag("jewelry"))
        {
            Debug.Log("宝石を検知しました！: " + other.gameObject.name);

            
        }
    }
}