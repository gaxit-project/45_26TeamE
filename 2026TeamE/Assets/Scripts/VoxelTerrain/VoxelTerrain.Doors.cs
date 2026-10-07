using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地中に埋まっている「扉」に関する処理。
/// 扉は掘り進めるうちにランダムな位置から現れ、入るとお金の両替・強化画面へ移行する。
/// 戻ってきたときは入った扉の位置から再開し、その扉は壊れて二度と入れない。
/// </summary>
public partial class VoxelTerrain
{
    /// <summary>扉から戻ってきたとき、プレイヤーの周囲を掘り抜く半径（ワールド単位）。</summary>
    private const float DoorReturnClearRadius = 4.0f;

    /// <summary>扉から戻ってきたときの復帰位置。地形のローカル座標で保持する。</summary>
    private Vector3 doorReturnLocalPosition;

    /// <summary>復帰待ちの扉があるか。</summary>
    public bool HasPendingDoorReturn { get; private set; }

    /// <summary>いまプレイヤーが入場できる扉。範囲外なら null。</summary>
    public DoorBehaviour CurrentDoorInRange { get; private set; }

    /// <summary>プレイヤーが入場範囲に入っている扉として登録する。</summary>
    public void SetDoorInRange(DoorBehaviour door)
    {
        CurrentDoorInRange = door;
    }

    /// <summary>登録されている扉を解除する。別の扉が登録済みなら何もしない。</summary>
    public void ClearDoorInRange(DoorBehaviour door)
    {
        if (CurrentDoorInRange == door) CurrentDoorInRange = null;
    }

    /// <summary>
    /// いま範囲内にある扉に入る。ボタン入力を受けた側（SelectPoint）から呼ぶ。
    /// 復帰位置の記録までを行い、画面の切り替えは呼び出し側に任せる。
    /// </summary>
    /// <returns>入場できた場合はtrue。呼び出し側はtrueならリザルトへ遷移させる。</returns>
    public bool TryEnterDoorInRange()
    {
        if (CurrentDoorInRange == null) return false;
        return CurrentDoorInRange.TryEnter();
    }

    // --- 生成 -------------------------------------------------------------

    /// <summary>
    /// 扉を深さ方向に等間隔で配置する。
    /// 地表から数えて <see cref="chunksPerDoor"/> チャンク（既定15）ずつを1単位とし、
    /// 各単位の最深部 <see cref="doorBandChunks"/> チャンクの中に扉を1つだけ置く。
    /// これで「一定の深さまで掘るごとに扉が1つ現れる」ペースになる。
    /// </summary>
    /// <remarks>
    /// チャンク番号について：Unityの Chunk_N は地面（最下部）が 0 で、地表に向かうほど大きくなる。
    /// 扉の配置は「掘り進めた量」で決めたいので、この処理の中では
    /// 地表から数えた 0 始まりのチャンク番号（地表が 0）で計算し、ログでは両方を出す。
    /// </remarks>
    private void SpawnDoors(System.Random rnd)
    {
        if (doorPrefab == null)
        {
            Debug.LogWarning("[VoxelTerrain] doorPrefab が未設定のため、扉が生成されません。");
            return;
        }

        int unitChunks = Mathf.Max(1, chunksPerDoor);
        int bandChunks = Mathf.Clamp(doorBandChunks, 1, unitChunks);

        int totalChunks = heightY / chunkSizeY;
        int unitCount = Mathf.CeilToInt((float)totalChunks / unitChunks);
        int placedCount = 0;

        for (int unit = 0; unit < unitCount; unit++)
        {
            // 地表から数えたチャンク範囲（0始まり・終端は含まない）。
            int unitStartChunk = unit * unitChunks;
            int unitEndChunk = Mathf.Min(unitStartChunk + unitChunks, totalChunks);
            if (unitEndChunk <= unitStartChunk) continue;

            // 単位の最深部 bandChunks チャンクが扉の出る帯。
            // 末尾の単位がチャンク数に満たない場合も、その単位の一番深いところに寄せる。
            int bandStartChunk = Mathf.Max(unitStartChunk, unitEndChunk - bandChunks);

            // y座標は上に行くほど大きいので、浅いチャンクの方が大きいyになる。
            int bandTopY = heightY - bandStartChunk * chunkSizeY;
            int bandBottomY = heightY - unitEndChunk * chunkSizeY;

            if (TrySpawnDoorInBand(unit, bandStartChunk, unitEndChunk, bandBottomY, bandTopY, rnd)) placedCount++;
        }

        Debug.Log($"[VoxelTerrain] 扉を {placedCount}/{unitCount} 個配置しました（全{totalChunks}チャンクを{unitChunks}チャンクずつに分割し、各単位の最深{bandChunks}チャンクに1つ）。");
    }

