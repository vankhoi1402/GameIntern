using TMPro;
using UnityEngine;

/// <summary>Hiển thị đếm ngược — gắn trên TimePanel (tạo tay trong scene).</summary>
public class LevelTimerUI : MonoBehaviour
{
    [SerializeField] private GameObject m_Root;
    [SerializeField] private TMP_Text m_TimeText;
    [SerializeField] private LevelManager2 m_LevelManager;
    [SerializeField] private Color m_WarningColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private float m_WarningThresholdSeconds = 10f;

    private Color m_DefaultColor = Color.white;
    private bool m_Initialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (m_LevelManager != null)
            m_LevelManager.OnLevelLoaded -= HandleLevelLoaded;
    }

    private void Update()
    {
        if (!m_Initialized || m_LevelManager == null || !m_LevelManager.IsTimerRunning)
            return;

        if (m_TimeText == null)
            return;

        float remaining = m_LevelManager.RemainingSeconds;
        m_TimeText.text = FormatTime(remaining);
        m_TimeText.color = remaining <= m_WarningThresholdSeconds
            ? m_WarningColor
            : m_DefaultColor;
    }

    private void HandleLevelLoaded(LevelData level)
    {
        bool show = level != null && level.TimeLimitSeconds > 0;
        SetPanelVisible(show);

        if (!show || m_TimeText == null || level == null)
            return;

        m_TimeText.text = FormatTime(level.TimeLimitSeconds);
        m_TimeText.color = level.TimeLimitSeconds <= m_WarningThresholdSeconds
            ? m_WarningColor
            : m_DefaultColor;
    }

    private void EnsureInitialized()
    {
        if (m_Initialized)
            return;

        m_Initialized = true;

        if (m_Root == null)
            m_Root = gameObject;

        if (m_TimeText == null)
            m_TimeText = GetComponentInChildren<TMP_Text>(true);

        if (m_TimeText == null)
            Debug.LogWarning("[LevelTimerUI] Chưa tìm thấy TimeText (TMP) dưới TimePanel.");

        if (m_TimeText != null)
            m_DefaultColor = m_TimeText.color;

        if (m_LevelManager == null)
            m_LevelManager = FindObjectOfType<LevelManager2>();

        if (m_LevelManager != null)
        {
            m_LevelManager.OnLevelLoaded += HandleLevelLoaded;

            if (m_LevelManager.HasLevel)
                HandleLevelLoaded(m_LevelManager.CurrentLevel);
            else
                SetPanelVisible(false);
        }
        else
        {
            SetPanelVisible(false);
        }
    }

    /// <summary>
    /// Không SetActive(false) lên chính gameObject chứa script — sẽ chặn OnEnable/event.
    /// Khi script gắn trên TimePanel: chỉ bật/tắt TimeText hoặc child Root khác.
    /// </summary>
    private void SetPanelVisible(bool show)
    {
        if (m_Root != null && m_Root != gameObject)
        {
            m_Root.SetActive(show);
            return;
        }

        if (m_TimeText != null)
            m_TimeText.gameObject.SetActive(show);
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = total / 60;
        int secs = total % 60;
        return $"{minutes:00}:{secs:00}";
    }
}
