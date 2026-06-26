using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Hiển thị và animation của một block — sprite, nền stack, DOTween feedback.
/// </summary>
public class BlockView : MonoBehaviour
{
    #region Constants
    private const float c_PickupScaleMultiplier = 1.12f;
    private const float c_PickupRotateZ = -8f;
    private const float c_PickupDuration = 0.15f;
    private const float c_ReleaseDuration = 0.18f;
    private const float c_AbsorbDuration = 0.18f;
    private const float c_AbsorbRotateZ = 20f;
    private const float c_RejectShakeDuration = 0.28f;
    private const float c_RejectShakeRotation = 10f;
    private const float c_RejectShakePosition = 0.06f;
    private const float c_HoverTargetScale = 1.08f;
    private const float c_HoverTargetDuration = 0.12f;
    private const float c_HoverFlashDuration = 0.06f;
    private const float c_HoverHoldDuration = 0.1f;
    private const float c_HoverFlashBlend = 0.45f;
    private const float c_HoverHoldBlend = 0.25f;
    private const float c_HoverBgFlashBlend = 0.2f;
    private const float c_HoverBgHoldBlend = 0.1f;
    public const int SortingOrderPerRow = 10;
    public const int DragSortingOrder = 1000;
    private const int c_Tier2PairOverlayMinStage = 2;
    #endregion

    [Header("Assign ngoài Inspector")]
    public SpriteRenderer _mainRenderer;
    public SpriteRenderer _bgRenderer;
    [SerializeField] private SpriteRenderer m_SelectedRenderer;

    [SerializeField] private StackBackgroundConfig stackBackgroundConfig;

    private SortingGroup m_SortingGroup;
    private Vector3 m_BaseScale;
    private float m_BaseRotationZ;
    private Color m_BaseMainColor = Color.white;
    private Color m_BaseBgColor = Color.white;
    private Tween m_HoverTween;
    private Tween m_HoverColorTween;

    /// <summary>Lưu scale và góc xoay mặc định để reset sau animation.</summary>
    private void Awake()
    {
        CacheTransformDefaults();
        m_SortingGroup = GetComponent<SortingGroup>();
        ResolveSelectedRenderer();
    }

    private void OnDestroy()
    {
        KillHoverTweens();
    }

    /// <summary>Ghi nhớ scale/rotation ban đầu của transform.</summary>
    private void CacheTransformDefaults()
    {
        m_BaseScale = transform.localScale;
        m_BaseRotationZ = transform.localEulerAngles.z;
    }

    /// <summary>Gán config sprite nền theo stack (từ BoardView hoặc handler).</summary>
    public void SetBackgroundConfig(StackBackgroundConfig config)
    {
        if (config != null)
            stackBackgroundConfig = config;
    }

    /// <summary>Thiết lập sprite chính, màu debug và nền stack ban đầu.</summary>
    public void Initialize(BlockData data, float cellWidth, float cellHeight)
    {
        if (_mainRenderer != null)
        {
            _mainRenderer.sprite = data.VisualSprite;
            _mainRenderer.color = data.DebugColor;
            m_BaseMainColor = data.DebugColor;
        }

        ResetSpriteAlpha();
        ResetTransformVisual();
        UpdateTier2StageVisual(1, 0);
        CacheBaseBgColor();
    }

    /// <summary>Khởi tạo block burst khi T3 nổ — icon + nền merge (stack 2).</summary>
    public void InitializeBurstFallOff(BlockData data, float cellWidth, float cellHeight)
    {
        if (_mainRenderer != null)
        {
            _mainRenderer.sprite = data.VisualSprite;
            _mainRenderer.color = data.DebugColor;
            m_BaseMainColor = data.DebugColor;
        }

        ResetSpriteAlpha();
        ResetTransformVisual();
        UpdateTier2StageVisual(StackBackgroundConfig.BurstFallVisualStack, 0);
        CacheBaseBgColor();
    }

    /// <summary>Đổi nền stack + overlay Selected khi đã ghép 2+2 (stage ≥ 2).</summary>
    public void UpdateTier2StageVisual(int stackCount, int tier2MergeStage)
    {
        bool tier2PairOverlay = stackCount == 2 && tier2MergeStage >= c_Tier2PairOverlayMinStage;
        UpdateStackVisual(tier2PairOverlay ? 1 : stackCount);
        ApplyTier2PairOverlay(stackCount, tier2MergeStage);
    }

    /// <summary>Đổi sprite nền theo stack (1 = mặc định, ≥ 2 = merge).</summary>
    public void UpdateStackVisual(int stackCount)
    {
        if (_bgRenderer == null || stackBackgroundConfig == null) return;

        Sprite bg = stackBackgroundConfig.GetBackground(stackCount);
        if (bg != null)
            _bgRenderer.sprite = bg;
    }

    private void ResolveSelectedRenderer()
    {
        if (m_SelectedRenderer != null)
            return;

        Transform selected = transform.Find("Selected");
        if (selected != null)
            m_SelectedRenderer = selected.GetComponent<SpriteRenderer>();
    }

