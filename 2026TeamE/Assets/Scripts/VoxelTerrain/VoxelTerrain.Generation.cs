using System.Collections;
using UnityEngine;

public partial class VoxelTerrain
{
    private const float LayerNoiseScale = 0.2f;
    private const float LayerBumpyIntensity = 12f;
    private const int GoalThresholdY = 80;
    private const int BedrockCeilingY = 5;
    private const int StoneMarkerY = 6;
    private const int SurfaceAirMargin = 3;
    private const float PlayerSpawnClearRadius = 4.0f;

    private static readonly int[] NeighborOffsetX = { 1, -1, 0, 0, 0, 0 };
    private static readonly int[] NeighborOffsetY = { 0, 0, 1, -1, 0, 0 };
    private static readonly int[] NeighborOffsetZ = { 0, 0, 0, 0, 1, -1 };

    public void CreateStage(int width, int height, float size)
    {
        if (IsGenerating) return;
        StartCoroutine(CreateStageRoutine(width, size));
    }

    private IEnumerator CreateStageRoutine(int requestedWidth, float size)
    {
        IsGenerating = true;
        SceneInitializationGate.Begin();

        try
        {
            ApplyStageDimensions(requestedWidth, size);

            System.Random rnd = CreateRandomGenerator();
            Vector3Int startPoint = CalculateStartPoint();
            var budget = new FrameBudget();

            yield return FillTerrainRoutine(rnd, startPoint, budget);
            yield return RemoveIsolatedBlocksRoutine(budget);

            ApplyTerrainOrigin();

            yield return BuildChunksRoutine(budget);

            SpawnZoneItems(rnd);
            SpawnRelayPoints();
            TeleportPlayerToStart(startPoint.x, startPoint.y, startPoint.z);
            ClearBlocksAroundPlayer();
        }
        finally
        {
            SceneInitializationGate.End();
            IsGenerating = false;
        }
    }

    private void ApplyStageDimensions(int requestedWidth, float size)
    {
        maxStageWidthZ = Mathf.Max(requestedWidth, GetWidestZoneWidth());
        blockSize = size;
        heightY = GetTotalHeight();

        mapData = new byte[thicknessX, heightY, maxStageWidthZ];
        zoneInitialGemValues.Clear();
    }

    private int GetWidestZoneWidth()
    {
        int widest = 0;
        foreach (ZoneData zone in zoneSettings)
        {
            widest = Mathf.Max(widest, zone.widthZ);
        }
        return widest;
    }

    private System.Random CreateRandomGenerator()
    {
        if (ForceUseSeed)
        {
            ForceUseSeed = false; 
            return new System.Random(LastUsedSeed);
        }

        if (useDeterministicSeed)
        {
            LastUsedSeed = seed;
            return new System.Random(LastUsedSeed);
        }
        else
        {
            LastUsedSeed = System.Environment.TickCount;
            return new System.Random(LastUsedSeed);
        }
    }
    
    private Vector3Int CalculateStartPoint()
    {
        return new Vector3Int(0, heightY - startDepthFromSurface, maxStageWidthZ / 2);
    }

    private void ApplyTerrainOrigin()
    {
        float offsetX = -(thicknessX * blockSize) / 2f;
        float offsetZ = -(maxStageWidthZ * blockSize) / 2f;
        transform.position = new Vector3(offsetX, -(heightY * blockSize), offsetZ);
    }

