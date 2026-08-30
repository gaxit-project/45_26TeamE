using UnityEngine;

public class Block_dirt : MonoBehaviour
{
    [SerializeField, Header("�u���b�N�̗̑�")] int maxHP = 10;
    public int currentHP;

    [Header("�i�K���Ƃ̃}�e���A��")]
    public Material matNormal;  
    public Material matCracked; 
    public Material matBroken;  

    private MeshRenderer meshRenderer;

    
    private bool isDead = false;

    void Start()
    {
        currentHP = maxHP;
        meshRenderer = GetComponent<MeshRenderer>();
        UpdateAppearance();
    }

    public void TakeDamage(int damageAmount)
    {
        
        if (isDead) return;

        currentHP -= damageAmount;

        if (currentHP <= 0)
        {
            isDead = true; 

            DestroyChain();
            Destroy(gameObject);
        }
        else
        {
            UpdateAppearance();
        }
    }

    private void UpdateAppearance()
    {
        
        float healthRatio = (float)currentHP / maxHP;

        if (healthRatio == 1.0f)
        {
            meshRenderer.material = matNormal;
        }
        else if (healthRatio > 0.33f) 
        {
            meshRenderer.material = matCracked;
        }
        else
        {
            meshRenderer.material = matBroken;
        }
    }

    private void DestroyChain()
    {
        
        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        float maxDistance = 0.55f;

        foreach (Vector3 dir in directions)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, dir, out hit, maxDistance))
            {
                if (hit.collider.CompareTag("Block_dirt"))
                {
                    Block_dirt targetBlock = hit.collider.GetComponent<Block_dirt>();

                    if (targetBlock != null)
                    {
                        
                        if (targetBlock.currentHP == 1)
                        {
                            
                            targetBlock.TakeDamage(1);
                        }
                        else
                        {
                            
                            
                            targetBlock.TakeDamage(targetBlock.maxHP / 3);
                        }
                    }
                }
            }
        }
    }
}