using UnityEngine;
using System.Collections;

public class BombReaction : BuriedItemBase
{
    [Header("爆風の半径")]
    [SerializeField] private float explosionRadius = 3f;

    [Header("拡がる円のスピード（単位/秒）")]
    [SerializeField] private float expandSpeed = 1f;

    [Header("点滅の発光強度(Emission)")]
    [SerializeField] private float emissionIntensity = 5f;

    [Header("爆風による換金予定金の減少額")]
    [SerializeField] private int moneyPenalty = 50000;

    [Header("爆発エフェクト（任意）")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Header("ソナー検知時のエコープレハブ")]
    public GameObject visualEchoPrefab;
    [Header("レベル2以上でのソナー検知時のエコープレハブ")]
    public GameObject visualEchoPrefabV2;
    [Header("ソナー検知時のマーカー")]
    public GameObject marker;
    [Header("レベル2以上でのソナー検知時のマーカー")]
    public GameObject markerV2;

    [Header("爆発範囲表示用のLineRenderer")]
    public LineRenderer rangeCircle;
    [Header("時間経過で広がる爆発目安のLineRenderer")]
    public LineRenderer expandingCircle;
    public int circleSegments = 36;

    [Header("円を手前に表示するためのXオフセット")]
    public float circleXOffset = 5.0f;

    private bool isExploding = false;
    private bool isChainReacting = false;

    protected override void Start()
    {
        base.Start();

        // 生成時、空中に露出していたら自分を削除
        if (VoxelTerrain.Instance.IsJewelExposed(transform.position, transform.localScale))
        {
            Destroy(this);
        }
    }

    protected override void Update()
    {
        if (isExploding) return;
        base.Update();
    }

    public void TriggerChainReaction()
    {
        // 既に誘爆処理済みなら無視
        if (isChainReacting) return;
        isChainReacting = true;

        // 円の拡大スピードを2倍にする
        expandSpeed *= 2f;

        // まだカウントダウンが始まっていなければ強制的に起爆する
        if (!isExposed)
        {
            isExposed = true;
            OnExposed();
        }
    }

    protected override bool IsExposedCheck()
    {
        // VoxelTerrainのマップデータから、自身のサイズに合わせて周囲がAirか確認する
        return VoxelTerrain.Instance.IsJewelExposed(transform.position, transform.localScale);
    }

    protected override void OnExposed()
    {
        // 露出してカウントダウンが始まったらマーカーを消す
        if (currentMarker != null)
        {
            Destroy(currentMarker);
        }

        StartCoroutine(ExplosionRoutine());
    }

    protected override void OnTriggerEnter(Collider other)
    {
        // 爆発中はソナーに反応させない
        if (isExploding) return;
        base.OnTriggerEnter(other);
    }

    // レベル2以上でのソナー検知時は専用のエコー/マーカーを使う
    protected override (GameObject echoPrefab, GameObject markerPrefab) GetReactionPrefabs()
    {
        int sonarLV = UpgradeManager.GetLevel("Sonar");
        return sonarLV >= 2 ? (visualEchoPrefabV2, markerV2) : (visualEchoPrefab, marker);
    }

