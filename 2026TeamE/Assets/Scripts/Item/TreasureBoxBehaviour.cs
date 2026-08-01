using System.Collections;
using UnityEngine;
using UnityEngine.Timeline;

public class TreasureBoxBehaviour : MonoBehaviour, ICollectible
{
    [Header("宝箱が開いた時のスプライト")]
    public Sprite openSprite;

    [Header("中身のプレハブ（生成時に設定される）")]
    [HideInInspector] public GameObject contentPrefab;
    [HideInInspector] public int zoneIndex = 0; // 鍵などに必要な情報

    [Header("エコープレハブ")]
    public GameObject visualEchoPrefab;
    [Header("レベル3以上で金の袋を探知した際のエコープレハブ")]
    public GameObject visualEchoPrefabV3;
    [Header("マーカー")]
    public GameObject marker;
    [Header("レベル3以上で金の袋を探知した際のマーカー")]
    public GameObject markerV3;
    [Header("クールダウン")]
    public float cooldownTime = 1.0f;
    [Header("ソナー用（取得可能状態）")]
    public bool isExposed = false;
    [Header("判別用プレハブ")]
    public GameObject GLeatherBagPrefab;      // エディタから金の袋をセットする

    [Header("露出判定の厳しさ")]
    [Tooltip("このサイズが大きいほど、より周りを広く掘らないと取得可能になりません")]
    public Vector3 exposureSize = new Vector3(1, 3, 3);

    private bool isCoolingDown = false;
    private GameObject currentMarker;
    private float startTime;
    private float checkDelay = 3.0f;
    private bool isGot = false;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        isExposed = false;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        startTime = Time.time;
    }

    private void Update()
    {
        if (Time.time - startTime < checkDelay) return;
        if (!isExposed) CheckExposed();
    }

    private void CheckExposed()
    {
        if (VoxelTerrain.Instance == null) return;
        if (VoxelTerrain.Instance.IsJewelExposed(transform.position, exposureSize))
        {
            isExposed = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name.Contains("sonar") && !isCoolingDown)
        {
            ExecuteReaction();
        }
    }

    private void ExecuteReaction()
    {
        isCoolingDown = true;
        int sonarLV = UpgradeManager.GetLevel("Sonar");
        if (visualEchoPrefab != null)
        {
            if (contentPrefab == GLeatherBagPrefab&&sonarLV>=3)
                Instantiate(visualEchoPrefabV3, transform.position, Quaternion.identity);
            else
                Instantiate(visualEchoPrefab, transform.position, Quaternion.identity);
            if (currentMarker != null) Destroy(currentMarker);

            if(contentPrefab == GLeatherBagPrefab&&sonarLV>=3)
                currentMarker = Instantiate(markerV3, transform);
            else
                currentMarker = Instantiate(marker, transform);
            currentMarker.transform.localPosition = new Vector3(0, 0, -4);
            currentMarker.transform.localRotation = Quaternion.Euler(0, -90, 0);

            Destroy(currentMarker, 5f);
        }
        Invoke(nameof(ResetReaction), cooldownTime);
    }

    private void ResetReaction()
    {
        isCoolingDown = false;
    }

    public void Collect()
    {
        if (!isExposed || isGot) return;
        isGot = true;

        if (currentMarker != null) Destroy(currentMarker);

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        // 手前（x=4.5）に移動させてブロックに埋まらないようにする
        transform.position = new Vector3(4.5f, transform.position.y, transform.position.z);

        // 1秒間のぽっぷあっぷ演出
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

        // 1. スプライトを開いた状態に変更
        if (spriteRenderer != null && openSprite != null)
        {
            spriteRenderer.sprite = openSprite;
            spriteRenderer.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        }

        // 2. 中身のアイテムを生成し、一時的にコライダーを無効化
        GameObject spawnedItem = null;
        if (contentPrefab != null)
        {
            spawnedItem = Instantiate(contentPrefab, transform.position, transform.rotation);
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

        // 3. アニメーション（宝箱沈み＆アイテム跳ね）
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

        // 4. アニメーション完了後の処理
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

    private void OnDestroy()
    {
        if (isGot || !gameObject.scene.isLoaded) return;

        // 鍵が入っている宝箱が未獲得のまま破壊された場合、VoxelTerrainにリスポーン処理を依頼
        if (contentPrefab != null && contentPrefab.GetComponent<KeyBehaviour>() != null)
        {
            if (VoxelTerrain.Instance != null)
            {
                VoxelTerrain.Instance.RespawnKeyTreasureBox(transform.position, zoneIndex);
            }
        }
    }
}