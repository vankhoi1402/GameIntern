using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class HomeUI : UICanvas
{
    #region Private Fields

    [Header("HUD")]
    [SerializeField] private Image m_Background;

    [Header("Buttons")]
    [SerializeField] private Button m_PlayButton;

    [Header("Animation")]
    [SerializeField] private CanvasGroup m_CanvasGroup;
    [SerializeField] private float m_OpenFadeDuration = 0.25f;
    [SerializeField] private float m_CloseFadeDuration = 0.2f;

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
        // Application.Quit();
    }

    #endregion

    #region Public Methods

    public Button PlayButton => m_PlayButton;

    public void Configure()
    {
        RefreshHUD();
    }

    public void SetBackground(Sprite _sprite)
    {
        if (m_Background == null)
            return;

        m_Background.sprite = _sprite;
        m_Background.gameObject.SetActive(_sprite != null);
    }

    #endregion

    #region Private Methods

    private void RefreshHUD()
    {
    }

    #endregion
}
