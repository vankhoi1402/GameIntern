using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>DOTween show/hide cho popup Layer Lab (Victory / Defeat).</summary>
public class LayerLabResultPopupAnimator : MonoBehaviour
{
    #region Constants
    private const string c_DimedName = "Dimed";
    private const string c_ButtonName = "Button_124_Blue";
    private const float c_ShownAlpha = 1f;
    #endregion

    #region Serialized Fields
    [Header("References (auto-bind nếu trống)")]
    [SerializeField] private CanvasGroup m_DimedGroup;
    [SerializeField] private RectTransform m_Button;
    [SerializeField] private RectTransform[] m_ContentRoots;

    [Header("Show")]
    [SerializeField] private float m_DimedFadeIn = 0.22f;
    [SerializeField] private float m_ContentPopDuration = 0.3f;
    [SerializeField, Range(0.5f, 1f)] private float m_ContentPopFromScale = 0.88f;
    [SerializeField] private float m_ContentStartDelay = 0.06f;
    [SerializeField] private float m_ContentStagger = 0.05f;
    [SerializeField] private float m_ButtonPopDuration = 0.26f;
    [SerializeField, Range(0.5f, 1f)] private float m_ButtonPopFromScale = 0.85f;
    [SerializeField] private float m_ButtonDelayAfterContent = 0.1f;
    [SerializeField] private float m_TitlePunchStrength = 0.08f;
    [SerializeField] private float m_TitlePunchDuration = 0.28f;

    [Header("Hide")]
    [SerializeField] private float m_HideDuration = 0.18f;
    [SerializeField, Range(0.5f, 1f)] private float m_HideScale = 0.92f;
    #endregion

