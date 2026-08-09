using UnityEngine;

// LineRendererとSphereColliderが自動で追加されるようにする
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(SphereCollider))]
public class Sonar : MonoBehaviour
{
    public float currentRadius = 1.0f; // 現在の半径
    public float expansionSpeed = 20f; // 広がるスピード
    public float maxRadius = 30.0f; // 消えるまでの最大半径
    public int segments = 36; // 円を構成する点の数
    public int sonarLV = 1;
    public int makertime = 10; //マーカー表示の時間

    // 最大サイズで止めておく時間
    public float holdTime = 0.3f;
    // 現在止まっているかどうかのフラグ
    private bool isHolding = false;

    private LineRenderer lineRenderer;
    private SphereCollider sphereCollider;

    void Start()
    {
        sonarLV = UpgradeManager.GetLevel("Sonar");
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;

        sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = currentRadius;

        Debug.Log("ソナー発射");
    }

    void Update()
    {
        // 止まっていない場合のみ、半径を広げる
        if (!isHolding)
        {
            currentRadius += expansionSpeed * Time.deltaTime;

            // 最大サイズに到達した瞬間の処理
            if (currentRadius >= CurrentMaxSonarRadius)
            {
                currentRadius = CurrentMaxSonarRadius;
                isHolding = true;

                // Destroyの第2引数に秒数を指定すると、その時間待機してから消去する
                Destroy(gameObject, holdTime);
            }
        }

        // 視覚的な円を描画
        LineRendererCircleUtil.DrawCircle(lineRenderer, currentRadius, segments);

        // 当たり判定のコライダーの大きさも連動させる
        sphereCollider.radius = currentRadius;
    }

    public float CurrentMaxSonarRadius
    {
        get
        {
            int sonarLevel = UpgradeManager.GetLevel(UpgradeManager.SONAR);
            float radiusBonus = (sonarLevel - 1) * 1.5f;
            return maxRadius + radiusBonus;
        }
    }

    // ソナーが何かに触れた瞬間に呼ばれる関数
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("jewelry"))
        {
            Debug.Log("宝石を検知しました！: " + other.gameObject.name);
        }
    }
}