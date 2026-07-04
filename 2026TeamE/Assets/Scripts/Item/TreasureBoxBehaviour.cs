using System.Collections;
using UnityEngine;

public class TreasureBoxBehaviour : MonoBehaviour, ICollectible
{
    [Header("宝箱が開いた時のスプライト")]
    public Sprite openSprite;
    
    [Header("中身のプレハブ（生成時に設定される）")]
    [HideInInspector] public GameObject contentPrefab;
    [HideInInspector] public int zoneIndex = 0; // 鍵などに必要な情報

    [Header("エコープレハブ")]
    public GameObject visualEchoPrefab;
    [Header("マーカー")]
    public GameObject marker;
    [Header("クールダウン")]
    public float cooldownTime = 1.0f;
    [Header("ソナー用（取得可能状態）")]
    public bool isExposed = false;
    
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
        if (visualEchoPrefab != null)
        {
            Instantiate(visualEchoPrefab, transform.position, Quaternion.identity);
            if (currentMarker != null) Destroy(currentMarker);

            currentMarker = Instantiate(marker, transform);
            currentMarker.transform.localPosition = new Vector3(0, 0, -4);
            currentMarker.transform.localRotation = Quaternion.Euler(0, -90, 0);

            Destroy(currentMarker, 5f);
        }
        Invoke("ResetReaction", cooldownTime);
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
        // 手前（x=4.5）に移動させてブロックに埋まらないようにする（アイテムより少し奥）
        transform.position = new Vector3(4.5f, transform.position.y, transform.position.z);

        // 1秒待機して宝箱を取得した感覚を出す
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

        // 2. 中身のアイテムを生成し、一時的にコライダーを無効化（プレイヤーの即時取得を防ぐため）
        GameObject spawnedItem = null;
        if (contentPrefab != null)
        {
            // 宝箱と同じ位置・回転で生成
            spawnedItem = Instantiate(contentPrefab, transform.position, transform.rotation);
            
            // アイテムは宝箱よりさらに手前（x=5.0）に配置する
            spawnedItem.transform.position = new Vector3(5.0f, transform.position.y, transform.position.z);
            
            // 鍵の場合はZoneIndexを設定
            var keyObj = spawnedItem.GetComponent<KeyBehaviour>();
            if (keyObj != null)
            {
                keyObj.Setup(zoneIndex);
            }

            // 出現中すぐにプレイヤーが触れないようコライダーを一時的にオフ
            var colliders = spawnedItem.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }

        // 3. アニメーション（宝箱は沈んで透明に、アイテムは跳ねる）
        float duration = 0.5f;
        float elapsed = 0f;

        Vector3 boxStartPos = transform.position;
        Vector3 itemStartPos = spawnedItem != null ? spawnedItem.transform.position : transform.position;
        Color boxColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        while (elapsed < duration)
        {
            float t = elapsed / duration;

            // 宝箱：徐々に透明になり、少し沈む
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(boxColor.r, boxColor.g, boxColor.b, 1f - t);
                transform.position = boxStartPos + new Vector3(0, -0.5f * t, 0);
            }

            // アイテム：少し跳ねる（放物線）
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
            // アイテムの位置を元に戻す（跳ね終わり）
            spawnedItem.transform.position = itemStartPos;

            // コライダーを戻す
            var colliders = spawnedItem.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = true;
            }

            // アイテムを取得状態（isExposed）にして、それぞれの Collect() を呼び出す
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
}