    private void ApplyTier2PairOverlay(int stackCount, int tier2MergeStage)
    {
        ResolveSelectedRenderer();
        if (m_SelectedRenderer == null)
            return;

        bool showOverlay = stackCount == 2 && tier2MergeStage >= c_Tier2PairOverlayMinStage;
        m_SelectedRenderer.gameObject.SetActive(showOverlay);
    }

    /// <summary>SortingGroup theo row — row cao hơn vẽ trên row thấp hơn.</summary>
    public void ApplyGridSorting(int row)
    {
        if (m_SortingGroup == null) return;
        m_SortingGroup.sortingOrder = row * SortingOrderPerRow;
    }

    /// <summary>Khi kéo: đẩy lên trên cùng; khi thả: trả order theo ô hiện tại.</summary>
    public void SetDragSorting(bool isDragging)
    {
        if (m_SortingGroup == null) return;

        if (isDragging)
        {
            m_SortingGroup.sortingOrder = DragSortingOrder;
            transform.SetAsLastSibling();
            return;
        }

        if (TryGetComponent<Block>(out var block) && block.CurrentSlot != null)
            ApplyGridSorting(block.CurrentSlot.Row);
    }

    /// <summary>Đặt block ngay lập tức tại vị trí world (spawn, không tween).</summary>
    public void MoveToPosition(Vector3 targetWorldPos)
    {
        transform.position = targetWorldPos;
    }

    /// <summary>Refill: tween từ dưới ô đích lên ô đích (row 0).</summary>
    public void PlayRiseFromBelow(Vector3 targetWorldPos, float cellHeight, float duration, Action onComplete = null)
        => PlayRiseFromBelow(targetWorldPos, cellHeight, duration, 0, 0f, onComplete);

    /// <summary>Tween từ dưới ô đích — hỗ trợ stagger và sorting theo row.</summary>
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

    /// <summary>Tween từ vị trí spawn tùy ý lên ô đích — dùng skill lấp cột từ đáy.</summary>
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

    /// <summary>Intro load level — rise mượt với fade + scale nhẹ (column wave).</summary>
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

    /// <summary>Ẩn block trước khi intro rise (tránh flash tại ô đích).</summary>
    public void PrepareForIntroRise()
    {
        transform.DOKill();
        ResetTransformVisual();
        SetSpritesAlpha(0f);
    }

    /// <summary>Reset tween/scale/alpha — gọi sau skill, gravity, refill.</summary>
    public void ResetVisualState()
    {
        transform.DOKill();
        ResetTransformVisual();
        ResetSpriteAlpha();
    }

    /// <summary>Snap về ô lưới sau illusion skill (không tween).</summary>
    public void SnapToGridCell(Vector3 worldPos, int row)
    {
        ResetVisualState();
        transform.position = worldPos;
        ApplyGridSorting(row);
    }

