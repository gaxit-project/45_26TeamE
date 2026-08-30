using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;











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

    
    private bool isStickNeutral = true;

    
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

        
        if (pages.Count <= 1) return;

        int direction = ReadPageDirection();
        if (direction != 0)
        {
            MovePage(direction);
        }
    }

    

    
    public void ShowPreviousPage()
    {
        MovePage(-1);
    }

    
    public void ShowNextPage()
    {
        MovePage(1);
    }

    
    
    
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

        
        if (nextIndex == currentPageIndex) return;

        currentPageIndex = nextIndex;
        ApplyCurrentPage();

        
        if (direction < 0) previousArrowPunchTimer = arrowPunchDuration;
        else nextArrowPunchTimer = arrowPunchDuration;

        if (!string.IsNullOrEmpty(pageTurnSeName))
        {
            SoundManager.Instance?.PlaySE(pageTurnSeName);
        }
    }

    
    private void ApplyCurrentPage()
    {
        if (tipImage != null)
        {
            bool hasPage = currentPageIndex >= 0
                        && currentPageIndex < pages.Count
                        && pages[currentPageIndex] != null;

            
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

    
    
    
    
    private void UpdateArrowVisibility()
    {
        bool hasMultiplePages = pages.Count > 1;
        bool canGoPrevious = hasMultiplePages && (loopPages || currentPageIndex > 0);
        bool canGoNext = hasMultiplePages && (loopPages || currentPageIndex < pages.Count - 1);

        if (previousArrow != null) previousArrow.SetActive(canGoPrevious);
        if (nextArrow != null) nextArrow.SetActive(canGoNext);
    }

    

    private void UpdateArrowPunch()
    {
        previousArrowPunchTimer = AdvancePunch(previousArrow, previousArrowBaseScale, previousArrowPunchTimer);
        nextArrowPunchTimer = AdvancePunch(nextArrow, nextArrowBaseScale, nextArrowPunchTimer);
    }

    
    
    
    private float AdvancePunch(GameObject arrow, Vector3 baseScale, float remainingTime)
    {
        if (arrow == null || arrowPunchDuration <= 0f) return 0f;

        if (remainingTime <= 0f)
        {
            arrow.transform.localScale = baseScale;
            return 0f;
        }

        remainingTime = Mathf.Max(0f, remainingTime - Time.unscaledDeltaTime);

        
        float progress = remainingTime / arrowPunchDuration;
        arrow.transform.localScale = baseScale * Mathf.Lerp(1f, arrowPunchScale, progress);

        return remainingTime;
    }
}
