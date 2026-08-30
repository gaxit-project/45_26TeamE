using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class VisualEcho : MonoBehaviour
{
    public float currentRadius = 0.5f;
    public float expansionSpeed = 15.0f; 
    public float maxRadius = 5.0f;       
    public int segments = 36;

    
    public Color echoColor = Color.blue;

    private LineRenderer lineRenderer;
    private Material lineMaterial;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false; 

        
        lineMaterial = lineRenderer.material;
    }

    void Update()
    {
        currentRadius += expansionSpeed * Time.deltaTime;

        
        
        float alpha = 1.0f - (currentRadius / maxRadius);

        
        Color currentColor = echoColor;
        currentColor.a = alpha;

        
        lineRenderer.startColor = currentColor;
        lineRenderer.endColor = currentColor;

        if (currentRadius > maxRadius)
        {
            Destroy(gameObject); 
            return;
        }

        LineRendererCircleUtil.DrawCircle(lineRenderer, currentRadius, segments);
    }
}
