using UnityEngine;


[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(SphereCollider))]
public class Sonar : MonoBehaviour
{
    public float currentRadius = 1.0f; 
    public float expansionSpeed = 20f; 
    public float maxRadius = 30.0f; 
    public int segments = 36; 
    public int sonarLV = 1;
    public int makertime = 10; 

    
    public float holdTime = 0.3f;
    
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
        
        if (!isHolding)
        {
            currentRadius += expansionSpeed * Time.deltaTime;

            
            if (currentRadius >= CurrentMaxSonarRadius)
            {
                currentRadius = CurrentMaxSonarRadius;
                isHolding = true;

                
                Destroy(gameObject, holdTime);
            }
        }

        
        LineRendererCircleUtil.DrawCircle(lineRenderer, currentRadius, segments);

        
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

    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("jewelry"))
        {
            Debug.Log("宝石を検知しました！: " + other.gameObject.name);
        }
    }
}