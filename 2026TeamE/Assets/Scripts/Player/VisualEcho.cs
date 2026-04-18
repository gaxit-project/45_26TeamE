using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class VisualEcho : MonoBehaviour
{
    public float currentRadius = 0.5f;
    public float expansionSpeed = 15.0f; // 少し早めに広がる設定
    public float maxRadius = 5.0f;       // 自機の波紋より小さめに消える
    public int segments = 36;

    // インスペクターで波紋の色を決められるようにする（例：黄色や水色）
    public Color echoColor = Color.blue;

    private LineRenderer lineRenderer;
    private Material lineMaterial;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false; // ローカル空間を使用

        // マテリアルを取得して、初期色を設定
        lineMaterial = lineRenderer.material;
    }

    void Update()
    {
        currentRadius += expansionSpeed * Time.deltaTime;

        // --- フェードアウトの計算 ---
        // 現在の半径が最大半径に近づくほど、アルファ値（透明度）を1から0に近づける
        float alpha = 1.0f - (currentRadius / maxRadius);

        // 色にアルファ値を適用
        Color currentColor = echoColor;
        currentColor.a = alpha;

        // Line Rendererの色を更新
        lineRenderer.startColor = currentColor;
        lineRenderer.endColor = currentColor;

        if (currentRadius > maxRadius)
        {
            Destroy(gameObject); // 消滅
            return;
        }

        DrawCircle();
    }

    void DrawCircle()
    {
        lineRenderer.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;

            // 縦（Y）と横（Z）に広げる
            float z = Mathf.Cos(angle) * currentRadius;
            float y = Mathf.Sin(angle) * currentRadius;

            // 地面に隠れないように X=5 の手前に描画する
            lineRenderer.SetPosition(i, new Vector3(5f, y, z));
        }
    }
}