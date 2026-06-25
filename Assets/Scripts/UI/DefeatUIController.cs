using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UI thua — show/hide và nút chơi lại.</summary>
public class DefeatUIController : MonoBehaviour
{
    [SerializeField] private GameObject m_Root;
    [SerializeField] private Button m_BtnRetry;
    [SerializeField] private TMP_Text m_TitleText;
    [SerializeField] private TMP_Text m_SubtitleText;

    public event Action OnRetryClicked;

    private bool m_Initialized;
    private bool m_IsHiding;

    private void OnDestroy()
    {
        if (m_BtnRetry != null)
            m_BtnRetry.onClick.RemoveListener(HandleRetryClicked);
    }

    public void Show(int levelId)
    {
        EnsureInitialized();

        if (m_TitleText != null)
            m_TitleText.text = "Bạn đã thua";

        if (m_SubtitleText != null)
            m_SubtitleText.text = $"Level {levelId}";

        if (m_BtnRetry != null)
            m_BtnRetry.interactable = true;

        GameObject root = m_Root != null ? m_Root : gameObject;
        m_IsHiding = false;
        root.SetActive(true);
    }

    public void Hide(Action onComplete = null, bool animated = false)
    {
        if (!m_Initialized && m_Root == null)
            m_Root = gameObject;

        GameObject root = m_Root != null ? m_Root : gameObject;

        if (!root.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        if (m_BtnRetry != null)
            m_BtnRetry.interactable = false;

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

        if (m_BtnRetry != null)
            m_BtnRetry.onClick.AddListener(HandleRetryClicked);
    }

    private void HandleRetryClicked()
    {
        if (m_IsHiding)
            return;

        OnRetryClicked?.Invoke();
    }

    private void AutoBindReferences()
    {
        if (m_Root == null)
            m_Root = gameObject;

        Transform panel = FindChildDeep(transform, "Panel_Center");
        if (panel == null)
        {
            Debug.LogWarning("[DefeatUI] Không tìm thấy Panel_Center.");
            return;
        }

        if (m_BtnRetry == null)
        {
            Transform btn = FindChildDeep(panel, "Btn_Retry");
            if (btn != null)
                m_BtnRetry = btn.GetComponent<Button>();
        }

        if (m_TitleText == null)
        {
            Transform title = FindChildDeep(panel, "TitleText");
            if (title != null)
                m_TitleText = title.GetComponent<TMP_Text>();
        }

        if (m_SubtitleText == null)
        {
            Transform subtitle = FindChildDeep(panel, "SubtitleText");
            if (subtitle != null)
                m_SubtitleText = subtitle.GetComponent<TMP_Text>();
        }

        if (m_BtnRetry == null)
            Debug.LogWarning("[DefeatUI] Không tìm thấy Btn_Retry — nút chơi lại sẽ không hoạt động.");
    }

    private static Transform FindChildDeep(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
