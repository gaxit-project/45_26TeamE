using System.Collections.Generic;
using UnityEngine;

// アイテム、チャンク生成
public partial class VoxelTerrain
{
    private void GenerateChunksAndItems(System.Random rnd)
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        int numChunks = Mathf.CeilToInt((float)heightY / chunkSizeY);
        chunks = new Chunk[numChunks];

        for (int i = 0; i < numChunks; i++)
        {
            GameObject go = Instantiate(chunkPrefab, transform);
            go.name = $"Chunk_{i}";
            go.transform.localPosition = Vector3.zero;
            chunks[i] = go.GetComponent<Chunk>();
            chunks[i].Init(dirtMaterial, oreMaterial, bedrockMaterial, stoneMaterial, hardRockMaterial, quartziteMaterial, boundaryMaterial);
            UpdateChunkMesh(i);
        }

        int currentStageTopY = heightY;
        for (int zIdx = 0; zIdx < zoneSettings.Count; zIdx++)
        {
            int zoneHeight = zoneSettings[zIdx].heightChunks * chunkSizeY;
            int currentStageBottomY = currentStageTopY - zoneHeight;

            if (!zoneInitialGemValues.ContainsKey(zIdx))
            {
                zoneInitialGemValues[zIdx] = 0;
            }

            if (zoneSettings[zIdx].isGoalZone)
            {
                // ゴールゾーンは通常の鍵・爆弾・アイテム抽選を行わず、中央にゴールのお宝だけを配置する
                SpawnGoalTreasure(zIdx, currentStageBottomY, currentStageTopY);
                currentStageTopY = currentStageBottomY;
                continue;
            }

            List<Vector3Int> validPositions = new List<Vector3Int>();
            for (int y = currentStageBottomY; y < currentStageTopY; y++)
            {
                for (int x = 0; x < thicknessX; x++)
                {
                    for (int z = 0; z < maxStageWidthZ; z++)
                    {
                        if (!IsInside(x, y, z)) continue;

                        byte b = mapData[x, y, z];
                        if (b == (byte)BlockType.Dirt || b == (byte)BlockType.Ore || b == (byte)BlockType.Stone || b == (byte)BlockType.HardRock)
                        {
                            validPositions.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }

            if (validPositions.Count > 0)
            {
                for (int i = validPositions.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    var temp = validPositions[i];
                    validPositions[i] = validPositions[j];
                    validPositions[j] = temp;
                }

                int currentValidIndex = 0;
                List<SpawnItemType> tresureSequence = new List<SpawnItemType>();

                const int REQUIRED_KEY_COUNT = 3;
                for (int i = 0; i < REQUIRED_KEY_COUNT; i++) tresureSequence.Add(SpawnItemType.Key);

                SpawnItemType[] normalPool = {
                    SpawnItemType.Oxygen,
                    SpawnItemType.LeatherBag,
                    SpawnItemType.GoldLeatherBag,
                };

                int zoneItemsPerStage = Mathf.Max(REQUIRED_KEY_COUNT, zoneSettings[zIdx].itemsPerStage);
                int remainingTresureCount = zoneItemsPerStage - tresureSequence.Count;
                for (int i = 0; i < remainingTresureCount; i++)
                {
                    tresureSequence.Add(normalPool[rnd.Next(normalPool.Length)]);
                }

                int tresureSpawnCount = Mathf.Min(tresureSequence.Count, validPositions.Count);
                for (int i = 0; i < tresureSpawnCount; i++)
                {
                    if (currentValidIndex >= validPositions.Count) break;
                    SpawnItemAt(tresureSequence[i], validPositions[currentValidIndex], zIdx);
                    currentValidIndex++;
                }

                int zoneBombCount = Mathf.Max(0, zoneSettings[zIdx].bombCount);
                int bombSpawnCount = Mathf.Min(zoneBombCount, validPositions.Count - currentValidIndex);
                for (int i = 0; i < bombSpawnCount; i++)
                {
                    if (currentValidIndex >= validPositions.Count) break;
                    SpawnItemAt(SpawnItemType.Bomb, validPositions[currentValidIndex], zIdx);
                    currentValidIndex++;
                }
            }

            currentStageTopY = currentStageBottomY; ;
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

        List<Vector3Int> validPositions = new List<Vector3Int>();
        for (int y = currentStageBottomY; y < currentStageTopY; y++)
        {
            for (int x = 0; x < thicknessX; x++)
            {
                for (int z = 0; z < maxStageWidthZ; z++)
                {
                    if (!IsInside(x, y, z)) continue;

                    byte b = mapData[x, y, z];
                    if (b == (byte)BlockType.Dirt || b == (byte)BlockType.Ore || b == (byte)BlockType.Stone || b == (byte)BlockType.HardRock)
                    {
                        validPositions.Add(new Vector3Int(x, y, z));
                    }
                }
            }
        }

        if (validPositions.Count > 0)
        {
            var rnd = new System.Random();
            Vector3Int targetCoord = validPositions[rnd.Next(validPositions.Count)];

            // 鍵のタイプで宝箱を再生成
            SpawnItemAt(SpawnItemType.Key, targetCoord, zoneIndex);
            Debug.Log($"<color=orange>[鍵リスポーン]</color> ゾーン {zoneIndex} の空きブロック ({targetCoord.x}, {targetCoord.y}, {targetCoord.z}) に再配置しました。");
        }
    }
}
