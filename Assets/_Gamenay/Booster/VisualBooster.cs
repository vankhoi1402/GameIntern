using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class VisualBooster : MonoBehaviour
{
    #region Shuffle Animation

    [Header("Shuffle Root")]
    [SerializeField] private Transform m_ShuffleBoardVisualRoot;

    [Header("Shuffle Flash")]
    [SerializeField] private Image m_ShuffleFlashImage;
    [SerializeField] private SpriteRenderer m_ShuffleFlashSprite;
    [SerializeField, Range(0f, 1f)] private float m_ShuffleFlashMaxAlpha = 0.9f;

    [Header("Shuffle Timing")]
    [SerializeField] private float m_ShuffleShakeDuration = 0.12f;
    [SerializeField] private float m_ShuffleFlashInDuration = 0.08f;
    [SerializeField] private float m_ShuffleFlashOutDuration = 0.12f;
    [SerializeField] private float m_ShuffleBlockPunchDuration = 0.28f;
    [SerializeField] private float m_ShuffleBoardBounceDuration = 0.22f;

    [Header("Shuffle Motion")]
    [SerializeField] private float m_ShuffleShakeStrength = 0.12f;
    [SerializeField] private int m_ShuffleShakeVibrato = 18;
    [SerializeField] private float m_ShuffleBlockScaleMultiplier = 1.08f;
    [SerializeField] private float m_ShuffleBlockRotateAngle = 5f;
    [SerializeField] private float m_ShuffleBoardBounceStrength = 0.08f;

    #endregion

    #region Runtime State

    private readonly List<BlockManager> m_ShuffleBlocks = new List<BlockManager>(32);
    private Sequence m_ShuffleSequence;
    private Vector3 m_ShuffleBoardRootStartWorldPos;
    private bool m_HasShuffleBoardRootStartWorldPos;

    private readonly List<BlockManager> m_MagnetBlocks = new List<BlockManager>(3);
    private Sequence m_MagnetSequence;
    private bool m_IsMagnetVisualPlaying;

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        KillActiveVisual(false);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Phát visual shuffle và callback về gameplay khi đạt đỉnh flash / khi hoàn tất.
    /// VisualBooster chỉ xử lý animation, không chạm vào logic board.
    /// </summary>
    public void PlayShuffle(Action _onPeakFlash, Action _onComplete, IList<BlockManager> _blocksToAnimate)
    {
        KillActiveVisual(false);

        Transform boardRoot = ResolveShuffleBoardRoot();
        if (boardRoot != null)
        {
            m_ShuffleBoardRootStartWorldPos = boardRoot.position;
            m_HasShuffleBoardRootStartWorldPos = true;
        }
        else
        {
            m_HasShuffleBoardRootStartWorldPos = false;
        }

        CacheShuffleBlocks(_blocksToAnimate);
        SetShuffleFlashAlpha(0f);

        m_ShuffleSequence = DOTween.Sequence();
        m_ShuffleSequence.SetAutoKill(false);
        m_ShuffleSequence.SetUpdate(UpdateType.Normal, false);

        // Rung nhẹ board root trước khi flash để tạo lực mở đầu.
        if (boardRoot != null)
        {
            m_ShuffleSequence.Append(
                boardRoot.DOShakePosition(
                        m_ShuffleShakeDuration,
                        new Vector3(m_ShuffleShakeStrength, m_ShuffleShakeStrength, 0f),
                        m_ShuffleShakeVibrato,
                        90f,
                        false,
                        true)
                    .SetEase(Ease.OutQuad));
        }

        // Flash trắng tăng sáng nhanh để tạo mốc timing rõ cho logic shuffle.
        m_ShuffleSequence.Append(
            DOTween.To(
                    GetShuffleFlashAlpha,
                    SetShuffleFlashAlpha,
                    m_ShuffleFlashMaxAlpha,
                    m_ShuffleFlashInDuration)
                .SetEase(Ease.OutQuad));

        // Gọi gameplay logic đúng lúc flash đạt đỉnh sáng nhất.
        m_ShuffleSequence.AppendCallback(() =>
        {
            _onPeakFlash?.Invoke();
            CacheShuffleBlocks(_blocksToAnimate);
        });

        // Sau khi state board đã đổi xong mới animate visual cho toàn bộ block hiện tại.
        m_ShuffleSequence.AppendCallback(PlayShuffleBlockPunch);

        m_ShuffleSequence.Append(
            DOTween.To(
                    GetShuffleFlashAlpha,
                    SetShuffleFlashAlpha,
                    0f,
                    m_ShuffleFlashOutDuration)
                .SetEase(Ease.InQuad));

        // Bounce nhẹ toàn board để kết thúc nhịp animation.
        if (boardRoot != null)
        {
            m_ShuffleSequence.Append(
                boardRoot.DOPunchPosition(
                        Vector3.up * m_ShuffleBoardBounceStrength,
                        m_ShuffleBoardBounceDuration,
                        8,
                        0.7f)
                    .SetEase(Ease.OutQuad));
        }

        // Giữ gameplay chờ đến khi punch block chạy xong.
        m_ShuffleSequence.AppendInterval(m_ShuffleBlockPunchDuration);

        m_ShuffleSequence.OnComplete(() =>
        {
            CleanupShuffleVisualState();
            _onComplete?.Invoke();
        });

        m_ShuffleSequence.OnKill(CleanupShuffleVisualState);

        m_ShuffleSequence.Play();
    }

    /// <summary>
    /// Hủy visual booster đang chạy và dọn trạng thái transform/flash.
    /// </summary>
    public void KillActiveVisual(bool _complete)
    {
        KillShuffleVisual(_complete);
        KillMagnetVisual(_complete);
    }

    #endregion

    #region Shuffle Helpers

    private Transform ResolveShuffleBoardRoot()
    {
        return m_ShuffleBoardVisualRoot != null ? m_ShuffleBoardVisualRoot : transform;
    }

    private void CacheShuffleBlocks(IList<BlockManager> _blocksToAnimate)
    {
        m_ShuffleBlocks.Clear();

        if (_blocksToAnimate == null)
            return;

        int count = _blocksToAnimate.Count;
        for (int i = 0; i < count; i++)
        {
            BlockManager block = _blocksToAnimate[i];
            if (block == null || block.IsPendingDestroy)
                continue;

            m_ShuffleBlocks.Add(block);
        }
    }

    private void PlayShuffleBlockPunch()
    {
        int blockCount = m_ShuffleBlocks.Count;
        float halfDuration = m_ShuffleBlockPunchDuration * 0.5f;

        for (int i = 0; i < blockCount; i++)
        {
            BlockManager block = m_ShuffleBlocks[i];
            if (block == null)
                continue;

            Transform blockTransform = block.transform;
            float targetRotation = (i & 1) == 0 ? m_ShuffleBlockRotateAngle : -m_ShuffleBlockRotateAngle;

            blockTransform.DOKill(false);
            blockTransform.localScale = Vector3.one;
            blockTransform.localRotation = Quaternion.identity;

            Sequence blockSequence = DOTween.Sequence();
            blockSequence.SetAutoKill(true);

            blockSequence.Append(
                blockTransform.DOScale(m_ShuffleBlockScaleMultiplier, halfDuration)
                    .SetEase(Ease.OutBack));
            blockSequence.Join(
                blockTransform.DOLocalRotate(new Vector3(0f, 0f, targetRotation), halfDuration, RotateMode.Fast)
                    .SetEase(Ease.OutSine));

            blockSequence.Append(
                blockTransform.DOScale(1f, halfDuration)
                    .SetEase(Ease.OutBack));
            blockSequence.Join(
                blockTransform.DOLocalRotate(Vector3.zero, halfDuration, RotateMode.Fast)
                    .SetEase(Ease.OutBack));
        }
    }

    private void CleanupShuffleVisualState()
    {
        Transform boardRoot = ResolveShuffleBoardRoot();
        if (boardRoot != null)
        {
            boardRoot.DOKill(false);
            if (m_HasShuffleBoardRootStartWorldPos)
                boardRoot.position = m_ShuffleBoardRootStartWorldPos;
        }

        SetShuffleFlashAlpha(0f);
        ResetShuffleBlocksVisual();

        m_ShuffleSequence = null;
    }

    private void KillShuffleVisual(bool _complete)
    {
        if (m_ShuffleSequence == null)
        {
            CleanupShuffleVisualState();
            return;
        }

        Sequence activeSequence = m_ShuffleSequence;
        m_ShuffleSequence = null;
        activeSequence.Kill(_complete);
    }

    private void CacheMagnetBlocks(IList<BlockManager> _blocks)
    {
        m_MagnetBlocks.Clear();

        if (_blocks == null)
            return;

        int count = _blocks.Count;
        for (int i = 0; i < count; i++)
        {
            BlockManager block = _blocks[i];
            if (block == null || block.IsPendingDestroy)
                continue;

            m_MagnetBlocks.Add(block);
        }
    }

    private void CleanupMagnetVisualState()
    {
        int blockCount = m_MagnetBlocks.Count;
        for (int i = 0; i < blockCount; i++)
        {
            BlockManager block = m_MagnetBlocks[i];
            if (block == null)
                continue;

            block.transform.DOKill(false);
            block.ResetVisualState();
            block.EndMagnetFlyVisual();
        }

        m_MagnetBlocks.Clear();
        m_MagnetSequence = null;
        m_IsMagnetVisualPlaying = false;
    }

    private void KillMagnetVisual(bool _complete)
    {
        if (m_MagnetSequence != null)
        {
            Sequence activeSequence = m_MagnetSequence;
            m_MagnetSequence = null;
            activeSequence.Kill(_complete);
        }

        if (!m_IsMagnetVisualPlaying)
            return;

        m_HammerAnimation?.ResetHammerState();
        CleanupMagnetVisualState();
    }

    private void ResetShuffleBlocksVisual()
    {
        int blockCount = m_ShuffleBlocks.Count;
        for (int i = 0; i < blockCount; i++)
        {
            BlockManager block = m_ShuffleBlocks[i];
            if (block == null)
                continue;

            Transform blockTransform = block.transform;
            blockTransform.DOKill(false);
            blockTransform.localScale = Vector3.one;
            blockTransform.localRotation = Quaternion.identity;
        }
    }

    private float GetShuffleFlashAlpha()
    {
        if (m_ShuffleFlashImage != null)
            return m_ShuffleFlashImage.color.a;

        if (m_ShuffleFlashSprite != null)
            return m_ShuffleFlashSprite.color.a;

        return 0f;
    }

    private void SetShuffleFlashAlpha(float _alpha)
    {
        if (m_ShuffleFlashImage != null)
        {
            Color imageColor = m_ShuffleFlashImage.color;
            imageColor.a = _alpha;
            m_ShuffleFlashImage.color = imageColor;
        }

        if (m_ShuffleFlashSprite != null)
        {
            Color spriteColor = m_ShuffleFlashSprite.color;
            spriteColor.a = _alpha;
            m_ShuffleFlashSprite.color = spriteColor;
        }
    }

    #endregion

    #region Magnet Booster

    [Header("Magnet Hammer")]
    [SerializeField] private HammerAnimation m_HammerAnimation;
    [SerializeField] private float m_MagnetFlyDuration = 0.22f;
    [SerializeField] private Ease m_MagnetFlyEase = Ease.OutQuad;
    [SerializeField] private float m_MagnetMergeHoldDuration = 0.12f;
    [SerializeField] private float m_MagnetFlyScaleMultiplier = 1.12f;

    /// <summary>
    /// Cho các block bay về giữa màn hình, gọi onFlyArrived khi chạm giữa, rồi búa đập (onHammerHit) và onFinished.
    /// </summary>
    public void PlayMagnet(
        IList<BlockManager> _blocks,
        Vector3 _screenCenterWorldPos,
        Action _onFlyArrived,
        Action _onHammerHit,
        Action _onFinished)
    {
        KillMagnetVisual(false);

        CacheMagnetBlocks(_blocks);
        if (m_MagnetBlocks.Count == 0)
        {
            _onFlyArrived?.Invoke();
            _onHammerHit?.Invoke();
            _onFinished?.Invoke();
            return;
        }

        Vector3 centerWorldPos = _screenCenterWorldPos;
        centerWorldPos.z = 0f;
        m_IsMagnetVisualPlaying = true;

        m_MagnetSequence = DOTween.Sequence();
        m_MagnetSequence.SetAutoKill(false);

        for (int i = 0; i < m_MagnetBlocks.Count; i++)
        {
            BlockManager block = m_MagnetBlocks[i];
            if (block == null)
                continue;

            block.BeginMagnetFlyVisual();

            Transform blockTransform = block.transform;
            blockTransform.DOKill(false);

            Vector3 flyScale = blockTransform.localScale * m_MagnetFlyScaleMultiplier;

            m_MagnetSequence.Join(
                blockTransform.DOMove(centerWorldPos, m_MagnetFlyDuration)
                    .SetEase(m_MagnetFlyEase));
            m_MagnetSequence.Join(
                blockTransform.DOScale(flyScale, m_MagnetFlyDuration)
                    .SetEase(m_MagnetFlyEase));
        }

        m_MagnetSequence.AppendCallback(() => _onFlyArrived?.Invoke());

        if (m_MagnetMergeHoldDuration > 0f)
            m_MagnetSequence.AppendInterval(m_MagnetMergeHoldDuration);

        m_MagnetSequence.AppendCallback(() =>
        {
            if (m_HammerAnimation == null)
            {
                _onHammerHit?.Invoke();
                CleanupMagnetVisualState();
                _onFinished?.Invoke();
                return;
            }

            m_HammerAnimation.gameObject.SetActive(true);
            m_HammerAnimation.Configure(centerWorldPos, _onHammerHit, () =>
            {
                CleanupMagnetVisualState();
                _onFinished?.Invoke();
            });
        });

        m_MagnetSequence.OnComplete(() => m_MagnetSequence = null);
        m_MagnetSequence.OnKill(() =>
        {
            m_MagnetSequence = null;
            if (!m_IsMagnetVisualPlaying)
                return;

            m_HammerAnimation?.ResetHammerState();
            CleanupMagnetVisualState();
        });

        m_MagnetSequence.Play();
    }

    #endregion
}
