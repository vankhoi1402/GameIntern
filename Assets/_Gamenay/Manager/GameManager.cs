using System.Collections;
using UnityEngine;

public enum GameState
{
    Home,
    Play,
}

public class GameManager : Singleton<GameManager>
{
    [Header("Input Block")]
    public GameObject block;

    [Header("Gameplay")]
    [SerializeField] private PlayManager m_PlayManager;

    [Header("Back Canvas")]
    [SerializeField] private GameObject m_HomeBg;
    [SerializeField] private GameObject m_GameplayBg;

    public GameState GameState { get; private set; }
    public bool IsCheat { get; set; }
    public bool RemoveAds { get; set; }

    private IEnumerator Start()
    {
        yield return null;

        Application.targetFrameRate = 60;
        Input.multiTouchEnabled = false;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        Block(false);

        BoosterManager.Ins.Initialized();

        OnHomeState();
        // Ví dụ trong GameManager.Start() hoặc script bootstrap
        AdsManager.Ins.Initialize(() =>
        {
            Debug.Log("Ads ready");
            // Có thể show banner ở đây
            AdsManager.Ins.ShowBanner();
        });
    }

    public void Block(bool _active)
    {
        if (block != null)
            block.SetActive(_active);
    }

    public bool IsBlock => block != null && block.activeSelf;

    public void OnHomeState()
    {
        GameState = GameState.Home;
        Time.timeScale = 1f;
        Block(false);

        UIManager.Ins.CloseUI<GamePlayUI>();
        UIManager.Ins.CloseUI<WinUI>();
        UIManager.Ins.CloseUI<LoseUI>();
        UIManager.Ins.OpenUI<HomeUI>();

        SetBackCanvas(home: true);
        GetPlayManager()?.OnCloseLevel();
        GetPlayManager()?.EnsureHomeFlowButtons();
    }

    public void OnPlayState()
    {
        GameState = GameState.Play;
        UIManager.Ins.CloseUI<HomeUI>(0f);

        SetBackCanvas(home: false);
        GetPlayManager()?.OnStartPlay();
    }

    private void SetBackCanvas(bool home)
    {
        if (m_HomeBg != null)
            m_HomeBg.SetActive(home);

        if (m_GameplayBg != null)
            m_GameplayBg.SetActive(!home);
    }

    private PlayManager GetPlayManager()
    {
        if (m_PlayManager == null)
            m_PlayManager = PlayManager.Ins;

        return m_PlayManager;
    }
}
