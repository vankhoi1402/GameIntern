using UnityEngine;

/// <summary>Điều phối thắng/thua, khóa input, load level tiếp.</summary>
public class LevelFlowController : MonoBehaviour
{
    [SerializeField] private BoardSettlementService m_SettlementService;
    [SerializeField] private BoardManager m_BoardManager;
    [SerializeField] private LevelManager m_LevelManager;
    [SerializeField] private VictoryUIController m_VictoryUI;

    private LevelOutcomeEvaluator m_Evaluator;
    private bool m_OutcomeResolved;

    private void Awake()
    {
        if (m_SettlementService == null)
            m_SettlementService = FindObjectOfType<BoardSettlementService>();
        if (m_BoardManager == null)
            m_BoardManager = FindObjectOfType<BoardManager>();
        if (m_LevelManager == null)
            m_LevelManager = FindObjectOfType<LevelManager>();
        if (m_VictoryUI == null)
            m_VictoryUI = FindObjectOfType<VictoryUIController>(true);
    }

    private void OnEnable()
    {
        if (m_SettlementService != null)
            m_SettlementService.OnSettlementCompleted += HandleSettlementCompleted;

        if (m_LevelManager != null)
            m_LevelManager.OnLevelLoaded += HandleLevelLoaded;

        if (m_VictoryUI != null)
            m_VictoryUI.OnNextClicked += HandleNextLevelClicked;
    }

    private void OnDisable()
    {
        if (m_SettlementService != null)
            m_SettlementService.OnSettlementCompleted -= HandleSettlementCompleted;

        if (m_LevelManager != null)
            m_LevelManager.OnLevelLoaded -= HandleLevelLoaded;

        if (m_VictoryUI != null)
            m_VictoryUI.OnNextClicked -= HandleNextLevelClicked;
    }

    private void Start()
    {
        RebuildEvaluator();
    }

    private void HandleLevelLoaded(LevelData level)
    {
        ResetSession();
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
        }
    }

    private void EnterWin()
    {
        m_OutcomeResolved = true;
        SetInputLocked(true);

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

    private void HandleNextLevelClicked()
    {
        SetInputLocked(false);

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

    private void ResetSession()
    {
        m_OutcomeResolved = false;
        m_VictoryUI?.Hide(animated: false);
        SetInputLocked(false);
        RebuildEvaluator();
    }

    private void RebuildEvaluator()
    {
        if (m_BoardManager == null || m_LevelManager == null)
            return;

        m_Evaluator = new LevelOutcomeEvaluator(m_BoardManager, m_LevelManager.RefillState);
    }

    private static void SetInputLocked(bool locked)
    {
        if (InputManager.Instance != null)
            InputManager.Instance.SetLockInput(locked);
    }
}
