using UnityEngine;

public partial class VoxelTerrain
{
    [Header("境界壁の描画範囲")]
    [Tooltip("層の左右の端から、境界壁を何ブロック先まで描画するか。カメラの横の見える範囲（orthographic size × 画面比 ÷ blockSize）より大きくしておく。")]
    [SerializeField] private int boundaryMeshMarginZ = 80;
    [Tooltip("層の上下方向に、境界壁を何ブロック先まで描画するか。カメラの縦の見える範囲（orthographic size ÷ blockSize）より大きくしておく。")]
    [SerializeField] private int boundaryMeshMarginY = 40;

    /// <summary>この先の画面比（横÷縦）まではカメラの見える範囲に収まるか確認する。21:9 のウルトラワイドまでを想定。</summary>
    private const float SupportedMaxAspect = 21f / 9f;

    // 行（y）ごとに、メッシュにするZの範囲。範囲外は境界壁しかなく、カメラにも映らない。
    private int[] meshMinZ;
    private int[] meshMaxZ;

    public void UpdateChunkMesh(int index)
    {
        if (chunks == null || index < 0 || index >= chunks.Length) return;
        int startY = index * chunkSizeY;
        int endY = Mathf.Min(startY + chunkSizeY, heightY);
        chunks[index].RebuildMesh(mapData, startY, endY, thicknessX, heightY, maxStageWidthZ, blockSize, meshMinZ, meshMaxZ);
    }

    /// <summary>
    /// 行ごとに、メッシュにするZの範囲を求める。
    /// </summary>
    /// <remarks>
    /// 層の外側はステージ幅いっぱいまで境界壁（Boundary）で埋まっていて、
    /// 1層目などは幅の8割以上が誰にも見えない壁のメッシュになっていた。
    /// 境界壁は壊れずプレイヤーも近づけないので、層から一定距離より先はメッシュを作らない。
    /// 地形データ（mapData）や当たり判定のロジックは変えず、描画とメッシュコライダーだけが減る。
    /// 層はどれもステージ中央を基準に配置されているため、近くの行の範囲は最も広い層の範囲に含まれる。
    /// </remarks>
    private void ComputeMeshZRanges()
    {
        meshMinZ = new int[heightY];
        meshMaxZ = new int[heightY];

        int centerZ = maxStageWidthZ / 2;
        int marginY = Mathf.Max(0, boundaryMeshMarginY);
        int marginZ = Mathf.Max(0, boundaryMeshMarginZ);

        var rowHalfWidth = new int[heightY];
        for (int y = 0; y < heightY; y++)
        {
            rowHalfWidth[y] = zoneSettings[GetRelayID(y)].widthZ / 2;
        }

        for (int y = 0; y < heightY; y++)
        {
            int widest = 0;
            int fromY = Mathf.Max(0, y - marginY);
            int toY = Mathf.Min(heightY - 1, y + marginY);
            for (int ny = fromY; ny <= toY; ny++)
            {
                widest = Mathf.Max(widest, rowHalfWidth[ny]);
            }

            meshMinZ[y] = Mathf.Max(0, centerZ - widest - marginZ);
            meshMaxZ[y] = Mathf.Min(maxStageWidthZ - 1, centerZ + widest + marginZ);
        }

        WarnIfBoundaryMarginTooSmall();
    }

    /// <summary>
    /// カメラの設定が変わって、描画を省いた境界壁の外側が画面に映ってしまう場合に警告する。
    /// </summary>
    private void WarnIfBoundaryMarginTooSmall()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic || blockSize <= 0f) return;

        // 先読みや画面揺れでカメラが少しずれる分の余裕。
        const float CameraSlackWorld = 3f;

        float aspect = Mathf.Max(cam.aspect, SupportedMaxAspect);
        int neededZ = Mathf.CeilToInt((cam.orthographicSize * aspect + CameraSlackWorld) / blockSize);
        int neededY = Mathf.CeilToInt((cam.orthographicSize + CameraSlackWorld) / blockSize);

        if (boundaryMeshMarginZ < neededZ || boundaryMeshMarginY < neededY)
        {
            Debug.LogWarning($"[VoxelTerrain] 境界壁の描画範囲がカメラの見える範囲より狭いため、画面端で壁が途切れて見える可能性があります。" +
                             $"boundaryMeshMarginZ を {neededZ} 以上、boundaryMeshMarginY を {neededY} 以上にしてください。" +
                             $"（現在 Z={boundaryMeshMarginZ}, Y={boundaryMeshMarginY}）");
        }
    }

    /// <summary>直前のフレームでメッシュを作り直したチャンクの数。重いフレームの原因を調べるために記録する。</summary>
    public int ChunksRebuiltLastFrame { get; private set; }

    private void LateUpdate()
    {
        ChunksRebuiltLastFrame = chunksToUpdate.Count;
        if (chunksToUpdate.Count > 0)
        {
            foreach (int index in chunksToUpdate)
            {
                UpdateChunkMesh(index);
            }
            chunksToUpdate.Clear();
        }
    }

    public long GetZoneInitialGemValue(int py)
    {
        int zoneIndex = GetRelayID(py);
        if (zoneInitialGemValues.ContainsKey(zoneIndex))
        {
            return zoneInitialGemValues[zoneIndex];
        }
        return 0;
    }

    public float GetHardnessAtDepth(int y)
    {
        float depth = heightY - y;
        return 1.0f + Mathf.Max(0, depth * hardnessScale * 0.1f);
    }

    public float GetHardnessAtPosition(int x, int y, int z)
    {
        if (!IsInside(x, y, z)) return 1.0f;
        byte blockType = mapData[x, y, z];

        if ((BlockType)blockType == BlockType.Bedrock || (BlockType)blockType == BlockType.Boundary) return float.MaxValue;

        float baseHardness = (BlockType)blockType switch
        {
            BlockType.Dirt => 1.0f,
            BlockType.Ore => 6.0f,
            BlockType.Stone => 5.0f,
            BlockType.HardRock => 10.0f,
            BlockType.Quartzite => 20.0f,
            _ => 1.0f
        };

        float depthFactor = (heightY - y) * hardnessScale * 0.05f;
        return baseHardness + depthFactor;
    }
}