    /// <summary>
    /// 指定した深さの帯の中に扉を1つだけ配置する。
    /// </summary>
    /// <returns>配置できた場合はtrue。</returns>
    private bool TrySpawnDoorInBand(int unit, int bandStartChunk, int bandEndChunk, int bottomY, int topY, System.Random rnd)
    {
        string bandLabel = $"単位{unit}（地表から{bandStartChunk + 1}～{bandEndChunk}チャンク目 / Chunk_{(heightY - bandEndChunk * chunkSizeY) / chunkSizeY}～Chunk_{(heightY - bandStartChunk * chunkSizeY) / chunkSizeY - 1}）";

        List<Vector3Int> candidates = CollectDoorCandidates(bottomY, topY);
        if (candidates.Count == 0)
        {
            if (!allowDoorsInGoalZone && IsEntirelyGoalZone(bottomY, topY))
            {
                Debug.Log($"[VoxelTerrain] {bandLabel} はゴールゾーンのため扉を配置しませんでした。配置したい場合は allowDoorsInGoalZone をオンにしてください。");
            }
            else
            {
                Debug.LogWarning($"[VoxelTerrain] {bandLabel} に扉を置ける場所がありません。");
            }
            return false;
        }

        Vector3Int coord = candidates[rnd.Next(candidates.Count)];
        SpawnDoorAt(coord);

        int chunkFromSurface = (heightY - coord.y - 1) / chunkSizeY + 1;
        Debug.Log($"[VoxelTerrain] 扉を配置：単位{unit} / Chunk_{coord.y / chunkSizeY}（地表から{chunkFromSurface}チャンク目） (y={coord.y}, z={coord.z})");
        return true;
    }

    /// <summary>指定した深さの帯がすべてゴールゾーンに含まれるか。</summary>
    private bool IsEntirelyGoalZone(int bottomY, int topY)
    {
        int minY = Mathf.Max(0, bottomY);
        int maxY = Mathf.Min(heightY - 1, topY - 1);

        for (int y = minY; y <= maxY; y++)
        {
            if (!zoneSettings[GetRelayID(y)].isGoalZone) return false;
        }
        return true;
    }

    /// <summary>
    /// 扉を置ける座標を集める。掘れるブロックのうち、ステージ左右の端から余白を取った位置のみ。
    /// </summary>
    private List<Vector3Int> CollectDoorCandidates(int bottomY, int topY)
    {
        var positions = new List<Vector3Int>();

        int minY = Mathf.Max(0, bottomY);
        int maxY = Mathf.Min(heightY - 1, topY - 1);
        int centerZ = maxStageWidthZ / 2;

        for (int y = minY; y <= maxY; y++)
        {
            int zoneIndex = GetRelayID(y);
            if (!allowDoorsInGoalZone && zoneSettings[zoneIndex].isGoalZone) continue;

            // 端に寄りすぎると壁に食い込んで見えるため、左右から余白分だけ内側に寄せる。
            int halfWidth = zoneSettings[zoneIndex].widthZ / 2;
            int minZ = centerZ - halfWidth + doorEdgeMarginZ;
            int maxZ = centerZ + halfWidth - doorEdgeMarginZ;
            if (minZ > maxZ) continue;

            for (int x = 0; x < thicknessX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!IsInside(x, y, z)) continue;
                    if (!IsDiggable(mapData[x, y, z])) continue;

                    positions.Add(new Vector3Int(x, y, z));
                }
            }
        }

        return positions;
    }

    private void SpawnDoorAt(Vector3Int coord)
    {
        Vector3 pos = transform.position + new Vector3(
            coord.x * blockSize,
            coord.y * blockSize + (blockSize / 2f),
            coord.z * blockSize + (blockSize / 2f)
        );

        // 向きはプレハブに設定されたものをそのまま使う。
        // 宝箱と違い扉は正面を向かせたいので、ここで回転を足さない。
        GameObject door = Instantiate(doorPrefab, pos, doorPrefab.transform.rotation, transform);
        door.name = $"Door_Y{coord.y}_Z{coord.z}";
    }

    // --- 入場と復帰 -------------------------------------------------------

    /// <summary>
    /// 扉に入ったことを記録する。メインシーンに戻ってきたときの復帰位置になる。
    /// 画面の切り替えはUI側（SelectPoint）が行う。
    /// </summary>
    public void MarkDoorEntered(Vector3 doorWorldPosition)
    {
        doorReturnLocalPosition = transform.InverseTransformPoint(doorWorldPosition);
        HasPendingDoorReturn = true;

        Debug.Log($"[VoxelTerrain] 扉に入りました。復帰位置を記録: {doorReturnLocalPosition}");
    }

    /// <summary>
    /// メインシーンに戻ってきたとき、入った扉の位置までプレイヤーを戻す。
    /// 扉は壊れているので、同じ扉で再び画面移行することはない。
    /// </summary>
    private void RestorePlayerAtDoor()
    {
        if (!HasPendingDoorReturn) return;
        HasPendingDoorReturn = false;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Vector3 worldPos = transform.TransformPoint(doorReturnLocalPosition);

        // 復帰直後に地形へ埋まらないよう、先に周囲を掘り抜いておく。
        ClearBlocksAroundPoint(worldPos, DoorReturnClearRadius);
        player.transform.position = worldPos;

        Debug.Log($"[VoxelTerrain] 扉の位置へ復帰しました: {worldPos}");
    }
}
