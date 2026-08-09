using UnityEngine;

// ゾーン、範囲判定
public partial class VoxelTerrain
{
    /// <summary>
    /// 全ゾーンの深さ（Yブロック数）を合計して算出
    /// </summary>
    public int GetTotalHeight()
    {
        int total = 0;
        foreach (var zone in zoneSettings)
        {
            total += zone.heightChunks * chunkSizeY;
        }
        return total;
    }

    /// <summary>
    /// Y座標から現在の層のインデックスを取得
    /// </summary>
    public int GetRelayID(int y)
    {
        int currentY = heightY;

        for (int i = 0; i < zoneSettings.Count; i++)
        {
            int zoneHeight = zoneSettings[i].heightChunks * chunkSizeY;
            int nextY = currentY - zoneHeight;

            if (y < currentY && y >= nextY)
            {
                return i;
            }
            currentY = nextY;
        }
        return zoneSettings.Count - 1;
    }

    /// <summary>
    /// ブロックが層ごとの有効領域内にあるかチェック
    /// </summary>
    public bool IsInside(int x, int y, int z)
    {
        if (x < 0 || x >= thicknessX) return false;
        if (y < 0 || y >= heightY) return false;

        int zoneIndex = GetRelayID(y);
        ZoneData currentZone = zoneSettings[zoneIndex];

        int centerZ = maxStageWidthZ / 2;
        int halfWidth = currentZone.widthZ / 2;

        int minZ = centerZ - halfWidth;
        int maxZ = centerZ + halfWidth;

        return (z >= minZ && z <= maxZ);
    }

    /// <summary>
    /// 指定ゾーンの最下部のY座標を取得（中継地点の設置基準）
    /// </summary>
    public int GetZoneBottomY(int zoneIndex)
    {
        int currentY = heightY;
        for (int i = 0; i <= zoneIndex && i < zoneSettings.Count; i++)
        {
            currentY -= zoneSettings[i].heightChunks * chunkSizeY;
        }
        return currentY;
    }
}
