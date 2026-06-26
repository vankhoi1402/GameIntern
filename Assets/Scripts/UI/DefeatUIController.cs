using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UI thua — show/hide và nút chơi lại.</summary>
public class DefeatUIController : MonoBehaviour
{
    private const string c_LayerLabButtonName = "Button_124_Blue";

    [SerializeField] private GameObject m_Root;
    [SerializeField] private Button m_BtnRetry;
    [SerializeField] private TMP_Text m_TitleText;
    [SerializeField] private TMP_Text m_SubtitleText;

    public event Action OnRetryClicked;

    private bool m_ClickListenerRegistered;
    private bool m_IsHiding;

    private void OnDestroy()
    {
        if (m_BtnRetry != null)
            m_BtnRetry.onClick.RemoveListener(HandleRetryClicked);
    }

    public void Show(int levelId)
    {
        GameObject root = m_Root != null ? m_Root : gameObject;
        m_IsHiding = false;
        root.SetActive(true);

        EnsureButtonBinding();

        if (m_TitleText != null)
            m_TitleText.text = "Bạn đã thua";

        if (m_SubtitleText != null)
            m_SubtitleText.text = $"Level {levelId}";

        if (m_BtnRetry != null)
            m_BtnRetry.interactable = true;
    }

    public void Hide(Action onComplete = null, bool animated = false)
    {
        if (m_Root == null)
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

    private void EnsureButtonBinding()
    {
        if (m_Root == null)
            m_Root = gameObject;

        if (m_BtnRetry == null)
            AutoBindReferences();

        if (m_BtnRetry != null && PopupUIButtonUtility.UsesLayerLabButton(transform))
            PopupUIButtonUtility.PrepareLayerLabPopup(transform, c_LayerLabButtonName);

        if (m_BtnRetry == null)
        {
            Debug.LogWarning("[DefeatUI] Không tìm thấy Button_124_Blue — nút chơi lại sẽ không hoạt động.");
            return;
        }

        if (!m_ClickListenerRegistered)
        {
            m_BtnRetry.onClick.AddListener(HandleRetryClicked);
            m_ClickListenerRegistered = true;
        }
    }

    private void HandleRetryClicked()
    {
        if (m_IsHiding)
            return;

        OnRetryClicked?.Invoke();
    }

    private void AutoBindReferences()
    {
        Transform panel = PopupUIButtonUtility.FindChildDeep(transform, "Panel_Center");
        if (panel != null)
        {
            if (m_BtnRetry == null)
            {
                Transform btn = PopupUIButtonUtility.FindChildDeep(panel, "Btn_Retry");
                if (btn != null)
                    m_BtnRetry = btn.GetComponent<Button>();
            }

            if (m_TitleText == null)
            {
                Transform title = PopupUIButtonUtility.FindChildDeep(panel, "TitleText");
                if (title != null)
                    m_TitleText = title.GetComponent<TMP_Text>();
            }

            if (m_SubtitleText == null)
            {
                Transform subtitle = PopupUIButtonUtility.FindChildDeep(panel, "SubtitleText");
                if (subtitle != null)
                    m_SubtitleText = subtitle.GetComponent<TMP_Text>();
            }

            return;
        }

        m_BtnRetry = PopupUIButtonUtility.EnsureButton(transform, c_LayerLabButtonName, m_BtnRetry);
    }
}
