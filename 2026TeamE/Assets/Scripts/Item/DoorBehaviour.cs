using UnityEngine;

/// <summary>
/// 地中に埋まっている小さな扉。
/// 掘り進めて露出させた扉に近づき、決定ボタン（Yボタン）を押すとお金の両替・強化画面へ移行する。
/// 戻ってきたときはこの扉の位置から再開し、使い終わった扉は壊れて二度と入れない。
/// </summary>
/// <remarks>
/// 露出判定は <see cref="BuriedItemBase"/> に任せている。宝箱と同じ仕組みなので、
/// 周りを掘るまでは当たり判定が有効でも入場できない。
/// ボタン入力の受付は扉ごとではなく <see cref="SelectPoint"/> が一括で行い、
/// この扉は「プレイヤーが範囲内にいるか」を <see cref="VoxelTerrain"/> に登録するだけ。
/// この GameObject は <see cref="VoxelTerrain"/> の子（DontDestroyOnLoad）なので、
/// シーンを往復しても「壊れた」状態はフィールドとして保持される。
/// </remarks>
public class DoorBehaviour : BuriedItemBase
{
    [Header("露出判定の厳しさ")]
    [Tooltip("このサイズが大きいほど、より周りを広く掘らないと入場可能になりません")]
    [SerializeField] private Vector3 exposureSize = new Vector3(1, 3, 3);

