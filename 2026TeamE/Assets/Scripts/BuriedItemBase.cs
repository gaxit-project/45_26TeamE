using UnityEngine;

public abstract class BuriedItemBase : MonoBehaviour
{
    [Header("クールダウン")]
    public float cooldownTime = 1.0f;

    [Header("デバッグ用（取得可能状態）")]
    public bool isExposed;

    protected bool isCoolingDown = false;
    protected GameObject currentMarker;
    protected float startTime;
    protected float checkDelay = 3.0f;

    protected virtual void Awake()
    {
        isExposed = false;
    }

    protected virtual void Start()
    {
        startTime = Time.time;
    }

    protected virtual void Update()
    {
        if(Time.time - startTime < checkDelay) return;
        if (!isExposed)
        {
            CheckExposed();
        }
    }

    protected virtual void CheckExposed()
    {
        if (VoxelTerrain.Instance == null) return;
        if (IsExposedCheck())
        {
            isExposed = true;
            OnExposed();
        }
    }


    
    
    
    protected virtual bool IsExposedCheck()
    {
        return VoxelTerrain.Instance.IsJewelExposed(transform.position);
    }

    
    
    
    protected virtual void OnExposed()
    {

    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.name.Contains("sonar") && !isCoolingDown)
        {
            ExecuteReaction();
        }
    }

    protected virtual void ExecuteReaction()
    {
        isCoolingDown = true;

        var (echoPrefab, markerPrefab) = GetReactionPrefabs();

        if(echoPrefab != null)
        {
            Instantiate(echoPrefab, transform.position, Quaternion.identity);

            if(currentMarker != null)
            {
                Destroy(currentMarker);
            }

            if(markerPrefab != null)
            {
                currentMarker = Instantiate(markerPrefab, transform);
                currentMarker.transform.localPosition = new Vector3(0, 0, -4);
                currentMarker.transform.localRotation = Quaternion.Euler(0, -90, 0);

                Destroy(currentMarker, 5f);
            }
        }
        Invoke(nameof(ResetReaction), cooldownTime);
    }

    
    
    
    protected abstract (GameObject echoPrefab, GameObject markerPrefab) GetReactionPrefabs();

    protected virtual void ResetReaction()
    {
        isCoolingDown = false;
    }
}
