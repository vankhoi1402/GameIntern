using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayUI : UICanvas
{
    #region Private Fields

    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI m_LevelText;
    [SerializeField] private TextMeshProUGUI m_TimeText;
    [SerializeField] private Image m_Background;

    [Header("Buttons")]
    [SerializeField] private Button m_PauseButton;
    [SerializeField] private Button m_SettingsButton;

    [Header("Booster Tutorial")]
    [SerializeField] private GameObject m_BoosterTutorialBG;
    [SerializeField] private Image m_BoosterTutorialIcon;

    [Header("Booster Button")]
    [SerializeField] private Button m_HintButton;
    [SerializeField] private Button m_SkipButton;
    [SerializeField] private Button m_MagnetButton;
    [SerializeField] private Button m_ShuffleButton;
    [SerializeField] private Button m_FreezeButton;

    [Header("Animation")]
    [SerializeField] private CanvasGroup m_CanvasGroup;
    [SerializeField] private float m_OpenFadeDuration = 0.25f;
    [SerializeField] private float m_CloseFadeDuration = 0.2f;

    private int m_CurrentLevelId;
    private int m_CurrentScore;

    #endregion

    #region UICanvas Overrides

    public override void Setup()
    {
        base.Setup();
        RegisterBoosterButtonListeners();
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
        if (PlayManager.Ins.IsPaused)
        {
            PlayManager.Ins.OnResumeGame();
            return;
        }

        PlayManager.Ins.OnPauseGame();
    }

    #endregion

    #region Public Methods

    public Button PauseButton => m_PauseButton;
    public Button SettingsButton => m_SettingsButton;

    public void Configure(int _levelId, float _timeLimit = -1f)
    {
        m_CurrentLevelId = _levelId;
        m_CurrentScore = 0;

        if (m_LevelText != null)
            m_LevelText.text = $"Level {_levelId}";

        if (m_TimeText != null)
            m_TimeText.text = _timeLimit > 0f ? FormatTime(_timeLimit) : "--:--";
    }

    public void UpdateTime(float _remainingTime)
    {
        if (m_TimeText != null)
            m_TimeText.text = FormatTime(Mathf.Max(0f, _remainingTime));
    }

    public void UpdateScore(int _score)
    {
        m_CurrentScore = _score;
    }

    public void SetBackground(Sprite _sprite)
    {
        if (m_Background == null)
            return;

        m_Background.sprite = _sprite;
        m_Background.gameObject.SetActive(_sprite != null);
    }

    public void ActiveBoosterTutorialBG(bool _active, BoosterType _boosterType)
    {
        if (m_BoosterTutorialBG == null)
            return;

        m_BoosterTutorialBG.SetActive(_active);

        if (_active && m_BoosterTutorialIcon != null)
        {
            // TODO: gán sprite theo BoosterType nếu có BoosterDatabase
        }
    }

    public void OnHintButtonClicked()
    {
        PlayManager.Ins.hintBooster();
    }

    public void OnSkipButtonClicked()
    {
        PlayManager.Ins.LoadNextLevel();
    }

    public void OnMagnetButtonClicked()
    {
        PlayManager.Ins.MagnetBooster();
    }

    public void OnShuffleButtonClicked()
    {
        PlayManager.Ins.ShuffleBoard();
    }

    public void OnFreezeButtonClicked()
    {
        PlayManager.Ins.FreezeTimer();
    }

    #endregion

    #region Private Methods

    private void RegisterBoosterButtonListeners()
    {
        if (m_HintButton != null)
            m_HintButton.onClick.AddListener(OnHintButtonClicked);

        if (m_SkipButton != null)
            m_SkipButton.onClick.AddListener(OnSkipButtonClicked);

        if (m_MagnetButton != null)
            m_MagnetButton.onClick.AddListener(OnMagnetButtonClicked);

        if (m_ShuffleButton != null)
            m_ShuffleButton.onClick.AddListener(OnShuffleButtonClicked);

        if (m_FreezeButton != null)
            m_FreezeButton.onClick.AddListener(OnFreezeButtonClicked);
    }

    private void RefreshHUD()
    {
    }

    private static string FormatTime(float _seconds)
    {
        int totalSeconds = Mathf.CeilToInt(_seconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    #endregion
}
