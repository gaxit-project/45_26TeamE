using UnityEngine;

public class JewelBehaviour : MonoBehaviour, ICollectible
{
    [SerializeField] private ItemData data;

    public void Collect()
    {
        if(VoxelTerrain.Instance != null)
        {
            VoxelTerrain.Instance.CollectedJewel(transform.position);
        }
        if(ItemInventoryManager.Instance != null)
        {
            ItemInventoryManager.Instance.AddItem(ItemType.Jewelry, data.uiIcon, transform.position);
        }
    }
}
