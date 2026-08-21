using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// アイテム、チャンク生成
public partial class VoxelTerrain
{
    /// <summary>1ゾーンをクリアするために必要な鍵の数。</summary>
    private const int RequiredKeyCount = 3;

    /// <summary>鍵以外の、通常の宝箱から出るアイテムの抽選対象。</summary>
    private static readonly SpawnItemType[] NormalItemPool =
    {
        SpawnItemType.Oxygen,
        SpawnItemType.LeatherBag,
        SpawnItemType.GoldLeatherBag,
    };

    // --- チャンク構築 ---------------------------------------------------

    /// <summary>
    /// 既存の子オブジェクトを破棄し、チャンクを生成してメッシュを構築する。
    /// メッシュ構築は非常に重いため、時間予算に応じてフレームをまたぎながら進める。
    /// </summary>
    private IEnumerator BuildChunksRoutine(FrameBudget budget)
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        int chunkCount = Mathf.CeilToInt((float)heightY / chunkSizeY);
        chunks = new Chunk[chunkCount];

        for (int i = 0; i < chunkCount; i++)
        {
            chunks[i] = CreateChunk(i);
            UpdateChunkMesh(i);

            if (budget.IsExhausted)
            {
                yield return null;
                budget.Renew();
            }
        }
    }

    private Chunk CreateChunk(int index)
    {
        GameObject chunkObject = Instantiate(chunkPrefab, transform);
        chunkObject.name = $"Chunk_{index}";
        chunkObject.transform.localPosition = Vector3.zero;

        Chunk chunk = chunkObject.GetComponent<Chunk>();
        chunk.Init(dirtMaterial, oreMaterial, bedrockMaterial, stoneMaterial, hardRockMaterial, quartziteMaterial, boundaryMaterial);

        return chunk;
    }

    // --- アイテム配置 ---------------------------------------------------

    /// <summary>
    /// 全ゾーンを上から順に走査し、それぞれのゾーンにアイテムを配置する。
    /// </summary>
    private void SpawnZoneItems(System.Random rnd)
    {
        int zoneTopY = heightY;

        for (int zoneIndex = 0; zoneIndex < zoneSettings.Count; zoneIndex++)
        {
            int zoneBottomY = zoneTopY - zoneSettings[zoneIndex].heightChunks * chunkSizeY;

            if (!zoneInitialGemValues.ContainsKey(zoneIndex))
            {
                zoneInitialGemValues[zoneIndex] = 0;
            }

            if (zoneSettings[zoneIndex].isGoalZone)
            {
                // ゴールゾーンは通常の鍵・爆弾・アイテム抽選を行わず、中央にゴールのお宝だけを配置する
                SpawnGoalTreasure(zoneIndex, zoneBottomY, zoneTopY);
            }
            else
            {
                SpawnItemsInZone(zoneIndex, zoneBottomY, zoneTopY, rnd);
            }

            zoneTopY = zoneBottomY;
        }
    }

    /// <summary>
    /// 1つのゾーンに、宝箱（鍵を含む）と爆弾をランダムな位置へ配置する。
    /// </summary>
    private void SpawnItemsInZone(int zoneIndex, int bottomY, int topY, System.Random rnd)
    {
        List<Vector3Int> candidates = CollectDiggablePositions(bottomY, topY);
        if (candidates.Count == 0) return;

        Shuffle(candidates, rnd);

        // シャッフル済みの候補を先頭から消費していくことで、配置が重複しないようにする
        int nextIndex = 0;

        List<SpawnItemType> treasureSequence = BuildTreasureSequence(zoneIndex, rnd);
        for (int i = 0; i < treasureSequence.Count; i++)
        {
            if (nextIndex >= candidates.Count) break;

            SpawnItemAt(treasureSequence[i], candidates[nextIndex], zoneIndex);
            nextIndex++;
        }

        int bombCount = Mathf.Max(0, zoneSettings[zoneIndex].bombCount);
        for (int i = 0; i < bombCount; i++)
        {
            if (nextIndex >= candidates.Count) break;

            SpawnItemAt(SpawnItemType.Bomb, candidates[nextIndex], zoneIndex);
            nextIndex++;
        }
    }

    /// <summary>
    /// このゾーンに配置する宝箱の中身を並べたリストを作る。
    /// 先頭には必ずクリアに必要な数の鍵が入り、残りは通常アイテムから抽選される。
    /// </summary>
    private List<SpawnItemType> BuildTreasureSequence(int zoneIndex, System.Random rnd)
    {
        var sequence = new List<SpawnItemType>();

        for (int i = 0; i < RequiredKeyCount; i++)
        {
            sequence.Add(SpawnItemType.Key);
        }

        int totalItemCount = Mathf.Max(RequiredKeyCount, zoneSettings[zoneIndex].itemsPerStage);
        for (int i = sequence.Count; i < totalItemCount; i++)
        {
            sequence.Add(NormalItemPool[rnd.Next(NormalItemPool.Length)]);
        }

        return sequence;
    }

    /// <summary>
    /// 指定した深さの範囲から、アイテムを埋め込める（掘って出せる）ブロックの座標を集める。
    /// </summary>
    private List<Vector3Int> CollectDiggablePositions(int bottomY, int topY)
    {
        var positions = new List<Vector3Int>();

        for (int y = bottomY; y < topY; y++)
        {
            for (int x = 0; x < thicknessX; x++)
            {
                for (int z = 0; z < maxStageWidthZ; z++)
                {
                    if (!IsInside(x, y, z)) continue;

                    if (IsDiggable(mapData[x, y, z]))
                    {
                        positions.Add(new Vector3Int(x, y, z));
                    }
                }
            }
        }

        return positions;
    }

    /// <summary>プレイヤーが掘って壊せるブロックかどうか（空洞・岩盤・境界壁は対象外）。</summary>
    private static bool IsDiggable(byte block)
    {
        return block == (byte)BlockType.Dirt
            || block == (byte)BlockType.Ore
            || block == (byte)BlockType.Stone
            || block == (byte)BlockType.HardRock;
    }

    /// <summary>フィッシャー・イェーツ法でリストの並びをランダムに入れ替える。</summary>
    private static void Shuffle(List<Vector3Int> items, System.Random rnd)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);

            Vector3Int temp = items[i];
            items[i] = items[j];
            items[j] = temp;
        }
    }

    /// <summary>
    /// 各ゾーンの最下部に中継地点を自動生成する。
    /// 最終ゾーン（最深部）の下にはさらに続くゾーンが無いため配置しない。
    /// </summary>
    private void SpawnRelayPoints()
    {
        if (relayPointPrefab == null) return;

        int centerZ = maxStageWidthZ / 2;

        for (int zoneIndex = 0; zoneIndex < zoneSettings.Count - 1; zoneIndex++)
        {
            int relayY = GetZoneBottomY(zoneIndex);
            Vector3 localPos = new Vector3(startOffsetX * blockSize, relayY * blockSize, centerZ * blockSize);
            Vector3 worldPos = transform.position + localPos;

            GameObject relay = Instantiate(relayPointPrefab, worldPos, Quaternion.identity, transform);
            relay.name = $"RelayPoint_{zoneIndex}";
        }
    }

    /// <summary>
    /// ゴールゾーンの中央に、最初から取得可能なゴールのお宝を配置する
    /// </summary>
    private void SpawnGoalTreasure(int zoneIndex, int bottomY, int topY)
    {
        if (goalTreasurePrefab == null) return;

        int centerX = thicknessX / 2;
        int centerY = (bottomY + topY) / 2;
        int centerZ = maxStageWidthZ / 2;

        Vector3 pos = transform.position + new Vector3(
            centerX * blockSize,
            centerY * blockSize + (blockSize / 2f),
            centerZ * blockSize + (blockSize / 2f)
        );
        Quaternion rotation = Quaternion.Euler(0, -90f, 0);

        GameObject goal = Instantiate(goalTreasurePrefab, pos, rotation, transform);
        spawnedTreasures.Add(goal);
        zoneInitialGemValues[zoneIndex] += GEM_VALUE;
    }

    private void SpawnItemAt(SpawnItemType type, Vector3Int coord, int zoneIndex)
    {
        Vector3 pos = transform.position + new Vector3(
            coord.x * blockSize,
            coord.y * blockSize + (blockSize / 2f),
            coord.z * blockSize + (blockSize / 2f)
        );
        Quaternion rotation = Quaternion.Euler(0, -90f, 0);

        if (type == SpawnItemType.Bomb)
        {
            if (bombPrefab != null)
            {
                Instantiate(bombPrefab, pos, rotation, transform);
            }
            return;
        }

        if (treasureBoxPrefab == null) return;

        GameObject box = Instantiate(treasureBoxPrefab, pos, rotation, transform);
        if (box.TryGetComponent<TreasureBoxBehaviour>(out var tb))
        {
            tb.zoneIndex = zoneIndex;
            switch (type)
            {
                case SpawnItemType.Key:
                    tb.contentPrefab = keyPrefab;
                    break;
                case SpawnItemType.Jewel:
                    tb.contentPrefab = treasurePrefab;
                    spawnedTreasures.Add(box);
                    zoneInitialGemValues[zoneIndex] += GEM_VALUE;
                    break;
                case SpawnItemType.Oxygen:
                    tb.contentPrefab = oxygenPrefab;
                    break;
                case SpawnItemType.LeatherBag:
                    tb.contentPrefab = leatherBagPrefab;
                    break;
                case SpawnItemType.GoldLeatherBag:
                    tb.contentPrefab = GoldleatherBagPrefab;
                    break;
            }
        }
    }

    /// <summary>
    /// 鍵の宝箱が未獲得で破壊された場合に、同じゾーンの別の土ブロックに再生成する
    /// </summary>
    public void RespawnKeyTreasureBox(Vector3 destroyedPos, int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= zoneSettings.Count) return;

        int zoneHeight = zoneSettings[zoneIndex].heightChunks * chunkSizeY;
        int currentStageBottomY = 0;
        for (int i = 0; i < zoneIndex; i++)
        {
            currentStageBottomY += zoneSettings[i].heightChunks * chunkSizeY;
        }
        int currentStageTopY = currentStageBottomY + zoneHeight;

        List<Vector3Int> validPositions = CollectDiggablePositions(currentStageBottomY, currentStageTopY);
        if (validPositions.Count == 0) return;

        var rnd = new System.Random();
        Vector3Int targetCoord = validPositions[rnd.Next(validPositions.Count)];

        // 鍵のタイプで宝箱を再生成
        SpawnItemAt(SpawnItemType.Key, targetCoord, zoneIndex);
        Debug.Log($"<color=orange>[鍵リスポーン]</color> ゾーン {zoneIndex} の空きブロック ({targetCoord.x}, {targetCoord.y}, {targetCoord.z}) に再配置しました。");
    }
}
