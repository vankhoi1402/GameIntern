using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoseUI : UICanvas
{
    #region Private Fields

    [Header("HUD")]
    [SerializeField] private Image m_Background;
    [SerializeField] private TextMeshProUGUI m_TitleText;
    [SerializeField] private TextMeshProUGUI m_LevelValueText;

    [Header("Buttons")]
    [SerializeField] private Button m_RetryButton;
    [SerializeField] private TextMeshProUGUI m_RetryButtonText;
    [SerializeField] private Button m_AddTimeButton;
    [SerializeField] private TextMeshProUGUI m_AddTimeButtonText;

    [Header("Animation")]
    [SerializeField] private CanvasGroup m_CanvasGroup;
    [SerializeField] private float m_OpenFadeDuration = 0.25f;
    [SerializeField] private float m_CloseFadeDuration = 0.2f;

    private int m_LevelId;

    #endregion

    #region UICanvas Overrides

    public override void Setup()
    {
        base.Setup();
        RefreshHUD();
    }

    public override void Open()
    {
        base.Open();

        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.DOFade(1f, m_OpenFadeDuration);
        }
    }

    public override void Close(float _delayTime)
    {
        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.DOFade(0f, m_CloseFadeDuration)
                .OnComplete(() => base.Close(_delayTime));
            return;
        }

        base.Close(_delayTime);
    }

    public override void BackKey()
    {
        PlayManager.Ins.OnLoseRetry();
    }

    #endregion

    #region Public Methods

    public Button RetryButton => m_RetryButton;
    public Button AddTimeButton => m_AddTimeButton;

    public void Configure(int _levelId, string _addTimeLabel = "Continue+30s")
    {
        m_LevelId = _levelId;

        if (m_TitleText != null)
            m_TitleText.text = $"LEVEL {_levelId}";

        if (m_LevelValueText != null)
            m_LevelValueText.text = _levelId.ToString();

        if (m_RetryButtonText != null)
            m_RetryButtonText.text = "Retry";

        if (m_AddTimeButtonText != null)
            m_AddTimeButtonText.text = _addTimeLabel;

        RefreshHUD();
    }

    public void SetBackground(Sprite _sprite)
    {
        if (m_Background == null)
            return;

        m_Background.sprite = _sprite;
        m_Background.gameObject.SetActive(_sprite != null);
    }

    public void SetAddTimeButtonActive(bool _active)
    {
        if (m_AddTimeButton != null)
            m_AddTimeButton.gameObject.SetActive(_active);
    }

    #endregion

    #region Private Methods

    private void RefreshHUD()
    {
    }

    #endregion
}
