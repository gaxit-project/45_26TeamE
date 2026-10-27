#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 複数の人のプレイ（HumanRoute）から学んだ「人の流れ」と「操作の癖」。自動プレイが人らしく動くために使う。
///
/// ・人の流れ: 「この深さ・この左右の位置では、人はどの向きにスティックを倒しがちか」を数えたもの。
///   地形そのものではなく深さと左右の位置で分けているので、お手本の無い新しい地形でも使える。
/// ・操作の癖: ドリル・ジェットパックを押している割合、ソナーの頻度、同じ向きを続ける長さ、
///   レーダーのギザギザにどれだけ従うか、扉に入る割合。
///
/// 2.8.0 より前の記録（位置だけのファイル）は、操作の情報が無いので数えない。
/// </summary>
public sealed class HumanModel
{
    /// <summary>深さをこの幅ごとに区切って数える。</summary>
    private const float DepthBand = 15f;
    private const int MaxDepthBands = 40;
    /// <summary>ステージ中央（Z=0）からこれ以上離れていたら「左寄り／右寄り」とする。</summary>
    private const float SideThreshold = 10f;
    /// <summary>スティックをこれ以上倒していたら「動かしている」とする。</summary>
    private const float MoveDeadZone = 0.3f;
    /// <summary>これより少ない回数しか数えていない区分は当てにせず、ひとつ粗い区分を使う。</summary>
    private const int MinCountsPerBucket = 20;

    // 8方向（右から反時計回り）＋「動かさない」
    private const int Directions = 9;
    private const int Still = 8;

    public int Runs { get; private set; }
    public int SampleCount { get; private set; }
    public float SampledSeconds { get; private set; }

    public float DrillRate { get; private set; }
    public float JumpRateUp { get; private set; }
    public float JumpRateOther { get; private set; }
    public float SonarPerMinute { get; private set; }
    public float MeanSegmentSeconds { get; private set; }
    /// <summary>ギザギザが出ているときに、その向き（±45°以内）へスティックを倒していた割合。</summary>
    public float RadarFollowRate { get; private set; }
    /// <summary>1プレイで1回以上扉に入ったプレイの割合。</summary>
    public float DoorRunRate { get; private set; }

    // 区分ごとの向きの回数。キーは (深さの段 * 3 + 左右)、深さだけ、全体 の3段階
    private readonly Dictionary<int, int[]> flowByCell = new Dictionary<int, int[]>();
    private readonly Dictionary<int, int[]> flowByDepth = new Dictionary<int, int[]>();
    private readonly int[] flowAll = new int[Directions];

    /// <summary>操作の記録がある人のプレイから学ぶ。学べるデータが無ければ null。</summary>
    public static HumanModel Build(List<HumanRoute> routes, int minSamples)
    {
        var model = new HumanModel();
        int drill = 0, up = 0, jumpUp = 0, other = 0, jumpOther = 0;
        int radarSignal = 0, radarFollowed = 0, sonarUses = 0, doorRuns = 0;
        int segments = 0;
        float segmentSeconds = 0f;

        foreach (HumanRoute route in routes)
        {
            List<HumanSample> samples = route.Samples;
            if (samples.Count < 2 || !samples[0].HasInput) continue;

            model.Runs++;
            if (route.Doors > 0) doorRuns++;
            sonarUses += samples[samples.Count - 1].SonarCount - samples[0].SonarCount;

            int lastDir = -1;
            float lastDirStart = samples[0].Time;
            for (int i = 0; i < samples.Count; i++)
            {
                HumanSample s = samples[i];
                model.SampleCount++;
                if (i > 0) model.SampledSeconds += Mathf.Min(1f, s.Time - samples[i - 1].Time);

                int dir = Quantize(s.Move);
                Count(model.flowByCell, CellKey(s.Position), dir);
                Count(model.flowByDepth, DepthKey(s.Position), dir);
                model.flowAll[dir]++;

                if (s.Drill) drill++;
                if (s.Move.y > MoveDeadZone) { up++; if (s.Jump) jumpUp++; }
                else { other++; if (s.Jump) jumpOther++; }

                if (s.RadarSignal)
                {
                    radarSignal++;
                    if (dir != Still && Vector2.Dot(s.Move.normalized, s.RadarDirection) > 0.7f) radarFollowed++;
                }

                // 同じ向きを続けた長さ（向きを変えた所で区切る）
                if (dir != lastDir)
                {
                    if (lastDir >= 0)
                    {
                        segments++;
                        segmentSeconds += s.Time - lastDirStart;
                    }
                    lastDir = dir;
                    lastDirStart = s.Time;
                }
            }
        }

        if (model.SampleCount < minSamples) return null;

        model.DrillRate = Ratio(drill, model.SampleCount);
        model.JumpRateUp = Ratio(jumpUp, up);
        model.JumpRateOther = Ratio(jumpOther, other);
        model.SonarPerMinute = model.SampledSeconds > 0f ? sonarUses / model.SampledSeconds * 60f : 0f;
        model.MeanSegmentSeconds = segments > 0 ? segmentSeconds / segments : 1f;
        model.RadarFollowRate = Ratio(radarFollowed, radarSignal);
        model.DoorRunRate = Ratio(doorRuns, model.Runs);
        return model;
    }

