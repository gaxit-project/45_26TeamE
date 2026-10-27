#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 自動プレイがステージのどこまで行けたかを記録するマス目。
/// 地形のブロックを cellBlocks × cellBlocks ごとに1マスにまとめ、プレイヤーが入ったマスに印を付ける。
/// マスの並びは地形と同じ（行 = Y、列 = Z）で、行0がいちばん深い。
/// </summary>
public sealed class AutoPlayCoverage
{
    public int Rows { get; }
    public int Cols { get; }
    public int CellBlocks { get; }
    public int HeightBlocks { get; }

    /// <summary>地形の原点（ブロック0番の位置）とブロックの大きさ。ダッシュボードでワールド座標に重ねるために残す。</summary>
    public float OriginY { get; }
    public float OriginZ { get; }
    public float BlockSize { get; }
    public int InsideCount { get; }
    public int VisitedCount { get; private set; }

    /// <summary>これまでに入ったマスのうち、いちばん深い行（行0が最深部）。まだどこにも入っていなければ -1。</summary>
    public int DeepestRow { get; private set; } = -1;

    private readonly bool[] inside;
    private readonly bool[] visited;
    private readonly bool[] abandoned;

    // 最寄りの未訪問マス探し（幅優先探索）で使い回す作業領域
    private readonly int[] searchQueue;
    private readonly int[] searchMark;
    private int searchStamp;

    private AutoPlayCoverage(int rows, int cols, int cellBlocks, int heightBlocks, float originY, float originZ, float blockSize, bool[] inside, int insideCount)
    {
        OriginY = originY;
        OriginZ = originZ;
        BlockSize = blockSize;
        Rows = rows;
        Cols = cols;
        CellBlocks = cellBlocks;
        HeightBlocks = heightBlocks;
        this.inside = inside;
        InsideCount = insideCount;
        visited = new bool[inside.Length];
        abandoned = new bool[inside.Length];
        searchQueue = new int[inside.Length];
        searchMark = new int[inside.Length];
    }

