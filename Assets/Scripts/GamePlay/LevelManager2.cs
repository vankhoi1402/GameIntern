using System;

using UnityEngine;



/// <summary>Quản lý gameplay level — load, timer, win/lose, refill state.</summary>

public class LevelManager2 : MonoBehaviour

{

    #region References



    [Header("Data")]

    [SerializeField] private LevelCatalog m_Catalog;

    [SerializeField] private BlockDatabase m_BlockDatabase;

    [SerializeField] private int m_StartLevelId = 1;

    [SerializeField] private int m_VisibleRows = LevelLoader.DefaultVisibleRows;



    [Header("Board")]

    [SerializeField] private BoardManager m_BoardManager;

    [Header("Gameplay")]

    [SerializeField] private PlayManager m_PlayManager;



    [Header("UI")]

    [SerializeField] private VictoryUIController m_VictoryUI;

    [SerializeField] private DefeatUIController m_DefeatUI;



    #endregion



    #region Runtime State



    private readonly LevelLoader m_Loader = new LevelLoader();

    private readonly LevelRefillState m_RefillState = new LevelRefillState();



    private LevelData m_CurrentLevel;

    private bool m_OutcomeResolved;



    #endregion



    #region Timer State



    public float RemainingSeconds { get; private set; }

    public int TimeLimitSeconds { get; private set; }

    public bool IsTimerRunning { get; private set; }

    public bool HasTimeLimit => TimeLimitSeconds > 0;

    public bool IsTimerExpired => HasTimeLimit && RemainingSeconds <= 0f;



    public event Action OnTimerExpired;



    #endregion



    #region Properties



    public LevelData CurrentLevel => m_CurrentLevel;

    public bool HasLevel => m_CurrentLevel != null;

    public int CurrentLevelId => m_CurrentLevel != null ? m_CurrentLevel.LevelId : m_StartLevelId;

    public bool IsOutcomeResolved => m_OutcomeResolved;



    #endregion



    #region Events



    public event Action<LevelData> OnLevelLoaded;



    #endregion



    #region Unity Lifecycle



    private void Start()
    {
        LoadStartLevel();
    }

    private void OnEnable()

    {

        if (m_PlayManager != null)

            m_PlayManager.OnSettlementCompleted += HandleSettlementCompleted;



        BindPopupEvents(true);

    }



    private void OnDisable()

    {

        if (m_PlayManager != null)

            m_PlayManager.OnSettlementCompleted -= HandleSettlementCompleted;



        BindPopupEvents(false);

    }



    private void Update()

    {

        TickTimer();

    }



    #endregion



    #region Load Level



    public void LoadStartLevel()

    {

        if (m_Catalog == null || m_Catalog.Levels == null || m_Catalog.Levels.Length == 0)

        {

            Debug.LogWarning("[LevelManager2] Chưa có LevelCatalog.");

            return;

        }



        LevelCatalogEntry entry = m_Catalog.FindById(m_StartLevelId);

        LevelData level = m_Loader.LoadFromEntry(entry, m_BlockDatabase, m_VisibleRows);



        if (level == null)

        {

            Debug.LogError($"[LevelManager2] Level {m_StartLevelId} chưa có dữ liệu. Chạy Import CSV trước.");

            return;

        }



        LoadLevel(level);

    }



    public void LoadLevel(LevelData level)

    {

        if (level == null || m_BoardManager == null || m_PlayManager == null)

            return;



        m_CurrentLevel = level;

        m_RefillState.Reset(level);

        StopTimer();



        if (m_BlockDatabase != null)

            m_Loader.ValidateAndLog(level, m_BlockDatabase);



        ResetOutcomeSession();

        SetInputLocked(true);

        SpawnBoardLayout(level, OnBoardLayoutReady);

    }



    public void ReloadCurrentLevel()

    {

        if (m_CurrentLevel != null)

            LoadLevel(m_CurrentLevel);

    }



    public bool TryLoadNextLevel()

