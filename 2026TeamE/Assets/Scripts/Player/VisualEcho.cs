using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class VisualEcho : MonoBehaviour
{
    public float currentRadius = 0.5f;
    public float expansionSpeed = 15.0f; // 広がる速さ
    public float maxRadius = 5.0f;       // 波紋が消えるまでの最大半径
    public int segments = 36;

    // インスペクターで波紋の色を決められるようにする（基本色・水色など）
    public Color echoColor = Color.blue;

    private LineRenderer lineRenderer;
    private Material lineMaterial;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false; // ローカル空間を使用

        // マテリアルを取得しておき、あとで色を設定する
        lineMaterial = lineRenderer.material;
    }

    void Update()
    {
        currentRadius += expansionSpeed * Time.deltaTime;

        // --- フェードアウトの計算 ---
        // 現在の半径が最大半径に近づくほど、アルファ値（透明度）が1から0に近づく
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

        LineRendererCircleUtil.DrawCircle(lineRenderer, currentRadius, segments);
    }
}
