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
    public const int SortingOrderPerRow = 10;
    public const int DragSortingOrder = 1000;
    #endregion

    [Header("Assign ngoài Inspector")]
    public SpriteRenderer _mainRenderer;
    public SpriteRenderer _bgRenderer;

    [SerializeField] private StackBackgroundConfig stackBackgroundConfig;

    private SortingGroup m_SortingGroup;
    private Vector3 m_BaseScale;
    private float m_BaseRotationZ;

    /// <summary>Lưu scale và góc xoay mặc định để reset sau animation.</summary>
    private void Awake()
    {
        CacheTransformDefaults();
        m_SortingGroup = GetComponent<SortingGroup>();
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
        }

        ResetSpriteAlpha();
        ResetTransformVisual();
        UpdateStackVisual(1);
    }

    /// <summary>Khởi tạo block burst khi T3 nổ — icon + nền merge (stack 2).</summary>
    public void InitializeBurstFallOff(BlockData data, float cellWidth, float cellHeight)
    {
        if (_mainRenderer != null)
        {
            _mainRenderer.sprite = data.VisualSprite;
            _mainRenderer.color = data.DebugColor;
        }

        ResetSpriteAlpha();
        ResetTransformVisual();
        UpdateStackVisual(StackBackgroundConfig.BurstFallVisualStack);
    }

    /// <summary>Đổi sprite nền theo stack (1 = mặc định, ≥ 2 = merge).</summary>
    public void UpdateStackVisual(int stackCount)
    {
        if (_bgRenderer == null || stackBackgroundConfig == null) return;

        Sprite bg = stackBackgroundConfig.GetBackground(stackCount);
        if (bg != null)
            _bgRenderer.sprite = bg;
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

    /// <summary>Refill: tween từ dưới ô đích lên row 0.</summary>
    public void PlayRiseFromBelow(Vector3 targetWorldPos, float cellHeight, float duration, Action onComplete = null)
    {
        transform.DOKill();
        transform.position = targetWorldPos + Vector3.down * cellHeight * 1.2f;
        transform.DOMove(targetWorldPos, duration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                ApplyGridSorting(0);
                onComplete?.Invoke();
            });
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
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            Color c = renderer.color;
            c.a = 1f;
            renderer.color = c;
        }
    }
}