    {

        if (m_Catalog == null || !m_Catalog.TryGetNextAfter(CurrentLevelId, out LevelCatalogEntry next))

            return false;



        LevelData level = m_Loader.LoadFromEntry(next, m_BlockDatabase, m_VisibleRows);

        if (level == null)

            return false;



        LoadLevel(level);

        return true;

    }



    public void LoadNextLevel()

    {

        TryLoadNextLevel();

    }



    public bool HasNextLevel()

    {

        return m_Catalog != null && m_Catalog.TryGetNextAfter(CurrentLevelId, out _);

    }



    #endregion



    #region Refill



    public LevelBinBlock? TryDequeueBin(int col) => m_RefillState.TryDequeue(col);



    #endregion



    #region Timer



    public void StartTimer(int timeLimitSeconds)

    {

        TimeLimitSeconds = Mathf.Max(0, timeLimitSeconds);

        RemainingSeconds = TimeLimitSeconds;

        IsTimerRunning = TimeLimitSeconds > 0;

    }



    public void StopTimer()

    {

        IsTimerRunning = false;

        RemainingSeconds = 0f;

        TimeLimitSeconds = 0;

    }



    private void TickTimer()

    {

        if (!IsTimerRunning || IsTimerPaused())

            return;



        RemainingSeconds -= Time.deltaTime;

        if (RemainingSeconds > 0f)

            return;



        RemainingSeconds = 0f;

        IsTimerRunning = false;

        OnTimerExpired?.Invoke();

        TryResolveOutcome();

    }



    private bool IsTimerPaused()

    {

        return m_PlayManager != null

            && (m_PlayManager.IsInputLocked || m_PlayManager.IsSettlementRunning);

    }



    #endregion



    #region Win / Lose



    public void TryResolveOutcome()

    {

        if (m_OutcomeResolved)

            return;



        if (IsLevelCleared())

        {

            EnterWin();

            return;

        }



        if (IsTimerExpired)

            EnterLose();

    }



    private bool IsLevelCleared()

    {

        if (m_BoardManager == null || !m_BoardManager.IsGridReady)

            return false;



        return m_BoardManager.IsBoardEmpty() && !m_RefillState.HasRemainingBlocks();

    }



    private void EnterWin()

    {

        m_OutcomeResolved = true;

        StopTimer();

        CancelGameplayInput();

        SetInputLocked(true);

        m_DefeatUI?.Hide(animated: false);



        int levelId = CurrentLevelId;

        bool hasNext = HasNextLevel();

        m_VictoryUI?.Show(levelId, hasNext);

    }



    private void EnterLose()

    {

        m_OutcomeResolved = true;

        StopTimer();

        CancelGameplayInput();

        SetInputLocked(true);

        m_VictoryUI?.Hide(animated: false);

        m_DefeatUI?.Show(CurrentLevelId);

    }



    #endregion



    #region Private Handlers



    private void OnBoardLayoutReady()

    {

        if (m_CurrentLevel == null)

            return;



        SetInputLocked(false);

        StartTimer(m_CurrentLevel.TimeLimitSeconds);



        Debug.Log($"[LevelManager2] Loaded level {m_CurrentLevel.LevelId}" +

                  (m_CurrentLevel.TimeLimitSeconds > 0

                      ? $" — time limit {m_CurrentLevel.TimeLimitSeconds}s"

                      : string.Empty));



        OnLevelLoaded?.Invoke(m_CurrentLevel);

    }



    private void HandleSettlementCompleted(bool _)

    {

        TryResolveOutcome();

    }



    private void HandleNextLevelClicked()

    {

        m_VictoryUI?.Hide(() =>

        {

            if (!TryLoadNextLevel())

                ResetOutcomeSession();

        });

    }



    private void HandleRetryClicked()

    {

        m_DefeatUI?.Hide(ReloadCurrentLevel);

    }



    #endregion



    #region Board Layout



