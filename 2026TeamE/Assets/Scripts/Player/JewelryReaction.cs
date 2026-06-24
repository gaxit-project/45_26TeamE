using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class JewelryReaction : MonoBehaviour
{
    [Header("アイテム情報")]
    public ItemType itemType = ItemType.Jewelry;
    [Tooltip("UI表示用のアイコンSprite（jewelry.pngなど）")]
    public Sprite uiIcon;
    [Tooltip("リザルト画面で宝箱を開けた時に加算される金額")]
    public int moneyValue = 300000;
    [Header("エコープレハブ")]
    public GameObject visualEchoPrefab;
    [Header("対象")]
    public Transform ob;
    [Header("マーカー")]
    public GameObject marker;
    [Header("エフェクトプレハブ")]
    public GameObject EfectPrefab;

    [Header("クールダウン")]
    public float cooldownTime = 1.0f;

    [Header("デバッグ用（取得可能状態）")]
    public bool isExposed = false;

    private bool isCoolingDown = false;
    
    // 生成したマーカーを覚えておくための変数
    private GameObject currentMarker;

    private float startTime;
    private float checkDelay = 3.0f;
    private bool isGot = false;

    private void Awake()
    {
        // インスペクターの保存値に影響されないよう確実に初期化
        isExposed = false;
    }

    private void Start()
    {
        ob = transform;
        startTime = Time.time; // 生成された時間を記録
    }

    private void Update()
    {
        // 生成から3秒間は地形生成などの猶予として判定を無効化
        if (Time.time - startTime < checkDelay)
        {
            return;
        }

        // まだ露出していない場合のみ判定を続ける
        if (!isExposed)
        {
            CheckExposed();
        }
    }

    void CheckExposed()
    {
        if (VoxelTerrain.Instance == null) return;

        // VoxelTerrainのマップデータから、自身の位置と上下左右がAirか確認する
        if (VoxelTerrain.Instance.IsJewelExposed(transform.position))
        {
            isExposed = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name.Contains("sonar") && !isCoolingDown)
        {
            ExecuteReaction();
        }
        
        // 露出している時のみプレイヤーとの接触を受け付ける
        if (isExposed && other.gameObject.CompareTag("Player"))
        {
            Get();
        }
    }

    void OnTriggerStay(Collider other)
    {
        // 既に触れている状態で露出した（掘り出された）場合にも取得できるようにする
        if (isExposed && other.gameObject.CompareTag("Player"))
        {
            Get();
        }
    }

    void ExecuteReaction()
    {
        isCoolingDown = true;

        if (visualEchoPrefab != null)
        {
            Instantiate(visualEchoPrefab, transform.position, Quaternion.identity);
            
            // 古いマーカーが残っていたら消す（重複防止）
            if (currentMarker != null)
            {
                Destroy(currentMarker);
            }

            // 新しく生成して変数に保存しておく
            currentMarker = Instantiate(marker, ob);
            currentMarker.transform.localPosition = new Vector3(0, 0, -4);
            currentMarker.transform.localRotation = Quaternion.Euler(0, -90, 0);

            // 生成したマーカーを5秒後に自動で消す
            Destroy(currentMarker, 5f);
        }

        Invoke("ResetReaction", cooldownTime);
    }

    void ResetReaction()
    {
        isCoolingDown = false;
    }

    void Get()
    {
        if (isGot) return; // 既に取得済みなら何もしない
        isGot = true;

        if (VoxelTerrain.Instance != null)
        {
            VoxelTerrain.Instance.CollectedJewel(transform.position);
        }

        // 宝石を取得した瞬間にマーカーを消す
        if (currentMarker != null)
        {
            Destroy(currentMarker);
        }

        // お金はリザルト画面で宝箱開封時に加算する（ここでは加算しない）
        // 右上UIにアイコンをフライアニメーションで追加
        if (ItemInventoryManager.Instance != null && uiIcon != null)
        {
            // GetAnime()が位置を変更する前にワールド座標をキャプチャ
            Vector3 capturedPos = transform.position;
            ItemInventoryManager.Instance.AddItem(itemType, uiIcon, capturedPos, moneyValue);
        }

        StartCoroutine(GetAnime());
    }

    IEnumerator GetAnime()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("宝石入手");
        }

        // 最初の位置を記録（Xのみ5に変更して画面手前に出す）
        Vector3 startPos = new Vector3(5, transform.position.y, transform.position.z);
        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        
        float animDuration = 0.5f; 
        float flashInterval = 0.05f; 
        float popHeight = 1.5f; 
        float elapsedTime = 0f;

        while (elapsedTime < animDuration)
        {
            float t = elapsedTime / animDuration;
            
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            float currentY = startPos.y + (popHeight * easeOut);
            
            transform.position = new Vector3(startPos.x, currentY, startPos.z);

            bool isVisible = (elapsedTime % (flashInterval * 2)) < flashInterval;
            foreach (Renderer r in renderers)
            {
                if (r != null) r.enabled = isVisible;
            }

            elapsedTime += Time.deltaTime; 
            yield return null; 
        }

        Instantiate(EfectPrefab, transform.position + new Vector3(5, 0, 0), Quaternion.Euler(-90, -90, 0));
        
        
        Destroy(gameObject);
    }
}
