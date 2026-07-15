using UnityEngine;
using System.Collections;

public class BombReaction : MonoBehaviour
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
    [Header("ソナー検知時のマーカー")]
    public GameObject marker;

    [Header("爆発範囲表示用のLineRenderer")]
    public LineRenderer rangeCircle;
    [Header("時間経過で広がる爆発目安のLineRenderer")]
    public LineRenderer expandingCircle;
    public int circleSegments = 36;

    [Header("円を手前に表示するためのXオフセット")]
    public float circleXOffset = 5.0f;

    private GameObject currentMarker;
    private bool isCoolingDown = false;
    public float cooldownTime = 1.0f;

    private bool isExposed = false;
    private bool isExploding = false;
    private bool isChainReacting = false;

    private float checkDelay = 3.0f;
    private float startTime;

    void Start()
    {
        startTime = Time.time;

        //生成時、空中に露出していたら自分を削除
        if (VoxelTerrain.Instance.IsJewelExposed(transform.position, transform.localScale))
        {
            Destroy(this);
        }
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
            if (currentMarker != null)
            {
                Destroy(currentMarker);
            }
            StartCoroutine(ExplosionRoutine());
        }
    }

    void CheckExposed()
    {
        if (VoxelTerrain.Instance == null) return;

        // VoxelTerrainのマップデータから、自身のサイズに合わせて周囲がAirか確認する
        if (VoxelTerrain.Instance.IsJewelExposed(transform.position, transform.localScale))
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

        // 1.4 爆発範囲内の他の爆弾を誘爆させる
        BombReaction[] bombs = FindObjectsOfType<BombReaction>();
        Vector2 bombPos2DForChain = new Vector2(transform.position.y, transform.position.z);

        foreach (BombReaction otherBomb in bombs)
        {
            if (otherBomb == this) continue; // 自分自身は無視

            Collider bombCol = otherBomb.GetComponent<Collider>();
            Vector3 bombCenter = bombCol != null ? bombCol.bounds.center : otherBomb.transform.position;
            Vector2 otherBombPos2D = new Vector2(bombCenter.y, bombCenter.z);

            float distanceToBomb = Vector2.Distance(bombPos2DForChain, otherBombPos2D);
            float otherBombRadius = 0f;
            if (bombCol != null)
            {
                otherBombRadius = Mathf.Max(bombCol.bounds.extents.y, bombCol.bounds.extents.z);
            }

            // プレイヤーや宝石と同じく2D平面（YZ）で距離判定を行う
            if (distanceToBomb <= explosionRadius + otherBombRadius)
            {
                otherBomb.TriggerChainReaction();
            }
        }

        // 1.5 爆発範囲内の宝石を破壊する
        JewelryReaction[] jewels = FindObjectsOfType<JewelryReaction>();
        Vector2 bombPos2D = new Vector2(transform.position.y, transform.position.z);

        foreach (JewelryReaction jewelScript in jewels)
        {
            GameObject jewel = jewelScript.gameObject;
            Collider jewelCol = jewel.GetComponent<Collider>();
            Vector3 jewelCenter = jewelCol != null ? jewelCol.bounds.center : jewel.transform.position;
            Vector2 jewelPos2D = new Vector2(jewelCenter.y, jewelCenter.z);

            float distanceToJewel = Vector2.Distance(bombPos2D, jewelPos2D);
            float jewelRadius = 0f;
            if (jewelCol != null)
            {
                jewelRadius = Mathf.Max(jewelCol.bounds.extents.y, jewelCol.bounds.extents.z);
            }

            // プレイヤーと同じく2D平面（YZ）で距離判定を行う
            if (distanceToJewel <= explosionRadius + jewelRadius)
            {
                Destroy(jewel);
            }
        }

        // 1.6 爆発範囲内の宝箱を破壊する
        TreasureBoxBehaviour[] treasureBoxes = FindObjectsOfType<TreasureBoxBehaviour>();

        foreach (TreasureBoxBehaviour boxScript in treasureBoxes)
        {
            GameObject box = boxScript.gameObject;
            Collider boxCol = box.GetComponent<Collider>();
            Vector3 boxCenter = boxCol != null ? boxCol.bounds.center : box.transform.position;
            Vector2 boxPos2D = new Vector2(boxCenter.y, boxCenter.z);

            float distanceToBox = Vector2.Distance(bombPos2D, boxPos2D);
            float boxRadius = 0f;
            if (boxCol != null)
            {
                boxRadius = Mathf.Max(boxCol.bounds.extents.y, boxCol.bounds.extents.z);
            }

            if (distanceToBox <= explosionRadius + boxRadius)
            {
                Destroy(box);
            }
        }

        // 2. プレイヤーへのダメージ処理（お金を減らす処理）
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            Collider col = player.GetComponent<Collider>();

            // プレイヤーの中心座標を取得（足元が基準座標になっている場合を考慮し、コライダーの中心を使う）
            Vector3 playerCenter = col != null ? col.bounds.center : player.transform.position;

            // 地形の破壊判定（YZ平面）に合わせて、X座標を無視して中心間の距離を計算する
            Vector2 bombPos2D2 = new Vector2(transform.position.y, transform.position.z);
            Vector2 playerPos2D = new Vector2(playerCenter.y, playerCenter.z);
            float distance = Vector2.Distance(bombPos2D2, playerPos2D);

            // プレイヤーの体の大きさ（コライダーの広がり）を取得して、当たり判定に加算する
            float playerRadius = 0.5f;
            if (col != null)
            {
                // Y軸（高さ）とZ軸（幅/奥行き）のうち、大きい方を体の半径として扱う
                playerRadius = Mathf.Max(col.bounds.extents.y, col.bounds.extents.z);
            }

            // 「爆発の半径」＋「プレイヤーの体の半径」の範囲内なら、体の一部が触れていると判定する
            if (distance <= explosionRadius + playerRadius)
            {
                // ==================== 【ここに追記しました】 ====================
                // プレイヤーのコントローラーを取得して被弾アニメーションを再生
                PlayerController pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.DamageAnim();
                }
                // ================================================================

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
