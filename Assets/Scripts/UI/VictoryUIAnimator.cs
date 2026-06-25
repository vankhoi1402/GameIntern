using System;
using DG.Tweening;
using UnityEngine;

/// <summary>DOTween show/hide cho Victory UI — overlay fade, panel pop, stagger nội dung.</summary>
public class VictoryUIAnimator : MonoBehaviour
{
    #region Constants
    private const float c_OverlayShownAlpha = 1f;
    #endregion

    #region Serialized Fields
    [Header("References (auto-bind nếu trống)")]
    [SerializeField] private CanvasGroup m_OverlayGroup;
    [SerializeField] private RectTransform m_Panel;
    [SerializeField] private RectTransform m_Title;
    [SerializeField] private CanvasGroup m_TitleGroup;
    [SerializeField] private CanvasGroup m_SubtitleGroup;
    [SerializeField] private RectTransform m_Button;
    [SerializeField] private RectTransform m_StarsRow;
    [SerializeField] private RectTransform[] m_StarItems;

    [Header("Show")]
    [SerializeField] private float m_OverlayFadeIn = 0.22f;
    [SerializeField] private float m_PanelPopDuration = 0.32f;
    [SerializeField, Range(0.5f, 1f)] private float m_PanelPopFromScale = 0.88f;
    [SerializeField] private float m_TitlePopDuration = 0.28f;
    [SerializeField, Range(0.5f, 1f)] private float m_TitlePopFromScale = 0.7f;
    [SerializeField] private float m_TitleStartDelay = 0.08f;
    [SerializeField] private float m_SubtitleFadeDuration = 0.2f;
    [SerializeField] private float m_SubtitleDelay = 0.08f;
    [SerializeField] private float m_ButtonPopDuration = 0.25f;
    [SerializeField, Range(0.5f, 1f)] private float m_ButtonPopFromScale = 0.85f;
    [SerializeField] private float m_ButtonDelay = 0.12f;
    [SerializeField] private float m_StarStartDelay = 0.1f;
    [SerializeField] private float m_StarStagger = 0.06f;
    [SerializeField] private float m_StarPopDuration = 0.2f;
    [SerializeField] private float m_StarPunchScale = 0.1f;

    [Header("Hide")]
    [SerializeField] private float m_HideDuration = 0.18f;
    [SerializeField, Range(0.5f, 1f)] private float m_HidePanelScale = 0.92f;
    #endregion