    [Header("見た目の切り替え")]
    [Tooltip("未使用の扉の見た目。壊れたときに非表示にします。未設定なら自動で全描画物を対象にします。")]
    [SerializeField] private GameObject intactVisual;
    [Tooltip("壊れた扉の見た目。シーン上の子オブジェクトでも、Projectのプレハブでもどちらでも構いません。")]
    [SerializeField] private GameObject brokenVisual;
    [Tooltip("brokenVisual が未設定のときに、壊れた扉を暗く見せるための色")]
    [SerializeField] private Color brokenTint = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("埋まっている間の目印ライト")]
    [Tooltip("土の中にある扉を光らせて見つけやすくする（鍵入りの宝箱と同じ演出）")]
    [SerializeField] private bool useHintLight = true;
    [Tooltip("掘って露出したらライトを消す。オフにすると露出後も光り続けます。")]
    [SerializeField] private bool turnOffHintLightWhenExposed = true;
    [Tooltip("ライトのワールドX座標。地形は薄いので、手前に出さないと地面に隠れてしまいます。")]
    [SerializeField] private float hintLightWorldX = 3f;
    [SerializeField] private float hintLightRange = 30f;
    [SerializeField] private float hintLightIntensity = 15f;
    [SerializeField] private Color hintLightColor = new Color(1f, 0.9f, 0.3f);

    [Header("入場の案内表示")]
    [Tooltip("プレイヤーが入場範囲にいる間だけ表示するUI（「Yボタンで入る」など）。任意。")]
    [SerializeField] private GameObject enterPrompt;

    [Header("演出")]
    [Tooltip("扉に入った瞬間に鳴らすSE名。空なら鳴らしません。")]
    [SerializeField] private string enterSeName = "";

    [Header("判定")]
    [SerializeField] private string playerTag = "Player";

    /// <summary>この扉が既に使われて壊れているか。</summary>
    public bool IsBroken { get; private set; }

    /// <summary>入場可能か。掘って露出済みで、まだ壊れていない扉だけが入れる。</summary>
    public bool CanEnter => isExposed && !IsBroken;

    /// <summary>壊れた見た目をプレハブから作った場合の実体。二重生成を防ぐために保持する。</summary>
    private GameObject spawnedBrokenVisual;

    private bool isPlayerInRange;

    /// <summary>埋まっている間の目印ライト。</summary>
    private Light hintLight;

    protected override void Start()
    {
        base.Start();
        CreateHintLight();
        ApplyVisual();
        UpdatePrompt();
    }

    /// <summary>
    /// 土の中の扉を見つけやすくするための光源を作る。
    /// 地形はX方向に薄いため、扉と同じ位置に置くと埋もれてしまう。
    /// そのためXだけ手前の固定値にずらす（鍵入りの宝箱と同じ方式）。
    /// </summary>
    private void CreateHintLight()
    {
        if (!useHintLight || hintLight != null) return;

        var lightObject = new GameObject("DoorHintLight");
        lightObject.transform.SetParent(transform);

        Vector3 lightPosition = transform.position;
        lightPosition.x = hintLightWorldX;
        lightObject.transform.position = lightPosition;

        hintLight = lightObject.AddComponent<Light>();
        hintLight.type = LightType.Point;
        hintLight.range = hintLightRange;
        hintLight.intensity = hintLightIntensity;
        hintLight.color = hintLightColor;
    }

    /// <summary>掘って露出したときに呼ばれる。目印としての役目を終えるのでライトを消す。</summary>
    protected override void OnExposed()
    {
        base.OnExposed();

        if (turnOffHintLightWhenExposed && hintLight != null)
        {
            hintLight.enabled = false;
        }
    }

    protected override void Update()
    {
        base.Update();

        // 露出した瞬間に案内表示を出すため、範囲内にいる間は毎フレーム更新する。
        if (isPlayerInRange) UpdatePrompt();
    }

    /// <summary>扉はソナーに反応させないため、エコーもマーカーも持たない。</summary>
    protected override (GameObject echoPrefab, GameObject markerPrefab) GetReactionPrefabs()
    {
        return (null, null);
    }

    protected override bool IsExposedCheck()
    {
        if (VoxelTerrain.Instance == null) return false;
        return VoxelTerrain.Instance.IsJewelExposed(transform.position, exposureSize);
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            SetPlayerInRange(true);
            return;
        }

        // プレイヤー以外（ソナーなど）は基底クラスの既定処理に任せる。
        base.OnTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            SetPlayerInRange(false);
        }
    }

    private void OnDisable()
    {
        // シーン遷移で扉が非アクティブになったとき、入場対象として残らないようにする。
        SetPlayerInRange(false);
    }

    private void SetPlayerInRange(bool inRange)
    {
        isPlayerInRange = inRange;

        if (VoxelTerrain.Instance != null)
        {
            if (inRange) VoxelTerrain.Instance.SetDoorInRange(this);
            else VoxelTerrain.Instance.ClearDoorInRange(this);
        }

        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (enterPrompt == null) return;

        bool shouldShow = isPlayerInRange && CanEnter;
        if (enterPrompt.activeSelf != shouldShow) enterPrompt.SetActive(shouldShow);
    }

    /// <summary>
    /// 扉に入る。入場条件を満たしていない場合は何もしない。
    /// 画面の切り替えは呼び出し側（SelectPoint）が行う。
    /// </summary>
    /// <returns>入場できた場合はtrue。</returns>
    public bool TryEnter()
    {
        if (!CanEnter) return false;
        if (VoxelTerrain.Instance == null) return false;

        Break();

        if (!string.IsNullOrEmpty(enterSeName) && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySE(enterSeName);
        }

        VoxelTerrain.Instance.MarkDoorEntered(transform.position);
        return true;
    }

    /// <summary>扉を使用済みにする。以降は入場できない。</summary>
    private void Break()
    {
        IsBroken = true;

        // 使い終わった扉を光らせ続けても誘導にならないので必ず消す。
        if (hintLight != null) hintLight.enabled = false;

        ApplyVisual();
        UpdatePrompt();
    }

    private void ApplyVisual()
    {
        if (!IsBroken)
        {
            if (intactVisual != null) intactVisual.SetActive(true);
            if (brokenVisual != null && IsSceneObject(brokenVisual)) brokenVisual.SetActive(false);
            return;
        }

        if (brokenVisual == null)
        {
            // 壊れた見た目が用意されていない場合の代替表現。描画は消さずに暗くする。
            if (intactVisual != null) intactVisual.SetActive(true);
            foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                spriteRenderer.color = brokenTint;
            }
            return;
        }

        ShowBrokenVisual();
        HideIntactVisual();
    }

    /// <summary>未使用時の見た目を隠す。壊れた見た目は対象から外す。</summary>
    private void HideIntactVisual()
    {
        if (intactVisual != null)
        {
            intactVisual.SetActive(false);
            return;
        }

        // 未使用時の見た目が指定されていない場合は、扉自身の描画物をすべて消す。
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (spawnedBrokenVisual != null && renderer.transform.IsChildOf(spawnedBrokenVisual.transform)) continue;
            if (IsSceneObject(brokenVisual) && renderer.transform.IsChildOf(brokenVisual.transform)) continue;

            renderer.enabled = false;
        }
    }

    private void ShowBrokenVisual()
    {
        if (IsSceneObject(brokenVisual))
        {
            // 既に子オブジェクトとして置かれている場合は表示を切り替えるだけ。
            brokenVisual.SetActive(true);
            return;
        }

        // Projectのプレハブが指定されている場合は実体化しないと表示されない。
        if (spawnedBrokenVisual != null)
        {
            spawnedBrokenVisual.SetActive(true);
            return;
        }

        spawnedBrokenVisual = Instantiate(brokenVisual, transform.position, transform.rotation, transform);
        spawnedBrokenVisual.name = "BrokenVisual";
    }

    /// <summary>シーンに配置済みのオブジェクトか（falseならProjectのプレハブ）。</summary>
    private static bool IsSceneObject(GameObject target)
    {
        return target.scene.IsValid();
    }
}
