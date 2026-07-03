using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WinUI : UICanvas
{
    #region Private Fields

    [Header("HUD")]
    [SerializeField] private Image m_Background;
    [SerializeField] private TextMeshProUGUI m_TitleText;
    [SerializeField] private TextMeshProUGUI m_LevelValueText;

    [Header("Rewards")]
    [SerializeField] private TextMeshProUGUI m_Reward1AmountText;
    [SerializeField] private TextMeshProUGUI m_Reward2AmountText;
    [SerializeField] private Image m_Reward1Icon;
    [SerializeField] private Image m_Reward2Icon;

    [Header("Buttons")]
    [SerializeField] private Button m_ContinueButton;
    [SerializeField] private TextMeshProUGUI m_ContinueButtonText;

    [Header("Animation")]
    [SerializeField] private CanvasGroup m_CanvasGroup;
    [SerializeField] private float m_OpenFadeDuration = 0.25f;
    [SerializeField] private float m_CloseFadeDuration = 0.2f;

    private int m_LevelId;
    private bool m_HasNextLevel;

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
        PlayManager.Ins.OnWinContinue();
    }

    #endregion

    #region Public Methods

    public Button ContinueButton => m_ContinueButton;

    public void Configure(int _levelId, int _rankValue, int _reward1Amount = 0, int _reward2Amount = 0, bool _hasNextLevel = true)
    {
        m_LevelId = _levelId;
        m_HasNextLevel = _hasNextLevel;

        if (m_TitleText != null)
            m_TitleText.text = $"LEVEL {_levelId}";

        if (m_LevelValueText != null)
            m_LevelValueText.text = _levelId.ToString();

        if (m_Reward1AmountText != null)
            m_Reward1AmountText.text = _reward1Amount.ToString();

        if (m_Reward2AmountText != null)
            m_Reward2AmountText.text = _reward2Amount.ToString();

        if (m_ContinueButtonText != null)
            m_ContinueButtonText.text = _hasNextLevel ? "Continue" : "Home";

        RefreshHUD();
    }

    public void SetBackground(Sprite _sprite)
    {
        if (m_Background == null)
            return;

        m_Background.sprite = _sprite;
        m_Background.gameObject.SetActive(_sprite != null);
    }

    public void SetRewardIcon(int _index, Sprite _sprite)
    {
        Image icon = _index == 0 ? m_Reward1Icon : m_Reward2Icon;
        if (icon == null)
            return;

        icon.sprite = _sprite;
        icon.gameObject.SetActive(_sprite != null);
    }

    #endregion

    #region Private Methods

    private void RefreshHUD()
    {
    }

    #endregion
}
