using UnityEngine;

public partial class VoxelTerrain
{
    /// <summary>ステージ全体の高さ（ブロック数）。ステージ生成前は 0。</summary>
    public int HeightY => heightY;

    /// <summary>ステージ全体の横幅（Z方向のブロック数）。各層の幅は IsInside で判定する。</summary>
    public int StageWidthZ => maxStageWidthZ;

    public int GetTotalHeight()
    {
        int total = 0;
        foreach (var zone in zoneSettings)
        {
            total += zone.heightChunks * chunkSizeY;
        }
        return total;
    }

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
