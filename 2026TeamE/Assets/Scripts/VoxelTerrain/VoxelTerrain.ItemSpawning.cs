using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public partial class VoxelTerrain
{
    
    private const int RequiredKeyCount = 3;

    
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
        List<Vector3Int> candidates = CollectDiggablePositions(bottomY, topY);
        if (candidates.Count == 0) return;

        Shuffle(candidates, rnd);

        
        List<SpawnItemType> treasureSequence = BuildTreasureSequence(zoneIndex, rnd);
        List<Vector3Int> treasureCoords = new List<Vector3Int>();
        List<Vector3Int> rejectedForTreasure = new List<Vector3Int>();

        int sequenceIndex = 0;
        int candidateIndex = 0;
        
        
        while (sequenceIndex < treasureSequence.Count && candidateIndex < candidates.Count)
        {
            Vector3Int candidate = candidates[candidateIndex++];

            bool isFarEnough = true;
            foreach (var tCoord in treasureCoords)
            {
                if (Vector3.Distance(candidate, tCoord) < treasureMinDistance)
                {
                    isFarEnough = false;
                    break;
                }
            }

            if (isFarEnough)
            {
                SpawnItemAt(treasureSequence[sequenceIndex], candidate, zoneIndex);
                treasureCoords.Add(candidate);
                sequenceIndex++;
            }
            else
            {
                rejectedForTreasure.Add(candidate);
            }
        }

        
        int rejectedIndex = 0;
        if (sequenceIndex < treasureSequence.Count)
        {
            Debug.LogWarning($"[VoxelTerrain] ゾーン {zoneIndex} で宝箱の距離制限が厳しすぎるため、距離制限を無視して配置を継続します。");
            while (sequenceIndex < treasureSequence.Count && rejectedIndex < rejectedForTreasure.Count)
            {
                Vector3Int candidate = rejectedForTreasure[rejectedIndex++];

                SpawnItemAt(treasureSequence[sequenceIndex], candidate, zoneIndex);
                treasureCoords.Add(candidate);
                sequenceIndex++;
            }
        }

        int bombCount = Mathf.Max(0, zoneSettings[zoneIndex].bombCount);
        
        
        List<Vector3Int> remainingCandidates = new List<Vector3Int>();
        for (int i = rejectedIndex; i < rejectedForTreasure.Count; i++) remainingCandidates.Add(rejectedForTreasure[i]);
        for (int i = candidateIndex; i < candidates.Count; i++) remainingCandidates.Add(candidates[i]);
        
        SpawnSmartBombs(zoneIndex, bombCount, remainingCandidates, treasureCoords, rnd);
    }

    
    
    
    
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

    
    private static bool IsDiggable(byte block)
    {
        return block == (byte)BlockType.Dirt
            || block == (byte)BlockType.Ore
            || block == (byte)BlockType.Stone
            || block == (byte)BlockType.HardRock;
    }

    
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

    
    
    
    public void RespawnKeyTreasureBox(Vector3 destroyedPos, int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex >= zoneSettings.Count) return;

        int currentStageTopY = heightY;
        for (int i = 0; i < zoneIndex; i++)
        {
            currentStageTopY -= zoneSettings[i].heightChunks * chunkSizeY;
        }
        int currentStageBottomY = currentStageTopY - (zoneSettings[zoneIndex].heightChunks * chunkSizeY);

        List<Vector3Int> validPositions = CollectDiggablePositions(currentStageBottomY, currentStageTopY);
        if (validPositions.Count == 0) return;

        var rnd = new System.Random();
        Vector3Int targetCoord = validPositions[rnd.Next(validPositions.Count)];

        
        SpawnItemAt(SpawnItemType.Key, targetCoord, zoneIndex);
        Debug.Log($"<color=orange>[鍵リスポーン]</color> ゾーン {zoneIndex} の空きブロック ({targetCoord.x}, {targetCoord.y}, {targetCoord.z}) に再配置しました。");
    }

    
    
    
    private void SpawnSmartBombs(int zoneIndex, int bombCount, List<Vector3Int> candidates, List<Vector3Int> treasureCoords, System.Random rnd)
    {
        if (candidates.Count == 0 || bombCount <= 0) return;

        List<Vector3Int> validCandidates = new List<Vector3Int>();
        List<float> weights = new List<float>();
        float totalWeight = 0f;

        float safeDistSq = bombSafeDistanceFromTreasure * bombSafeDistanceFromTreasure;

        
        foreach (var coord in candidates)
        {
            float minTreasureDistSq = float.MaxValue;
            foreach (var tCoord in treasureCoords)
            {
                float dx = coord.x - tCoord.x;
                float dy = coord.y - tCoord.y;
                float dz = coord.z - tCoord.z;
                float distSq = dx * dx + dy * dy + dz * dz;
                if (distSq < minTreasureDistSq) minTreasureDistSq = distSq;
            }

            if (treasureCoords.Count > 0 && minTreasureDistSq < safeDistSq)
                continue; 

            float minDist = treasureCoords.Count == 0 ? 0 : Mathf.Sqrt(minTreasureDistSq);
            float weight = Mathf.Max(1f, 100f - (minDist * bombWeightFalloff));
            
            validCandidates.Add(coord);
            weights.Add(weight);
            totalWeight += weight;
        }

        if (validCandidates.Count == 0)
        {
            Debug.LogWarning($"[VoxelTerrain] ゾーン {zoneIndex} で爆弾の安全距離制限が厳しすぎるため、配置可能な場所がありません。");
            return;
        }

        List<Vector3Int> spawnedBombCoords = new List<Vector3Int>();

        for (int i = 0; i < bombCount; i++)
        {
            if (validCandidates.Count == 0 || totalWeight <= 0) break;

            
            float roll = (float)(rnd.NextDouble() * totalWeight);
            int selectedIndex = -1;
            
            for (int w = 0; w < weights.Count; w++)
            {
                if (roll < weights[w])
                {
                    selectedIndex = w;
                    break;
                }
                roll -= weights[w];
            }
            if (selectedIndex == -1) selectedIndex = weights.Count - 1;

            Vector3Int selectedCoord = validCandidates[selectedIndex];
            SpawnItemAt(SpawnItemType.Bomb, selectedCoord, zoneIndex);
            spawnedBombCoords.Add(selectedCoord);
            
            
            totalWeight -= weights[selectedIndex];
            
            int lastIndex = validCandidates.Count - 1;
            validCandidates[selectedIndex] = validCandidates[lastIndex];
            weights[selectedIndex] = weights[lastIndex];
            
            validCandidates.RemoveAt(lastIndex);
            weights.RemoveAt(lastIndex);
        }
    }
}
