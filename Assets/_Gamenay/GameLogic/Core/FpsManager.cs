using UnityEngine;

/// <summary>Giới hạn FPS và tắt VSync để gameplay ổn định trên build.</summary>
[DefaultExecutionOrder(-10000)]
public class FpsManager : MonoBehaviour
{
    #region Constants
    private const int c_DefaultTargetFps = 60;
    private const int c_MinTargetFps = 30;
    #endregion

    #region Serialized Fields
    [SerializeField] private int m_TargetFps = c_DefaultTargetFps;
    [SerializeField] private bool m_DisableVSync = true;
    [SerializeField] private bool m_KeepScreenAwakeOnMobile = true;
    #endregion

    #region Private Fields
    private static FpsManager s_Instance;
    #endregion

    #region Unity Lifecycle
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (s_Instance != null)
            return;

        var host = new GameObject(nameof(FpsManager));
        s_Instance = host.AddComponent<FpsManager>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplySettings();
    }
    #endregion

    #region Public Methods
    public void ApplySettings()
    {
        if (m_DisableVSync)
            QualitySettings.vSyncCount = 0;

        Application.targetFrameRate = Mathf.Max(c_MinTargetFps, m_TargetFps);

        if (m_KeepScreenAwakeOnMobile)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    public void SetTargetFps(int targetFps)
    {
        m_TargetFps = Mathf.Max(c_MinTargetFps, targetFps);
        Application.targetFrameRate = m_TargetFps;
    }
    #endregion
}
