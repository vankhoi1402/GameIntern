using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UI thắng — show/hide và nút Next.</summary>
public class VictoryUIController : MonoBehaviour
{
    [SerializeField] private GameObject m_Root;
    [SerializeField] private Button m_BtnNext;
    [SerializeField] private TMP_Text m_TitleText;
    [SerializeField] private TMP_Text m_SubtitleText;
    [SerializeField] private GameObject m_StarsRow;
    [SerializeField] private VictoryUIAnimator m_Animator;

    public event Action OnNextClicked;

    private bool m_Initialized;
    private bool m_IsHiding;

    private void OnDestroy()
    {
        if (m_BtnNext != null)
            m_BtnNext.onClick.RemoveListener(HandleNextClicked);
    }

    public void Show(int levelId, bool hasNextLevel)
    {
        EnsureInitialized();

        if (m_StarsRow != null)
            m_StarsRow.SetActive(false);

        if (m_SubtitleText != null)
            m_SubtitleText.text = hasNextLevel
                ? $"Level {levelId} hoàn thành"
                : $"Level {levelId} — đã hoàn thành tất cả";

        if (m_BtnNext != null)
        {
            m_BtnNext.interactable = hasNextLevel;
            m_BtnNext.gameObject.SetActive(true);
        }

        GameObject root = m_Root != null ? m_Root : gameObject;
        m_IsHiding = false;
        root.SetActive(true);

        bool starsVisible = m_StarsRow != null && m_StarsRow.activeSelf;
        m_Animator?.PlayShow(starsVisible);
    }

    public void Hide(Action onComplete = null, bool animated = true)
    {
        if (!m_Initialized && m_Root == null)
            m_Root = gameObject;

        GameObject root = m_Root != null ? m_Root : gameObject;

        if (!root.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        if (m_BtnNext != null)
            m_BtnNext.interactable = false;

        if (animated && m_Animator != null && !m_IsHiding)
        {
            m_IsHiding = true;
            m_Animator.PlayHide(() =>
            {
                m_IsHiding = false;
                root.SetActive(false);
                onComplete?.Invoke();
            });
            return;
        }

        m_Animator?.SnapHidden();
        m_IsHiding = false;
        root.SetActive(false);
        onComplete?.Invoke();
    }

    private void EnsureInitialized()
    {
        if (m_Initialized)
            return;

        m_Initialized = true;
        AutoBindReferences();

        if (m_Animator == null)
            m_Animator = GetComponent<VictoryUIAnimator>();

        if (m_Animator == null)
            m_Animator = gameObject.AddComponent<VictoryUIAnimator>();

        if (m_BtnNext != null)
            m_BtnNext.onClick.AddListener(HandleNextClicked);
    }

    private void HandleNextClicked()
    {
        if (m_IsHiding)
            return;

        OnNextClicked?.Invoke();
    }

    private void AutoBindReferences()
    {
        if (m_Root == null)
            m_Root = gameObject;

        Transform panel = transform.Find("Panel_Center");
        if (panel == null)
            return;

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
    }
}
