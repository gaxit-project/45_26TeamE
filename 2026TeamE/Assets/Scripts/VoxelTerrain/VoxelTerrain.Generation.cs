using System.Collections.Generic;
using UnityEngine;

// ステージ生成
public partial class VoxelTerrain
{
    public void CreateStage(int width, int height, float size)
    {
        heightY = GetTotalHeight();
        int widestZone = 0;
        foreach (var zone in zoneSettings)
        {
            widestZone = Mathf.Max(widestZone, zone.widthZ);
        }
        if (widestZone > width)
        {
            width = widestZone;
        }

        maxStageWidthZ = width;
        blockSize = size;
        int totalChunksY = 0;
        foreach (var zone in zoneSettings)
        {
            totalChunksY += zone.heightChunks;
        }
        heightY = totalChunksY * chunkSizeY;
        mapData = new byte[thicknessX, heightY, maxStageWidthZ];
        zoneInitialGemValues.Clear();

        var rnd = useDeterministicSeed ? new System.Random(seed) : new System.Random();
        float layerNoiseScale = 0.2f;
        float layerBumpyIntensity = 12f;

        int startX = 0;
        int startY = heightY - startDepthFromSurface;
        int startZ = maxStageWidthZ / 2;

        int goalThresholdY = 80;

        // ゴールゾーン（isGoalZone）の中央に開けた部屋を掘るための位置を算出
        int goalZoneIndex = zoneSettings.FindIndex(z => z.isGoalZone);
        bool hasGoalZone = goalZoneIndex >= 0;
        int goalChamberCenterY = 0;
        int goalChamberCenterZ = maxStageWidthZ / 2;
        if (hasGoalZone)
        {
            int goalZoneTopY = heightY;
            for (int i = 0; i < goalZoneIndex; i++)
            {
                goalZoneTopY -= zoneSettings[i].heightChunks * chunkSizeY;
            }
            int goalZoneBottomY = goalZoneTopY - zoneSettings[goalZoneIndex].heightChunks * chunkSizeY;
            goalChamberCenterY = (goalZoneTopY + goalZoneBottomY) / 2;
        }
        int goalChamberHalfSize = Mathf.RoundToInt(goalChamberRadius);

        for (int y = 0; y < heightY; y++)
        {
            for (int x = 0; x < thicknessX; x++)
            {
                for (int z = 0; z < maxStageWidthZ; z++)
                {
                    if (!IsInside(x, y, z))
                    {
                        mapData[x, y, z] = (byte)BlockType.Boundary;
                        continue;
                    }

                    float dz = z - startZ;
                    float dy = y - startY;
                    float distSphere = Mathf.Sqrt(dy * dy + dz * dz);

                    // ゴールゾーンの中央付近を部屋として掘る（最下段は床として残す。岩盤層(y<=5)は掘らない）
                    bool inGoalChamber = false;
                    if (hasGoalZone && y > 5)
                    {
                        int dyToChamber = y - goalChamberCenterY;
                        int dzToChamber = z - goalChamberCenterZ;
                        if (Mathf.Abs(dzToChamber) <= goalChamberHalfSize
                            && dyToChamber > -goalChamberHalfSize
                            && dyToChamber <= goalChamberHalfSize)
                        {
                            inGoalChamber = true;
                        }
                    }

                    if (distSphere < startHoleRadius || (y > startY && Mathf.Abs(dz) < startShaftRadius) || inGoalChamber)
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                        continue;
                    }
                    if (y > heightY - 3)
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                    }
                    else if (y < goalThresholdY)
                    {
                        if (y <= 5) mapData[x, y, z] = (byte)BlockType.Bedrock;
                        else if (y == 6 && z == startZ) mapData[x, y, z] = (byte)BlockType.Stone;
                        else mapData[x, y, z] = (byte)BlockType.Air;
                    }
                    else if (IsRelayZoneBottom(y))
                    {
                        mapData[x, y, z] = (byte)BlockType.Bedrock;
                    }
                    else
                    {
                        if (generationMode == GenerationMode.Layered && rnd.NextDouble() * 100.0 < oreProbability)
                        {
                            mapData[x, y, z] = (byte)BlockType.Ore;
                        }
                        else
                        {
                            float bumpyNoise = Mathf.PerlinNoise(x * layerNoiseScale, z * layerNoiseScale + (seed * 0.1f));
                            float yOffset = (bumpyNoise - 0.5f) * layerBumpyIntensity;
                            float bumpyY = y + yOffset;
                            float depthRatio = bumpyY / heightY;

                            if (generationMode == GenerationMode.Layered)
                            {
                                if (depthRatio < -0.2f) mapData[x, y, z] = (byte)BlockType.Quartzite;
                                else if (depthRatio < 0.2f) mapData[x, y, z] = (byte)BlockType.HardRock;
                                else if (depthRatio < 0.6f) mapData[x, y, z] = (byte)BlockType.Stone;
                                else mapData[x, y, z] = (byte)BlockType.Dirt;
                            }
                            else
                            {
                                float noiseVal = Mathf.PerlinNoise(z * patternNoiseScale + seed * 0.1f, y * patternNoiseScale + seed * 0.1f);
                                if (depthRatio >= 0.6f)
                                {
                                    mapData[x, y, z] = noiseVal > 0.6f ? (byte)BlockType.Stone : (byte)BlockType.Dirt;
                                }
                                else if (depthRatio >= 0.2f)
                                {
                                    if (noiseVal > 0.7f) mapData[x, y, z] = (byte)BlockType.HardRock;
                                    else if (noiseVal > 0.3f) mapData[x, y, z] = (byte)BlockType.Stone;
                                    else mapData[x, y, z] = (byte)BlockType.Dirt;
                                }
                                else if (depthRatio >= -0.2f)
                                {
                                    if (noiseVal > 0.7f) mapData[x, y, z] = (byte)BlockType.Quartzite;
                                    else if (noiseVal > 0.3f) mapData[x, y, z] = (byte)BlockType.HardRock;
                                    else mapData[x, y, z] = (byte)BlockType.Stone;
                                }
                                else
                                {
                                    mapData[x, y, z] = noiseVal < 0.4f ? (byte)BlockType.HardRock : (byte)BlockType.Quartzite;
                                }
                            }
                        }
                    }
                }
            }
        }

        CleanupIsolatedDirtBlocks();

        float offsetX = -(thicknessX * blockSize) / 2f;
        float offsetZ = -(maxStageWidthZ * blockSize) / 2f;
        transform.position = new Vector3(offsetX, -(heightY * blockSize), offsetZ);

        GenerateChunksAndItems(rnd);
        SpawnRelayPoints();
        TeleportPlayerToStart(startX, startY, startZ);

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            ClearBlocksAroundPoint(playerObj.transform.position, 4.0f);
        }
    }

    /// <summary>
    /// 最下層かどうか判定する
    /// </summary>
    public bool IsRelayZoneBottom(int y)
    {
        int currentY = heightY;
        for (int i = 0; i < zoneSettings.Count - 1; i++)
        {
            currentY -= zoneSettings[i].heightChunks * chunkSizeY;
            if (y == currentY || y == currentY - 1)
            {
                return true;
            }
        }
        return false;
    }

    private static readonly int[] dx = { 1, -1, 0, 0, 0, 0 };
    private static readonly int[] dy = { 0, 0, 1, -1, 0, 0 };
    private static readonly int[] dz = { 0, 0, 0, 0, 1, -1 };

    private void CleanupIsolatedDirtBlocks()
    {
        if (mapData == null) return;

        int sizeX = mapData.GetLength(0);
        int sizeY = mapData.GetLength(1);
        int sizeZ = mapData.GetLength(2);

        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (mapData[x, y, z] == (byte)BlockType.Air) continue;

                    bool hasNeighbor = false;

                    int[] dx = { 1, -1, 0, 0, 0, 0 };
                    int[] dy = { 0, 0, 1, -1, 0, 0 };
                    int[] dz = { 0, 0, 0, 0, 1, -1 };

                    for (int i = 0; i < 6; i++)
                    {
                        int nx = x + dx[i];
                        int ny = y + dy[i];
                        int nz = z + dz[i];

                        if (nx >= 0 && nx < sizeX && ny >= 0 && ny < sizeY && nz >= 0 && nz < sizeZ)
                        {
                            if (mapData[nx, ny, nz] != (byte)BlockType.Air)
                            {
                                hasNeighbor = true;
                                break;
                            }
                        }
                    }
                    if (!hasNeighbor)
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                    }
                }
            }
        }
    }
}
