#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// デバッグ用の自動プレイ。仮想ゲームパッドを1台追加し、そこへランダムな入力を流してゲームを動かし続ける。
/// プレイヤーと同じ入力経路（PlayerInput / UI のイベント）を通るので、ゲーム側のコードは一切変更しない。
///
/// 起動方法（エディターと Development Build のみ）:
///   ・プレイ中に F9 キーで ON/OFF
///   ・ビルドを "-autoplay" 付きで起動すると最初から ON（"-autoplay-seed 1234" で乱数シードを指定）
///
/// 記録は GameLogger に書く。プレイヤーが一定時間ほぼ動かなければ「Stuck」としてスクリーンショット付きで残す。
///
/// 進む向きは次の優先順で決める。
///   1. まだ使っていない扉があれば、そこへ向かい、入れる範囲に入ったら Y ボタンを押す
///   2. 宝石レーダー（GemRadar）のギザギザが出ていれば、その向きへ掘り進む
///   3. まだ行っていないマスへ向かう
///   4. ランダムに動く（変な操作でしか出ないバグを拾うため、少しだけ残す）
/// 進む先が掘れないブロック（岩盤・外周の壁）なら、どの理由で向かっていても引き返す。
///
/// ステージを数ブロック四方のマスに区切って訪れたマスを記録し、まだ行っていないマスへ優先して向かう。
/// プレイごと（メインシーンを抜けたとき）に RunCoverage、停止・終了時にシードごとの SessionCoverage と
/// 文字で描いた地図（coverage_seed〇〇.txt）を残す。
/// </summary>
public sealed class AutoPlayBot : MonoBehaviour
{
    private const Key ToggleKey = Key.F9;

    [Header("入力の切り替え間隔（秒）")]
    [SerializeField] private float minSegmentSeconds = 0.4f;
    [SerializeField] private float maxSegmentSeconds = 2.0f;

    [Header("プレイ中の入力確率")]
    [SerializeField, Range(0f, 1f)] private float drillChance = 0.7f;
    [SerializeField, Range(0f, 1f)] private float jumpChance = 0.25f;
    [SerializeField, Range(0f, 1f)] private float sonarChance = 0.05f;

    [Header("メニュー・演出中の入力")]
    [SerializeField] private float menuTapIntervalMin = 0.5f;
    [SerializeField] private float menuTapIntervalMax = 1.5f;
    [SerializeField, Range(0f, 1f)] private float menuCancelChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float menuNavigateChance = 0.4f;

    [Header("扉")]
    [Tooltip("まだ使っていない扉のうち、この距離以内で最も近いものへ向かう")]
    [SerializeField] private float doorSearchDistance = 120f;
    [Tooltip("この秒数向かっても入れなかった扉は、そのプレイの間は諦める")]
    [SerializeField] private float doorGiveUpSeconds = 40f;

    [Header("宝石レーダー")]
    [Tooltip("ギザギザが出ているとき、その向きへ進む確率")]
    [SerializeField, Range(0f, 1f)] private float radarChance = 0.85f;
    [Tooltip("ギザギザの向きへ進むときにドリルを押す確率")]
    [SerializeField, Range(0f, 1f)] private float radarDrillChance = 0.95f;

    [Header("掘れないブロック")]
    [Tooltip("体の中心からこの距離先のブロックを見て、掘れなければ引き返す")]
    [SerializeField] private float wallProbeDistance = 1.2f;
    [Tooltip("引き返したあと、この秒数は元の向きへ戻らない")]
    [SerializeField] private float reverseSeconds = 2f;
    [Tooltip("レーダーの向きが壁の先だったとき、この秒数はレーダーに従わない（行ったり来たりを防ぐ）")]
    [SerializeField] private float radarCooldownAfterWall = 6f;

    [Header("未探索エリア優先")]
    [Tooltip("入力を選び直すとき、未訪問のマスへ向かう確率。残りはランダムに動く")]
    [SerializeField, Range(0f, 1f)] private float exploreChance = 0.75f;
    [Tooltip("未訪問のマスへ向かうときにドリルを押す確率")]
    [SerializeField, Range(0f, 1f)] private float exploreDrillChance = 0.9f;
    [Tooltip("1マスの大きさ（ブロック数）。小さいほど細かく記録するが、全部埋めるのに時間がかかる")]
    [SerializeField, Min(1)] private int coverageCellBlocks = 4;
    [Tooltip("この秒数向かってもたどり着けなかったマスは諦めて、別のマスを目指す")]
    [SerializeField] private float targetGiveUpSeconds = 20f;

