using UnityEngine;

/// <summary>Điều phối thắng/thua, khóa input, load level tiếp / chơi lại.</summary>
public class LevelFlowController : MonoBehaviour
{
    [SerializeField] private BoardSettlementService m_SettlementService;
    [SerializeField] private BoardManager m_BoardManager;
    [SerializeField] private LevelManager m_LevelManager;
    [SerializeField] private LevelTimer m_LevelTimer;
    [SerializeField] private VictoryUIController m_VictoryUI;
    [SerializeField] private DefeatUIController m_DefeatUI;
    [SerializeField] private DragController m_DragController;

    private LevelOutcomeEvaluator m_Evaluator;
    private bool m_OutcomeResolved;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (m_SettlementService != null)
            m_SettlementService.OnSettlementCompleted += HandleSettlementCompleted;

        if (m_LevelManager != null)
            m_LevelManager.OnLevelLoaded += HandleLevelLoaded;

        BindTimerExpired(true);

        BindPopupEvents();
    }

    private void OnDisable()
    {
        if (m_SettlementService != null)
            m_SettlementService.OnSettlementCompleted -= HandleSettlementCompleted;

        if (m_LevelManager != null)
            m_LevelManager.OnLevelLoaded -= HandleLevelLoaded;

        BindTimerExpired(false);

        if (m_VictoryUI != null)
            m_VictoryUI.OnNextClicked -= HandleNextLevelClicked;

        if (m_DefeatUI != null)
            m_DefeatUI.OnRetryClicked -= HandleRetryClicked;
    }

    private void Start()
    {
        BindTimerExpired(true);
        RebuildEvaluator();
    }

    private void HandleLevelLoaded(LevelData level)
    {
        ResolveReferences();
        BindTimerExpired(true);
        ResetSession();
    }

    private void HandleTimeExpired()
    {
        TryResolveOutcome();
    }

    private void HandleSettlementCompleted(bool includeRefill)
    {
        TryResolveOutcome();
    }

    private void TryResolveOutcome()
    {
        if (m_OutcomeResolved || m_Evaluator == null)
            return;

        switch (m_Evaluator.Evaluate())
        {
            case LevelOutcome.Won:
                EnterWin();
                break;
            case LevelOutcome.Lost:
                EnterLose();
                break;
        }
    }

    private void EnterWin()
    {
        m_OutcomeResolved = true;
        m_LevelTimer?.StopTimer();
        CancelGameplayInput();
        SetInputLocked(true);
        m_DefeatUI?.Hide(animated: false);

        ResolveReferences();

        int levelId = m_LevelManager != null ? m_LevelManager.CurrentLevelId : 0;
        bool hasNext = m_LevelManager != null && m_LevelManager.HasNextLevel();

        if (m_VictoryUI == null)
        {
            Debug.LogWarning("[LevelFlow] Chưa gán VictoryUIController — không hiện UI thắng.");
            return;
        }

        m_VictoryUI.Show(levelId, hasNext);
        Debug.Log($"[LevelFlow] Thắng level {levelId} — VictoryUI hiển thị.");
    }

    private void EnterLose()
    {
        m_OutcomeResolved = true;
        m_LevelTimer?.StopTimer();
        CancelGameplayInput();
        SetInputLocked(true);
        m_VictoryUI?.Hide(animated: false);

        int levelId = m_LevelManager != null ? m_LevelManager.CurrentLevelId : 0;

        ResolveReferences();

        if (m_DefeatUI == null)
        {
            Debug.LogWarning("[LevelFlow] Chưa gán DefeatUIController — không hiện UI thua.");
            Debug.Log($"[LevelFlow] Thua level {levelId} — hết thời gian.");
            return;
        }

        m_DefeatUI.Show(levelId);
        Debug.Log($"[LevelFlow] Thua level {levelId} — DefeatUI hiển thị.");
    }

    private void HandleNextLevelClicked()
    {
        if (m_VictoryUI == null)
            return;

        m_VictoryUI.Hide(() =>
        {
            if (m_LevelManager == null)
                return;

            if (!m_LevelManager.TryLoadNextLevel())
            {
                Debug.Log("[LevelFlow] Hết level trong catalog.");
                ResetSession();
            }
        });
    }

    private void HandleRetryClicked()
    {
        if (m_DefeatUI == null || m_LevelManager == null)
            return;

        m_DefeatUI.Hide(() => m_LevelManager.ReloadCurrentLevel());
    }

    private void ResetSession()
    {
        m_OutcomeResolved = false;
        m_VictoryUI?.Hide(animated: false);
        m_DefeatUI?.Hide(animated: false);
        SetInputLocked(false);
        RebuildEvaluator();
    }

    private void ResolveReferences()
    {
        if (m_SettlementService == null)
            m_SettlementService = FindObjectOfType<BoardSettlementService>();
        if (m_BoardManager == null)
            m_BoardManager = FindObjectOfType<BoardManager>();
        if (m_LevelManager == null)
            m_LevelManager = FindObjectOfType<LevelManager>();

        if (m_LevelTimer == null && m_LevelManager != null)
            m_LevelTimer = m_LevelManager.GetComponent<LevelTimer>();

        if (m_LevelTimer == null)
            m_LevelTimer = FindObjectOfType<LevelTimer>();

        if (m_VictoryUI == null)
            m_VictoryUI = FindObjectOfType<VictoryUIController>(true);

        if (m_DefeatUI == null)
            m_DefeatUI = FindObjectOfType<DefeatUIController>(true);

        EnsurePopupController(
            ref m_VictoryUI,
            "PopupDim_Play_Result_Victory",
            root => root.GetComponent<VictoryUIController>() ?? root.gameObject.AddComponent<VictoryUIController>());

        EnsurePopupController(
            ref m_DefeatUI,
            "PopupDim_Play_Result_Defeat",
            root => root.GetComponent<DefeatUIController>() ?? root.gameObject.AddComponent<DefeatUIController>());

        if (m_DragController == null)
            m_DragController = FindObjectOfType<DragController>();

        BindPopupEvents();
    }

    private void BindPopupEvents()
    {
        if (m_VictoryUI != null)
        {
            m_VictoryUI.OnNextClicked -= HandleNextLevelClicked;
            m_VictoryUI.OnNextClicked += HandleNextLevelClicked;
        }

        if (m_DefeatUI != null)
        {
            m_DefeatUI.OnRetryClicked -= HandleRetryClicked;
            m_DefeatUI.OnRetryClicked += HandleRetryClicked;
        }
    }

    private void BindTimerExpired(bool subscribe)
    {
        if (m_LevelTimer == null)
            return;

        m_LevelTimer.OnExpired -= HandleTimeExpired;
        if (subscribe)
            m_LevelTimer.OnExpired += HandleTimeExpired;
    }

    private void RebuildEvaluator()
    {
        if (m_BoardManager == null || m_LevelManager == null)
            return;

        m_Evaluator = new LevelOutcomeEvaluator(
            m_BoardManager,
            m_LevelManager.RefillState,
            m_LevelTimer);
    }

    private static void SetInputLocked(bool locked)
    {
        if (InputManager.Instance != null)
            InputManager.Instance.SetLockInput(locked);
    }

    private void CancelGameplayInput()
    {
        if (m_DragController == null)
            m_DragController = FindObjectOfType<DragController>();

        m_DragController?.CancelActiveDrag();
    }

    private static void EnsurePopupController<T>(ref T controller, string popupName, System.Func<Transform, T> factory)
        where T : Component
    {
        if (controller != null)
            return;

        Transform root = FindPopupRoot(popupName);
        if (root == null)
            return;

        controller = factory(root);
    }

    private static Transform FindPopupRoot(string popupName)
    {
        foreach (Transform candidate in Object.FindObjectsOfType<Transform>(true))
        {
            if (candidate.name == popupName)
                return candidate;
        }

        return null;
    }
}
