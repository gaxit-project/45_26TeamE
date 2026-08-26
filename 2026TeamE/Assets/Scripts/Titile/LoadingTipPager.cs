using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ローディング画面の中央に説明画像を表示し、左右の入力でページをめくれるようにする。
/// </summary>
/// <remarks>
/// このコンポーネントはローディング画面の配下に置き、画面が表示されている間だけ動作する。
/// SceneLoader 側から操作する必要はなく、ローディング画面が非表示になれば自動的に止まる。
///
/// ローディング中は Time.timeScale が 0 になっているため、
/// 時間に依存しない入力判定と unscaled な時間のみで動作するようにしている。
/// </remarks>
public class LoadingTipPager : MonoBehaviour
{
    [Header("説明画像を表示するImage（ローディング画面の中央に配置する）")]
    [SerializeField] private Image tipImage;

    [Header("説明画像（表示したい順に登録）")]
    [SerializeField] private List<Sprite> pages = new List<Sprite>();

    [Header("画像の縦横比を保つか")]
    [SerializeField] private bool preserveAspect = true;

    [Header("ページ番号の表示（任意）")]
    [SerializeField] private TMP_Text pageIndicator;

    [Header("最後のページの次を最初に戻すか")]
    [SerializeField] private bool loopPages = true;

    [Header("ページ送りの矢印（任意）")]
    [Tooltip("説明画像の左に置く「<」の矢印")]
    [SerializeField] private GameObject previousArrow;
    [Tooltip("説明画像の右に置く「>」の矢印")]
    [SerializeField] private GameObject nextArrow;

    [Header("矢印を押した時の演出")]
    [Tooltip("一瞬だけ拡大する倍率。1にすると演出なし")]
    [SerializeField] private float arrowPunchScale = 1.2f;
    [Tooltip("元の大きさに戻るまでの時間（秒）")]
    [SerializeField] private float arrowPunchDuration = 0.15f;

    [Header("ページをめくった時のSE（空なら鳴らさない）")]
    [SerializeField] private string pageTurnSeName = "つるはしで掘る3";

    [Header("スティックを倒したと判定するしきい値")]
    [Range(0.1f, 1f)]
    [SerializeField] private float stickThreshold = 0.5f;

    private int currentPageIndex;

    // スティックを倒しっぱなしにした時に連続でめくられないようにするための状態
    private bool isStickNeutral = true;

    // 矢印の拡大演出用。元の大きさを覚えておき、そこからの倍率で動かす
    private Vector3 previousArrowBaseScale = Vector3.one;
    private Vector3 nextArrowBaseScale = Vector3.one;
    private float previousArrowPunchTimer;
    private float nextArrowPunchTimer;

    private void Awake()
    {
        if (previousArrow != null) previousArrowBaseScale = previousArrow.transform.localScale;
        if (nextArrow != null) nextArrowBaseScale = nextArrow.transform.localScale;

        WireArrowButtons();
    }

    private void OnEnable()
    {
        currentPageIndex = 0;
        isStickNeutral = true;
        previousArrowPunchTimer = 0f;
        nextArrowPunchTimer = 0f;

        ApplyCurrentPage();
    }

    private void Update()
    {
        UpdateArrowPunch();

        // ページが1枚以下ならめくる意味がない
        if (pages.Count <= 1) return;

        int direction = ReadPageDirection();
        if (direction != 0)
        {
            MovePage(direction);
        }
    }

    // --- 外部から呼べるページ送り（矢印のButtonから呼ばれる） ---------------

    /// <summary>前のページへ移動する。</summary>
    public void ShowPreviousPage()
    {
        MovePage(-1);
    }

    /// <summary>次のページへ移動する。</summary>
    public void ShowNextPage()
    {
        MovePage(1);
    }

    /// <summary>
    /// 矢印にButtonが付いていれば、クリックでもページを送れるように繋いでおく。
    /// </summary>
    private void WireArrowButtons()
    {
        if (previousArrow != null && previousArrow.TryGetComponent(out Button previousButton))
        {
            previousButton.onClick.RemoveListener(ShowPreviousPage);
            previousButton.onClick.AddListener(ShowPreviousPage);
        }

        if (nextArrow != null && nextArrow.TryGetComponent(out Button nextButton))
        {
            nextButton.onClick.RemoveListener(ShowNextPage);
            nextButton.onClick.AddListener(ShowNextPage);
        }
    }

    // --- 入力 -------------------------------------------------------------