    #region Private Fields
    private Sequence m_Sequence;
    private Vector3 m_PanelBaseScale = Vector3.one;
    private Vector3 m_TitleBaseScale = Vector3.one;
    private Vector3 m_ButtonBaseScale = Vector3.one;
    private Vector3[] m_StarBaseScales;
    private bool m_Bound;
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        KillSequence();
    }
    #endregion

    #region Public Methods
    public void PlayShow(bool starsVisible)
    {
        EnsureBound();
        KillSequence();
        PrepareShowState(starsVisible);

        m_Sequence = DOTween.Sequence().SetLink(gameObject);

        m_Sequence.Append(
            m_OverlayGroup.DOFade(c_OverlayShownAlpha, m_OverlayFadeIn).SetEase(Ease.OutQuad));

        m_Sequence.Join(
            m_Panel.DOScale(m_PanelBaseScale, m_PanelPopDuration)
                .SetEase(Ease.OutBack));

        float titleStart = m_TitleStartDelay;
        m_Sequence.Insert(
            titleStart,
            m_Title.DOScale(m_TitleBaseScale, m_TitlePopDuration).SetEase(Ease.OutBack));

        if (m_TitleGroup != null)
        {
            m_Sequence.Insert(
                titleStart,
                m_TitleGroup.DOFade(1f, m_TitlePopDuration).SetEase(Ease.OutQuad));
        }

        float subtitleStart = titleStart + m_SubtitleDelay;
        if (m_SubtitleGroup != null)
        {
            m_Sequence.Insert(
                subtitleStart,
                m_SubtitleGroup.DOFade(1f, m_SubtitleFadeDuration).SetEase(Ease.OutQuad));
        }

        float buttonStart = subtitleStart + m_ButtonDelay;
        m_Sequence.Insert(
            buttonStart,
            m_Button.DOScale(m_ButtonBaseScale, m_ButtonPopDuration).SetEase(Ease.OutBack));

        if (starsVisible)
            AppendStarTweens(titleStart + m_StarStartDelay);
    }

    public void PlayHide(Action onComplete)
    {
        EnsureBound();
        KillSequence();

        m_Sequence = DOTween.Sequence().SetLink(gameObject);
        m_Sequence.Join(
            m_Panel.DOScale(m_PanelBaseScale * m_HidePanelScale, m_HideDuration).SetEase(Ease.InQuad));
        m_Sequence.Join(
            m_OverlayGroup.DOFade(0f, m_HideDuration).SetEase(Ease.InQuad));
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
        EnsureCanvasGroups();
        CacheBaseScales();
    }

    private void AutoBindReferences()
    {
        Transform overlay = transform.Find("Overlay");
        if (overlay != null && m_OverlayGroup == null)
            m_OverlayGroup = overlay.GetComponent<CanvasGroup>();

        Transform panel = transform.Find("Panel_Center");
        if (panel == null)
            return;

        if (m_Panel == null)
            m_Panel = panel as RectTransform;

        if (m_Title == null)
        {
            Transform title = panel.Find("TitleText");
            if (title != null)
                m_Title = title as RectTransform;
        }

        if (m_SubtitleGroup == null)
        {
            Transform subtitle = panel.Find("SubtitleText");
            if (subtitle != null)
                m_SubtitleGroup = subtitle.GetComponent<CanvasGroup>();
        }

        if (m_Button == null)
        {
            Transform button = panel.Find("Btn_Next");
            if (button != null)
                m_Button = button as RectTransform;
        }

        if (m_StarsRow == null)
        {
            Transform stars = panel.Find("StarsRow");
            if (stars != null)
                m_StarsRow = stars as RectTransform;
        }

        if (m_StarItems == null || m_StarItems.Length == 0)
            BindStarItems();
    }

    private void BindStarItems()
    {
        if (m_StarsRow == null)
            return;

        int childCount = m_StarsRow.childCount;
        if (childCount == 0)
            return;

        m_StarItems = new RectTransform[childCount];
        m_StarBaseScales = new Vector3[childCount];
        for (int i = 0; i < childCount; i++)
        {
            m_StarItems[i] = m_StarsRow.GetChild(i) as RectTransform;
            m_StarBaseScales[i] = m_StarItems[i] != null ? m_StarItems[i].localScale : Vector3.one;
        }
    }

    private void EnsureCanvasGroups()
    {
        if (m_OverlayGroup == null)
        {
            Transform overlay = transform.Find("Overlay");
            if (overlay != null)
                m_OverlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
        }

        if (m_Title != null && m_TitleGroup == null)
            m_TitleGroup = m_Title.gameObject.AddComponent<CanvasGroup>();

        if (m_SubtitleGroup == null)
        {
            Transform panel = transform.Find("Panel_Center");
            Transform subtitle = panel != null ? panel.Find("SubtitleText") : null;
            if (subtitle != null)
                m_SubtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void CacheBaseScales()
    {
        if (m_Panel != null)
            m_PanelBaseScale = m_Panel.localScale;

        if (m_Title != null)
            m_TitleBaseScale = m_Title.localScale;

        if (m_Button != null)
            m_ButtonBaseScale = m_Button.localScale;
    }

    private void PrepareShowState(bool starsVisible)
    {
        if (m_OverlayGroup != null)
        {
            m_OverlayGroup.alpha = 0f;
            m_OverlayGroup.blocksRaycasts = true;
            m_OverlayGroup.interactable = false;
        }

        if (m_Panel != null)
            m_Panel.localScale = m_PanelBaseScale * m_PanelPopFromScale;

        if (m_Title != null)
            m_Title.localScale = m_TitleBaseScale * m_TitlePopFromScale;

        if (m_TitleGroup != null)
            m_TitleGroup.alpha = 0f;

        if (m_SubtitleGroup != null)
            m_SubtitleGroup.alpha = 0f;

        if (m_Button != null)
            m_Button.localScale = m_ButtonBaseScale * m_ButtonPopFromScale;

        if (m_StarsRow != null)
            m_StarsRow.gameObject.SetActive(starsVisible);

        if (starsVisible && m_StarItems != null)
        {
            for (int i = 0; i < m_StarItems.Length; i++)
            {
                if (m_StarItems[i] != null)
                    m_StarItems[i].localScale = Vector3.zero;
            }
        }
    }

    private void ApplyHiddenState()
    {
        if (m_OverlayGroup != null)
        {
            m_OverlayGroup.alpha = 0f;
            m_OverlayGroup.blocksRaycasts = false;
        }

        if (m_Panel != null)
            m_Panel.localScale = m_PanelBaseScale * m_HidePanelScale;

        if (m_TitleGroup != null)
            m_TitleGroup.alpha = 0f;

        if (m_SubtitleGroup != null)
            m_SubtitleGroup.alpha = 0f;
    }

    private void AppendStarTweens(float startTime)
    {
        if (m_StarItems == null)
            return;

        for (int i = 0; i < m_StarItems.Length; i++)
        {
            RectTransform star = m_StarItems[i];
            if (star == null)
                continue;

            Vector3 baseScale = m_StarBaseScales != null && i < m_StarBaseScales.Length
                ? m_StarBaseScales[i]
                : Vector3.one;
            float delay = startTime + i * m_StarStagger;

            m_Sequence.Insert(
                delay,
                star.DOScale(baseScale, m_StarPopDuration).SetEase(Ease.OutBack));

            m_Sequence.Insert(
                delay + m_StarPopDuration * 0.6f,
                star.DOPunchScale(Vector3.one * m_StarPunchScale, 0.25f, 4, 0.5f));
        }
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