    /// <summary>
    /// 生成済みの地形からマス目を作る。マスの中心が各層の幅の内側にあれば「行けるマス」として数える。
    /// </summary>
    public static AutoPlayCoverage Create(VoxelTerrain terrain, int cellBlocks)
    {
        int rows = Mathf.CeilToInt(terrain.HeightY / (float)cellBlocks);
        int cols = Mathf.CeilToInt(terrain.StageWidthZ / (float)cellBlocks);
        var inside = new bool[rows * cols];
        int count = 0;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int y = Mathf.Min(r * cellBlocks + cellBlocks / 2, terrain.HeightY - 1);
                int z = Mathf.Min(c * cellBlocks + cellBlocks / 2, terrain.StageWidthZ - 1);
                if (terrain.IsInside(0, y, z))
                {
                    inside[r * cols + c] = true;
                    count++;
                }
            }
        }

        Vector3 origin = terrain.transform.position;
        return new AutoPlayCoverage(rows, cols, cellBlocks, terrain.HeightY, origin.y, origin.z, terrain.BlockSize, inside, count);
    }

    /// <summary>同じ形で、まだどこも訪れていないマス目を作る。</summary>
    public AutoPlayCoverage CloneEmpty() => new AutoPlayCoverage(Rows, Cols, CellBlocks, HeightBlocks, OriginY, OriginZ, BlockSize, inside, InsideCount);

    /// <summary>ダッシュボードが地図を読むための1行。</summary>
    public string GridLine() => string.Format(System.Globalization.CultureInfo.InvariantCulture,
        "grid rows={0} cols={1} cell={2} blockSize={3} originY={4} originZ={5} height={6}",
        Rows, Cols, CellBlocks, BlockSize, OriginY, OriginZ, HeightBlocks);

    /// <summary>行の下端が地表から何ブロック下か。</summary>
    private int DepthOfRow(int row) => Mathf.Max(0, HeightBlocks - row * CellBlocks);

    /// <summary>同じ地形から作ったマス目か（シード違いや大きさ違いを混ぜないための確認）。</summary>
    public bool SameShape(AutoPlayCoverage other) => other != null && other.Rows == Rows && other.Cols == Cols && other.CellBlocks == CellBlocks;

    public float Ratio => InsideCount == 0 ? 0f : VisitedCount / (float)InsideCount;

    /// <summary>ワールド座標がどのマスにあたるか。ステージの外なら false。</summary>
    public bool TryGetCell(VoxelTerrain terrain, Vector3 worldPos, out int row, out int col)
    {
        Vector3 local = terrain.transform.InverseTransformPoint(worldPos);
        int y = Mathf.RoundToInt(local.y / terrain.BlockSize);
        int z = Mathf.RoundToInt(local.z / terrain.BlockSize);
        row = Mathf.FloorToInt(y / (float)CellBlocks);
        col = Mathf.FloorToInt(z / (float)CellBlocks);
        return row >= 0 && row < Rows && col >= 0 && col < Cols;
    }

    public void Visit(int row, int col)
    {
        int i = row * Cols + col;
        if (visited[i]) return;

        visited[i] = true;
        if (inside[i]) VisitedCount++;
        if (DeepestRow < 0 || row < DeepestRow) DeepestRow = row;
    }

    /// <summary>何度向かっても届かなかったマス。以後は目標に選ばない。</summary>
    public void Abandon(int row, int col)
    {
        abandoned[row * Cols + col] = true;
    }

    /// <summary>
    /// 今いるマスから、まだ行っていない最寄りのマスを探す。
    /// 掘れば通れるので、地形の中身は見ずに「行けるマス」の上だけを上下左右に広げて探す。
    /// </summary>
    public bool TryFindNearestUnvisited(int fromRow, int fromCol, out int row, out int col)
    {
        row = col = -1;
        if (fromRow < 0 || fromRow >= Rows || fromCol < 0 || fromCol >= Cols) return false;

        searchStamp++;
        int head = 0, tail = 0;
        int start = fromRow * Cols + fromCol;
        searchQueue[tail++] = start;
        searchMark[start] = searchStamp;

        while (head < tail)
        {
            int i = searchQueue[head++];
            if (i != start && !visited[i] && !abandoned[i])
            {
                row = i / Cols;
                col = i % Cols;
                return true;
            }

            int r = i / Cols, c = i % Cols;
            TryEnqueue(r - 1, c, ref tail);
            TryEnqueue(r + 1, c, ref tail);
            TryEnqueue(r, c - 1, ref tail);
            TryEnqueue(r, c + 1, ref tail);
        }
        return false;
    }

    private void TryEnqueue(int r, int c, ref int tail)
    {
        if (r < 0 || r >= Rows || c < 0 || c >= Cols) return;
        int i = r * Cols + c;
        if (!inside[i] || searchMark[i] == searchStamp) return;
        searchMark[i] = searchStamp;
        searchQueue[tail++] = i;
    }

    /// <summary>別のプレイで訪れたマスをこちらにも足し込む（同じ形のマス目どうしのみ）。</summary>
    public void Merge(AutoPlayCoverage other)
    {
        if (!SameShape(other)) return;
        for (int i = 0; i < visited.Length; i++)
        {
            if (other.visited[i]) Visit(i / Cols, i % Cols);
        }
    }

    /// <summary>
    /// 訪れた範囲を文字で描いた地図。上が地表、下が深い側。左端の数字は地表からの深さ（ブロック）。
    ///   # = 訪れた  . = まだ  x = 諦めた  （空白）= ステージの外
    /// </summary>
    public string ToAsciiMap()
    {
        var sb = new StringBuilder((Cols + 8) * Rows);
        for (int r = Rows - 1; r >= 0; r--)
        {
            sb.Append(DepthOfRow(r).ToString().PadLeft(5)).Append(' ');
            for (int c = 0; c < Cols; c++)
            {
                int i = r * Cols + c;
                char ch = !inside[i] ? ' ' : visited[i] ? '#' : abandoned[i] ? 'x' : '.';
                sb.Append(ch);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public string Summary()
    {
        string deepest = DeepestRow < 0 ? "-" : DepthOfRow(DeepestRow).ToString();
        return $"visited={VisitedCount}/{InsideCount} ({Ratio * 100f:F1}%) cell={CellBlocks}blocks maxDepth={deepest}";
    }
}

/// <summary>セッション全体のカバー率を、地形のシードごとにまとめておく入れ物。</summary>
public sealed class AutoPlayCoverageBook
{
    private readonly Dictionary<int, AutoPlayCoverage> bySeed = new Dictionary<int, AutoPlayCoverage>();

    public IEnumerable<KeyValuePair<int, AutoPlayCoverage>> Entries => bySeed;

    public void Add(int seed, AutoPlayCoverage run)
    {
        if (!bySeed.TryGetValue(seed, out AutoPlayCoverage total) || !total.SameShape(run))
        {
            // 初めてのシード（または同じシードでも形が違う）なら、このプレイの分から数え始める
            bySeed[seed] = total = run.CloneEmpty();
        }
        total.Merge(run);
    }
}
#endif
