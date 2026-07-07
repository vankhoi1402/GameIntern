using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Thực thể block — logic state + hiển thị/animation. Không điều phối gameplay.
/// </summary>
public class BlockManager : MonoBehaviour
{
    #region Constants

    private const float c_PickupScaleMultiplier = 1.05f;
    private const float c_PickupRotateZ = -8f;
    private const float c_PickupDuration = 0.15f;
    private const float c_ReleaseDuration = 0.18f;
    private const float c_AbsorbDuration = 0.18f;
    private const float c_AbsorbRotateZ = 20f;
    private const float c_DragReturnDuration = 0.2f;
    private const float c_RejectShakeDuration = 0.28f;
    private const float c_RejectShakeRotation = 10f;
    private const float c_RejectShakePosition = 0.06f;
    private const float c_HoverTargetScale = 0.95f;
    private const float c_HoverTargetDuration = 0.12f;
    private const float c_HoverFlashDuration = 0.06f;
    private const float c_HoverHoldDuration = 0.1f;
    private const float c_HoverFlashBlend = 0.45f;
    private const float c_HoverHoldBlend = 0.25f;
    private const float c_HoverBgFlashBlend = 0.2f;
    private const float c_HoverBgHoldBlend = 0.1f;
    private const int c_Tier2PairOverlayMinStage = 2;
    private const int c_BurstFallVisualStack = -1;
    private const float c_SelectedRotateDuration = 2f;
    private const float c_HintPulseHalfDuration = 1f;

    public const int SortingOrderPerRow = 10;
    public const int DragSortingOrder = 1000;

    private const int MaxSortingOrder = 7;

    private bool m_IsDragging;
    public const int HoverMergeTargetSortingOrder = DragSortingOrder - 50; // 1050 — trên block đang kéo (1000)
    public const int MagnetFlySortingOrder = 1200;

    private int m_SavedHoverSortingOrder;
    private bool m_HasHoverSortingBoost;
    private int m_SavedMagnetFlySortingOrder;
    private bool m_HasMagnetFlySortingBoost;

    #endregion

    #region Logic State

    public BlockData Data { get; private set; }
    public string TypeKey => Data != null ? Data.TypeKey : string.Empty;
    public int TypeId => Data != null ? Data.BlockId : 0;
    public Slot CurrentSlot { get; private set; }
    public int StackCount { get; private set; } = 1;
    public int Tier2MergeStage { get; private set; }
    public bool IsPendingDestroy { get; set; }

    /// <summary>Hai block cùng loại khi BlockId trùng nhau.</summary>
    public bool IsSameTypeAs(BlockManager other)
    {
        if (Data == null || other == null || other.Data == null)
            return false;
        return Data.BlockId == other.Data.BlockId;
    }

    /// <summary>Khởi tạo block với BlockData, stack mặc định 1.</summary>
    public void Init(BlockData data) => Init(data, 1);

    /// <summary>Khởi tạo block với BlockData và stack ban đầu (1 hoặc 2).</summary>
    public void Init(BlockData data, int stack)
    {
        Data = data;
        StackCount = Mathf.Clamp(stack, 1, 2);
        Tier2MergeStage = StackCount == 2 ? 1 : 0;
        IsPendingDestroy = false;
    }

    /// <summary>Áp state sau merge resolve.</summary>
    public void ApplyMergeState(int stackCount, int tier2MergeStage)
    {
        if (IsPendingDestroy)
            return;
        StackCount = stackCount;
        Tier2MergeStage = Mathf.Max(0, tier2MergeStage);
    }

    /// <summary>Đánh dấu block "2" sau 1+1 hoặc khi spawn stack 2.</summary>
    public void SetTier2MergeStage(int stage)
    {
        if (IsPendingDestroy)
            return;
        Tier2MergeStage = Mathf.Max(0, stage);
    }

    /// <summary>Gán ô lưới — chỉ BoardManager/Slot gọi.</summary>
    internal void SetCurrentSlot(Slot slot)
    {
        if (CurrentSlot == slot)
            return;
        CurrentSlot = slot;
    }

    #endregion

    #region Visual References

    [Header("Renderers")]
    public SpriteRenderer m_MainRenderer;
    public SpriteRenderer m_BgRenderer;
    [SerializeField] private SpriteRenderer m_SelectedRenderer;

