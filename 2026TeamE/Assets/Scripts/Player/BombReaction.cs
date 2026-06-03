using UnityEngine;
using System.Collections;

public class BombReaction : MonoBehaviour
{
    [Header("爆風の半径")]
    [SerializeField] private float explosionRadius = 3f;

    [Header("爆破までの時間")]
    [SerializeField] private float timeToExplode = 2f;

    [Header("点滅の発光強度(Emission)")]
    [SerializeField] private float emissionIntensity = 5f;

    [Header("爆風による換金予定金の減少額")]
    [SerializeField] private int moneyPenalty = 50000;

    [Header("爆発エフェクト（任意）")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Header("ソナー検知時のエコープレハブ")]
    public GameObject visualEchoPrefab;
    [Header("ソナー検知時のマーカー")]
    public GameObject marker;

    private GameObject currentMarker;
    private bool isCoolingDown = false;
    public float cooldownTime = 1.0f;

    private bool isExposed = false;
    private bool isExploding = false;

    private float checkDelay = 3.0f;
    private float startTime;

    void Start()
    {
        startTime = Time.time;
    }

    void Update()
    {
        if (isExploding) return;

        if (Time.time - startTime < checkDelay)
        {
            return;
        }

        if (!isExposed)
        {
            CheckExposed();
        }
    }

    void CheckExposed()
    {
        if (VoxelTerrain.Instance == null) return;

        // VoxelTerrainのマップデータから、自身の位置の周囲がAirか確認する
        if (VoxelTerrain.Instance.IsJewelExposed(transform.position))
        {
            isExposed = true;

            // 露出してカウントダウンが始まったらマーカーを消す
            if (currentMarker != null)
            {
                Destroy(currentMarker);
            }

            StartCoroutine(ExplosionRoutine());
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // 爆発中はソナーに反応させない
        if (!isExploding && other.gameObject.name.Contains("sonar") && !isCoolingDown)
        {
            ExecuteReaction();
        }
    }

    void ExecuteReaction()
    {
        isCoolingDown = true;

        if (visualEchoPrefab != null)
        {
            Instantiate(visualEchoPrefab, transform.position, Quaternion.identity);
            
            if (currentMarker != null)
            {
                Destroy(currentMarker);
            }

            currentMarker = Instantiate(marker, transform);
            currentMarker.transform.localPosition = new Vector3(0, 0, -4);
            currentMarker.transform.localRotation = Quaternion.Euler(0, -90, 0);

            Destroy(currentMarker, 5f);
        }

        Invoke("ResetReaction", cooldownTime);
    }

    void ResetReaction()
    {
        isCoolingDown = false;
    }

    private IEnumerator ExplosionRoutine()
    {
        isExploding = true;

        // 自身および子オブジェクトの Renderer を取得
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        
        // マテリアルの元の色を保存
        Color[] origColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                if (renderers[i].material.HasProperty("_BaseColor"))
                    origColors[i] = renderers[i].material.GetColor("_BaseColor");
                else if (renderers[i].material.HasProperty("_Color"))
                    origColors[i] = renderers[i].material.color;
                else
                    origColors[i] = Color.white;
            }
        }

        float elapsedTime = 0f;

        // 爆破までの間、点滅ループ
        while (elapsedTime < timeToExplode)
        {
            // 爆破が近づくにつれて点滅を速くする（スピード5から20へ変化）
            float currentSpeed = Mathf.Lerp(5f, 20f, elapsedTime / timeToExplode);
            
            // 0〜1の間を往復する値（pingPong）を生成
            float pingPong = Mathf.PingPong(elapsedTime * currentSpeed, 1f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    // 元の色と赤色をブレンド
                    Color blinkColor = Color.Lerp(origColors[i], Color.red, pingPong);

                    // マテリアルの色を更新（URP環境と標準環境の両対応）
                    if (renderers[i].material.HasProperty("_BaseColor"))
                        renderers[i].material.SetColor("_BaseColor", blinkColor);
                    else if (renderers[i].material.HasProperty("_Color"))
                        renderers[i].material.color = blinkColor;

                    // 発光（Emission）を有効化して、赤色×強さを設定
                    renderers[i].material.EnableKeyword("_EMISSION");
                    renderers[i].material.SetColor("_EmissionColor", Color.red * pingPong * emissionIntensity);
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null; // 次のフレームまで待機
        }

        Explode();
    }

    private void Explode()
    {
        // 1. 周囲のブロックを破壊する（VoxelTerrainに破壊処理を依頼）
        if (VoxelTerrain.Instance != null)
        {
            // 爆弾の中心位置を取得
            Vector3 centerPos = transform.position;
            
            // ExecuteDig メソッドを使って球状にブロックを破壊
            // 引数: centerX, centerY, centerZ, radius, minLimit, maxLimit
            
            // ローカル座標に変換してブロックのインデックスを計算
            Vector3 localPos = VoxelTerrain.Instance.transform.InverseTransformPoint(centerPos);
            int centerX = Mathf.RoundToInt(localPos.x / VoxelTerrain.Instance.BlockSize);
            int centerY = Mathf.RoundToInt(localPos.y / VoxelTerrain.Instance.BlockSize);
            int centerZ = Mathf.RoundToInt(localPos.z / VoxelTerrain.Instance.BlockSize);

            // 破壊範囲をブロック単位に変換
            float radiusInBlocks = explosionRadius / VoxelTerrain.Instance.BlockSize;

            // マップの制限（とりあえず全範囲）
            Vector3 minLimit = Vector3.zero;
            Vector3 maxLimit = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

            VoxelTerrain.Instance.ExecuteDig(centerX, centerY, centerZ, radiusInBlocks, minLimit, maxLimit, false);
        }

        // 2. プレイヤーへのダメージ処理（お金を減らす処理）
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= explosionRadius)
            {
                if (MoneyManager.Instance != null)
                {
                    int currentMoney = MoneyManager.Instance.GetMoneyOnHand();
                    int actualPenalty = Mathf.Min(moneyPenalty, currentMoney);
                    
                    if (actualPenalty > 0)
                    {
                        MoneyManager.Instance.MoneyOnHandDecrease(actualPenalty);
                        Debug.Log($"[BombReaction] プレイヤーが爆発に巻き込まれました！ 換金予定のお金が {actualPenalty} 減りました。");
                    }
                    else
                    {
                        Debug.Log("[BombReaction] プレイヤーが爆発に巻き込まれましたが、換金予定のお金はすでに0です。");
                    }
                }
            }
        }

        // 3. 爆発エフェクトを生成
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        // 4. SE再生
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("爆発"); // SEの名前は適当ですが、のちほど調整してください
        }

        // 5. 自分自身を破棄
        Destroy(gameObject);
    }
}
