using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class CheatUI : UICanvas
{
    [Header("Close")]
    [SerializeField] private Button m_CloseButton;

    [Header("Go To Level")]
    [SerializeField] private TMP_InputField m_GoToLevelInput;
    [SerializeField] private Button m_ConfirmGoToLevelButton;

    [Header("Set Time")]
    [SerializeField] private TMP_InputField m_TimeInput;
    [SerializeField] private Button m_ConfirmTimeButton;

    [Header("Outcome")]
    [SerializeField] private Button m_WinButton;
    [SerializeField] private Button m_LoseButton;

    [Header("Booster")]
    [SerializeField] private Button m_AddBoosterButton;

    [Header("Save")]
    [SerializeField] private Button m_ResetSaveButton;

    private void Awake()
    {
        BindButton(m_CloseButton, () => Close(0f));
        BindButton(m_ConfirmGoToLevelButton, OnGoToLevelCheat);
        BindButton(m_ConfirmTimeButton, OnSetTimeCheat);
        BindButton(m_WinButton, OnWinCheat);
        BindButton(m_LoseButton, OnLoseCheat);
        BindButton(m_AddBoosterButton, OnAddBoosterCheat);
        BindButton(m_ResetSaveButton, OnResetSaveCheat);
    }

    public override void Open()
    {
        base.Open();
        transform.SetAsLastSibling();
        UIManager.Ins.IsBlockRay = true;

        if (PlayManager.Exists() && !PlayManager.Ins.IsPaused)
            PlayManager.Ins.OnPauseGame();
        UIManager.Ins.OpenUI<CheatUI>();
    }

    public override void Close(float _delayTime)
    {
        base.Close(_delayTime);
        UIManager.Ins.IsBlockRay = false;
        PlayManager.Ins.OnResumeGame();
    }

    private static void BindButton(Button _button, UnityEngine.Events.UnityAction _action)
    {
        if (_button == null || _action == null)
            return;

        _button.onClick.RemoveListener(_action);
        _button.onClick.AddListener(_action);
    }

    private void OnGoToLevelCheat()
    {
        if (!PlayManager.Exists())
        {
            Debug.LogWarning("[CheatUI] PlayManager chưa sẵn sàng.");
            Close(0f);
            return;
        }

        if (m_GoToLevelInput != null && int.TryParse(m_GoToLevelInput.text, out int levelId))
        {
            Debug.Log($"[CHEAT] Go to level: {levelId}");
            PlayManager.Ins.CheatGoToLevel(levelId);
            m_GoToLevelInput.text = string.Empty;
        }
        else
        {
            Debug.LogWarning("[CheatUI] Level input không hợp lệ.");
        }

        Close(0f);
    }

    private void OnSetTimeCheat()
    {
        if (!PlayManager.Exists())
        {
            Debug.LogWarning("[CheatUI] PlayManager chưa sẵn sàng.");
            Close(0f);
            return;
        }

        if (m_TimeInput != null && int.TryParse(m_TimeInput.text, out int timeValue))
        {
            Debug.Log($"[CHEAT] Set time: {timeValue}s");
            PlayManager.Ins.CheatSetTime(timeValue);
            m_TimeInput.text = string.Empty;
        }
        else
        {
            Debug.LogWarning("[CheatUI] Time input không hợp lệ.");
        }

        Close(0f);
    }

    private void OnWinCheat()
    {
        if (!PlayManager.Exists())
        {
            Debug.LogWarning("[CheatUI] PlayManager chưa sẵn sàng.");
            Close(0f);
            return;
        }

        Debug.Log("[CHEAT] Force win.");
        PlayManager.Ins.CheatWin();
        Close(0f);
    }

    private void OnLoseCheat()
    {
        if (!PlayManager.Exists())
        {
            Debug.LogWarning("[CheatUI] PlayManager chưa sẵn sàng.");
            Close(0f);
            return;
        }

        Debug.Log("[CHEAT] Force lose.");
        PlayManager.Ins.CheatLose();
        Close(0f);
    }

    private void OnAddBoosterCheat()
    {
        if (!PlayManager.Exists())
        {
            Debug.LogWarning("[CheatUI] PlayManager chưa sẵn sàng.");
            Close(0f);
            return;
        }

        Debug.Log("[CHEAT] Add 100 booster each.");
        PlayManager.Ins.CheatAddBoosters();
        Close(0f);
    }

    private void OnResetSaveCheat()
    {
        if (!PlayManager.Exists())
        {
            Debug.LogWarning("[CheatUI] PlayManager chưa sẵn sàng.");
            Close(0f);
            return;
        }

        Debug.Log("[CHEAT] Reset save.");
        PlayManager.Ins.CheatResetSave();
        Close(0f);
    }
}
