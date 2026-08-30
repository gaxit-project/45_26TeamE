using UnityEngine;





public static class LineRendererCircleUtil
{
    public static void DrawCircle(LineRenderer lineRenderer, float radius, int segments, float xOffset = 5f)
    {
        lineRenderer.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float z = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;

            lineRenderer.SetPosition(i, new Vector3(xOffset, y, z));
        }
    }
}
