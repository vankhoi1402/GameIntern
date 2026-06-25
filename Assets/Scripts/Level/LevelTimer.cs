using System;
using UnityEngine;

/// <summary>Đếm ngược thời gian level — pause khi khóa input hoặc settlement.</summary>
public class LevelTimer : MonoBehaviour
{
    [SerializeField] private BoardSettlementService m_SettlementService;

    public float RemainingSeconds { get; private set; }
    public int TimeLimitSeconds { get; private set; }
    public bool IsRunning { get; private set; }
    public bool HasTimeLimit => TimeLimitSeconds > 0;
    public bool IsExpired => HasTimeLimit && RemainingSeconds <= 0f;

    public event Action OnExpired;

    private void Awake()
    {
        if (m_SettlementService == null)
            m_SettlementService = FindObjectOfType<BoardSettlementService>();
    }

    private void Update()
    {
        if (!IsRunning || IsPaused())
            return;

        RemainingSeconds -= Time.deltaTime;
        if (RemainingSeconds > 0f)
            return;

        RemainingSeconds = 0f;
        IsRunning = false;
        OnExpired?.Invoke();
    }

    public void StartTimer(int timeLimitSeconds)
    {
        TimeLimitSeconds = Mathf.Max(0, timeLimitSeconds);
        RemainingSeconds = TimeLimitSeconds;

        if (TimeLimitSeconds <= 0)
        {
            IsRunning = false;
            return;
        }

        IsRunning = true;
    }

    public void StopTimer()
    {
        IsRunning = false;
        RemainingSeconds = 0f;
        TimeLimitSeconds = 0;
    }

    private bool IsPaused()
    {
        if (InputManager.Instance != null && InputManager.Instance.IsInputLocked)
            return true;

        return m_SettlementService != null && m_SettlementService.IsRunning;
    }
}
