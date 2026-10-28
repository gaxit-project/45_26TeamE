using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class VoxelTerrain
{

    private static readonly SpawnItemType[] NormalItemPool =
    {
        SpawnItemType.Oxygen,
        SpawnItemType.LeatherBag,
        SpawnItemType.GoldLeatherBag,
    };

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

                SpawnGoalTreasure(zoneIndex, zoneBottomY, zoneTopY);
            }
            else
            {
                SpawnItemsInZone(zoneIndex, zoneBottomY, zoneTopY, rnd);
            }

            zoneTopY = zoneBottomY;
        }
    }

    private void SpawnItemsInZone(int zoneIndex, int bottomY, int topY, System.Random rnd)
    {
        var area = new SpawnArea(this, zoneIndex, bottomY, topY);
        if (area.IsEmpty) return;

        List<SpawnItemType> treasureSequence = BuildTreasureSequence(zoneIndex, rnd);
        var occupied = new HashSet<Vector3Int>();
        var treasureCoords = new List<Vector3Int>();
        bool warnedDistance = false;

        foreach (SpawnItemType type in treasureSequence)
        {
            bool found = TryPickDiggablePosition(area, rnd,
                c => !occupied.Contains(c) && IsFarFromAll(c, treasureCoords, treasureMinDistance),
                out Vector3Int coord);

            if (!found)
            {
                if (!warnedDistance)
                {
                    Debug.LogWarning($"[VoxelTerrain] ゾーン {zoneIndex} で宝箱の距離制限が厳しすぎるため、距離制限を無視して配置を継続します。");
                    warnedDistance = true;
                }
                found = TryPickDiggablePosition(area, rnd, c => !occupied.Contains(c), out coord);
            }
            if (!found) break;

            SpawnItemAt(type, coord, zoneIndex);
            occupied.Add(coord);
            treasureCoords.Add(coord);
        }

        int bombCount = Mathf.Max(0, zoneSettings[zoneIndex].bombCount);
        SpawnSmartBombs(zoneIndex, bombCount, area, occupied, treasureCoords, rnd);
    }

    private List<SpawnItemType> BuildTreasureSequence(int zoneIndex, System.Random rnd)
    {
        var sequence = new List<SpawnItemType>();

        int totalItemCount = Mathf.Max(0, zoneSettings[zoneIndex].itemsPerStage);
        for (int i = 0; i < totalItemCount; i++)
        {
            sequence.Add(NormalItemPool[rnd.Next(NormalItemPool.Length)]);
        }

        return sequence;
    }

    /// <summary>ランダムに引いた座標が条件に合わなかったとき、引き直す回数の上限（1アイテムあたり）。</summary>
    private const int SpawnAttemptsPerItem = 5000;

    /// <summary>アイテムを置く候補を引く範囲。ゾーンの高さと横幅で囲まれた箱。</summary>
    private readonly struct SpawnArea
    {
        public readonly int BottomY;
        public readonly int TopY;
        public readonly int MinZ;
        public readonly int MaxZ;

        public bool IsEmpty => BottomY >= TopY || MinZ > MaxZ;

        public SpawnArea(VoxelTerrain terrain, int zoneIndex, int bottomY, int topY)
        {
            BottomY = Mathf.Max(0, bottomY);
            TopY = Mathf.Min(terrain.heightY, topY);

            int centerZ = terrain.maxStageWidthZ / 2;
            int halfWidth = terrain.zoneSettings[zoneIndex].widthZ / 2;
            MinZ = Mathf.Max(0, centerZ - halfWidth);
            MaxZ = Mathf.Min(terrain.maxStageWidthZ - 1, centerZ + halfWidth);
        }
    }

    /// <summary>
    /// 範囲内から掘れるブロックの座標をランダムに引き、accept を満たすものを返す。
    /// </summary>
    /// <remarks>
    /// 以前は範囲内の掘れるブロックをすべてリストに集めてシャッフルしていたが、
    /// 1ゾーンで数十万件になり、ステージ生成の最後に数秒止まる原因になっていた。
    /// 範囲内から一様に引いて条件外を捨てる方式でも、選ばれる確率の分布は変わらない。
    /// </remarks>
    private bool TryPickDiggablePosition(SpawnArea area, System.Random rnd, System.Func<Vector3Int, bool> accept, out Vector3Int coord)
    {
        for (int attempt = 0; attempt < SpawnAttemptsPerItem; attempt++)
        {
            int x = rnd.Next(thicknessX);
            int y = rnd.Next(area.BottomY, area.TopY);
            int z = rnd.Next(area.MinZ, area.MaxZ + 1);

            if (!IsInside(x, y, z) || !IsDiggable(mapData[x, y, z])) continue;
            if (!IsBuriedForItem(x, y, z)) continue;

            coord = new Vector3Int(x, y, z);
            if (accept(coord)) return true;
        }

        coord = default;
        return false;
    }

    /// <summary>
    /// 宝箱・爆弾を置く位置の周囲が、空気に面していないか。
    /// </summary>
    /// <remarks>
    /// 掘れるブロックかどうかだけで選ぶと、地表のすぐ下や開始地点の空洞の縁にも置かれ、
    /// 地上と地面の境で最初から見えてしまう。扉の IsFullyBuried と同じ考え方で、
    /// 上下左右 itemBurialMargin ブロック以内に空気が無い位置だけを候補にする。
    /// 境界壁や岩盤は空気ではないので、壁際や底に置くことは妨げない。
    /// </remarks>
    private bool IsBuriedForItem(int x, int centerY, int centerZ)
    {
        int margin = Mathf.Max(0, itemBurialMargin);

        for (int y = centerY - margin; y <= centerY + margin; y++)
        {
            if (y < 0 || y >= heightY) return false;

            for (int z = centerZ - margin; z <= centerZ + margin; z++)
            {
                if (z < 0 || z >= maxStageWidthZ) return false;
                if (mapData[x, y, z] == (byte)BlockType.Air) return false;
            }
        }

        return true;
    }

    private static bool IsFarFromAll(Vector3Int candidate, List<Vector3Int> others, float minDistance)
    {
        foreach (var other in others)
        {
            if (Vector3.Distance(candidate, other) < minDistance) return false;
        }
        return true;
    }

    /// <summary>
    /// アイテムや扉を埋め込んでよいブロックか。
    /// Quartzite は硬すぎるため、意図的に配置対象から外している。
    /// </summary>
    private static bool IsDiggable(byte block)
    {
        return block == (byte)BlockType.Dirt
            || block == (byte)BlockType.Ore
            || block == (byte)BlockType.Stone
            || block == (byte)BlockType.HardRock;
    }

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
    /// 爆弾を宝箱の近くほど出やすくなるように配置する。
    /// </summary>
    /// <remarks>
    /// 宝箱からの距離で決まる重み（最大100）を、引いた座標を採用する確率として使う。
    /// 全候補に重みを付けて抽選していた以前の方式と、選ばれる確率の分布は同じになる。
    /// </remarks>
    private void SpawnSmartBombs(int zoneIndex, int bombCount, SpawnArea area, HashSet<Vector3Int> occupied, List<Vector3Int> treasureCoords, System.Random rnd)
    {
        if (bombCount <= 0) return;

        float safeDistSq = bombSafeDistanceFromTreasure * bombSafeDistanceFromTreasure;

        for (int i = 0; i < bombCount; i++)
        {
            bool found = TryPickDiggablePosition(area, rnd,
                c => !occupied.Contains(c) && RollBombPlacement(c, treasureCoords, safeDistSq, rnd),
                out Vector3Int coord);

            if (!found)
            {
                Debug.LogWarning($"[VoxelTerrain] ゾーン {zoneIndex} で爆弾の安全距離制限が厳しすぎるため、配置可能な場所がありません。");
                return;
            }

            SpawnItemAt(SpawnItemType.Bomb, coord, zoneIndex);
            occupied.Add(coord);
        }
    }

    /// <summary>その座標に爆弾を置くかを、宝箱からの距離に応じた確率で決める。</summary>
    private bool RollBombPlacement(Vector3Int coord, List<Vector3Int> treasureCoords, float safeDistSq, System.Random rnd)
    {
        if (treasureCoords.Count == 0) return true;

        float minTreasureDistSq = float.MaxValue;
        foreach (var tCoord in treasureCoords)
        {
            float dx = coord.x - tCoord.x;
            float dy = coord.y - tCoord.y;
            float dz = coord.z - tCoord.z;
            float distSq = dx * dx + dy * dy + dz * dz;
            if (distSq < minTreasureDistSq) minTreasureDistSq = distSq;
        }

        if (minTreasureDistSq < safeDistSq) return false;

        float weight = Mathf.Max(1f, 100f - (Mathf.Sqrt(minTreasureDistSq) * bombWeightFalloff));
        return rnd.NextDouble() * 100.0 < weight;
    }
}
