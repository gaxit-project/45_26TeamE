using UnityEngine;


public partial class VoxelTerrain
{
    public void UpdateChunkMesh(int index)
    {
        if (chunks == null || index < 0 || index >= chunks.Length) return;
        int startY = index * chunkSizeY;
        int endY = Mathf.Min(startY + chunkSizeY, heightY);
        chunks[index].RebuildMesh(mapData, startY, endY, thicknessX, heightY, maxStageWidthZ, blockSize);
    }

    private void LateUpdate()
    {
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
