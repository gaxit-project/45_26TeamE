using UnityEngine;

public class KeyBehaviour : MonoBehaviour, ICollectible
{
    [SerializeField] private ItemData data;
    private bool isGot = false;
    private int myZoneIndex = 0;

    public void Setup(int zoneIndex)
    {
        myZoneIndex = zoneIndex;
    }

    public void Collect()
    {
        if (isGot) return;
        isGot = true;

        if(VoxelTerrain.Instance != null)
        {
            VoxelTerrain.Instance.CollectedKeyDirect(myZoneIndex, transform.position);
        }
        if(SoundManager.Instance != null && data != null && !string.IsNullOrEmpty(data.seName))
        {
            SoundManager.Instance.PlaySE(data.seName);
        }
        if(data != null && data.effectPrefab != null)
        {
            Instantiate(data.effectPrefab, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
