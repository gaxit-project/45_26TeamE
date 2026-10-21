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

    [Header("露出チェック間隔（秒）")]
    [SerializeField] protected float checkInterval = 0.15f;
    protected float nextCheckTime;

    protected virtual void Awake()
    {
        isExposed = false;
    }

    protected virtual void Start()
    {
        startTime = Time.time;
        // 同一フレームに露出チェックが集中しないよう少し分散させる
        nextCheckTime = startTime + checkDelay + (Mathf.Abs(GetInstanceID()) % 10) * 0.02f;
    }

    protected virtual void Update()
    {
        if (isExposed) return;
        if (Time.time < nextCheckTime) return;

        nextCheckTime = Time.time + checkInterval;
        CheckExposed();
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