    /// <summary>Skill cột: block trượt xuống (illusion sweep).</summary>
    public void PlaySkillDuckDown(Vector3 duckWorldPos, float duration, float delay, Action onComplete = null)
    {
        transform.DOKill();
        ResetVisualState();

        Tween move = transform.DOMove(duckWorldPos, duration).SetEase(Ease.InQuad);
        if (delay > 0f)
            move.SetDelay(delay);
        move.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Skill cột: block bị xóa — tụt xuống + thu nhỏ + mờ.</summary>
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

    /// <summary>Block mới spawn từ trên cột — tween rơi xuống ô đích.</summary>
    public void PlaySpawnFall(Vector3 targetWorldPos, float duration, Action onComplete = null)
    {
        transform.DOKill();
        transform.DOMove(targetWorldPos, duration)
            .SetEase(Ease.OutBounce)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Nảy nhẹ khi T3 vỡ thành 3 block trước khi rơi khỏi màn hình.</summary>
    public void PlayBurstPop()
    {
        KillPickupTweens();
        transform.DOPunchScale(Vector3.one * 0.2f, 0.12f, 6, 0.5f);
    }

    /// <summary>T3 nổ: tóe ra (scale + bay) rồi rơi khỏi màn hình + fade.</summary>
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

    /// <summary>Hiệu ứng khi nhấc block — phóng to nhẹ và xoay.</summary>
    public void PlayPickupFeedback()
    {
        KillPickupTweens();
        transform.DOScale(m_BaseScale * c_PickupScaleMultiplier, c_PickupDuration).SetEase(Ease.OutBack);
        transform.DORotate(new Vector3(0f, 0f, m_BaseRotationZ + c_PickupRotateZ), c_PickupDuration)
            .SetEase(Ease.OutQuad);
    }

    /// <summary>Hiệu ứng khi thả block — trả scale/rotation về mặc định.</summary>
    public void PlayReleaseFeedback()
    {
        KillPickupTweens();
        transform.DOScale(m_BaseScale, c_ReleaseDuration).SetEase(Ease.OutQuad);
        transform.DORotate(new Vector3(0f, 0f, m_BaseRotationZ), c_ReleaseDuration).SetEase(Ease.OutQuad);
    }

    /// <summary>Tween block tới ô đích khi di chuyển vào ô trống.</summary>
    public void PlayMoveToSlot(Vector3 targetWorldPos, Action onComplete = null)
    {
        KillPickupTweens();
        PlayReleaseFeedback();
        transform.DOMove(targetWorldPos, c_ReleaseDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Source block bay vào target khi merge — thu nhỏ và xoay.</summary>
    public void PlayAbsorbInto(Vector3 targetWorldPos, Action onComplete)
    {
        transform.DOKill();
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOMove(targetWorldPos, c_AbsorbDuration).SetEase(Ease.InQuad));
        seq.Join(transform.DOScale(Vector3.zero, c_AbsorbDuration).SetEase(Ease.InBack));
        seq.Join(transform.DORotate(new Vector3(0f, 0f, m_BaseRotationZ + c_AbsorbRotateZ), c_AbsorbDuration));
        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Hiệu ứng rung nhẹ trên target sau khi nhận stack merge.</summary>
    public void PlayMergeImpact()
    {
        KillPickupTweens();
        transform.DOPunchScale(Vector3.one * 0.15f, 0.22f, 8, 0.4f);
    }

    /// <summary>Block đích phóng to nhẹ + lóe sáng khi block khác kéo qua.</summary>
    public void PlayHoverTargetFeedback()
    {
        KillHoverTweens();
        m_HoverTween = transform
            .DOScale(m_BaseScale * c_HoverTargetScale, c_HoverTargetDuration)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject);

        PlayHoverFlash();
    }

    /// <summary>Trả scale và màu block đích về bình thường khi không còn hover.</summary>
    public void ClearHoverTargetFeedback()
    {
        m_HoverTween?.Kill();
        m_HoverTween = transform
            .DOScale(m_BaseScale, c_HoverTargetDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);

        ClearHoverFlash();
    }

    private void PlayHoverFlash()
    {
        KillHoverColorTween();
        if (_mainRenderer == null)
            return;

        Color flashMain = Color.Lerp(m_BaseMainColor, Color.white, c_HoverFlashBlend);
        Color holdMain = Color.Lerp(m_BaseMainColor, Color.white, c_HoverHoldBlend);
        Color flashBg = Color.Lerp(m_BaseBgColor, Color.white, c_HoverBgFlashBlend);
        Color holdBg = Color.Lerp(m_BaseBgColor, Color.white, c_HoverBgHoldBlend);

        Sequence seq = DOTween.Sequence();
        seq.Append(_mainRenderer.DOColor(flashMain, c_HoverFlashDuration).SetEase(Ease.OutQuad));
        if (_bgRenderer != null)
            seq.Join(_bgRenderer.DOColor(flashBg, c_HoverFlashDuration).SetEase(Ease.OutQuad));

        seq.Append(_mainRenderer.DOColor(holdMain, c_HoverHoldDuration).SetEase(Ease.OutQuad));
        if (_bgRenderer != null)
            seq.Join(_bgRenderer.DOColor(holdBg, c_HoverHoldDuration).SetEase(Ease.OutQuad));

        m_HoverColorTween = seq.SetLink(gameObject);
    }

    private void ClearHoverFlash()
    {
        KillHoverColorTween();

        if (_mainRenderer == null)
            return;

        Sequence seq = DOTween.Sequence();
        seq.Join(_mainRenderer.DOColor(m_BaseMainColor, c_HoverTargetDuration).SetEase(Ease.OutQuad));
        if (_bgRenderer != null)
            seq.Join(_bgRenderer.DOColor(m_BaseBgColor, c_HoverTargetDuration).SetEase(Ease.OutQuad));

        m_HoverColorTween = seq.SetLink(gameObject);
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

        if (_mainRenderer != null)
            _mainRenderer.DOKill();

        if (_bgRenderer != null)
            _bgRenderer.DOKill();
    }

    private void CacheBaseBgColor()
    {
        if (_bgRenderer != null)
            m_BaseBgColor = _bgRenderer.color;
    }

    /// <summary>Lắc nhẹ block đang kéo tại chỗ thả — xong gọi onComplete rồi mới về ô gốc.</summary>
    public void PlayRejectShake(Action onComplete = null)
    {
        KillPickupTweens();

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOPunchRotation(
            new Vector3(0f, 0f, c_RejectShakeRotation),
            c_RejectShakeDuration,
            14,
            0.55f));
        seq.Join(transform.DOPunchPosition(
            new Vector3(c_RejectShakePosition, 0f, 0f),
            c_RejectShakeDuration,
            12,
            0.45f).SetRelative(true));
        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>Block rơi xuống dưới màn hình và mờ dần khi stack đạt max (≥3).</summary>
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

    /// <summary>Dừng các tween scale/rotate đang chạy (không kill fall/absorb).</summary>
    private void KillPickupTweens()
    {
        transform.DOKill(false);
    }

    /// <summary>Reset scale và rotation về giá trị gốc.</summary>
    private void ResetTransformVisual()
    {
        transform.localScale = m_BaseScale;
        transform.rotation = Quaternion.Euler(0f, 0f, m_BaseRotationZ);
    }

    /// <summary>Đảm bảo alpha sprite = 1 (sau fall-off có thể bị fade).</summary>
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
}
