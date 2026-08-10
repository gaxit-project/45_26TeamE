using System.Collections.Generic;
using UnityEngine;

// çÃå@ÅAëÄçÏèàóù
public partial class VoxelTerrain
{
    public void ExecuteDig(int centerX, int centerY, int centerZ, float radius, Vector3 minLimit, Vector3 maxLimit, bool isPlayerDigging = true)
    {
        int r = Mathf.CeilToInt(radius);
        bool changed = false;
        HashSet<int> changedYRows = new HashSet<int>();
        int destroyedDirt = 0;
        int destroyedOre = 0;

        for (int y = centerY - r; y <= centerY + r; y++)
        {
            for (int z = centerZ - r; z <= centerZ + r; z++)
            {
                float distSq = (centerY - y) * (centerY - y) + (centerZ - z) * (centerZ - z);
                if (distSq > radius * radius) continue;
                if (y < minLimit.y || y > maxLimit.y || z < minLimit.z || z > maxLimit.z) continue;

                bool emittedForThisCell = false;

                for (int x = 0; x < thicknessX; x++)
                {
                    if (!IsInside(x, y, z)) continue;
                    byte currentBlock = mapData[x, y, z];
                    if (currentBlock == (byte)BlockType.Air || currentBlock == (byte)BlockType.Bedrock || currentBlock == (byte)BlockType.Boundary) continue;

                    if (currentBlock == (byte)BlockType.Dirt)
                    {
                        destroyedDirt++;
                        if (!emittedForThisCell && BlockEffectManager.Instance != null)
                        {
                            Vector3 worldPos = transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize);
                            BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.Dirt);
                            emittedForThisCell = true;
                        }
                    }
                    else if (currentBlock == (byte)BlockType.Ore)
                    {
                        destroyedOre++;
                        if (!emittedForThisCell && BlockEffectManager.Instance != null)
                        {
                            Vector3 worldPos = transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize);
                            BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.Ore);
                            emittedForThisCell = true;
                        }
                    }
                    else if (currentBlock == (byte)BlockType.Stone && !emittedForThisCell && BlockEffectManager.Instance != null)
                    {
                        Vector3 worldPos = transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize);
                        BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.Stone);
                        emittedForThisCell = true;
                    }
                    else if (currentBlock == (byte)BlockType.HardRock && !emittedForThisCell && BlockEffectManager.Instance != null)
                    {
                        Vector3 worldPos = transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * BlockSize);
                        BlockEffectManager.Instance.PlayEffectAt(worldPos, EffectType.HardRock);
                        emittedForThisCell = true;
                    }

                    mapData[x, y, z] = 0;
                    changed = true;
                    changedYRows.Add(y);
                }
            }
        }

        if (isPlayerDigging && (destroyedDirt > 0 || destroyedOre > 0))
        {
            OnBlocksDestroyedByPlayer?.Invoke(destroyedDirt, destroyedOre);
        }

        if (changed)
        {
            foreach (var yIndex in changedYRows)
            {
                int cIndex = yIndex / chunkSizeY;
                if (chunks == null || cIndex < 0 || cIndex >= chunks.Length) continue;
                chunksToUpdate.Add(cIndex);

                if (yIndex % chunkSizeY == 0 && cIndex > 0)
                    chunksToUpdate.Add(cIndex - 1);
                if (yIndex % chunkSizeY == chunkSizeY - 1 && cIndex < chunks.Length - 1)
                    chunksToUpdate.Add(cIndex + 1);
            }
        }
    }

    public void RemoveBedrockAroundPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;

        Vector3 localPos = transform.InverseTransformPoint(playerObj.transform.position);
        int py = Mathf.FloorToInt(localPos.y / blockSize);
        int pz = Mathf.FloorToInt(localPos.z / blockSize);
        int searchRange = 5;

        for (int x = 0; x < thicknessX; x++)
        {
            for (int y = py - 2; y <= py + 2; y++)
            {
                for (int z = pz - searchRange; z <= pz + searchRange; z++)
                {
                    if (!IsInside(x, y, z)) continue;

                    if (mapData[x, y, z] == (byte)BlockType.Bedrock)
                    {
                        mapData[x, y, z] = (byte)BlockType.Air;
                        int cIndex = y / chunkSizeY;
                        chunksToUpdate.Add(cIndex);

                        if (y % chunkSizeY == 0 && cIndex > 0) chunksToUpdate.Add(cIndex - 1);
                        if (y % chunkSizeY == chunkSizeY - 1 && cIndex < chunks.Length - 1) chunksToUpdate.Add(cIndex + 1);
                    }
                }
            }
        }
    }

    public void ClearBlocksAroundPoint(Vector3 worldCenter, float radius)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldCenter);
        int centerX = Mathf.RoundToInt(localPos.x / blockSize);
        int centerY = Mathf.RoundToInt(localPos.y / blockSize);
        int centerZ = Mathf.RoundToInt(localPos.z / blockSize);

        int r = Mathf.CeilToInt(radius / blockSize);

        for (int x = centerX - r; x <= centerX + r; x++)
        {
            for (int y = centerY - r; y <= centerY + r; y++)
            {
                for (int z = centerZ - r; z <= centerZ + r; z++)
                {
                    if (!IsInside(x, y, z)) continue;
                    float distSq = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) + (z - centerZ) * (z - centerZ);
                    if (distSq <= (radius / blockSize) * (radius / blockSize))
                    {
                        if (mapData[x, y, z] != (byte)BlockType.Air)
                        {
                            mapData[x, y, z] = (byte)BlockType.Air;
                            int cIndex = y / chunkSizeY;
                            chunksToUpdate.Add(cIndex);
                            if (y % chunkSizeY == 0 && cIndex > 0) chunksToUpdate.Add(cIndex - 1);
                            if (y % chunkSizeY == chunkSizeY - 1 && cIndex < chunks.Length - 1) chunksToUpdate.Add(cIndex + 1);
                        }
                    }
                }
            }
        }
    }
}