    [Header("スタック検知")]
    [Tooltip("操作できる状態でこの秒数のあいだ、ほぼ同じ場所にいたらスタックとみなす")]
    [SerializeField] private float stuckSeconds = 15f;
    [Tooltip("この距離以上動いたら「動いた」とみなす")]
    [SerializeField] private float stuckMoveThreshold = 1.5f;
    [Tooltip("メインシーンでこの秒数以上操作できない状態が続いたら記録する（進行不能の検知）")]
    [SerializeField] private float noControlSeconds = 90f;

    // プレイ中に選ぶスティックの向き。掘り進めるゲームなので下方向を多めにしている
    private static readonly (Vector2 dir, float weight)[] MoveChoices =
    {
        (new Vector2( 1f,  0f), 2f),
        (new Vector2(-1f,  0f), 2f),
        (new Vector2( 0f, -1f), 3f),
        (new Vector2( 1f, -1f).normalized, 1f),
        (new Vector2(-1f, -1f).normalized, 1f),
        (new Vector2( 0f,  1f), 1f),
        (Vector2.zero, 0.5f),
    };

    public static AutoPlayBot Instance { get; private set; }

    /// <summary>自動プレイ中か。</summary>
    public bool IsRunning => pad != null;

    /// <summary>今回の自動プレイで詰まった回数。</summary>
    public int StuckCount => stuckCount;

    /// <summary>今のプレイのカバー率（0～1）。地形がまだ無ければ null。</summary>
    public float? CurrentCoverage => runCoverage?.Ratio;

    private Gamepad pad;
    private System.Random random;

    // 今の入力状態
    private Vector2 stick;
    private bool holdDrill;
    private bool holdJump;
    private bool tapSonar;
    private bool tapSubmit;
    private bool tapCancel;
    private bool tapDoor;
    private float doorReleaseTime;
    private float nextDoorPressTime;
    private float nextSegmentTime;
    private float tapReleaseTime;

    // スタック検知
    private Vector3 anchorPos;
    private float stillTime;
    private bool stuckReported;
    private float noControlTime;
    private bool noControlReported;
    private int stuckCount;
    private int screenshotCount;

    // 未探索エリア優先
    private VoxelTerrain trackedTerrain;
    private AutoPlayCoverage runCoverage;
    private int runSeed;
    private AutoPlayCoverageBook coverageBook;
    private bool hasCell;
    private int cellRow, cellCol;
    private bool hasTarget;
    private int targetRow, targetCol;
    private float targetSince;
    private int abandonedCount;

    // 扉と宝石レーダー
    private DoorBehaviour doorTarget;
    private float doorTargetSince;
    private readonly HashSet<DoorBehaviour> skippedDoors = new HashSet<DoorBehaviour>();
    private GemRadar radar;
    private int doorPresses;

    // どの理由で進む向きを決めたかの回数（停止時にログへ出す）
    private enum Steer { Door, Radar, Explore, Random, Reverse }
    private readonly int[] steerCounts = new int[5];
    private Steer currentSteer = Steer.Random;