    private void DrawExplosionRangeCircle()
    {
        if (rangeCircle == null) return;

        rangeCircle.gameObject.SetActive(true);
        rangeCircle.useWorldSpace = true; // 爆弾の回転（傾き）の影響を受けないようにワールド座標を使用
        rangeCircle.positionCount = circleSegments + 1;

        Vector3 center = transform.position;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;

            // Y座標とZ座標で円を描く
            float y = Mathf.Sin(angle) * explosionRadius;
            float z = Mathf.Cos(angle) * explosionRadius;

            // ワールド座標で中心位置に加算する（X方向にずらしてブロックの手前に表示）
            rangeCircle.SetPosition(i, center + new Vector3(circleXOffset, y, z));
        }
    }

    private IEnumerator ExplosionRoutine()
    {
        isExploding = true;

        // 爆発範囲の赤い円を表示する
        DrawExplosionRangeCircle();

        // 拡がる円（タイマー）の初期化
        if (expandingCircle != null)
        {
            expandingCircle.gameObject.SetActive(true);
            expandingCircle.useWorldSpace = true; // こちらもワールド座標を使用
            expandingCircle.positionCount = circleSegments + 1;
        }

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
        while (true)
        {
            // スピードが途中で変わる可能性があるため、ループ内で毎フレーム計算する
            float safeExpandSpeed = Mathf.Max(0.01f, expandSpeed);
            float calculatedTimeToExplode = explosionRadius / safeExpandSpeed;

            // 時間経過が制限時間を超えたらループを抜けて爆発
            if (elapsedTime >= calculatedTimeToExplode)
            {
                break;
            }

            // 爆破が近づくにつれて点滅を速くする（スピード5から20へ変化）
            float currentSpeed = Mathf.Lerp(5f, 20f, elapsedTime / calculatedTimeToExplode);

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

            // --- 広がる円（爆発タイマー）の更新 ---
            if (expandingCircle != null)
            {
                // 時間経過の割合 (0.0 ～ 1.0)
                float t = elapsedTime / calculatedTimeToExplode;
                // 現在の半径 (0 から explosionRadius へ広がる)
                float currentExpandingRadius = Mathf.Lerp(0f, explosionRadius, t);

                Vector3 center = transform.position;

                for (int i = 0; i <= circleSegments; i++)
                {
                    float angle = (float)i / circleSegments * Mathf.PI * 2f;
                    float y = Mathf.Sin(angle) * currentExpandingRadius;
                    float z = Mathf.Cos(angle) * currentExpandingRadius;

                    // ワールド座標で中心位置に加算する（X方向にずらしてブロックの手前に表示）
                    expandingCircle.SetPosition(i, center + new Vector3(circleXOffset, y, z));
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null; // 次のフレームまで待機
        }

        // 爆発直前に円を非表示にする
        if (rangeCircle != null) rangeCircle.gameObject.SetActive(false);
        if (expandingCircle != null) expandingCircle.gameObject.SetActive(false);

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

        // 1.4 〜 2. 爆発範囲内のオブジェクト（爆弾、宝石、宝箱、プレイヤー）の判定
        Vector2 bombPos2DForChain = new Vector2(transform.position.y, transform.position.z);
        Vector3 boxExtents = new Vector3(100f, explosionRadius + 2f, explosionRadius + 2f); // 2D平面判定のためXは広め、YZは爆風半径＋α
        Collider[] hits = Physics.OverlapBox(transform.position, boxExtents);

        foreach (Collider hitCol in hits)
        {
            GameObject obj = hitCol.gameObject;
            if (obj == gameObject) continue;

            Vector3 center = hitCol.bounds.center;
            Vector2 pos2D = new Vector2(center.y, center.z);
            float distance = Vector2.Distance(bombPos2DForChain, pos2D);
            float radius = Mathf.Max(hitCol.bounds.extents.y, hitCol.bounds.extents.z);

            if (distance <= explosionRadius + radius)
            {
                // 1.4 爆発範囲内の他の爆弾を誘爆させる
                BombReaction otherBomb = obj.GetComponent<BombReaction>();
                if (otherBomb != null)
                {
                    otherBomb.TriggerChainReaction();
                    continue;
                }

                // 1.5 爆発範囲内の宝石を破壊する
                JewelryReaction jewelScript = obj.GetComponent<JewelryReaction>();
                if (jewelScript != null && !jewelScript.IsGot)
                {
                    Destroy(obj);
                    continue;
                }

                // 1.6 爆発範囲内の宝箱を破壊する
                TreasureBoxBehaviour boxScript = obj.GetComponent<TreasureBoxBehaviour>();
                if (boxScript != null && !boxScript.IsGot)
                {
                    Destroy(obj);
                    continue;
                }

                // 2. プレイヤーへのダメージ処理
                if (obj.CompareTag("Player"))
                {
                    PlayerController pc = obj.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.DamageAnim();
                    }

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

                        // 取得済みアイテムをランダムに1つ喪失させる
                        if (ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.GetTotalItemCount() > 0)
                        {
                            int removed = ItemInventoryManager.Instance.RemoveItemsRandom(1);
                            if (removed > 0)
                            {
                                Debug.Log("[BombReaction] 爆発によりアイテムを1つ失いました！");
                            }
                        }
                    }
                }
            }
        }

        // 3. 爆発エフェクトを生成
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);

            // 爆発範囲(explosionRadius)に応じてエフェクトの大きさを自動調整する
            float baseRadius = 8.0f;
            float scale = explosionRadius / baseRadius;

            effect.transform.localScale = new Vector3(scale, scale, scale);
        }

        // 4. SE再生
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("Bomb_01");
        }

        // 5. 自分自身を破棄
        Destroy(gameObject);
    }
}
