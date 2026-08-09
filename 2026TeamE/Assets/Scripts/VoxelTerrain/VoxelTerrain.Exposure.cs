using UnityEngine;

// òIèoîªíË
public partial class VoxelTerrain
{
    public bool IsJewelExposed(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int x = Mathf.RoundToInt(localPos.x / blockSize);
        int y = Mathf.FloorToInt(localPos.y / blockSize);
        int z = Mathf.FloorToInt(localPos.z / blockSize);

        bool allAir = true;

        if (IsInside(x, y, z) && mapData[x, y, z] != (byte)BlockType.Air && mapData[x, y, z] != (byte)BlockType.Bedrock) allAir = false;

        if (IsInside(x, y + 1, z) && mapData[x, y + 1, z] != (byte)BlockType.Air && mapData[x, y + 1, z] != (byte)BlockType.Bedrock) allAir = false;
        if (IsInside(x, y - 1, z) && mapData[x, y - 1, z] != (byte)BlockType.Air && mapData[x, y - 1, z] != (byte)BlockType.Bedrock) allAir = false;
        if (IsInside(x, y, z + 1) && mapData[x, y, z + 1] != (byte)BlockType.Air && mapData[x, y, z + 1] != (byte)BlockType.Bedrock) allAir = false;
        if (IsInside(x, y, z - 1) && mapData[x, y, z - 1] != (byte)BlockType.Air && mapData[x, y, z - 1] != (byte)BlockType.Bedrock) allAir = false;

        return allAir;
    }

    public bool IsJewelExposed(Vector3 worldPos, Vector3 scale)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int x = Mathf.RoundToInt(localPos.x / blockSize);
        int centerY = Mathf.FloorToInt(localPos.y / blockSize);
        int centerZ = Mathf.FloorToInt(localPos.z / blockSize);

        int extentY = Mathf.CeilToInt(scale.y / 2f);
        int extentZ = Mathf.CeilToInt(scale.z / 2f);

        if (extentY < 1) extentY = 1;
        if (extentZ < 1) extentZ = 1;

        for (int y = centerY - extentY; y <= centerY + extentY; y++)
        {
            for (int z = centerZ - extentZ; z <= centerZ + extentZ; z++)
            {
                if (IsInside(x, y, z) && mapData[x, y, z] != (byte)BlockType.Air && mapData[x, y, z] != (byte)BlockType.Bedrock)
                {
                    return false;
                }
            }
        }
        return true;
    }
}