    // 掘れないブロックから引き返す
    private float reverseUntil;
    private float radarSuppressedUntil;
    private float nextBlockedLogTime;
    private int blockedCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // 常駐させて F9 を見張る。自動プレイしていない間は何もしない
        var go = new GameObject("[AutoPlayBot]");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        var bot = go.AddComponent<AutoPlayBot>();

        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "-autoplay") >= 0)
        {
            bot.Begin(ReadSeedArg(args) ?? Environment.TickCount);
        }
    }

    private static int? ReadSeedArg(string[] args)
    {
        int i = Array.IndexOf(args, "-autoplay-seed");
        if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return value;
        }
        return null;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        End();
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 自動プレイを始める。同じ seed なら同じ順番で入力を選ぶ
    /// （ただしフレームの長さで切り替わるタイミングがずれるため、プレイ内容が完全に同じになるわけではない）。
    /// </summary>
    public void Begin(int seed)
    {
        if (IsRunning) return;

        random = new System.Random(seed);
        // 普通の Gamepad はウィンドウのフォーカスが外れると Input System にリセット・無効化されるため、
        // 「バックグラウンドでも動く」設定の Gamepad として登録する（DebugBackgroundRunner と組み合わせて使う）
        InputSystem.RegisterLayout(@"{ ""name"": ""AutoPlayGamepad"", ""extend"": ""Gamepad"", ""runInBackground"": ""enabled"" }");
        pad = (Gamepad)InputSystem.AddDevice("AutoPlayGamepad", "AutoPlayPad");
        ResetInput();
        ResetWatch();
        stuckCount = 0;
        abandonedCount = 0;
        doorPresses = 0;
        blockedCount = 0;
        Array.Clear(steerCounts, 0, steerCounts.Length);
        coverageBook = new AutoPlayCoverageBook();
        trackedTerrain = null;
        runCoverage = null;
        hasCell = hasTarget = false;

        Application.wantsToQuit += BlockQuit;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameLogger.SessionEnding += End;

        GameLogger.Event("AutoPlayStart", $"seed={seed}");
        Debug.Log($"[AutoPlayBot] 自動プレイを開始しました（seed={seed}）。{ToggleKey} キーで停止します。");
    }

    /// <summary>自動プレイを止め、仮想ゲームパッドを取り外す。</summary>
    public void End()
    {
        if (!IsRunning) return;

        Application.wantsToQuit -= BlockQuit;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameLogger.SessionEnding -= End;

        InputSystem.RemoveDevice(pad);
        pad = null;

        FinishRun();
        ReportSessionCoverage();
        GameLogger.Event("AutoPlayStop",
            $"stuck={stuckCount} abandonedTargets={abandonedCount} doorPresses={doorPresses} blocked={blockedCount} " +
            $"steer=door:{steerCounts[(int)Steer.Door]},radar:{steerCounts[(int)Steer.Radar]},explore:{steerCounts[(int)Steer.Explore]},random:{steerCounts[(int)Steer.Random]},reverse:{steerCounts[(int)Steer.Reverse]}");
        Debug.Log("[AutoPlayBot] 自動プレイを停止しました。");
    }

    /// <summary>自動プレイ中にタイトルの「ゲーム終了」が押されても、ビルドが終了しないようにする。</summary>
    private static bool BlockQuit()
    {
        GameLogger.Event("QuitBlocked");
        return false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetInput();
        ResetWatch();
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[ToggleKey].wasPressedThisFrame)
        {
            if (IsRunning) End();
            else Begin(Environment.TickCount);
        }

        if (!IsRunning) return;

        PlayerController player = PlayerController.Instance;
        bool inControl = player != null && player.IsControlEnabled;

        TrackCoverage(player);

        if (inControl) UpdateGameplayInput(player);
        else UpdateMenuInput();

        WatchStuck(player, inControl);
        SendState();
    }

    /// <summary>プレイ中: 一定間隔ごとに「移動方向・ドリル・ジャンプ」の組み合わせを選び直す。</summary>
    private void UpdateGameplayInput(PlayerController player)
    {
        tapSubmit = false;
        tapCancel = false;

        UpdateDoorButton();

        if (Time.unscaledTime < nextSegmentTime)
        {
            // ソナーは押しっぱなしにせず、1フレームだけ押して離す
            tapSonar = false;
        }
        else
        {
            nextSegmentTime = Time.unscaledTime + Range(minSegmentSeconds, maxSegmentSeconds);
            tapSonar = Chance(sonarChance);
            ChooseSteering(player);
            steerCounts[(int)currentSteer]++;
        }

        // 向きを選び直すタイミングに関係なく、毎フレーム進む先を確かめる
        AvoidUndiggable(player);
    }

    /// <summary>進む向きを、扉 → レーダー → 未探索のマス → ランダム の順で決める。</summary>
    private void ChooseSteering(PlayerController player)
    {
        if (TrySteerToDoor(player))
        {
            currentSteer = Steer.Door;
            return;
        }

        if (Time.unscaledTime >= radarSuppressedUntil && TrySteerByRadar())
        {
            currentSteer = Steer.Radar;
            return;
        }

        if (Chance(exploreChance) && PickExploreTarget())
        {
            // スティックの左右がワールドのZ、上下がY。マス目も同じ向きなので差をそのまま使える
            int dRow = targetRow - cellRow;
            int dCol = targetCol - cellCol;
            stick = new Vector2(dCol, dRow).normalized;
            holdDrill = Chance(exploreDrillChance);
            // 上へはドリルだけでは登れないので、ジェットパックを吹かす
            holdJump = dRow > 0;
            currentSteer = Steer.Explore;
            return;
        }

        currentSteer = Steer.Random;
        stick = PickMove();
        holdDrill = Chance(drillChance);
        holdJump = Chance(jumpChance);
    }

    /// <summary>
    /// タイトル・ロード画面・ショップ・リザルト・演出中: 決定ボタンを中心にときどき押す。
    /// ポーズ（Start）は押さない。ポーズメニューの「ゲーム終了」はエディターのプレイを止めてしまうため。
    /// </summary>
    private void UpdateMenuInput()
    {
        holdDrill = false;
        holdJump = false;
        tapSonar = false;
        tapDoor = false;

        if (Time.unscaledTime >= tapReleaseTime)
        {
            // 押したボタンは 0.1 秒で離す。離している時間がないと次の押下が検出されない
            tapSubmit = false;
            tapCancel = false;
            stick = Vector2.zero;
        }

        if (Time.unscaledTime < nextSegmentTime) return;

        nextSegmentTime = Time.unscaledTime + Range(menuTapIntervalMin, menuTapIntervalMax);
        tapReleaseTime = Time.unscaledTime + 0.1f;

        if (Chance(menuNavigateChance))
        {
            stick = PickMenuDirection();
        }
        else if (Chance(menuCancelChance))
        {
            tapCancel = true;
        }
        else
        {
            tapSubmit = true;
        }
    }

    private void SendState()
    {
        var state = new GamepadState { leftStick = stick };
        // Player マップ: West=ドリル(Attack), South=ジャンプ, RightShoulder=ソナー
        // UI マップ:     South=決定(Submit), East=キャンセル(Cancel)
        if (holdDrill) state = state.WithButton(GamepadButton.West);
        if (holdJump || tapSubmit) state = state.WithButton(GamepadButton.South);
        if (tapCancel) state = state.WithButton(GamepadButton.East);
        if (tapSonar) state = state.WithButton(GamepadButton.RightShoulder);
        // 扉に入る: Y（North）。SelectPoint が <Gamepad>/buttonNorth で受け付けている
        if (tapDoor) state = state.WithButton(GamepadButton.North);

        InputSystem.QueueStateEvent(pad, state);
    }

    private void ResetInput()
    {
        stick = Vector2.zero;
        holdDrill = holdJump = tapSonar = tapSubmit = tapCancel = tapDoor = false;
        nextSegmentTime = 0f;
        tapReleaseTime = 0f;
    }

    // ーーーーーーーーーーーーーーーーースタック検知ーーーーーーーーーーーーーー

    private void ResetWatch()
    {
        stillTime = 0f;
        stuckReported = false;
        noControlTime = 0f;
        noControlReported = false;
        if (PlayerController.Instance != null) anchorPos = PlayerController.Instance.transform.position;
    }

    private void WatchStuck(PlayerController player, bool inControl)
    {
        if (player == null) return;

        WatchNoControl(player, inControl);

        // ポーズ・ショップ・演出中は「動けない」のが正常なので数えない
        if (!inControl) return;

        Vector3 pos = player.transform.position;
        if ((pos - anchorPos).sqrMagnitude >= stuckMoveThreshold * stuckMoveThreshold)
        {
            if (stuckReported)
            {
                GameLogger.Event("StuckResolved", $"after={stillTime:F1}s");
            }
            anchorPos = pos;
            stillTime = 0f;
            stuckReported = false;
            return;
        }

        stillTime += Time.unscaledDeltaTime;
        if (!stuckReported && stillTime >= stuckSeconds)
        {
            stuckReported = true;
            stuckCount++;
            AbandonTarget();
            SkipDoorTarget("stuck");
            string shot = TakeScreenshot("stuck");
            GameLogger.Event("Stuck", $"#{stuckCount} still={stillTime:F1}s anchor={anchorPos} battery={player.currentBattery:F0} screenshot={shot}");
        }
    }

    /// <summary>メインシーンで長時間操作できないまま＝導入演出やショップから戻れないなどの進行不能を拾う。</summary>
    private void WatchNoControl(PlayerController player, bool inControl)
    {
        if (inControl)
        {
            if (noControlReported)
            {
                GameLogger.Event("NoControlResolved", $"after={noControlTime:F1}s");
            }
            noControlTime = 0f;
            noControlReported = false;
            return;
        }

        // 時間切れやクリア後の演出中は除く
        PlayerController.PlayerState state = player.currentState;
        if (state == PlayerController.PlayerState.GameOver || state == PlayerController.PlayerState.GameClear) return;

        noControlTime += Time.unscaledDeltaTime;
        if (!noControlReported && noControlTime >= noControlSeconds)
        {
            noControlReported = true;
            string shot = TakeScreenshot("nocontrol");
            GameLogger.Event("NoControl", $"for={noControlTime:F1}s timeScale={Time.timeScale} screenshot={shot}");
        }
    }

    // ーーーーーーーーーーーーーーーーー掘れないブロックーーーーーーーーーーーーーー

    /// <summary>
    /// 進む先が掘れないブロックなら引き返す。逆側も塞がっていれば（角や狭い隙間）横へ逃げる。
    /// 引き返している間（reverseSeconds）は向きを選び直さないので、すぐに同じ壁へ戻ることはない。
    /// </summary>
    private void AvoidUndiggable(PlayerController player)
    {
        if (Time.unscaledTime < reverseUntil || stick.sqrMagnitude < 0.01f) return;

        VoxelTerrain terrain = VoxelTerrain.Instance;
        if (terrain == null || !terrain.IsStageGenerated) return;

        Vector3 pos = player.transform.position;
        if (!IsBlocked(terrain, pos, stick, out VoxelTerrain.BlockType wall)) return;

        Vector2 escape = -stick;
        if (IsBlocked(terrain, pos, escape, out _))
        {
            Vector2 side = new Vector2(-stick.y, stick.x);
            if (Chance(0.5f)) side = -side;
            escape = IsBlocked(terrain, pos, side, out _) ? -side : side;
        }

        // 掘れない壁の先にある目標は、そのまま追いかけても届かないので諦める
        switch (currentSteer)
        {
            case Steer.Door: SkipDoorTarget("blocked"); break;
            case Steer.Explore: AbandonTarget(); break;
            case Steer.Radar: radarSuppressedUntil = Time.unscaledTime + radarCooldownAfterWall; break;
        }

        Vector2 from = stick;
        stick = escape.normalized;
        holdDrill = true;
        holdJump = stick.y > 0.3f;
        reverseUntil = Time.unscaledTime + reverseSeconds;
        nextSegmentTime = reverseUntil;
        currentSteer = Steer.Reverse;
        steerCounts[(int)Steer.Reverse]++;
        blockedCount++;

        // 壁に沿って何度も当たることがあるので、ログは間引く
        if (Time.unscaledTime >= nextBlockedLogTime)
        {
            nextBlockedLogTime = Time.unscaledTime + 2f;
            GameLogger.Event("BotBlocked", string.Format(CultureInfo.InvariantCulture,
                "block={0} dir=({1:F1},{2:F1}) escape=({3:F1},{4:F1})", wall, from.x, from.y, stick.x, stick.y));
        }
    }

    /// <summary>体の上下2か所から、向いている先のブロックを調べる。</summary>
    private bool IsBlocked(VoxelTerrain terrain, Vector3 feet, Vector2 dir, out VoxelTerrain.BlockType wall)
    {
        var step = new Vector3(0f, dir.y, dir.x).normalized * wallProbeDistance;
        foreach (float height in ProbeHeights)
        {
            VoxelTerrain.BlockType? block = terrain.GetBlockAt(feet + Vector3.up * height + step);
            if (VoxelTerrain.IsUndiggable(block))
            {
                wall = block.Value;
                return true;
            }
        }
        wall = VoxelTerrain.BlockType.Air;
        return false;
    }

    // プレイヤーの原点は足元にあるので、すねと胸のあたりを見る
    private static readonly float[] ProbeHeights = { 0.5f, 1.4f };

    // ーーーーーーーーーーーーーーーーー扉ーーーーーーーーーーーーーー

    /// <summary>
    /// 入れる扉の範囲にいれば Y を押す。押したら 0.1 秒で離し、1 秒は押し直さない
    /// （押しっぱなしだと「押した瞬間」が1回しか検出されないため）。
    /// </summary>
    private void UpdateDoorButton()
    {
        if (tapDoor && Time.unscaledTime >= doorReleaseTime) tapDoor = false;
        if (tapDoor || Time.unscaledTime < nextDoorPressTime) return;

        VoxelTerrain terrain = VoxelTerrain.Instance;
        DoorBehaviour door = terrain != null ? terrain.CurrentDoorInRange : null;
        if (door == null) return;

        tapDoor = true;
        doorReleaseTime = Time.unscaledTime + 0.1f;
        nextDoorPressTime = Time.unscaledTime + 1f;
        doorPresses++;
        GameLogger.Event("BotPressY", $"door={door.transform.position}");
    }

    /// <summary>
    /// まだ使っていない扉があれば、そこへ向かう。埋まっている扉は周りを掘らないと入れないので、ドリルを押しながら近づく。
    /// </summary>
    private bool TrySteerToDoor(PlayerController player)
    {
        if (doorTarget != null && (doorTarget.IsBroken || !doorTarget.isActiveAndEnabled))
        {
            doorTarget = null;
        }

        if (doorTarget != null && Time.unscaledTime - doorTargetSince > doorGiveUpSeconds)
        {
            SkipDoorTarget("timeout");
        }

        if (doorTarget == null)
        {
            doorTarget = FindNearestDoor(player.transform.position);
            if (doorTarget == null) return false;

            doorTargetSince = Time.unscaledTime;
            float dist = Vector3.Distance(Flat(doorTarget.transform.position), Flat(player.transform.position));
            GameLogger.Event("BotDoorTarget", $"door={doorTarget.transform.position} dist={dist:F1} exposed={doorTarget.CanEnter}");
        }

        // 入れる範囲に入っているなら、その場に留まって Y を押すのを待つ
        VoxelTerrain terrain = VoxelTerrain.Instance;
        if (terrain != null && terrain.CurrentDoorInRange == doorTarget)
        {
            stick = Vector2.zero;
            holdDrill = false;
            holdJump = false;
            return true;
        }

        Vector3 delta = doorTarget.transform.position - player.transform.position;
        var dir = new Vector2(delta.z, delta.y);
        stick = dir.sqrMagnitude > 0.01f ? dir.normalized : PickMove();
        holdDrill = true;
        holdJump = dir.y > 0.5f;
        return true;
    }

    private DoorBehaviour FindNearestDoor(Vector3 from)
    {
        DoorBehaviour best = null;
        float bestSqr = doorSearchDistance * doorSearchDistance;
        foreach (DoorBehaviour door in FindObjectsByType<DoorBehaviour>())
        {
            if (door.IsBroken || skippedDoors.Contains(door)) continue;
            float sqr = (Flat(door.transform.position) - Flat(from)).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = door;
            }
        }
        return best;
    }

    private void SkipDoorTarget(string reason)
    {
        if (doorTarget == null) return;
        GameLogger.Event("BotDoorGiveUp", $"door={doorTarget.transform.position} reason={reason}");
        skippedDoors.Add(doorTarget);
        doorTarget = null;
    }

    /// <summary>横から見るゲームなので、奥行き（X）を無視した位置。</summary>
    private static Vector3 Flat(Vector3 p) => new Vector3(0f, p.y, p.z);

    // ーーーーーーーーーーーーーーーーー宝石レーダーーーーーーーーーーーーーーー

    /// <summary>
    /// レーダーのギザギザが出ている向きへ進む。プレイヤーが画面で見ているのと同じ情報だけを使う
    /// （宝石の正確な位置は使わない。爆弾にも反応するのも、精度による誤差があるのも同じ）。
    /// </summary>
    private bool TrySteerByRadar()
    {
        if (radar == null || !radar.isActiveAndEnabled)
        {
            radar = FindAnyObjectByType<GemRadar>();
            if (radar == null) return false;
        }

        if (!radar.HasSignal || !Chance(radarChance)) return false;

        stick = radar.SignalDirection;
        holdDrill = Chance(radarDrillChance);
        holdJump = stick.y > 0.3f;
        return true;
    }

    // ーーーーーーーーーーーーーーーーー未探索エリア優先ーーーーーーーーーーーーーー

    /// <summary>今いるマスに印を付ける。地形が作り直されたら、そこまでのプレイ分を締めて数え直す。</summary>
    private void TrackCoverage(PlayerController player)
    {
        VoxelTerrain terrain = VoxelTerrain.Instance;

        // シーンを抜けて地形が消えた、または別の地形に変わった
        if (runCoverage != null && (trackedTerrain == null || !ReferenceEquals(terrain, trackedTerrain)))
        {
            FinishRun();
        }

        hasCell = false;
        if (terrain == null || !terrain.IsStageGenerated || terrain.IsGenerating) return;

        if (runCoverage == null)
        {
            trackedTerrain = terrain;
            runSeed = VoxelTerrain.LastUsedSeed;
            runCoverage = AutoPlayCoverage.Create(terrain, coverageCellBlocks);
            GameLogger.Event("CoverageStart", $"seed={runSeed} cells={runCoverage.InsideCount} grid={runCoverage.Rows}x{runCoverage.Cols}");
        }

        if (player == null) return;
        if (!runCoverage.TryGetCell(terrain, player.transform.position, out cellRow, out cellCol)) return;

        hasCell = true;
        runCoverage.Visit(cellRow, cellCol);
    }

    /// <summary>
    /// 向かう先のマスを決める。前に決めたマスがまだ未訪問で、諦める時間にもなっていなければそのまま続ける。
    /// </summary>
    private bool PickExploreTarget()
    {
        if (runCoverage == null || !hasCell) return false;

        if (hasTarget && !(targetRow == cellRow && targetCol == cellCol))
        {
            if (Time.unscaledTime - targetSince < targetGiveUpSeconds) return true;
            AbandonTarget();
        }

        hasTarget = runCoverage.TryFindNearestUnvisited(cellRow, cellCol, out targetRow, out targetCol);
        targetSince = Time.unscaledTime;
        return hasTarget;
    }

    private void AbandonTarget()
    {
        if (!hasTarget || runCoverage == null) return;
        runCoverage.Abandon(targetRow, targetCol);
        abandonedCount++;
        hasTarget = false;
    }

    /// <summary>1回分のプレイ（1つの地形）の記録を締めて、セッション全体の集計に足す。</summary>
    private void FinishRun()
    {
        if (runCoverage == null) return;

        GameLogger.Event("RunCoverage", $"seed={runSeed} {runCoverage.Summary()}");
        coverageBook.Add(runSeed, runCoverage);

        runCoverage = null;
        trackedTerrain = null;
        hasCell = hasTarget = false;
        doorTarget = null;
        skippedDoors.Clear();
        radar = null;
    }

    /// <summary>セッション全体のカバー率をシードごとに記録し、文字の地図をファイルに書く。</summary>
    private void ReportSessionCoverage()
    {
        foreach (var entry in coverageBook.Entries)
        {
            string fileName = WriteCoverageMap(entry.Key, entry.Value);
            GameLogger.Event("SessionCoverage", $"seed={entry.Key} {entry.Value.Summary()} map={fileName}");
        }
    }

    private static string WriteCoverageMap(int seed, AutoPlayCoverage coverage)
    {
        if (string.IsNullOrEmpty(GameLogger.SessionFolder)) return "-";

        string fileName = $"coverage_seed{seed}.txt";
        try
        {
            File.WriteAllText(
                Path.Combine(GameLogger.SessionFolder, fileName),
                $"seed={seed} {coverage.Summary()}\n{coverage.GridLine()}\n# =訪れた  . = まだ  x = 諦めた\n\n{coverage.ToAsciiMap()}");
            return fileName;
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[AutoPlayBot] カバー率の地図を書けませんでした。{e.Message}");
            return "-";
        }
    }

    private string TakeScreenshot(string label)
    {
        if (string.IsNullOrEmpty(GameLogger.SessionFolder)) return "-";

        screenshotCount++;
        string fileName = $"{label}_{screenshotCount:000}.png";
        ScreenCapture.CaptureScreenshot(Path.Combine(GameLogger.SessionFolder, fileName));
        return fileName;
    }

    // ーーーーーーーーーーーーーーーーー乱数ーーーーーーーーーーーーーー

    private bool Chance(float p) => random.NextDouble() < p;

    private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

    private Vector2 PickMove()
    {
        float total = 0f;
        foreach (var choice in MoveChoices) total += choice.weight;

        float r = Range(0f, total);
        foreach (var choice in MoveChoices)
        {
            r -= choice.weight;
            if (r <= 0f) return choice.dir;
        }
        return Vector2.zero;
    }

    private Vector2 PickMenuDirection()
    {
        switch (random.Next(4))
        {
            case 0: return Vector2.up;
            case 1: return Vector2.down;
            case 2: return Vector2.left;
            default: return Vector2.right;
        }
    }
}
#endif
