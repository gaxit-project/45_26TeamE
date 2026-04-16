using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SonarRing : MonoBehaviour
{
    public float currentRadius = 1.0f; // Œ»İ‚Ì”¼Œa
    public float expansionSpeed = 5.0f; // L‚ª‚éƒXƒs[ƒh
    public float maxRadius = 10.0f; // Á‚¦‚é‚Ü‚Å‚ÌÅ‘å”¼Œa
    public int segments = 36; // ‰~‚ğ\¬‚·‚é“_‚Ì”

    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    void Update()
    {
        // 1. ”¼Œa‚ğL‚°‚é
        currentRadius += expansionSpeed * Time.deltaTime;

        // 2. Å‘å”¼Œa‚É’B‚µ‚½‚çÁ‹
        if (currentRadius > maxRadius)
        {
            Destroy(gameObject);
            return;
        }

        // 3. ‰~‚ÌŒ`‚ğŒvZ‚µ‚Ä•`‰æ
        DrawCircle();
    }

    void DrawCircle()
    {
        lineRenderer.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;

            // ‰¡²(Z)‚Æc²(Y)‚ÉŒvZŒ‹‰Ê‚ğŠ„‚è“–‚Ä‚é
            float z = Mathf.Cos(angle) * currentRadius;
            float y = Mathf.Sin(angle) * currentRadius;

            // XÀ•W‚Í0i‚ ‚é‚¢‚Í©‹@‚ÌXˆÊ’uj‚ÉŒÅ’è‚·‚é
            lineRenderer.SetPosition(i, new Vector3(0, y, z));
        }
    }
}