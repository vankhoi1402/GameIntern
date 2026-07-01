using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UI thắng — show/hide và nút Next.</summary>
public class VictoryUIController : MonoBehaviour
{
    private const string c_LayerLabButtonName = "Button_124_Blue";

    [SerializeField] private GameObject m_Root;
    [SerializeField] private Button m_BtnNext;
    [SerializeField] private TMP_Text m_TitleText;
    [SerializeField] private TMP_Text m_SubtitleText;
    [SerializeField] private GameObject m_StarsRow;
    [SerializeField] private VictoryUIAnimator m_LegacyAnimator;
    [SerializeField] private LayerLabResultPopupAnimator m_LayerLabAnimator;

    public event Action OnNextClicked;

    private bool m_ClickListenerRegistered;
    private bool m_IsHiding;

    private void OnDestroy()
    {
        if (m_BtnNext != null)
            m_BtnNext.onClick.RemoveListener(HandleNextClicked);
    }

    public void Show(int levelId, bool hasNextLevel)
    {
        GameObject root = m_Root != null ? m_Root : gameObject;
        m_IsHiding = false;
        root.SetActive(true);

        EnsureButtonBinding();

        if (m_StarsRow != null)
            m_StarsRow.SetActive(false);

        if (m_SubtitleText != null)
            m_SubtitleText.text = hasNextLevel
                ? $"Level {levelId} hoàn thành"
                : $"Level {levelId} — đã hoàn thành tất cả";

        if (m_BtnNext != null)
        {
            m_BtnNext.interactable = true;
            m_BtnNext.gameObject.SetActive(true);
        }

        bool starsVisible = m_StarsRow != null && m_StarsRow.activeSelf;
        PlayShowAnimation(starsVisible);
    }

    public void Hide(Action onComplete = null, bool animated = true)
    {
        if (m_Root == null)
            m_Root = gameObject;

        GameObject root = m_Root != null ? m_Root : gameObject;

        if (!root.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        if (m_BtnNext != null)
            m_BtnNext.interactable = false;

        if (animated && TryGetActiveAnimator(out _) && !m_IsHiding)
        {
            m_IsHiding = true;
            PlayHideAnimation(() =>
            {
                m_IsHiding = false;
                root.SetActive(false);
                onComplete?.Invoke();
            });
            return;
        }

        SnapHiddenAnimation();
        m_IsHiding = false;
        root.SetActive(false);
        onComplete?.Invoke();
    }

    private void EnsureButtonBinding()
    {
        if (m_Root == null)
            m_Root = gameObject;

        EnsureAnimators();

        if (m_BtnNext == null)
            AutoBindReferences();

        if (m_BtnNext != null && PopupUIButtonUtility.UsesLayerLabButton(transform))
            PopupUIButtonUtility.PrepareLayerLabPopup(transform, c_LayerLabButtonName);

        if (m_BtnNext == null)
        {
            Debug.LogWarning("[VictoryUI] Không tìm thấy nút Continue/Next — popup sẽ không đóng được.");
            return;
        }

        if (!m_ClickListenerRegistered)
        {
            m_BtnNext.onClick.AddListener(HandleNextClicked);
            m_ClickListenerRegistered = true;
        }
    }

    private void HandleNextClicked()
    {
        if (m_IsHiding)
            return;

        OnNextClicked?.Invoke();
    }

    private void AutoBindReferences()
    {
        Transform panel = transform.Find("Panel_Center");
        if (panel != null)
        {
            if (m_BtnNext == null)
            {
                Transform btn = panel.Find("Btn_Next");
                if (btn != null)
                    m_BtnNext = btn.GetComponent<Button>();
            }

            if (m_TitleText == null)
            {
                Transform title = panel.Find("TitleText");
                if (title != null)
                    m_TitleText = title.GetComponent<TMP_Text>();
            }

            if (m_SubtitleText == null)
            {
                Transform subtitle = panel.Find("SubtitleText");
                if (subtitle != null)
                    m_SubtitleText = subtitle.GetComponent<TMP_Text>();
            }

            if (m_StarsRow == null)
            {
                Transform stars = panel.Find("StarsRow");
                if (stars != null)
                    m_StarsRow = stars.gameObject;
            }

            return;
        }

        m_BtnNext = PopupUIButtonUtility.EnsureButton(transform, c_LayerLabButtonName, m_BtnNext);
    }

    private void EnsureAnimators()
    {
        if (m_LayerLabAnimator == null)
            m_LayerLabAnimator = GetComponent<LayerLabResultPopupAnimator>();

        if (m_LegacyAnimator == null)
            m_LegacyAnimator = GetComponent<VictoryUIAnimator>();

        if (PopupUIButtonUtility.UsesLayerLabButton(transform) && m_LayerLabAnimator == null)
            m_LayerLabAnimator = gameObject.AddComponent<LayerLabResultPopupAnimator>();
    }

    private bool TryGetActiveAnimator(out bool useLayerLab)
    {
        EnsureAnimators();
        useLayerLab = PopupUIButtonUtility.UsesLayerLabButton(transform) && m_LayerLabAnimator != null;
        return useLayerLab || m_LegacyAnimator != null;
    }

    private void PlayShowAnimation(bool starsVisible)
    {
        if (PopupUIButtonUtility.UsesLayerLabButton(transform) && m_LayerLabAnimator != null)
        {
            m_LayerLabAnimator.PlayShow(titlePunch: false);
            return;
        }

        m_LegacyAnimator?.PlayShow(starsVisible);
    }

    private void PlayHideAnimation(Action onComplete)
    {
        if (PopupUIButtonUtility.UsesLayerLabButton(transform) && m_LayerLabAnimator != null)
        {
            m_LayerLabAnimator.PlayHide(onComplete);
            return;
        }

        m_LegacyAnimator?.PlayHide(onComplete);
    }

    private void SnapHiddenAnimation()
    {
        if (PopupUIButtonUtility.UsesLayerLabButton(transform))
            m_LayerLabAnimator?.SnapHidden();
        else
            m_LegacyAnimator?.SnapHidden();
    }
}
