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

    [Header("コントローラー振動が始まる接近距離")]
    [SerializeField] private float vibrationWarningDistance = 20f;

    [Header("接近時の振動の強さ")]
    [SerializeField] private float warningVibrationStrength = 0.15f;

    private PlayerController player;

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

        player = FindObjectOfType<PlayerController>();

        
        if (VoxelTerrain.Instance.IsJewelExposed(transform.position, transform.localScale))
        {
            Destroy(this);
        }
    }

    protected override void Update()
    {
        CheckProximityVibration();

        if (isExploding) return;
        base.Update();
    }

    private void CheckProximityVibration()
    {
        if (player == null) return;
        if (Time.timeScale == 0f) return; // ポーズ中は強制的に振動をストップ
        
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance <= vibrationWarningDistance)
        {
            float cycle = Time.time % 1.0f;
            if (cycle < 0.5f)
            {
                if (HapticsManager.Instance != null)
                {
                    HapticsManager.Instance.PlayPulse(warningVibrationStrength, warningVibrationStrength, 0.05f);
                }
            }
        }
    }

    public void TriggerChainReaction()
    {
        
        if (isChainReacting) return;
        isChainReacting = true;

        
        expandSpeed *= 2f;

        
        if (!isExposed)
        {
            isExposed = true;
            OnExposed();
        }
    }

    protected override bool IsExposedCheck()
    {
        
        return VoxelTerrain.Instance.IsJewelExposed(transform.position, transform.localScale);
    }

    protected override void OnExposed()
    {
        
        if (currentMarker != null)
        {
            Destroy(currentMarker);
        }

        StartCoroutine(ExplosionRoutine());
    }

    protected override void OnTriggerEnter(Collider other)
    {
        
        if (isExploding) return;
        base.OnTriggerEnter(other);
    }

    
    protected override (GameObject echoPrefab, GameObject markerPrefab) GetReactionPrefabs()
    {
        int sonarLV = UpgradeManager.GetLevel("Sonar");
        return sonarLV >= 2 ? (visualEchoPrefabV2, markerV2) : (visualEchoPrefab, marker);
    }

    private void DrawExplosionRangeCircle()
    {
        if (rangeCircle == null) return;

        rangeCircle.gameObject.SetActive(true);
        rangeCircle.useWorldSpace = true; 
        rangeCircle.positionCount = circleSegments + 1;

        Vector3 center = transform.position;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;

            
            float y = Mathf.Sin(angle) * explosionRadius;
            float z = Mathf.Cos(angle) * explosionRadius;

            
            rangeCircle.SetPosition(i, center + new Vector3(circleXOffset, y, z));
        }
    }

    private IEnumerator ExplosionRoutine()
    {
        isExploding = true;

        
        DrawExplosionRangeCircle();

        
        if (expandingCircle != null)
        {
            expandingCircle.gameObject.SetActive(true);
            expandingCircle.useWorldSpace = true; 
            expandingCircle.positionCount = circleSegments + 1;
        }

        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        
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

        
        while (true)
        {
            
            float safeExpandSpeed = Mathf.Max(0.01f, expandSpeed);
            float calculatedTimeToExplode = explosionRadius / safeExpandSpeed;

            
            if (elapsedTime >= calculatedTimeToExplode)
            {
                break;
            }

            
            float currentSpeed = Mathf.Lerp(5f, 20f, elapsedTime / calculatedTimeToExplode);

            
            float pingPong = Mathf.PingPong(elapsedTime * currentSpeed, 1f);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    
                    Color blinkColor = Color.Lerp(origColors[i], Color.red, pingPong);

                    
                    if (renderers[i].material.HasProperty("_BaseColor"))
                        renderers[i].material.SetColor("_BaseColor", blinkColor);
                    else if (renderers[i].material.HasProperty("_Color"))
                        renderers[i].material.color = blinkColor;

                    
                    renderers[i].material.EnableKeyword("_EMISSION");
                    renderers[i].material.SetColor("_EmissionColor", Color.red * pingPong * emissionIntensity);
                }
            }

            
            if (expandingCircle != null)
            {
                
                float t = elapsedTime / calculatedTimeToExplode;
                
                float currentExpandingRadius = Mathf.Lerp(0f, explosionRadius, t);

                Vector3 center = transform.position;

                for (int i = 0; i <= circleSegments; i++)
                {
                    float angle = (float)i / circleSegments * Mathf.PI * 2f;
                    float y = Mathf.Sin(angle) * currentExpandingRadius;
                    float z = Mathf.Cos(angle) * currentExpandingRadius;

                    
                    expandingCircle.SetPosition(i, center + new Vector3(circleXOffset, y, z));
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null; 
        }

        
        if (rangeCircle != null) rangeCircle.gameObject.SetActive(false);
        if (expandingCircle != null) expandingCircle.gameObject.SetActive(false);

        Explode();
    }

    private void Explode()
    {
        // スコア用：爆弾が起爆した時点で加算
        FinalResultManager.AddTriggeredBomb();

        
        if (VoxelTerrain.Instance != null)
        {
            
            Vector3 centerPos = transform.position;

            
            
            

            
            Vector3 localPos = VoxelTerrain.Instance.transform.InverseTransformPoint(centerPos);
            int centerX = Mathf.RoundToInt(localPos.x / VoxelTerrain.Instance.BlockSize);
            int centerY = Mathf.RoundToInt(localPos.y / VoxelTerrain.Instance.BlockSize);
            int centerZ = Mathf.RoundToInt(localPos.z / VoxelTerrain.Instance.BlockSize);

            
            float radiusInBlocks = explosionRadius / VoxelTerrain.Instance.BlockSize;

            
            Vector3 minLimit = Vector3.zero;
            Vector3 maxLimit = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

            VoxelTerrain.Instance.ExecuteDig(centerX, centerY, centerZ, radiusInBlocks, minLimit, maxLimit, false);
        }

        
        Vector2 bombPos2DForChain = new Vector2(transform.position.y, transform.position.z);
        Vector3 boxExtents = new Vector3(100f, explosionRadius + 2f, explosionRadius + 2f); 
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
                
                BombReaction otherBomb = obj.GetComponent<BombReaction>();
                if (otherBomb != null)
                {
                    otherBomb.TriggerChainReaction();
                    continue;
                }

                
                JewelryReaction jewelScript = obj.GetComponent<JewelryReaction>();
                if (jewelScript != null && !jewelScript.IsGot)
                {
                    Destroy(obj);
                    continue;
                }

                
                TreasureBoxBehaviour boxScript = obj.GetComponent<TreasureBoxBehaviour>();
                if (boxScript != null && !boxScript.IsGot)
                {
                    Destroy(obj);
                    continue;
                }

                
                if (obj.CompareTag("Player"))
                {
                    PlayerController pc = obj.GetComponent<PlayerController>();
                    
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

                    if (TimerManager.Instance != null)
                    {
                        TimerManager.Instance.AddTime(-10f);
                        Debug.Log("[BombReaction] 爆発により制限時間が10秒減少しました！");
                    }

                    System.Collections.Generic.List<ItemInventoryManager.ItemData> removedItems = null;
                    if (ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.GetTotalItemCount() > 0)
                    {
                        removedItems = ItemInventoryManager.Instance.RemoveItemsRandomWithData(1);
                        if (removedItems.Count > 0)
                        {
                            Debug.Log("[BombReaction] 爆発によりアイテムを1つ失いました！");
                        }
                    }

                    if (pc != null)
                    {
                        pc.TakeDamageWithItems(removedItems);
                    }
                }
            }
        }

        
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);

            
            float baseRadius = 8.0f;
            float scale = explosionRadius / baseRadius;

            effect.transform.localScale = new Vector3(scale, scale, scale);
        }

        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE("Bomb_01");
        }

        // 画面を大きく揺らして爆発の衝撃を伝える
        if (CameraController.Instance != null)
        {
            CameraController.Instance.AddShake(1f, 0.45f);
        }


        Destroy(gameObject);
    }
}
