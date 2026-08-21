using System.Collections;
using UnityEngine;

// ステージ生成
public partial class VoxelTerrain
{
    // --- 地形の見た目を決めるパラメータ ---------------------------------

    /// <summary>地層の境目をデコボコさせるノイズの細かさ。</summary>
    private const float LayerNoiseScale = 0.2f;
    /// <summary>地層の境目をデコボコさせる強さ（ブロック数）。</summary>
    private const float LayerBumpyIntensity = 12f;
    /// <summary>この深さより浅い場所は、ゴール演出用の空洞として扱う。</summary>
    private const int GoalThresholdY = 80;
    /// <summary>この高さ以下は掘れない岩盤にする。</summary>
    private const int BedrockCeilingY = 5;
    /// <summary>岩盤のすぐ上、中央に1マスだけ置く目印ブロックの高さ。</summary>
    private const int StoneMarkerY = 6;
    /// <summary>ステージ最上部のこの範囲は空洞にする。</summary>
    private const int SurfaceAirMargin = 3;
    /// <summary>プレイヤーの初期位置まわりを掘り抜く半径（ワールド単位）。</summary>
    private const float PlayerSpawnClearRadius = 4.0f;

    /// <summary>隣接6方向のオフセット。孤立ブロックの判定に使う。</summary>
    private static readonly int[] NeighborOffsetX = { 1, -1, 0, 0, 0, 0 };
    private static readonly int[] NeighborOffsetY = { 0, 0, 1, -1, 0, 0 };
    private static readonly int[] NeighborOffsetZ = { 0, 0, 0, 0, 1, -1 };

    // --- 生成のエントリポイント -----------------------------------------

    /// <summary>
    /// ステージを生成する。
    /// </summary>
    /// <param name="width">希望するステージ幅。実際の幅は最も広いゾーンの幅と比較して大きい方が採用される。</param>
    /// <param name="height">
    /// 互換性のために残している引数。実際の高さは zoneSettings の heightChunks 合計から算出されるため使用されない。
    /// </param>
    /// <param name="size">1ブロックのワールド上のサイズ。</param>
    /// <remarks>
    /// 処理が非常に重いため、複数フレームに分割して非同期に実行される。
    /// このメソッドから戻った時点では生成は完了していない。完了判定には
    /// <see cref="IsGenerating"/> または <see cref="SceneInitializationGate"/> を使うこと。
    /// </remarks>
    public void CreateStage(int width, int height, float size)
    {
        if (IsGenerating) return;

        StartCoroutine(CreateStageRoutine(width, size));
    }

    /// <summary>
    /// ステージ生成の全工程。重い工程はフレームをまたぎながら実行する。
    /// </summary>
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
            // 途中で中断された場合でもローディング画面が残り続けないよう、必ず完了を通知する
            SceneInitializationGate.End();
            IsGenerating = false;
        }
    }

    // --- 準備 -----------------------------------------------------------

    /// <summary>
    /// ステージの寸法を確定し、ブロックデータの領域を確保する。
    /// </summary>
    private void ApplyStageDimensions(int requestedWidth, float size)
    {
        maxStageWidthZ = Mathf.Max(requestedWidth, GetWidestZoneWidth());
        blockSize = size;
        heightY = GetTotalHeight();

        mapData = new byte[thicknessX, heightY, maxStageWidthZ];
        zoneInitialGemValues.Clear();
    }

    /// <summary>最も横幅の広いゾーンの幅を返す。</summary>
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
        return useDeterministicSeed ? new System.Random(seed) : new System.Random();
    }

    /// <summary>プレイヤーの初期位置（ブロック座標）を求める。</summary>
    private Vector3Int CalculateStartPoint()
    {
        return new Vector3Int(0, heightY - startDepthFromSurface, maxStageWidthZ / 2);
    }

    /// <summary>ステージ全体がプレイヤーから見て正しい位置に来るよう原点を移動する。</summary>
    private void ApplyTerrainOrigin()
    {
        float offsetX = -(thicknessX * blockSize) / 2f;
        float offsetZ = -(maxStageWidthZ * blockSize) / 2f;
        transform.position = new Vector3(offsetX, -(heightY * blockSize), offsetZ);
    }

    /// <summary>プレイヤーが埋まったまま開始しないよう、初期位置まわりを掘り抜く。</summary>
    private void ClearBlocksAroundPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        ClearBlocksAroundPoint(player.transform.position, PlayerSpawnClearRadius);
    }

    // --- 地形の充填 -----------------------------------------------------

    /// <summary>
    /// 全ブロックの種類を決定して mapData を埋める。
    /// </summary>
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

    /// <summary>
    /// 指定座標に置くべきブロックの種類を決める。
    /// 判定は「範囲外 → 空洞 → 浅層 → 中継地点の岩盤 → 鉱石 → 地層」の優先順位で行う。
    /// </summary>
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

    /// <summary>スタート地点の球状の空洞、またはそこから地表へ伸びる縦穴の内側かどうか。</summary>
    private bool IsInStartCavity(int y, int z, Vector3Int startPoint)
    {
        float dz = z - startPoint.z;
        float dy = y - startPoint.y;
        float distanceFromStart = Mathf.Sqrt(dy * dy + dz * dz);

        bool insideStartHole = distanceFromStart < startHoleRadius;
        bool insideVerticalShaft = y > startPoint.y && Mathf.Abs(dz) < startShaftRadius;

        return insideStartHole || insideVerticalShaft;
    }

    /// <summary>ゴール閾値より浅い層のブロック種類。基本は空洞で、最下部だけ岩盤にする。</summary>
    private byte DetermineShallowBlockType(int y, int z, int startZ)
    {
        if (y <= BedrockCeilingY) return (byte)BlockType.Bedrock;
        if (y == StoneMarkerY && z == startZ) return (byte)BlockType.Stone;
        return (byte)BlockType.Air;
    }

    /// <summary>
    /// 深さに応じた地層のブロック種類。
    /// 深いほど硬い岩になるよう、ノイズでデコボコさせた深さの割合で判定する。
    /// </summary>
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

    // --- ゴール部屋 -----------------------------------------------------

    /// <summary>
    /// ゴールゾーンの中央に掘る部屋の範囲。ゴールゾーンが無い場合は <see cref="Exists"/> が false になる。
    /// </summary>
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

        /// <summary>指定座標が部屋の内側かどうか。岩盤層は床として残すため掘らない。</summary>
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

    // --- 後処理 ---------------------------------------------------------

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

    /// <summary>
    /// 周囲6方向すべてが空洞になっている、宙に浮いたブロックを取り除く。
    /// </summary>
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

    /// <summary>隣接6方向のいずれかに空洞でないブロックがあるかどうか。</summary>
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