    private void ClearBlocksAroundPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        ClearBlocksAroundPoint(player.transform.position, PlayerSpawnClearRadius);
    }

    private IEnumerator FillTerrainRoutine(System.Random rnd, Vector3Int startPoint, FrameBudget budget)
    {
        GoalChamber goalChamber = CreateGoalChamber();

        for (int y = 0; y < heightY; y++)
        {
            for (int x = 0; x < thicknessX; x++)
            {
                for (int z = 0; z < maxStageWidthZ; z++)
                {
                    mapData[x, y, z] = DetermineBlockType(x, y, z, startPoint, goalChamber, rnd);
                }

                if (budget.IsExhausted)
                {
                    yield return null;
                    budget.Renew();
                }
            }
        }
    }

    private byte DetermineBlockType(int x, int y, int z, Vector3Int startPoint, GoalChamber goalChamber, System.Random rnd)
    {
        if (!IsInside(x, y, z)) return (byte)BlockType.Boundary;

        if (IsInStartCavity(y, z, startPoint) || goalChamber.Contains(y, z)) return (byte)BlockType.Air;

        if (y > heightY - SurfaceAirMargin) return (byte)BlockType.Air;

        if (y < GoalThresholdY) return DetermineShallowBlockType(y, z, startPoint.z);

        if (IsRelayZoneBottom(y)) return (byte)BlockType.Bedrock;

        bool isOre = generationMode == GenerationMode.Layered && rnd.NextDouble() * 100.0 < oreProbability;
        if (isOre) return (byte)BlockType.Ore;

        return DetermineStrataBlockType(x, y, z);
    }

    private bool IsInStartCavity(int y, int z, Vector3Int startPoint)
    {
        float dz = z - startPoint.z;
        float dy = y - startPoint.y;
        float distanceFromStart = Mathf.Sqrt(dy * dy + dz * dz);

        bool insideStartHole = distanceFromStart < startHoleRadius;
        bool insideVerticalShaft = y > startPoint.y && Mathf.Abs(dz) < startShaftRadius;

        return insideStartHole || insideVerticalShaft;
    }

    private byte DetermineShallowBlockType(int y, int z, int startZ)
    {
        if (y <= BedrockCeilingY) return (byte)BlockType.Bedrock;
        if (y == StoneMarkerY && z == startZ) return (byte)BlockType.Stone;
        return (byte)BlockType.Air;
    }

    private byte DetermineStrataBlockType(int x, int y, int z)
    {
        float bumpyNoise = Mathf.PerlinNoise(x * LayerNoiseScale, z * LayerNoiseScale + (seed * 0.1f));
        float yOffset = (bumpyNoise - 0.5f) * LayerBumpyIntensity;
        float depthRatio = (y + yOffset) / heightY;

        if (generationMode == GenerationMode.Layered)
        {
            if (depthRatio < -0.2f) return (byte)BlockType.Quartzite;
            if (depthRatio < 0.2f) return (byte)BlockType.HardRock;
            if (depthRatio < 0.6f) return (byte)BlockType.Stone;
            return (byte)BlockType.Dirt;
        }

        float noiseVal = Mathf.PerlinNoise(z * patternNoiseScale + seed * 0.1f, y * patternNoiseScale + seed * 0.1f);

        if (depthRatio >= 0.6f)
        {
            return noiseVal > 0.6f ? (byte)BlockType.Stone : (byte)BlockType.Dirt;
        }
        if (depthRatio >= 0.2f)
        {
            if (noiseVal > 0.7f) return (byte)BlockType.HardRock;
            if (noiseVal > 0.3f) return (byte)BlockType.Stone;
            return (byte)BlockType.Dirt;
        }
        if (depthRatio >= -0.2f)
        {
            if (noiseVal > 0.7f) return (byte)BlockType.Quartzite;
            if (noiseVal > 0.3f) return (byte)BlockType.HardRock;
            return (byte)BlockType.Stone;
        }
        return noiseVal < 0.4f ? (byte)BlockType.HardRock : (byte)BlockType.Quartzite;
    }

    private readonly struct GoalChamber
    {
        public readonly bool Exists;
        private readonly int centerY;
        private readonly int centerZ;
        private readonly int halfSize;

        public GoalChamber(bool exists, int centerY, int centerZ, int halfSize)
        {
            Exists = exists;
            this.centerY = centerY;
            this.centerZ = centerZ;
            this.halfSize = halfSize;
        }

        public bool Contains(int y, int z)
        {
            if (!Exists || y <= BedrockCeilingY) return false;

            int dy = y - centerY;
            int dz = z - centerZ;

            return Mathf.Abs(dz) <= halfSize && dy > -halfSize && dy <= halfSize;
        }
    }

    private GoalChamber CreateGoalChamber()
    {
        int goalZoneIndex = zoneSettings.FindIndex(zone => zone.isGoalZone);
        if (goalZoneIndex < 0)
        {
            return new GoalChamber(false, 0, 0, 0);
        }

        int zoneTopY = heightY;
        for (int i = 0; i < goalZoneIndex; i++)
        {
            zoneTopY -= zoneSettings[i].heightChunks * chunkSizeY;
        }
        int zoneBottomY = zoneTopY - zoneSettings[goalZoneIndex].heightChunks * chunkSizeY;

        return new GoalChamber(
            exists: true,
            centerY: (zoneTopY + zoneBottomY) / 2,
            centerZ: maxStageWidthZ / 2,
            halfSize: Mathf.RoundToInt(goalChamberRadius));
    }

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

    private IEnumerator RemoveIsolatedBlocksRoutine(FrameBudget budget)
    {
        if (mapData == null) yield break;

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

                    if (!HasSolidNeighbor(x, y, z, sizeX, sizeY, sizeZ))
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                    }
                }

                if (budget.IsExhausted)
                {
                    yield return null;
                    budget.Renew();
                }
            }
        }
    }

    private bool HasSolidNeighbor(int x, int y, int z, int sizeX, int sizeY, int sizeZ)
    {
        for (int i = 0; i < NeighborOffsetX.Length; i++)
        {
            int nx = x + NeighborOffsetX[i];
            int ny = y + NeighborOffsetY[i];
            int nz = z + NeighborOffsetZ[i];

            bool isOutside = nx < 0 || nx >= sizeX || ny < 0 || ny >= sizeY || nz < 0 || nz >= sizeZ;
            if (isOutside) continue;

            if (mapData[nx, ny, nz] != (byte)BlockType.Air) return true;
        }
        return false;
    }
}