    [Header("Stack Background Colors")]
    [SerializeField] private Color m_Stack1BgColor = Color.white;
    [SerializeField] private Color m_Stack2BgColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField] private Color m_Stack2PairBgColor = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private Color m_BurstFallBgColor = Color.white;

    private SortingGroup m_SortingGroup;
    private Vector3 m_BaseScale;
    private float m_BaseRotationZ;
    private Color m_BaseMainColor = Color.white;
    private Color m_BaseBgColor = Color.white;
    private Tween m_HoverTween;
    private Tween m_HoverColorTween;
    private Tween m_HintTween;
    private Tween m_SelectedRotateTween;
    [SerializeField]private Color s_HintBgColor = new Color(1f, 0.92f, 0.35f);
    private float m_BaseSelectedRotationZ;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheTransformDefaults();
        m_SortingGroup = GetComponent<SortingGroup>();
        ResolveSelectedRenderer();
        if (m_SelectedRenderer != null)
            m_BaseSelectedRotationZ = m_SelectedRenderer.transform.localEulerAngles.z;
    }

    private void OnDestroy()
    {
        KillHoverTweens();
        ClearHintHighlight();
        StopSelectedOverlayRotate();
    }

    #endregion

    #region Setup & Refresh

    /// <summary>Thiết lập sprite, màu và nền ban đầu.</summary>
    public void Initialize(BlockData data, float cellWidth, float cellHeight)
    {
        if (m_MainRenderer != null)
        {
            m_MainRenderer.sprite = data.VisualSprite;
            m_MainRenderer.color = data.DebugColor;
            m_BaseMainColor = data.DebugColor;
        }

        ResetSpriteAlpha();
        ResetTransformVisual();
        UpdateTier2StageVisual(1, 0);
        CacheBaseBgColor();
    }

    /// <summary>Khởi tạo block burst T3 — icon + nền merge.</summary>
    public void InitializeBurstFallOff(BlockData data, float cellWidth, float cellHeight)
    {
        if (m_MainRenderer != null)
        {
            m_MainRenderer.sprite = data.VisualSprite;
            m_MainRenderer.color = data.DebugColor;
            m_BaseMainColor = data.DebugColor;
        }

        ResetSpriteAlpha();
        ResetTransformVisual();
        UpdateTier2StageVisual(c_BurstFallVisualStack, 2);
        CacheBaseBgColor();
    }

    /// <summary>Reset visual theo stack/slot hiện tại.</summary>
    public void RefreshVisual()
    {
        ResetVisualState();
        UpdateTier2StageVisual(StackCount, Tier2MergeStage);
        if (CurrentSlot != null)
            ApplyGridSorting(CurrentSlot.Row);
    }

    /// <summary>Đặt block ngay tại world pos (không tween).</summary>
    public void MoveToPosition(Vector3 targetWorldPos)
    {
        transform.position = targetWorldPos;
    }

    #endregion

    #region Stack Visual

    /// <summary>Đổi nền stack + overlay Selected khi 2+2 (stage ≥ 2).</summary>
    public void UpdateTier2StageVisual(int stackCount, int tier2MergeStage)
    {
        UpdateStackVisual(stackCount, tier2MergeStage);
        ApplyTier2PairOverlay(stackCount, tier2MergeStage);
    }

    /// <summary>Đổi màu nền theo stack + stage — giữ nguyên sprite trên m_BgRenderer.</summary>
    public void UpdateStackVisual(int stackCount, int tier2MergeStage)
    {
        if (m_BgRenderer == null)
            return;

        KillHoverColorTween();

        Color bgColor = ResolveStackBgColor(stackCount, tier2MergeStage);
        m_BgRenderer.color = bgColor;
        m_BaseBgColor = bgColor;
    }

    private Color ResolveStackBgColor(int stackCount, int tier2MergeStage)
    {
        if (stackCount == c_BurstFallVisualStack)
            return m_BurstFallBgColor;

        if (stackCount == 2 && tier2MergeStage >= c_Tier2PairOverlayMinStage)
            return m_Stack2PairBgColor;

        if (stackCount == 2)
            return m_Stack2BgColor;

        return m_Stack1BgColor;
    }

    private void ApplyTier2PairOverlay(int stackCount, int tier2MergeStage)
    {
        ResolveSelectedRenderer();
        if (m_SelectedRenderer == null)
            return;

        bool showOverlay = stackCount == 2 && tier2MergeStage >= c_Tier2PairOverlayMinStage;
        if (!showOverlay)
        {
            StopSelectedOverlayRotate();
            m_SelectedRenderer.gameObject.SetActive(false);
            return;
        }

        PlaySelectedOverlayRotate();
    }

    /// <summary>Bật Selected và quay liên tục quanh trục Z.</summary>
    public void PlaySelectedOverlayRotate()
    {
        ResolveSelectedRenderer();
        if (m_SelectedRenderer == null)
            return;

        StopSelectedOverlayRotate();
        m_SelectedRenderer.gameObject.SetActive(true);

        Transform selectedTransform = m_SelectedRenderer.transform;
        selectedTransform.localRotation = Quaternion.Euler(0f, 0f, m_BaseSelectedRotationZ);

        m_SelectedRotateTween = selectedTransform
            .DOLocalRotate(new Vector3(0f, 0f, 360f), c_SelectedRotateDuration, RotateMode.LocalAxisAdd)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetLink(m_SelectedRenderer.gameObject);
    }

    /// <summary>Dừng quay Selected và reset góc Z.</summary>
    public void StopSelectedOverlayRotate()
    {
        m_SelectedRotateTween?.Kill();
        m_SelectedRotateTween = null;

        if (m_SelectedRenderer == null)
            return;

        m_SelectedRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, m_BaseSelectedRotationZ);
    }

    private void ResolveSelectedRenderer()
    {
        if (m_SelectedRenderer != null)
            return;

        Transform selected = transform.Find("Selected");
        if (selected != null)
            m_SelectedRenderer = selected.GetComponent<SpriteRenderer>();
    }

    #endregion

    #region Sorting

    /// <summary>SortingGroup theo row — row cao vẽ trên.</summary>
    public void ApplyGridSorting(int row)
    {
        if (m_SortingGroup == null)
            return;
        m_SortingGroup.sortingOrder = (MaxSortingOrder - row) * SortingOrderPerRow;
    }

    /// <summary>Khi kéo: order cao; khi thả: trả theo ô hiện tại.</summary>
    public void SetDragSorting(bool isDragging)
    {
        if (m_SortingGroup == null)
            return;
        m_IsDragging = isDragging;
        if (isDragging)
        {
            m_SortingGroup.sortingOrder = DragSortingOrder;
            transform.SetAsLastSibling();
            return;
        }

        if (CurrentSlot != null)
            ApplyGridSorting(CurrentSlot.Row);
    }
    /// <summary>Set sorting order hover merge target</summary>
    /// <summary>Tăng sorting order khi block là merge target (hover scale).</summary>
    public void SetHoverMergeTargetSorting()
    {
        if (m_SortingGroup == null || m_IsDragging)
            return;

        if (!m_HasHoverSortingBoost)
        {
            m_SavedHoverSortingOrder = m_SortingGroup.sortingOrder;
            m_HasHoverSortingBoost = true;
        }

        m_SortingGroup.sortingOrder = HoverMergeTargetSortingOrder;
        transform.SetAsLastSibling();
    }

    /// <summary>Trả sorting order về theo hàng sau khi hết hover.</summary>
    public void ClearHoverMergeTargetSorting()
    {
        if (m_SortingGroup == null || !m_HasHoverSortingBoost)
            return;

        m_HasHoverSortingBoost = false;

        if (CurrentSlot != null)
            ApplyGridSorting(CurrentSlot.Row);
        else
            m_SortingGroup.sortingOrder = m_SavedHoverSortingOrder;
    }

    /// <summary>Magnet fly — vẽ block trên board/VFX trong lúc bay về giữa.</summary>
    public void BeginMagnetFlyVisual()
    {
        if (m_SortingGroup == null)
            return;

        if (!m_HasMagnetFlySortingBoost)
        {
            m_SavedMagnetFlySortingOrder = m_SortingGroup.sortingOrder;
            m_HasMagnetFlySortingBoost = true;
        }

        m_SortingGroup.sortingOrder = MagnetFlySortingOrder;
        transform.SetAsLastSibling();
    }

    /// <summary>Trả sorting order sau magnet fly (nếu block còn trên board).</summary>
    public void EndMagnetFlyVisual()
    {
        if (m_SortingGroup == null || !m_HasMagnetFlySortingBoost)
            return;

        m_HasMagnetFlySortingBoost = false;

        if (CurrentSlot != null)
            ApplyGridSorting(CurrentSlot.Row);
        else
            m_SortingGroup.sortingOrder = m_SavedMagnetFlySortingOrder;
    }
    #endregion

    #region Drag

    /// <summary>Bắt đầu kéo — sorting + pickup feedback.</summary>
    public void BeginDrag()
    {
        PlayPickupFeedback();
        SetDragSorting(true);
    }

    /// <summary>Cập nhật vị trí khi đang kéo.</summary>
    public void FollowDrag(Vector3 worldPos) => FollowDragPosition(worldPos);

    /// <summary>Cập nhật vị trí world khi drag (không tween).</summary>
    public void FollowDragPosition(Vector3 worldPos)
    {
        transform.position = worldPos;
    }

    /// <summary>Kết thúc drag — trả về ô (có thể lắc nếu reject).</summary>
    public void ReturnToSlot(Vector3 worldPos, int row, bool shakeReject, Action onComplete = null)
        => EndDragReturnTo(worldPos, row, shakeReject, onComplete);

    /// <summary>Thả không hợp lệ — lắc rồi tween về ô gốc.</summary>
    public void EndDragReturnTo(Vector3 worldPos, int row, bool shakeReject, Action onComplete = null)
    {
        // if (shakeReject)
        // {
        //     PlayRejectShake(() => PlayReturnToGridTween(worldPos, row, onComplete));
        //     return;
        // }

        PlayReturnToGridTween(worldPos, row, onComplete);
    }

    private void PlayReturnToGridTween(Vector3 worldPos, int row, Action onComplete)
    {
        PlayReleaseFeedback();
        transform.DOMove(worldPos, c_DragReturnDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                SetDragSorting(false);
                ApplyGridSorting(row);
                onComplete?.Invoke();
            });
    }

    private void PlayPickupFeedback()
    {
        KillPickupTweens();
        transform.DOScale(m_BaseScale * c_PickupScaleMultiplier, c_PickupDuration).SetEase(Ease.OutBack);
        transform.DORotate(new Vector3(0f, 0f, m_BaseRotationZ + c_PickupRotateZ), c_PickupDuration)
            .SetEase(Ease.OutQuad);
    }

    private void PlayReleaseFeedback()
    {
        KillPickupTweens();
        transform.DOScale(m_BaseScale, c_ReleaseDuration).SetEase(Ease.OutQuad);
        transform.DORotate(new Vector3(0f, 0f, m_BaseRotationZ), c_ReleaseDuration).SetEase(Ease.OutQuad);
    }

    // private void PlayRejectShake(Action onComplete = null)
    // {
    //     KillPickupTweens();

    //     Sequence seq = DOTween.Sequence();
    //     // seq.Append(transform.DOPunchRotation(
    //     //     new Vector3(0f, 0f, c_RejectShakeRotation),
    //     //     c_RejectShakeDuration,
    //     //     14,
    //     //     0.55f));
    //     seq.Join(transform.DOPunchPosition(
    //         new Vector3(c_RejectShakePosition, 0f, 0f),
    //         c_RejectShakeDuration,
    //         12,
    //         0.45f).SetRelative(true));
    //     seq.OnComplete(() => onComplete?.Invoke());
    // }
    private void PlayRejectShake(Action onComplete = null)
    {
        KillPickupTweens();

        transform.DOLocalMove(transform.localPosition + new Vector3(c_RejectShakePosition, 0f, 0f), c_RejectShakeDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() => onComplete?.Invoke());
    }
    #endregion

    #region Hover

    /// <summary>Hover merge hợp lệ — scale + flash.</summary>
    public void PlayHover() => PlayHoverTargetFeedback();

    /// <summary>Phóng to nhẹ + lóe sáng khi hover merge target.</summary>
    public void PlayHoverTargetFeedback()
    {
        KillHoverTweens();
        m_HoverTween = transform
            .DOScale(m_BaseScale * c_HoverTargetScale, c_HoverTargetDuration)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject);

        SetHoverMergeTargetSorting();
        PlayHoverFlash();
    }

    /// <summary>Xóa hover feedback.</summary>
    public void ClearHover() => ClearHoverTargetFeedback();

    /// <summary>Trả scale/màu target về bình thường.</summary>
    public void ClearHoverTargetFeedback()
    {
        m_HoverTween?.Kill();
        m_HoverTween = transform
            .DOScale(m_BaseScale, c_HoverTargetDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);

        ClearHoverFlash();
        ClearHoverMergeTargetSorting();
    }

    private void PlayHoverFlash()
    {
        KillHoverColorTween();
        if (m_MainRenderer == null)
            return;

        Color flashMain = Color.Lerp(m_BaseMainColor, Color.white, c_HoverFlashBlend);
        Color holdMain = Color.Lerp(m_BaseMainColor, Color.white, c_HoverHoldBlend);
        Color flashBg = Color.Lerp(m_BaseBgColor, Color.white, c_HoverBgFlashBlend);
        Color holdBg = Color.Lerp(m_BaseBgColor, Color.white, c_HoverBgHoldBlend);

        Sequence seq = DOTween.Sequence();
        seq.Append(m_MainRenderer.DOColor(flashMain, c_HoverFlashDuration).SetEase(Ease.OutQuad));
        if (m_BgRenderer != null)
            seq.Join(m_BgRenderer.DOColor(flashBg, c_HoverFlashDuration).SetEase(Ease.OutQuad));

        seq.Append(m_MainRenderer.DOColor(holdMain, c_HoverHoldDuration).SetEase(Ease.OutQuad));
        if (m_BgRenderer != null)
            seq.Join(m_BgRenderer.DOColor(holdBg, c_HoverHoldDuration).SetEase(Ease.OutQuad));

        m_HoverColorTween = seq.SetLink(gameObject);
    }

    private void ClearHoverFlash()
    {
        KillHoverColorTween();

        if (m_MainRenderer != null)
            m_MainRenderer.color = m_BaseMainColor;

        if (m_BgRenderer != null)
            m_BgRenderer.color = m_BaseBgColor;
    }

    #endregion

    #region Booster Visual

    /// <summary>Pulse màu nền + icon trong duration giây — dùng cho Hint booster.</summary>
    public void PlayHintHighlight(float duration)
    {
        ClearHintHighlight();
        KillHoverTweens();

        if (m_MainRenderer == null)
            return;

        Color pulseMain = Color.Lerp(m_BaseMainColor, Color.white, 0.4f);
        Color pulseBg = Color.Lerp(m_BaseBgColor, s_HintBgColor, 0.7f);
        int loops = Mathf.Max(2, Mathf.CeilToInt(duration / (c_HintPulseHalfDuration * 2f)));

        Sequence seq = DOTween.Sequence();
        seq.Append(m_MainRenderer.DOColor(pulseMain, c_HintPulseHalfDuration).SetEase(Ease.InOutSine));
        if (m_BgRenderer != null)
            seq.Join(m_BgRenderer.DOColor(pulseBg, c_HintPulseHalfDuration).SetEase(Ease.InOutSine));

        seq.Append(m_MainRenderer.DOColor(m_BaseMainColor, c_HintPulseHalfDuration).SetEase(Ease.InOutSine));
        if (m_BgRenderer != null)
            seq.Join(m_BgRenderer.DOColor(m_BaseBgColor, c_HintPulseHalfDuration).SetEase(Ease.InOutSine));

        seq.SetLoops(loops, LoopType.Restart);
        m_HintTween = seq
            .SetLink(gameObject)
            .OnComplete(ClearHintHighlight);
    }

    /// <summary>Dừng hint pulse và trả màu về mặc định.</summary>
    public void ClearHintHighlight()
    {
        m_HintTween?.Kill();
        m_HintTween = null;

        if (m_MainRenderer != null)
            m_MainRenderer.color = m_BaseMainColor;

        if (m_BgRenderer != null)
            m_BgRenderer.color = m_BaseBgColor;
    }

    #endregion

    #region Merge Visual

    /// <summary>Source hút vào target khi merge.</summary>
    public void PlayAbsorbInto(Vector3 targetWorldPos, Action onComplete)
    {
        transform.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMove(targetWorldPos, c_AbsorbDuration).SetEase(Ease.InQuad));
        seq.Join(transform.DOScale(Vector3.zero, c_AbsorbDuration).SetEase(Ease.InBack));
        seq.Join(transform.DORotate(new Vector3(0f, 0f, m_BaseRotationZ + c_AbsorbRotateZ), c_AbsorbDuration));
        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Impact punch trên block đích sau merge.</summary>
    public void PlayMergeImpact(Action onComplete = null)
    {
        transform.DOPunchScale(Vector3.one * 0.15f, 0.22f, 8, 0.4f)
            .SetLink(gameObject)
            .OnComplete(() => onComplete?.Invoke())
            .OnKill(() => onComplete?.Invoke());
    }

    /// <summary>2+2+2 wind-up trước khi nổ.</summary>
    public void PlayTier2ExplosionWindUp(Action onComplete = null)
    {
        const float windUpDuration = 0.18f;
        transform.DOKill();
        KillPickupTweens();
        ResolveSelectedRenderer();

        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        seq.Append(transform.DOScale(m_BaseScale * 1.22f, windUpDuration * 0.55f).SetEase(Ease.OutBack));
        seq.Join(transform.DOPunchScale(Vector3.one * 0.28f, windUpDuration, 10, 0.35f));

        if (m_SelectedRenderer != null)
        {
            m_SelectedRenderer.gameObject.SetActive(true);
            Color flash = m_SelectedRenderer.color;
            flash.a = 1f;
            m_SelectedRenderer.color = flash;
            seq.Join(m_SelectedRenderer.DOFade(0.25f, windUpDuration).SetLoops(2, LoopType.Yoyo));
        }

        if (m_MainRenderer != null)
            seq.Join(m_MainRenderer.DOColor(Color.white, windUpDuration * 0.35f).SetLoops(2, LoopType.Yoyo));

        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Tween tới ô trống khi move hợp lệ.</summary>
    public void PlayMoveToSlot(Vector3 targetWorldPos, Action onComplete = null)
    {
        KillPickupTweens();
        PlayReleaseFeedback();
        transform.DOMove(targetWorldPos, c_ReleaseDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => onComplete?.Invoke());
    }

    #endregion

    #region Remove Visual

    /// <summary>Block rơi khỏi màn hình và mờ dần.</summary>
    public void PlayRemove(Camera cam, float fallWorldDistance, float duration, Action onComplete)
        => PlayFallOffScreen(cam, fallWorldDistance, duration, onComplete);

    /// <summary>Alias PlayRemove.</summary>
    public void PlayFallOffScreen(Camera cam, float fallWorldDistance, float duration, Action onComplete)
    {
        transform.SetParent(null);
        transform.DOKill();

        if (m_SortingGroup != null)
            m_SortingGroup.sortingOrder = DragSortingOrder + 100;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer renderer in renderers)
        {
            Color c = renderer.color;
            c.a = 1f;
            renderer.color = c;
        }

        if (cam == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (cam.orthographic)
            fallWorldDistance = Mathf.Max(fallWorldDistance, cam.orthographicSize * 2f);

        Vector3 targetWorld = transform.position + Vector3.down * fallWorldDistance;

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMove(targetWorld, duration).SetEase(Ease.InQuad));
        foreach (SpriteRenderer renderer in renderers)
            seq.Join(renderer.DOFade(0f, duration * 0.85f));

        seq.OnComplete(() => onComplete?.Invoke());
    }

    #endregion

    #region Spawn Visual

    /// <summary>Spawn rơi xuống ô đích.</summary>
    public void PlaySpawn(Vector3 targetWorldPos, float duration, Action onComplete = null)
        => PlaySpawnFall(targetWorldPos, duration, onComplete);

    /// <summary>Block mới tween rơi xuống ô.</summary>
    public void PlaySpawnFall(Vector3 targetWorldPos, float duration, Action onComplete = null)
    {
        transform.DOKill();
        transform.DOMove(targetWorldPos, duration)
            .SetEase(Ease.OutBounce)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Refill: tween từ dưới ô đích lên row 0.</summary>
    public void PlayRiseFromBelow(Vector3 targetWorldPos, float cellHeight, float duration, Action onComplete = null)
        => PlayRiseFromBelow(targetWorldPos, cellHeight, duration, 0, 0f, onComplete);

    /// <summary>Rise refill với stagger và sorting.</summary>
    public void PlayRiseFromBelow(
        Vector3 targetWorldPos,
        float cellHeight,
        float duration,
        int row,
        float delay,
        Action onComplete = null)
    {
        Vector3 spawnPos = targetWorldPos + Vector3.down * cellHeight * 1.2f;
        PlayRiseFromWorldPosition(spawnPos, targetWorldPos, duration, row, delay, onComplete);
    }

    /// <summary>Rise từ world pos tùy ý.</summary>
    public void PlayRiseFromWorldPosition(
        Vector3 spawnWorldPos,
        Vector3 targetWorldPos,
        float duration,
        int row,
        float delay,
        Action onComplete = null)
    {
        transform.DOKill();
        ResetTransformVisual();
        ResetSpriteAlpha();
        transform.position = spawnWorldPos;

        Tween move = transform.DOMove(targetWorldPos, duration)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject);
        if (delay > 0f)
            move.SetDelay(delay);

        move.OnComplete(() =>
        {
            ResetTransformVisual();
            ApplyGridSorting(row);
            onComplete?.Invoke();
        });
    }

    /// <summary>Intro load level — rise + fade + scale.</summary>
    public void PlayIntroRise(
        Vector3 spawnWorldPos,
        Vector3 targetWorldPos,
        float duration,
        float delay,
        int row,
        float startScale,
        float fadeInDuration,
        Action onComplete = null)
    {
        transform.DOKill();
        ResetTransformVisual();
        SetSpritesAlpha(0f);
        transform.localScale = m_BaseScale * startScale;
        transform.position = spawnWorldPos;

        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        if (delay > 0f)
            seq.AppendInterval(delay);

        seq.Append(transform.DOMove(targetWorldPos, duration).SetEase(Ease.OutSine));
        seq.Join(transform.DOScale(m_BaseScale, duration).SetEase(Ease.OutSine));

        float fadeDuration = Mathf.Min(fadeInDuration, duration);
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
            seq.Join(renderer.DOFade(1f, fadeDuration).SetEase(Ease.OutSine));

        seq.OnComplete(() =>
        {
            ResetTransformVisual();
            ResetSpriteAlpha();
            ApplyGridSorting(row);
            onComplete?.Invoke();
        });
    }

    /// <summary>Ẩn trước intro rise — tránh flash tại ô đích.</summary>
    public void PrepareForIntroRise()
    {
        transform.DOKill();
        ResetTransformVisual();
        SetSpritesAlpha(0f);
    }

    #endregion

    #region Gravity Visual

    /// <summary>Một lệnh gravity — gọi từ orchestrator board.</summary>
    public void PlayGravity(
        Vector3 fromWorldPos,
        Vector3 targetWorldPos,
        float duration,
        float delay,
        int toRow,
        Ease ease,
        bool preserveCurrentPosition,
        Action onComplete)
    {
        if (!preserveCurrentPosition)
        {
            ResetVisualState();
            transform.position = fromWorldPos;
        }

        Tween move = transform
            .DOMove(targetWorldPos, duration)
            .SetEase(ease)
            .SetLink(gameObject);

        if (delay > 0f)
            move.SetDelay(delay);

        move.OnComplete(() =>
        {
            SnapToGridCell(targetWorldPos, toRow);
            onComplete?.Invoke();
        }).OnKill(() =>
        {
            SnapToGridCell(targetWorldPos, toRow);
            onComplete?.Invoke();
        });
    }

    /// <summary>Refill push — dịch block lên một ô trong cột.</summary>
    public void PlayRefillPush(
        Vector3 fromWorldPos,
        Vector3 toWorldPos,
        float duration,
        int toRow,
        Ease ease,
        Action onComplete)
    {
        ResetVisualState();
        transform.position = fromWorldPos;

        transform.DOMove(toWorldPos, duration)
            .SetEase(ease)
            .OnComplete(() =>
            {
                SnapToGridCell(toWorldPos, toRow);
                onComplete?.Invoke();
            })
            .OnKill(() =>
            {
                SnapToGridCell(toWorldPos, toRow);
                onComplete?.Invoke();
            });
    }

    /// <summary>Anticipation trước gravity.</summary>
    public void PlayGravityLiftAnticipation(float liftWorldDistance, float duration, Action onComplete = null)
    {
        transform.DOKill();
        transform.DOMove(transform.position + Vector3.up * liftWorldDistance, duration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Snap về ô lưới (không tween).</summary>
    public void SnapToGridCell(Vector3 worldPos, int row)
    {
        if (m_IsDragging)
            return;
        ResetVisualState();
        transform.position = worldPos;
        ApplyGridSorting(row);
    }

    #endregion

    #region Burst & Tier2 Visual

    /// <summary>Nảy nhẹ trước burst T3.</summary>
    public void PlayBurstPop()
    {
        KillPickupTweens();
        transform.DOPunchScale(Vector3.one * 0.2f, 0.12f, 6, 0.5f);
    }

    /// <summary>T3: tóe ra rồi rơi khỏi màn hình.</summary>
    public void PlayBurstExplodeThenFall(
        Vector3 explodeOffset,
        Camera cam,
        float explodeDuration,
        float fallDuration,
        float fallWorldDistance,
        float startDelay,
        Action onExplodeComplete,
        Action onFallComplete)
    {
        transform.SetParent(null);
        transform.DOKill();

        if (m_SortingGroup != null)
            m_SortingGroup.sortingOrder = DragSortingOrder + 100;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer renderer in renderers)
        {
            Color c = renderer.color;
            c.a = 1f;
            renderer.color = c;
        }

        transform.localScale = m_BaseScale * 0.15f;

        if (cam != null && cam.orthographic)
            fallWorldDistance = Mathf.Max(fallWorldDistance, cam.orthographicSize * 2f);

        Vector3 explodeTarget = transform.position + explodeOffset;
        Vector3 fallTarget = explodeTarget + Vector3.down * fallWorldDistance;
        float rotateZ = m_BaseRotationZ + Mathf.Sign(explodeOffset.x) * 14f;

        Sequence seq = DOTween.Sequence();
        if (startDelay > 0f)
            seq.AppendInterval(startDelay);

        seq.Append(transform.DOScale(m_BaseScale, explodeDuration).SetEase(Ease.OutBack));
        seq.Join(transform.DOMove(explodeTarget, explodeDuration).SetEase(Ease.OutCubic));
        seq.Join(transform.DORotate(new Vector3(0f, 0f, rotateZ), explodeDuration).SetEase(Ease.OutQuad));
        seq.AppendCallback(() => onExplodeComplete?.Invoke());

        seq.Append(transform.DOMove(fallTarget, fallDuration).SetEase(Ease.InCubic));
        foreach (SpriteRenderer renderer in renderers)
            seq.Join(renderer.DOFade(0f, fallDuration * 0.9f));

        seq.OnComplete(() => onFallComplete?.Invoke());
    }

    /// <summary>2+2+2 neighbor blast.</summary>
    public void PlayTier2NeighborBlast(
        Vector3 homeWorldPos,
        Vector3 blastWorldPos,
        float blastDuration,
        float dissolveDuration,
        float delay,
        Action onComplete = null)
    {
        transform.DOKill();
        ResetVisualState();
        transform.position = homeWorldPos;

        Vector3 pushDir = blastWorldPos - homeWorldPos;
        float rotateZ = m_BaseRotationZ;
        if (pushDir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(pushDir.y, pushDir.x) * Mathf.Rad2Deg;
            rotateZ = angle * 0.12f;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        if (delay > 0f)
            seq.AppendInterval(delay);

        seq.Append(transform.DOMove(blastWorldPos, blastDuration).SetEase(Ease.OutExpo));
        seq.Join(transform.DOScale(m_BaseScale * 1.12f, blastDuration * 0.65f).SetEase(Ease.OutBack));
        seq.Join(transform.DORotate(new Vector3(0f, 0f, rotateZ), blastDuration).SetEase(Ease.OutQuad));

        seq.Append(transform.DOScale(m_BaseScale * 0.08f, dissolveDuration).SetEase(Ease.InBack));
        foreach (SpriteRenderer renderer in renderers)
            seq.Join(renderer.DOFade(0f, dissolveDuration));

        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>2+2+2 neighbor illusion vanish.</summary>
    public void PlayTier2NeighborIllusion(
        Vector3 homeWorldPos,
        Vector3 pushOffset,
        Vector3 duckWorldPos,
        float shockDuration,
        float vanishDuration,
        float delay,
        Action onComplete = null)
    {
        transform.DOKill();
        ResetVisualState();
        transform.position = homeWorldPos;

        float rotateZ = m_BaseRotationZ + Mathf.Sign(pushOffset.x) * 8f;
        if (Mathf.Abs(pushOffset.x) < 0.001f)
            rotateZ = m_BaseRotationZ + Mathf.Sign(pushOffset.y) * 6f;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        if (delay > 0f)
            seq.AppendInterval(delay);

        seq.Append(transform.DOPunchScale(Vector3.one * 0.14f, shockDuration, 5, 0.45f));
        seq.Join(transform.DOMove(homeWorldPos + pushOffset, shockDuration).SetEase(Ease.OutQuad));
        seq.Join(transform.DORotate(new Vector3(0f, 0f, rotateZ), shockDuration).SetEase(Ease.OutQuad));

        seq.Append(transform.DOMove(duckWorldPos, vanishDuration).SetEase(Ease.InQuad));
        seq.Join(transform.DOScale(m_BaseScale * 0.12f, vanishDuration).SetEase(Ease.InBack));
        foreach (SpriteRenderer renderer in renderers)
            seq.Join(renderer.DOFade(0f, vanishDuration * 0.92f));

        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Neighbor hiện lại sau illusion.</summary>
    public void PlayTier2NeighborRecovery(
        Vector3 homeWorldPos,
        Vector3 spawnWorldPos,
        float duration,
        int row,
        float delay,
        Action onComplete = null)
    {
        transform.DOKill();
        transform.position = spawnWorldPos;
        transform.localScale = m_BaseScale * 0.2f;
        SetSpritesAlpha(0f);

        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        if (delay > 0f)
            seq.AppendInterval(delay);

        seq.Append(transform.DOMove(homeWorldPos, duration).SetEase(Ease.OutBack));
        seq.Join(transform.DOScale(m_BaseScale, duration).SetEase(Ease.OutBack));
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
            seq.Join(renderer.DOFade(1f, duration * 0.85f));

        seq.OnComplete(() =>
        {
            ResetTransformVisual();
            ResetSpriteAlpha();
            transform.position = homeWorldPos;
            ApplyGridSorting(row);
            onComplete?.Invoke();
        });
    }

    /// <summary>Skill: block trượt xuống.</summary>
    public void PlaySkillDuckDown(Vector3 duckWorldPos, float duration, float delay, Action onComplete = null)
    {
        transform.DOKill();
        ResetVisualState();

        Tween move = transform.DOMove(duckWorldPos, duration).SetEase(Ease.InQuad);
        if (delay > 0f)
            move.SetDelay(delay);
        move.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Skill: block vanish.</summary>
    public void PlaySkillVanish(Vector3 duckWorldPos, float duration, float delay, Action onComplete = null)
    {
        transform.DOKill();
        ResetVisualState();

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        Sequence seq = DOTween.Sequence();
        if (delay > 0f)
            seq.AppendInterval(delay);

        seq.Join(transform.DOMove(duckWorldPos, duration).SetEase(Ease.InQuad));
        seq.Join(transform.DOScale(m_BaseScale * 0.15f, duration).SetEase(Ease.InBack));
        foreach (SpriteRenderer renderer in renderers)
            seq.Join(renderer.DOFade(0f, duration * 0.9f));

        seq.OnComplete(() => onComplete?.Invoke());
    }

    #endregion

    #region Private Visual Utilities

    /// <summary>Reset tween/scale/alpha sau skill, gravity, refill.</summary>
    public void ResetVisualState()
    {
        //StopSelectedOverlayRotate();
        transform.DOKill();
        ResetTransformVisual();
        ResetSpriteAlpha();
    }

    private void CacheTransformDefaults()
    {
        m_BaseScale = transform.localScale;
        m_BaseRotationZ = transform.localEulerAngles.z;
    }

    private void CacheBaseBgColor()
    {
        if (m_BgRenderer != null)
            m_BaseBgColor = m_BgRenderer.color;
    }

    private void KillPickupTweens()
    {
        transform.DOKill(false);
    }

    private void KillHoverTweens()
    {
        m_HoverTween?.Kill();
        m_HoverTween = null;
        KillHoverColorTween();
    }

    private void KillHoverColorTween()
    {
        m_HoverColorTween?.Kill();
        m_HoverColorTween = null;

        if (m_MainRenderer != null)
            m_MainRenderer.DOKill();

        if (m_BgRenderer != null)
            m_BgRenderer.DOKill();
    }

    private void ResetTransformVisual()
    {
        transform.localScale = m_BaseScale;
        transform.rotation = Quaternion.Euler(0f, 0f, m_BaseRotationZ);
    }

    private void ResetSpriteAlpha()
    {
        SetSpritesAlpha(1f);
    }

    private void SetSpritesAlpha(float alpha)
    {
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            Color c = renderer.color;
            c.a = alpha;
            renderer.color = c;
        }
    }

    #endregion
}