    #region Private Fields
    private Sequence m_Sequence;
    private Vector3 m_ButtonBaseScale = Vector3.one;
    private Vector3[] m_ContentBaseScales;
    private readonly Dictionary<RectTransform, CanvasGroup> m_ContentGroups = new();
    private bool m_Bound;
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        KillSequence();
    }
    #endregion

    #region Public Methods
    public void PlayShow(bool titlePunch = false)
    {
        EnsureBound();
        KillSequence();
        PrepareShowState();

        m_Sequence = DOTween.Sequence().SetLink(gameObject);

        if (m_DimedGroup != null)
        {
            m_Sequence.Append(
                m_DimedGroup.DOFade(c_ShownAlpha, m_DimedFadeIn).SetEase(Ease.OutQuad));
        }

        float contentStart = m_DimedGroup != null ? m_ContentStartDelay : 0f;
        AppendContentShowTweens(contentStart, titlePunch);

        float buttonStart = contentStart + GetContentShowDuration() + m_ButtonDelayAfterContent;
        AppendButtonShowTween(buttonStart);
    }

    public void PlayHide(Action onComplete)
    {
        EnsureBound();
        KillSequence();

        m_Sequence = DOTween.Sequence().SetLink(gameObject);

        if (m_DimedGroup != null)
        {
            m_Sequence.Join(
                m_DimedGroup.DOFade(0f, m_HideDuration).SetEase(Ease.InQuad));
        }

        if (m_Button != null)
        {
            m_Sequence.Join(
                m_Button.DOScale(m_ButtonBaseScale * m_HideScale, m_HideDuration).SetEase(Ease.InQuad));
        }

        if (m_ContentRoots != null)
        {
            for (int i = 0; i < m_ContentRoots.Length; i++)
            {
                RectTransform content = m_ContentRoots[i];
                if (content == null)
                    continue;

                Vector3 baseScale = m_ContentBaseScales != null && i < m_ContentBaseScales.Length
                    ? m_ContentBaseScales[i]
                    : Vector3.one;

                m_Sequence.Join(
                    content.DOScale(baseScale * m_HideScale, m_HideDuration).SetEase(Ease.InQuad));

                if (m_ContentGroups.TryGetValue(content, out CanvasGroup group) && group != null)
                {
                    m_Sequence.Join(
                        group.DOFade(0f, m_HideDuration).SetEase(Ease.InQuad));
                }
            }
        }

        m_Sequence.OnComplete(() => onComplete?.Invoke());
    }

    public void SnapHidden()
    {
        EnsureBound();
        KillSequence();
        ApplyHiddenState();
    }
    #endregion

    #region Private Methods
    private void EnsureBound()
    {
        if (m_Bound)
            return;

        m_Bound = true;
        AutoBindReferences();
        CacheBaseScales();
    }

    private void AutoBindReferences()
    {
        if (m_DimedGroup == null)
        {
            Transform dimed = PopupUIButtonUtility.FindChildDeep(transform, c_DimedName);
            if (dimed != null)
                m_DimedGroup = GetOrAddCanvasGroup(dimed);
        }

        if (m_Button == null)
        {
            Transform button = PopupUIButtonUtility.FindChildDeep(transform, c_ButtonName);
            if (button != null)
                m_Button = button as RectTransform;
        }

        if (m_ContentRoots == null || m_ContentRoots.Length == 0)
            BindContentRoots();
    }

    private void BindContentRoots()
    {
        var roots = new List<RectTransform>();

        foreach (Transform child in transform)
        {
            if (child == null)
                continue;

            if (child.name == c_DimedName || child.name == c_ButtonName)
                continue;

            if (child is RectTransform rect)
                roots.Add(rect);
        }

        m_ContentRoots = roots.ToArray();
    }

    private void CacheBaseScales()
    {
        if (m_Button != null)
            m_ButtonBaseScale = m_Button.localScale;

        if (m_ContentRoots == null)
            return;

        m_ContentBaseScales = new Vector3[m_ContentRoots.Length];
        for (int i = 0; i < m_ContentRoots.Length; i++)
        {
            RectTransform content = m_ContentRoots[i];
            m_ContentBaseScales[i] = content != null ? content.localScale : Vector3.one;
            if (content != null)
                m_ContentGroups[content] = GetOrAddCanvasGroup(content);
        }
    }

    private void PrepareShowState()
    {
        if (m_DimedGroup != null)
        {
            m_DimedGroup.alpha = 0f;
            m_DimedGroup.blocksRaycasts = false;
            m_DimedGroup.interactable = false;
        }

        if (m_ContentRoots != null)
        {
            for (int i = 0; i < m_ContentRoots.Length; i++)
            {
                RectTransform content = m_ContentRoots[i];
                if (content == null)
                    continue;

                Vector3 baseScale = m_ContentBaseScales != null && i < m_ContentBaseScales.Length
                    ? m_ContentBaseScales[i]
                    : Vector3.one;

                content.localScale = baseScale * m_ContentPopFromScale;

                if (m_ContentGroups.TryGetValue(content, out CanvasGroup group) && group != null)
                    group.alpha = 0f;
            }
        }

        if (m_Button != null)
            m_Button.localScale = m_ButtonBaseScale * m_ButtonPopFromScale;
    }

    private void AppendContentShowTweens(float startTime, bool titlePunch)
    {
        if (m_ContentRoots == null)
            return;

        for (int i = 0; i < m_ContentRoots.Length; i++)
        {
            RectTransform content = m_ContentRoots[i];
            if (content == null)
                continue;

            Vector3 baseScale = m_ContentBaseScales != null && i < m_ContentBaseScales.Length
                ? m_ContentBaseScales[i]
                : Vector3.one;

            float delay = startTime + i * m_ContentStagger;

            m_Sequence.Insert(
                delay,
                content.DOScale(baseScale, m_ContentPopDuration).SetEase(Ease.OutBack));

            if (m_ContentGroups.TryGetValue(content, out CanvasGroup group) && group != null)
            {
                m_Sequence.Insert(
                    delay,
                    group.DOFade(1f, m_ContentPopDuration).SetEase(Ease.OutQuad));
            }

            if (titlePunch && content.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                m_Sequence.Insert(
                    delay + m_ContentPopDuration * 0.55f,
                    content.DOPunchScale(Vector3.one * m_TitlePunchStrength, m_TitlePunchDuration, 5, 0.45f));
            }
        }
    }

    private void AppendButtonShowTween(float startTime)
    {
        if (m_Button == null)
            return;

        m_Sequence.Insert(
            startTime,
            m_Button.DOScale(m_ButtonBaseScale, m_ButtonPopDuration).SetEase(Ease.OutBack));
    }

    private float GetContentShowDuration()
    {
        if (m_ContentRoots == null || m_ContentRoots.Length == 0)
            return m_ContentPopDuration;

        return (m_ContentRoots.Length - 1) * m_ContentStagger + m_ContentPopDuration;
    }

    private void ApplyHiddenState()
    {
        if (m_DimedGroup != null)
        {
            m_DimedGroup.alpha = 0f;
            m_DimedGroup.blocksRaycasts = false;
        }

        if (m_Button != null)
            m_Button.localScale = m_ButtonBaseScale * m_HideScale;

        if (m_ContentRoots == null)
            return;

        for (int i = 0; i < m_ContentRoots.Length; i++)
        {
            RectTransform content = m_ContentRoots[i];
            if (content == null)
                continue;

            Vector3 baseScale = m_ContentBaseScales != null && i < m_ContentBaseScales.Length
                ? m_ContentBaseScales[i]
                : Vector3.one;

            content.localScale = baseScale * m_HideScale;

            if (m_ContentGroups.TryGetValue(content, out CanvasGroup group) && group != null)
                group.alpha = 0f;
        }
    }

    private static CanvasGroup GetOrAddCanvasGroup(Transform target)
    {
        if (!target.TryGetComponent(out CanvasGroup group))
            group = target.gameObject.AddComponent<CanvasGroup>();

        return group;
    }

    private void KillSequence()
    {
        if (m_Sequence == null)
            return;

        m_Sequence.Kill();
        m_Sequence = null;
    }
    #endregion
}