    /// <summary>
    /// 今いる場所で人が倒しがちな向きを、数えた割合どおりに1つ選ぶ。
    /// 「動かさない」が選ばれたら Vector2.zero を返す。
    /// </summary>
    public Vector2 SampleDirection(Vector2 position, System.Random random)
    {
        int[] counts = Pick(flowByCell, CellKey(position)) ?? Pick(flowByDepth, DepthKey(position)) ?? flowAll;

        int total = 0;
        foreach (int n in counts) total += n;
        if (total == 0) return Vector2.zero;

        int r = random.Next(total);
        for (int d = 0; d < Directions; d++)
        {
            r -= counts[d];
            if (r < 0) return DirectionOf(d);
        }
        return Vector2.zero;
    }

    /// <summary>ログやダッシュボードで人とBotを比べるための1行。BotHabits と同じ項目名にしている。</summary>
    public string Summary()
    {
        var c = CultureInfo.InvariantCulture;
        return string.Format(c,
            "runs={0} samples={1} drill={2:F2} jumpUp={3:F2} jumpOther={4:F2} sonarPerMin={5:F1} segment={6:F2} radarFollow={7:F2} doorRuns={8:F2}",
            Runs, SampleCount, DrillRate, JumpRateUp, JumpRateOther, SonarPerMinute, MeanSegmentSeconds, RadarFollowRate, DoorRunRate);
    }

    private static int[] Pick(Dictionary<int, int[]> table, int key)
    {
        if (!table.TryGetValue(key, out int[] counts)) return null;
        int total = 0;
        foreach (int n in counts) total += n;
        return total >= MinCountsPerBucket ? counts : null;
    }

    private static void Count(Dictionary<int, int[]> table, int key, int dir)
    {
        if (!table.TryGetValue(key, out int[] counts)) table[key] = counts = new int[Directions];
        counts[dir]++;
    }

    private static int DepthKey(Vector2 position)
    {
        float depth = Mathf.Max(0f, -position.y);
        return Mathf.Min(MaxDepthBands - 1, Mathf.FloorToInt(depth / DepthBand));
    }

    private static int CellKey(Vector2 position)
    {
        int side = position.x < -SideThreshold ? 0 : position.x > SideThreshold ? 2 : 1;
        return DepthKey(position) * 3 + side;
    }

    /// <summary>スティックの入力を 8 方向＋「動かさない」に丸める。</summary>
    public static int Quantize(Vector2 move)
    {
        if (move.magnitude < MoveDeadZone) return Still;
        float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg;
        return Mathf.RoundToInt((angle + 360f) % 360f / 45f) % 8;
    }

    private static Vector2 DirectionOf(int dir)
    {
        if (dir == Still) return Vector2.zero;
        float rad = dir * 45f * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private static float Ratio(int a, int b) => b > 0 ? a / (float)b : 0f;
}
#endif
