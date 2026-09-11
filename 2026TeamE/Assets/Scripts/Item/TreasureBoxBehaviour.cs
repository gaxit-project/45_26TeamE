using System.Collections;
using UnityEngine;

public class TreasureBoxBehaviour : BuriedItemBase, ICollectible
{
    [Header("宝箱が開いた時のスプライト")]
    public Sprite openSprite;

    [Header("中身のプレハブ（生成時に設定される）")]
    [HideInInspector] public GameObject contentPrefab;
    [HideInInspector] public int zoneIndex = 0;

    [Header("エコープレハブ")]
    public GameObject visualEchoPrefab;
    [Header("レベル3以上で金の袋を探知した際のエコープレハブ")]
    public GameObject visualEchoPrefabV3;
    [Header("マーカー")]
    public GameObject marker;
    [Header("レベル3以上で金の袋を探知した際のマーカー")]
    public GameObject markerV3;
    [Header("判別用プレハブ")]
    public GameObject GLeatherBagPrefab;

    [Header("露出判定の厳しさ")]
    [Tooltip("このサイズが大きいほど、より周りを広く掘らないと取得可能になりません")]
    public Vector3 exposureSize = new Vector3(1, 3, 3);

    private bool isGot = false;
    public bool IsGot => isGot;
    private bool isDestroyingByBomb = false;
    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    protected override bool IsExposedCheck()
    {
        return VoxelTerrain.Instance.IsJewelExposed(transform.position, exposureSize);
    }
    protected override (GameObject echoPrefab, GameObject markerPrefab) GetReactionPrefabs()
    {
        int sonarLV = UpgradeManager.GetLevel("Sonar");
        bool isGoldBag = contentPrefab == GLeatherBagPrefab && sonarLV >= 3;
        return isGoldBag ? (visualEchoPrefabV3, markerV3) : (visualEchoPrefab, marker);
    }

    public void Collect()
    {
        if (!isExposed || isGot || isDestroyingByBomb) return;
        isGot = true;

        if (currentMarker != null) Destroy(currentMarker);

        FinalResultManager.AddCollectedTreasureBox();

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        transform.position = new Vector3(4.5f, transform.position.y, transform.position.z);

        float delayDuration = 1.0f;
        float elapsedDelay = 0f;
        Vector3 initialPos = transform.position;

        while (elapsedDelay < delayDuration)
        {
            float t = elapsedDelay / delayDuration;
            float popUpHeight = 1.8f;
            float currentY = initialPos.y + Mathf.Sin(t * Mathf.PI) * popUpHeight;
            transform.position = new Vector3(initialPos.x, currentY, initialPos.z);
            elapsedDelay += Time.deltaTime;
            yield return null;
        }
        transform.position = initialPos;

        
        if (spriteRenderer != null && openSprite != null)
        {
            spriteRenderer.sprite = openSprite;
            spriteRenderer.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        }

        
        GameObject spawnedItem = null;
        if (contentPrefab != null)
        {
            spawnedItem = Instantiate(contentPrefab, transform.position, transform.rotation);
            spawnedItem.SetActive(true);
            spawnedItem.transform.position = new Vector3(5.0f, transform.position.y, transform.position.z);

            var keyObj = spawnedItem.GetComponent<KeyBehaviour>();
            if (keyObj != null)
            {
                keyObj.Setup(zoneIndex);
            }

            var colliders = spawnedItem.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }

        
        float duration = 0.5f;
        float elapsed = 0f;

        Vector3 boxStartPos = transform.position;
        Vector3 itemStartPos = spawnedItem != null ? spawnedItem.transform.position : transform.position;
        Color boxColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(boxColor.r, boxColor.g, boxColor.b, 1f - t);
                transform.position = boxStartPos + new Vector3(0, -0.5f * t, 0);
            }

            if (spawnedItem != null)
            {
                float bounceHeight = 1.0f;
                float currentY = itemStartPos.y + Mathf.Sin(t * Mathf.PI) * bounceHeight;
                spawnedItem.transform.position = new Vector3(itemStartPos.x, currentY, itemStartPos.z);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        
        if (spawnedItem != null)
        {
            spawnedItem.transform.position = itemStartPos;

            var colliders = spawnedItem.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = true;
            }

            var jewelry = spawnedItem.GetComponent<JewelryReaction>();
            if (jewelry != null) jewelry.isExposed = true;

            var cylinder = spawnedItem.GetComponent<CylinderReaction>();
            if (cylinder != null) cylinder.isExposed = true;

            var collectible = spawnedItem.GetComponent<ICollectible>();
            if (collectible != null)
            {
                collectible.Collect();
            }
        }

        Destroy(gameObject);
    }

    public void DestroyByBomb()
    {
        if (isGot || isDestroyingByBomb) return;
        isDestroyingByBomb = true;
        StartCoroutine(DestroyByBombRoutine());
    }

    private IEnumerator DestroyByBombRoutine()
    {
        float duration = 1.0f;
        float elapsed = 0f;
        
        Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        
        while (elapsed < duration)
        {
            if (spriteRenderer != null)
            {
                float pingPong = Mathf.PingPong(elapsed * 15f, 1f); 
                spriteRenderer.color = Color.Lerp(originalColor, Color.red, pingPong);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (isGot || !gameObject.scene.isLoaded) return;

        
        if (contentPrefab != null && contentPrefab.GetComponent<KeyBehaviour>() != null)
        {
            if (VoxelTerrain.Instance != null)
            {
                VoxelTerrain.Instance.RespawnKeyTreasureBox(transform.position, zoneIndex);
            }
        }
    }
}