    /// <summary>
    /// このフレームのページ送り入力を返す。-1で前のページ、1で次のページ、0で入力なし。
    /// </summary>
    private int ReadPageDirection()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame) return -1;
            if (keyboard.rightArrowKey.wasPressedThisFrame) return 1;
        }

        Gamepad pad = Gamepad.current;
        if (pad == null) return 0;

        if (pad.dpad.left.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame) return -1;
        if (pad.dpad.right.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame) return 1;

        // スティックは、一度ニュートラルに戻ってから再度倒された時だけ反応させる
        float horizontal = pad.leftStick.ReadValue().x;

        if (Mathf.Abs(horizontal) < stickThreshold)
        {
            isStickNeutral = true;
            return 0;
        }

        if (!isStickNeutral) return 0;

        isStickNeutral = false;
        return horizontal < 0f ? -1 : 1;
    }

    // --- ページ切り替え ---------------------------------------------------

    private void MovePage(int direction)
    {
        if (pages.Count <= 1) return;

        int nextIndex = currentPageIndex + direction;

        if (loopPages)
        {
            if (nextIndex < 0) nextIndex = pages.Count - 1;
            else if (nextIndex >= pages.Count) nextIndex = 0;
        }
        else
        {
            nextIndex = Mathf.Clamp(nextIndex, 0, pages.Count - 1);
        }

        // 端で止まっている場合は何もしない
        if (nextIndex == currentPageIndex) return;

        currentPageIndex = nextIndex;
        ApplyCurrentPage();

        // 押した側の矢印を一瞬拡大して、入力が通ったことを伝える
        if (direction < 0) previousArrowPunchTimer = arrowPunchDuration;
        else nextArrowPunchTimer = arrowPunchDuration;

        if (!string.IsNullOrEmpty(pageTurnSeName))
        {
            SoundManager.Instance?.PlaySE(pageTurnSeName);
        }
    }

    /// <summary>現在のページの画像を表示し、ページ番号と矢印の表示を更新する。</summary>
    private void ApplyCurrentPage()
    {
        if (tipImage != null)
        {
            bool hasPage = currentPageIndex >= 0
                        && currentPageIndex < pages.Count
                        && pages[currentPageIndex] != null;

            // 画像が無いページは、前のページが残らないようImageごと非表示にする
            tipImage.enabled = hasPage;

            if (hasPage)
            {
                tipImage.sprite = pages[currentPageIndex];
                tipImage.preserveAspect = preserveAspect;
            }
        }

        if (pageIndicator != null)
        {
            pageIndicator.text = pages.Count > 0
                ? $"{currentPageIndex + 1} / {pages.Count}"
                : string.Empty;
        }

        UpdateArrowVisibility();
    }

    /// <summary>
    /// その方向にめくれる時だけ矢印を表示する。
    /// ループ設定が有効なら常にめくれるため、矢印は出したままになる。
    /// </summary>
    private void UpdateArrowVisibility()
    {
        bool hasMultiplePages = pages.Count > 1;
        bool canGoPrevious = hasMultiplePages && (loopPages || currentPageIndex > 0);
        bool canGoNext = hasMultiplePages && (loopPages || currentPageIndex < pages.Count - 1);

        if (previousArrow != null) previousArrow.SetActive(canGoPrevious);
        if (nextArrow != null) nextArrow.SetActive(canGoNext);
    }

    // --- 矢印の拡大演出 ---------------------------------------------------

    private void UpdateArrowPunch()
    {
        previousArrowPunchTimer = AdvancePunch(previousArrow, previousArrowBaseScale, previousArrowPunchTimer);
        nextArrowPunchTimer = AdvancePunch(nextArrow, nextArrowBaseScale, nextArrowPunchTimer);
    }

    /// <summary>
    /// 矢印の拡大演出を1フレーム分進め、残り時間を返す。
    /// </summary>
    private float AdvancePunch(GameObject arrow, Vector3 baseScale, float remainingTime)
    {
        if (arrow == null || arrowPunchDuration <= 0f) return 0f;

        if (remainingTime <= 0f)
        {
            arrow.transform.localScale = baseScale;
            return 0f;
        }

        remainingTime = Mathf.Max(0f, remainingTime - Time.unscaledDeltaTime);

        // 残り時間が多いほど大きく、0に近づくほど元の大きさへ戻る
        float progress = remainingTime / arrowPunchDuration;
        arrow.transform.localScale = baseScale * Mathf.Lerp(1f, arrowPunchScale, progress);

        return remainingTime;
    }
}