    private void SpawnBoardLayout(LevelData level, Action onComplete)
    {
        m_PlayManager.SpawnLevelBoard(level);
        onComplete?.Invoke();
    }

    #endregion



    #region Session



    private void ResetOutcomeSession()

    {

        m_OutcomeResolved = false;

        m_VictoryUI?.Hide(animated: false);

        m_DefeatUI?.Hide(animated: false);

        SetInputLocked(false);

    }



    private void SetInputLocked(bool locked)

    {

        m_PlayManager?.SetLockInput(locked);

    }



    private void CancelGameplayInput()

    {

        m_PlayManager?.CancelActiveDrag();

    }



    #endregion



    #region UI Binding



    private void BindPopupEvents(bool subscribe)

    {

        if (m_VictoryUI != null)

        {

            m_VictoryUI.OnNextClicked -= HandleNextLevelClicked;

            if (subscribe)

                m_VictoryUI.OnNextClicked += HandleNextLevelClicked;

        }



        if (m_DefeatUI != null)

        {

            m_DefeatUI.OnRetryClicked -= HandleRetryClicked;

            if (subscribe)

                m_DefeatUI.OnRetryClicked += HandleRetryClicked;

        }

    }



    #endregion

    #region Refill Bin State

    private sealed class LevelRefillState
    {
        private static readonly LevelLoader s_CellParser = new LevelLoader();

        private readonly System.Collections.Generic.Dictionary<int, LevelGridRow> m_RowsByDepth =
            new System.Collections.Generic.Dictionary<int, LevelGridRow>();

        private readonly int[] m_NextDepthByCol = new int[LevelLoader.GridColumnCount];
        private int m_MaxDepth = -1;
        private int m_VisibleRows;

        public void Reset(LevelData level)
        {
            m_RowsByDepth.Clear();
            m_MaxDepth = -1;
            m_VisibleRows = level != null ? level.VisibleRows : LevelLoader.DefaultVisibleRows;

            for (int col = 0; col < LevelLoader.GridColumnCount; col++)
                m_NextDepthByCol[col] = m_VisibleRows;

            if (level?.Rows == null)
                return;

            foreach (LevelGridRow row in level.Rows)
            {
                m_RowsByDepth[row.Row] = row;
                if (row.Row > m_MaxDepth)
                    m_MaxDepth = row.Row;
            }
        }

        public LevelBinBlock? TryDequeue(int col)
        {
            if (col < 0 || col >= LevelLoader.GridColumnCount)
                return null;

            while (m_NextDepthByCol[col] <= m_MaxDepth)
            {
                int depth = m_NextDepthByCol[col]++;

                if (!m_RowsByDepth.TryGetValue(depth, out LevelGridRow row))
                    continue;

                if (row.ColBlockTypes == null || col >= row.ColBlockTypes.Length)
                    continue;

                if (s_CellParser.TryParseCell(row.ColBlockTypes[col], out string typeKey, out int stack))
                {
                    return new LevelBinBlock
                    {
                        BlockType = typeKey,
                        Stack = stack
                    };
                }
            }

            return null;
        }

        public bool HasRemainingBlocks()
        {
            for (int col = 0; col < LevelLoader.GridColumnCount; col++)
            {
                if (HasRemainingInColumn(col))
                    return true;
            }

            return false;
        }

        private bool HasRemainingInColumn(int col)
        {
            int savedDepth = m_NextDepthByCol[col];

            while (m_NextDepthByCol[col] <= m_MaxDepth)
            {
                int depth = m_NextDepthByCol[col]++;

                if (!m_RowsByDepth.TryGetValue(depth, out LevelGridRow row))
                    continue;

                if (row.ColBlockTypes == null || col >= row.ColBlockTypes.Length)
                    continue;

                if (s_CellParser.TryParseCell(row.ColBlockTypes[col], out _, out _))
                {
                    m_NextDepthByCol[col] = savedDepth;
                    return true;
                }
            }

            m_NextDepthByCol[col] = savedDepth;
            return false;
        }
    }

    #endregion

}

