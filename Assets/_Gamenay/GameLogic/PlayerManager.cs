
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using Lean.Pool;
using UnityEngine.Pool;


/// <summary>
/// Trung tâm điều phối gameplay — input, merge, settlement, spawn refill.
/// </summary>
public class PlayManager : Singleton<PlayManager>
{
    [Header("Block Pool vfx")]
    [SerializeField] private LeanGameObjectPool m_BlockPool;
    private GameObject m_CurrentBlockVfx;

    #region Constants

    private const float c_GravityAnimTimeout = 5f;
    private const float c_SettlementTimeout = 8f;
    private const float c_ClearFallDistance = 8f;
    private const float c_ClearFallDuration = 0.45f;
    private const float c_Tier2ExplodeDuration = 0.15f;
    private const float c_Tier2FallDuration = 0.4f;
    private const float c_HintDuration = 3f;
    private const float c_FreezeDuration = 10f;
    private const int c_DragOverlapBufferSize = 16;

    #endregion

    #region References

    [Header("Board")]
    [SerializeField] private BoardManager m_BoardManager;

    [Header("Blocks")]
    [SerializeField] private BlockManager m_BlockPrefab;
    [SerializeField] private BlockDatabase m_BlockDatabase;
    [SerializeField] private Transform m_BlockParent;

    [Header("Level")]
    [SerializeField] private LevelCatalog m_Catalog;
    [SerializeField] private int m_StartLevelId = 1;

    [Header("UI")]
    // [SerializeField] private VictoryUIController m_VictoryUI;
    //[SerializeField] private DefeatUIController m_DefeatUI;
    [SerializeField] private TextMeshProUGUI m_TimeLimitText;

    [Header("Input")]
    [SerializeField] private Camera m_MainCamera;
    [SerializeField] private LayerMask m_BlockLayer;
    [SerializeField] private float m_SameTypeOverlapThreshold = 0.50f;

    [Header("Refill Animation")]
    [SerializeField] private float m_RefillRiseDuration = 0.16f;
    [SerializeField] private float m_RefillPushDuration = 0.15f;
    [SerializeField] private Ease m_RefillPushEase = Ease.OutSine;

    [Header("Boosters")]
    [SerializeField] private FrostAnimation m_FrostAnimation;
    [SerializeField] private VisualBooster m_VisualBooster;

    #endregion

    #region Runtime State

    private readonly LevelLoader m_LevelLoader = new LevelLoader();
    private readonly LevelRefillState m_RefillState = new LevelRefillState();

    private LevelData m_CurrentLevel;
    private bool m_OutcomeResolved;

    private float m_RemainingSeconds;
    private int m_TimeLimitSeconds;
    private bool m_IsTimerRunning;

    private float m_TimeForHint;
    private float m_TimeForHintValue = 10f;

    private BlockManager m_DraggedBlock;
    private BlockManager m_HoverTargetBlock;
    private Slot m_SourceSlot;
    private Slot m_HoverSlot;
    private Vector3 m_DragOffset;

    private bool m_IsDragging;
    private bool m_IsInputLocked;
    private bool m_IsTurnProcessing;
    private bool m_IsSettlementRunning;

    private BlockManager m_PendingTier2TripleClearTarget;
    private bool m_PendingGravityAfterSettlement;

    private Coroutine m_SettlementCoroutine;
    private Coroutine m_HintCoroutine;
    private Coroutine m_FreezeCoroutine;
    private Coroutine m_MagnetCoroutine;
    private readonly List<BlockManager> m_ActiveHintBlocks = new List<BlockManager>();
    private readonly List<BlockManager> m_ShuffleBlocks = new List<BlockManager>(32);

    private bool m_IsTimerFrozen;

    private bool m_MagnetDeferSettlement;
    private BlockManager m_MagnetSurvivor;
    private MergeClearKind m_MagnetSurvivorClearKind;

    private bool m_MagnetIsPairMode;
    private bool m_IsPaused;

    private const float c_ComboVoiceWindow = 3f;
    private int m_ClearStreakCount;
    private float m_LastClearTime;

    #endregion

    #region Timer State

    public float RemainingSeconds => m_RemainingSeconds;
    public int TimeLimitSeconds => m_TimeLimitSeconds;
    public bool IsTimerRunning => m_IsTimerRunning;
    public bool IsTimerFrozen => m_IsTimerFrozen;
    public bool HasTimeLimit => m_TimeLimitSeconds > 0;
    public bool IsTimerExpired => HasTimeLimit && m_RemainingSeconds <= 0f;
    private bool m_IsFreezeAnimationPlaying;
    private const int c_ContinueBonusSeconds = 30;
    public event Action OnTimerExpired;

    #endregion

    #region Properties

    public LevelData CurrentLevel => m_CurrentLevel;
    public bool HasLevel => m_CurrentLevel != null;
    public int CurrentLevelId => m_CurrentLevel != null ? m_CurrentLevel.LevelId : SaveManager.CurrentLevel;
    public bool IsOutcomeResolved => m_OutcomeResolved;
    public bool IsPaused => m_IsPaused;

    #endregion

    #region Events

    public event Action<bool> OnSettlementCompleted;
    public event Action<LevelData> OnLevelLoaded;

    public bool IsSettlementRunning => m_IsSettlementRunning;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        OnSettlementCompleted += HandleSettlementCompleted;
    }

    private void OnDisable()
    {
        OnSettlementCompleted -= HandleSettlementCompleted;
        m_VisualBooster?.KillActiveVisual(false);
    }
    private void Start()
    {

        AnalyticManager.Ins.Initialize();
    }

    private void Update()
    {
        HandleInput();
        TickTimer();
        UpdateTimeForHint();
    }

    #endregion

    #region Flow Game

    public void OnPlayFromHome()
    {
        UIManager.Ins.CloseUI<HomeUI>();
        GameManager.Ins.OnPlayState();
    }
    public void OnHome()
    {
        UIManager.Ins.CloseUI<SettingUI>();
        GameManager.Ins.OnHomeState();
    }

    public void OnStartPlay()
    {
        m_IsPaused = false;
        Time.timeScale = 1f;
        GameManager.Ins.Block(false);

        UIManager.Ins.CloseUI<WinUI>();
        UIManager.Ins.CloseUI<LoseUI>();

        GamePlayUI gameplayUI = UIManager.Ins.OpenUI<GamePlayUI>();
        gameplayUI.Configure(CurrentLevelId, -1f);

        LoadStartLevel();
        EnsureGameplayFlowButtons();
        AnalyticManager.Ins.LogEvent("game_start");
    }

    public void OnQuitPlay()
    {
        SetLockInput(true);
        UIManager.Ins.CloseUI<GamePlayUI>();
        UIManager.Ins.CloseUI<WinUI>();
        UIManager.Ins.CloseUI<LoseUI>();
        m_CurrentLevel = null;

        GameManager.Ins.OnHomeState();
    }
    public void OnCloseSetting()
    {
        UIManager.Ins.CloseUI<SettingUI>();
        OnResumeGame();
    }

    public void OnCloseLevel()
    {
        m_IsPaused = false;
        Time.timeScale = 1f;
        GameManager.Ins.Block(false);

        ClearActiveHint();
        CancelActiveDrag();
        CancelFreeze();
        m_VisualBooster?.KillActiveVisual(false);
        m_FrostAnimation?.OffFrostImageBG();
        PrepareForLevelLoad();
        StopTimer();

        if (m_BoardManager != null)
            m_BoardManager.ClearAllBlocks();

        DestroyOrphanBlocks();
    }

    public void OnContinueWithBonusTime()
    {
        if (!m_OutcomeResolved || m_CurrentLevel == null)
            return;

        m_OutcomeResolved = false;
        m_RemainingSeconds += c_ContinueBonusSeconds;

        if (m_TimeLimitSeconds <= 0)
            m_TimeLimitSeconds = m_CurrentLevel.TimeLimitSeconds;

        m_IsTimerRunning = true;
        SetLockInput(false);

        UIManager.Ins.OpenUI<GamePlayUI>();
        RefreshGameplayHUD();
    }

    public void OnPauseGame()
    {
        if (m_IsPaused)
            return;

        m_IsPaused = true;
        Time.timeScale = 0f;
        GameManager.Ins.Block(true);
    }

    public void OnResumeGame()
    {
        if (!m_IsPaused)
            return;

        m_IsPaused = false;
        Time.timeScale = 1f;
        GameManager.Ins.Block(false);
    }

    public void OnOpenSettings()
    {
        UIManager.Ins.OpenUI<SettingUI>();
        OnPauseGame();
    }

    public void OnWinContinue()
    {
        UIManager.Ins.CloseUI<WinUI>();

        if (TryLoadNextLevel())
            return;

        OnQuitPlay();
    }

    public void OnLoseRetry()
    {
        UIManager.Ins.CloseUI<LoseUI>();
        ReloadCurrentLevel();
    }

    public void OnLoseContinueTime()
    {
        Debug.Log("Người chơi bấm nút +giây. Đang chuẩn bị gọi quảng cáo...");

        // 1. Gọi hàm hiển thị quảng cáo Reward từ AdsManager
        AdsManager.Ins.ShowRewardAds(() =>
        {
            // ==========================================================
            // KHU VỰC ĐÓN XỬ LÝ: CHỈ CHẠY KHI NGƯỜI CHƠI XEM HẾT SẠCH 30S
            // ==========================================================
            Debug.Log("Google xác nhận xem hết! Tiến hành cộng giây và cho chơi tiếp.");

            // 2. Gọi hàm thực hiện cộng thêm thời gian bonus của bạn
            OnContinueWithBonusTime();

            // 3. Nếu game của bạn đang bị Pause (Time.timeScale = 0), hãy cho nó chạy lại
            //Time.timeScale = 1f;

            // 4. Đóng bảng LoseUI hoặc bảng thông báo hết giờ (nếu có)
            UIManager.Ins.CloseUI<LoseUI>();
        });

    }

    public void EnsureHomeFlowButtons()
    {
        if (!UIManager.Exists())
            return;

        HomeUI homeUI = UIManager.Ins.GetUI<HomeUI>();
        EnsureButtonBound(homeUI.PlayButton, OnPlayFromHome);
    }

    public void EnsureGameplayFlowButtons()
    {
        if (!UIManager.Exists())
            return;

        GamePlayUI gameplayUI = UIManager.Ins.GetUI<GamePlayUI>();
        EnsureButtonBound(gameplayUI.PauseButton, OnPauseGame);
        EnsureButtonBound(gameplayUI.SettingsButton, OnOpenSettings);
    }

    public void EnsureWinFlowButtons()
    {
        if (!UIManager.Exists())
            return;

        WinUI winUI = UIManager.Ins.GetUI<WinUI>();
        EnsureButtonBound(winUI.ContinueButton, OnWinContinue);
    }

    public void EnsureLoseFlowButtons()
    {
        if (!UIManager.Exists())
            return;

        LoseUI loseUI = UIManager.Ins.GetUI<LoseUI>();
        EnsureButtonBound(loseUI.RetryButton, OnLoseRetry);
        EnsureButtonBound(loseUI.AddTimeButton, OnLoseContinueTime);
    }

    private static void EnsureButtonBound(Button button, UnityEngine.Events.UnityAction handler)
    {
        if (button == null || handler == null)
            return;

        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) != null)
                return;
        }

        button.onClick.RemoveListener(handler);
        button.onClick.AddListener(handler);
    }

    #endregion

    #region Level Management

    public void LoadStartLevel()
    {
        if (m_Catalog == null || m_Catalog.Levels == null || m_Catalog.Levels.Length == 0)
        {
            Debug.LogWarning("[PlayManager] Chưa có LevelCatalog.");
            return;
        }

        int levelId = SaveManager.CurrentLevel;
        LevelCatalogEntry entry = m_Catalog.FindById(levelId);

        if (entry.LevelAsset == null)
        {
            levelId = m_StartLevelId;
            entry = m_Catalog.FindById(levelId);
        }

        LevelData level = m_LevelLoader.LoadFromEntry(entry, m_BlockDatabase);

        if (level == null)
        {
            Debug.LogError($"[PlayManager] Level {levelId} chưa có dữ liệu. Chạy Import CSV trước.");
            return;
        }
        // ========================================================
        // ĐOẠN THÊM VÀO: Bắn event khi người chơi BẮT ĐẦU bấm vào màn
        // Dùng cách tạo mảng tham số truyền số màn chơi thực tế (levelId)
        AnalyticParameter[] startParameters = new AnalyticParameter[]
        {
        new AnalyticParameter("level_id", levelId)
        };
        AnalyticManager.Ins.LogEvent("level_start", startParameters);
        // ========================================================

        LoadLevel(level);
    }

    public void LoadLevel(LevelData level)
    {
        if (level == null || m_BoardManager == null)
            return;

        ClearActiveHint();
        m_CurrentLevel = level;
        m_RefillState.Reset(level, m_BoardManager.Rows, m_BoardManager.Columns);
        WarnIfLevelCsvNarrowerThanBoard(level);

        StopTimer();

        if (m_BlockDatabase != null)
            m_LevelLoader.ValidateAndLog(level, m_BlockDatabase);

        ResetOutcomeSession();
        SetLockInput(true);
        PrepareForLevelLoad();
        m_BoardManager.RefreshScreenLayout();
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

        LevelData level = m_LevelLoader.LoadFromEntry(next, m_BlockDatabase);
        if (level == null)
            return false;

        LoadLevel(level);
        return true;
    }

    public void LoadNextLevel()
    {

        TryLoadNextLevel();
        // AdsManager.Ins.TryShowInter();

    }

    public bool HasNextLevel()
    {
        return m_Catalog != null && m_Catalog.TryGetNextAfter(CurrentLevelId, out _);
    }

    private void SpawnBoardLayout(LevelData level, Action onComplete)
    {
        SpawnLevelBoard(level);
        onComplete?.Invoke();
    }

    private void OnBoardLayoutReady()
    {
        if (m_CurrentLevel == null)
            return;

        SetLockInput(false);
        StartTimer(m_CurrentLevel.TimeLimitSeconds);

        Debug.Log($"[PlayManager] Loaded level {m_CurrentLevel.LevelId}" +
                  $" — board {m_BoardManager.Rows}x{m_BoardManager.Columns}" +
                  (m_CurrentLevel.TimeLimitSeconds > 0
                      ? $" — time limit {m_CurrentLevel.TimeLimitSeconds}s"
                      : string.Empty));

        if (BoosterManager.Exists())
            BoosterManager.Ins.CheckUnlockByLevel(CurrentLevelId);


        ConfigureGameplayHUD();
        OnLevelLoaded?.Invoke(m_CurrentLevel);
    }

    #endregion

    #region Timer

    public void StartTimer(int timeLimitSeconds)
    {
        m_TimeLimitSeconds = Mathf.Max(0, timeLimitSeconds);
        m_RemainingSeconds = m_TimeLimitSeconds;
        m_IsTimerRunning = m_TimeLimitSeconds > 0;
        RefreshTimeLimitText();
    }

    public void StopTimer()
    {
        CancelFreeze();
        m_IsTimerRunning = false;
        m_RemainingSeconds = 0f;
        m_TimeLimitSeconds = 0;
        RefreshTimeLimitText();
    }
    private void ResetHintTimer()
    {
        m_TimeForHint = m_TimeForHintValue;
        //ClearActiveHint(); // nếu đang hiện hint mà người chơi chạm thì tắt luôn (tùy chọn)
    }

    private void TickTimer()
    {
        if (!m_IsTimerRunning || IsTimerPaused())
            return;

        m_RemainingSeconds -= Time.deltaTime;
        RefreshTimeLimitText();

        if (m_RemainingSeconds > 0f)
            return;

        m_RemainingSeconds = 0f;
        m_IsTimerRunning = false;
        OnTimerExpired?.Invoke();
        TryResolveOutcome();
    }

    private void RefreshTimeLimitText()
    {
        if (m_TimeLimitText != null)
        {
            if (!HasTimeLimit)
            {
                m_TimeLimitText.gameObject.SetActive(false);
            }
            else
            {
                m_TimeLimitText.gameObject.SetActive(true);

                int totalSeconds = Mathf.CeilToInt(m_RemainingSeconds);
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                m_TimeLimitText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        RefreshGameplayHUD();
    }

    private void RefreshGameplayHUD()
    {
        if (!UIManager.Exists() || !UIManager.Ins.IsOpened<GamePlayUI>())
            return;

        UIManager.Ins.GetUI<GamePlayUI>().UpdateTime(m_RemainingSeconds);
    }

    private void ConfigureGameplayHUD()
    {
        if (!UIManager.Exists() || !UIManager.Ins.IsOpened<GamePlayUI>())
            return;

        GamePlayUI gameplayUI = UIManager.Ins.GetUI<GamePlayUI>();
        gameplayUI.Configure(CurrentLevelId, m_RemainingSeconds);
        gameplayUI.UpdateTime(m_RemainingSeconds);
    }

    private bool IsTimerPaused()
    {
        return m_IsSettlementRunning || m_IsTimerFrozen;
    }
    private void UpdateTimeForHint()
    {
        // Không đếm khi đang kéo block
        if (m_IsDragging)
            return;
        m_TimeForHint -= Time.deltaTime;
        if (m_TimeForHint <= 0f)
        {
            m_TimeForHint = m_TimeForHintValue;
            hintBooster();
        }
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
        AudioManager.Ins.PlaySFX(AudioManager.Win);
        m_OutcomeResolved = true;
        StopTimer();
        CancelActiveDrag();
        SetLockInput(true);

        if (HasNextLevel())
            SaveManager.CompleteLevel(CurrentLevelId);



        WinUI winUI = UIManager.Ins.OpenUI<WinUI>();
        winUI.Configure(CurrentLevelId, 0, _hasNextLevel: HasNextLevel());
        EnsureWinFlowButtons();
        AnalyticManager.Ins.LogEvent("game_win");
        AdsManager.Ins.ShowInterAds();
    }

    private void EnterLose()
    {
        m_OutcomeResolved = true;
        m_IsTimerRunning = false;
        CancelActiveDrag();
        SetLockInput(true);

        LoseUI loseUI = UIManager.Ins.OpenUI<LoseUI>();
        loseUI.Configure(CurrentLevelId, $"Continue +{c_ContinueBonusSeconds}s");
        EnsureLoseFlowButtons();
        AnalyticManager.Ins.LogEvent("game_lose");
    }

    #endregion

    #region Refill

    public BlockState? TryDequeueBin(int col) => m_RefillState.TryDequeue(col);

    #endregion

    #region Session

    private void ResetOutcomeSession()
    {
        m_OutcomeResolved = false;
        // m_VictoryUI?.Hide(animated: false);
        // m_DefeatUI?.Hide(animated: false);
        SetLockInput(false);
    }

    private void HandleSettlementCompleted(bool _)
    {
        TryResolveOutcome();
    }

    private void WarnIfLevelCsvNarrowerThanBoard(LevelData level)
    {
        if (level?.Rows == null || m_BoardManager == null)
            return;

        if (level.VisibleRows > 0 && level.VisibleRows != m_BoardManager.Rows)
        {
            Debug.LogWarning(
                $"[PlayManager] Level {level.LevelId}: VisibleRows asset ({level.VisibleRows}) " +
                $"khác BoardManager.rows ({m_BoardManager.Rows}) — runtime dùng BoardManager.");
        }

        int csvCols = 0;
        foreach (LevelGridRow row in level.Rows)
        {
            if (row.ColBlockTypes != null && row.ColBlockTypes.Length > csvCols)
                csvCols = row.ColBlockTypes.Length;
        }

        if (csvCols > 0 && m_BoardManager.Columns > csvCols)
        {
            Debug.LogWarning(
                $"[PlayManager] Level {level.LevelId}: BoardManager.columns ({m_BoardManager.Columns}) " +
                $"lớn hơn số cột CSV ({csvCols}).");
        }
    }

    #endregion

    #region Input

    public void SetLockInput(bool isLocked)
    {
        m_IsInputLocked = isLocked;
        if (isLocked)
            CancelActiveDrag();
    }

    public bool IsInputLocked => m_IsInputLocked;

    public bool IsHintActive => m_HintCoroutine != null;

    public void CancelActiveDrag()
    {
        if (!m_IsDragging)
            return;

        BlockManager block = m_DraggedBlock != null ? m_DraggedBlock : m_SourceSlot?.CurrentBlock;
        ClearHoverTarget();
        ReturnBlockToSource(block, false);
        ResetDragState();
    }

    public void HandleInput()
    {
        if (IsInputBlocked() || m_MainCamera == null || m_BoardManager == null)
            return;

        if (IsPointerOverUI() && Input.GetMouseButtonDown(0))
            return;

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        // Bất kỳ khi nào có tương tác chuột/chạm -> reset timer
        if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
            ResetHintTimer();

        if (Input.GetMouseButtonDown(0))
        {
            BlockManager hit = GetBlockAtPosition(mouseWorldPos);
            if (hit != null)
                BeginDrag(hit);
        }

        if (Input.GetMouseButton(0) && m_IsDragging && m_DraggedBlock != null)
            UpdateDrag(m_DraggedBlock, mouseWorldPos);

        if (Input.GetMouseButtonUp(0) && m_IsDragging && m_DraggedBlock != null)
            EndDrag(m_DraggedBlock);
    }

    public void BeginDrag(BlockManager block)
    {
        if (IsInputBlocked() || block == null || m_IsDragging)
            return;

        m_SourceSlot = block.CurrentSlot;
        if (m_SourceSlot == null)
            return;

        m_DraggedBlock = block;
        m_IsDragging = true;
        m_DragOffset = block.transform.position - GetMouseWorldPosition();
        block.BeginDrag();
    }

    public void UpdateDrag(BlockManager block, Vector3 mouseWorldPos)
    {
        if (!m_IsDragging || block == null)
            return;

        block.FollowDragPosition(mouseWorldPos + m_DragOffset);
        UpdateHoverTarget(mouseWorldPos);
    }

    public void EndDrag(BlockManager block)
    {
        if (!m_IsDragging || block == null)
            return;

        ClearHoverTarget();

        Slot targetSlot = ResolveDropTargetSlot();
        bool shakeReject = targetSlot != null && targetSlot.HasBlock;

        if (targetSlot != null && targetSlot != m_SourceSlot && TryValidateMove(m_SourceSlot, targetSlot))
        {
            m_HoverSlot = targetSlot;
            AcceptMove();
        }
        else
        {
            RejectMove(block, shakeReject);
        }

        ResetDragState();
    }

    #endregion

    #region Turn Flow

    public void ProcessTurn()
    {
        if (m_SourceSlot == null || m_HoverSlot == null)
            return;

        BlockManager source = m_SourceSlot.CurrentBlock;
        BlockManager target = m_HoverSlot.CurrentBlock;
        if (source == null || target == null)
            return;

        m_IsTurnProcessing = true;
        ExecuteMerge(source, target);
    }

    private void ExecuteMerge(BlockManager sourceBlock, BlockManager targetBlock)
    {
        if (sourceBlock == null || targetBlock == null)
        {
            m_IsTurnProcessing = false;
            return;
        }

        if (targetBlock.IsPendingDestroy || sourceBlock.IsPendingDestroy)
        {
            m_IsTurnProcessing = false;
            return;
        }

        MergeResult result = ResolveMerge(sourceBlock, targetBlock);

        if (!result.IsValid)
        {
            m_IsTurnProcessing = false;
            return;
        }

        ApplyMergeResult(sourceBlock, targetBlock, result);
    }

    private void ApplyMergeResult(BlockManager sourceBlock, BlockManager targetBlock, MergeResult result)
    {
        sourceBlock.IsPendingDestroy = true;
        targetBlock.ApplyMergeState(result.TargetStackCount, result.TargetTier2Stage);

        Slot sourceSlot = sourceBlock.CurrentSlot;
        if (sourceSlot != null)
            m_BoardManager.ClearSlot(sourceSlot.Row, sourceSlot.Col);

        if (result.NeedsWindUp)
            m_PendingTier2TripleClearTarget = targetBlock;

        if (m_MagnetDeferSettlement)
        {
            if (sourceBlock != null)
                Destroy(sourceBlock.gameObject);

            targetBlock.UpdateTier2StageVisual(targetBlock.StackCount, targetBlock.Tier2MergeStage);
            m_MagnetSurvivor = targetBlock;
            m_MagnetSurvivorClearKind = result.ClearKind;
            m_IsTurnProcessing = false;
            return;
        }

        BeginMergeVisual(sourceBlock, targetBlock);
        PlayMergeStackVisual(sourceBlock, targetBlock);

        if (!result.ShouldClear)
        {
            RequestGravityOnly();
            return;
        }

        if (result.NeedsWindUp)
            return;

        ClearMergedBlock(targetBlock, isTier2TripleClear: false);
    }

    private void CommitPendingTier2TripleClear()
    {
        if (m_PendingTier2TripleClearTarget == null)
            return;

        BlockManager target = m_PendingTier2TripleClearTarget;
        m_PendingTier2TripleClearTarget = null;

        if (target == null)
        {
            RequestGravityOnly();
            return;
        }

        ClearMergedBlock(target, isTier2TripleClear: true);
    }

    private void TryPlayComboVoice(bool isTier2TripleClear)
    {
        float now = Time.time;

        if (isTier2TripleClear)
        {
            m_ClearStreakCount = 2;
            m_LastClearTime = now;
            AudioManager.Ins.PlaySFX(AudioManager.SoundGood);
            return;
        }

        if (m_ClearStreakCount > 0 && now - m_LastClearTime > c_ComboVoiceWindow)
            m_ClearStreakCount = 0;

        m_ClearStreakCount++;
        m_LastClearTime = now;

        if (m_ClearStreakCount < 2)
            return;

        string sfx = m_ClearStreakCount switch
        {
            2 => AudioManager.SoundGood,
            3 => AudioManager.SoundGreat,
            4 => AudioManager.SoundExcellent,
            5 => AudioManager.SoundAmazing,
            _ => AudioManager.SoundUnbelievable
        };

        AudioManager.Ins.PlaySFX(sfx);
    }

    private void ClearMergedBlock(BlockManager targetBlock, bool isTier2TripleClear)
    {
        Slot targetSlot = targetBlock?.CurrentSlot;
        if (targetSlot == null)
        {
            if (targetBlock != null && isTier2TripleClear)
                Destroy(targetBlock.gameObject);

            RequestGravityOnly();
            return;
        }

        targetBlock.IsPendingDestroy = true;
        int row = targetSlot.Row;
        int col = targetSlot.Col;

        m_BoardManager.ClearSlot(row, col);
        Vector3 vfxPos = targetBlock.transform.position;
        SpawnVfxBlock(vfxPos);

        Destroy(targetBlock.gameObject);
        RequestGravityOnly();
        AudioManager.Ins.PlaySFX(AudioManager.BlockMergeSuccess);
        TryPlayComboVoice(isTier2TripleClear);
    }

    private void RequestGravityOnly()
    {
        if (m_IsSettlementRunning)
        {
            if (!m_PendingGravityAfterSettlement)
            {
                m_PendingGravityAfterSettlement = true;
                OnSettlementCompleted += HandlePendingGravityAfterSettlement;
            }

            return;
        }

        ClearMergeVisual();
        StartSettlement(neighborRestores: null);
    }

    private void HandlePendingGravityAfterSettlement(bool _)
    {
        OnSettlementCompleted -= HandlePendingGravityAfterSettlement;
        m_PendingGravityAfterSettlement = false;
        RequestGravityOnly();
    }

    #endregion

    #region Settlement

    public void SettlementLoop()
    {
        StartSettlement(neighborRestores: null);
    }

    private void StartSettlement(IReadOnlyList<Tier2NeighborRestore> neighborRestores)
    {
        if (m_SettlementCoroutine != null)
            StopCoroutine(m_SettlementCoroutine);

        m_SettlementCoroutine = StartCoroutine(SettlementRoutine(neighborRestores));
    }

    private IEnumerator SettlementRoutine(IReadOnlyList<Tier2NeighborRestore> neighborRestores)
    {
        if (m_IsSettlementRunning)
            yield break;

        m_IsSettlementRunning = true;
        bool refillRan = false;

        try
        {
            if (m_BoardManager != null)
                yield return m_BoardManager.ApplyGravity(c_GravityAnimTimeout);

            Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues =
                BuildNeighborQueues(neighborRestores);

            bool hasNeighborRefill = HasPendingNeighborRestore(neighborQueues);
            bool needsBinRefill = m_BoardManager != null && m_BoardManager.IsTopRowCompletelyEmpty();

            if (needsBinRefill || hasNeighborRefill)
            {
                refillRan = true;
                yield return RunRefillPhase(needsBinRefill, neighborQueues);
            }
        }
        finally
        {
            m_IsSettlementRunning = false;
            m_IsTurnProcessing = false;
            m_SettlementCoroutine = null;
            OnSettlementCompleted?.Invoke(refillRan);
        }
    }

    private IEnumerator RunRefillPhase(
        bool includeBin,
        Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues)
    {
        List<ColumnRefillPacket> wave = CollectFillWave(includeBin, neighborQueues);
        if (wave.Count == 0)
            yield break;

        var batch = new AnimationCompletionBatch();
        PlayRefillPushAnimations(wave, batch);
        PlayRefillRiseAnimations(wave, batch);

        yield return batch.WaitRoutine(c_SettlementTimeout, "refill wave");
    }

    #endregion

    #region Spawn Level Layout

    /// <summary>Reset gameplay session trước khi load layout level mới.</summary>
    public void PrepareForLevelLoad()
    {
        CancelActiveDrag();
        m_VisualBooster?.KillActiveVisual(false);

        if (m_SettlementCoroutine != null)
        {
            StopCoroutine(m_SettlementCoroutine);
            m_SettlementCoroutine = null;
        }

        m_IsSettlementRunning = false;
        m_IsTurnProcessing = false;
        m_PendingTier2TripleClearTarget = null;
        m_PendingGravityAfterSettlement = false;
        m_ClearStreakCount = 0;
        m_LastClearTime = 0f;
        ResetMagnetDeferredState();
    }

    /// <summary>Xóa bàn và spawn layout ban đầu từ level data.</summary>
    public void SpawnLevelBoard(LevelData level)
    {
        if (level == null || m_BoardManager == null)
            return;

        PrepareForLevelLoad();
        m_BoardManager.ClearAllBlocks();
        DestroyOrphanBlocks();

        if (level.Rows == null)
            return;

        int boardRows = m_BoardManager.Rows;
        int boardCols = m_BoardManager.Columns;

        foreach (LevelGridRow gridRow in level.Rows)
        {
            if (gridRow.Row >= boardRows || gridRow.ColBlockTypes == null)
                continue;

            int mappedRow = (boardRows - 1) - gridRow.Row;

            int colCount = Mathf.Min(boardCols, gridRow.ColBlockTypes.Length);
            for (int col = 0; col < colCount; col++)
            {
                if (!m_LevelLoader.TryParseCell(gridRow.ColBlockTypes[col], out string typeKey, out int stack))
                    continue;

                SpawnBlockAt(mappedRow, col, typeKey, stack);
            }
        }
    }

    #endregion

    #region Spawn Refill

    private void SpawnBlockAt(int row, int col, string typeKey, int stack)
    {
        BlockManager block = SpawnBlockInstance(typeKey, stack);
        if (block == null)
            return;

        if (!m_BoardManager.IsValidPosition(row, col))
        {
            Destroy(block.gameObject);
            return;
        }

        m_BoardManager.PlaceBlock(block, row, col);
    }

    private BlockManager SpawnBlockInstance(string typeKey, int stack)
    {
        if (m_BlockPrefab == null || m_BlockDatabase == null)
            return null;

        BlockData data = m_BlockDatabase.GetBlockData(typeKey);
        if (data == null)
        {
            Debug.LogWarning($"[PlayManager] Không tìm thấy BlockData cho '{typeKey}'.");
            return null;
        }

        BlockManager block = Instantiate(m_BlockPrefab);
        block.gameObject.SetActive(true);
        SetupSpawnedBlock(block, data, stack);
        return block;
    }

    private void SetupSpawnedBlock(BlockManager block, BlockData data, int stack)
    {
        if (block == null || data == null || m_BoardManager == null)
            return;

        if (m_BlockParent != null)
            block.transform.SetParent(m_BlockParent);

        block.Init(data, stack);
        block.Initialize(data, m_BoardManager.CellWidth, m_BoardManager.CellHeight);
        block.UpdateTier2StageVisual(block.StackCount, block.Tier2MergeStage);
    }

    private BlockManager SpawnRestoredNeighbor(Tier2NeighborRestore restore)
    {
        BlockManager block = SpawnBlockInstance(restore.TypeKey, restore.Stack);
        if (block == null)
            return null;

        ApplyTier2StageIfNeeded(block, restore.Tier2MergeStage);
        return block;
    }

    private BlockManager SpawnBlockFromState(BlockState state)
    {
        BlockManager block = SpawnBlockInstance(state.TypeKey, state.Stack);
        if (block == null)
            return null;

        ApplyTier2StageIfNeeded(block, state.Tier2MergeStage);
        return block;
    }

    private void ApplyBlockStateToBlock(BlockManager block, BlockState state)
    {
        if (block == null || m_BlockDatabase == null || m_BoardManager == null)
            return;

        BlockData data = m_BlockDatabase.GetBlockData(state.TypeKey);
        if (data == null)
        {
            Debug.LogWarning($"[PlayManager] Shuffle: không tìm thấy BlockData '{state.TypeKey}'.");
            return;
        }

        block.Init(data, state.Stack);
        block.Initialize(data, m_BoardManager.CellWidth, m_BoardManager.CellHeight);
        ApplyTier2StageIfNeeded(block, state.Tier2MergeStage);
    }

    private static void ApplyTier2StageIfNeeded(BlockManager block, int tier2MergeStage)
    {
        if (tier2MergeStage <= 0)
            return;

        block.SetTier2MergeStage(tier2MergeStage);
        block.UpdateTier2StageVisual(block.StackCount, block.Tier2MergeStage);
    }

    private List<ColumnRefillPacket> CollectFillWave(
        bool includeBin,
        Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues)
    {
        var wave = new List<ColumnRefillPacket>();
        if (m_BoardManager == null || m_BlockPrefab == null || m_BlockDatabase == null)
            return wave;

        for (int col = 0; col < m_BoardManager.Columns; col++)
        {
            if (!m_BoardManager.ColumnNeedsRefill(col))
                continue;

            bool hasNeighbor = neighborQueues.TryGetValue(col, out Queue<Tier2NeighborRestore> queue)
                && queue.Count > 0;

            Tier2NeighborRestore? restore = null;
            if (hasNeighbor)
                restore = queue.Dequeue();

            BlockState? binState = null;
            if (!restore.HasValue && includeBin)
                binState = TryDequeueBin(col);

            if (!restore.HasValue && !binState.HasValue)
                continue;

            if (!m_BoardManager.TryShiftColumnUp(col, out List<ColumnPushCommand> commands))
            {
                if (restore.HasValue && queue != null)
                    queue.Enqueue(restore.Value);
                continue;
            }

            BlockManager newBlock = restore.HasValue
                ? SpawnRestoredNeighbor(restore.Value)
                : SpawnBlockFromState(binState.Value);

            if (newBlock == null)
                continue;

            m_BoardManager.PlaceBlock(newBlock, 0, col);
            wave.Add(new ColumnRefillPacket
            {
                Col = col,
                PushCommands = commands,
                NewBlock = newBlock
            });
        }

        return wave;
    }

    private void PlayRefillPushAnimations(IReadOnlyList<ColumnRefillPacket> wave, AnimationCompletionBatch batch)
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady)
            return;
        AudioManager.Ins.PlaySFX(AudioManager.Refill);

        foreach (ColumnRefillPacket packet in wave)
        {
            if (packet.PushCommands == null)
                continue;

            foreach (ColumnPushCommand cmd in packet.PushCommands)
            {
                if (cmd.Block == null)
                    continue;

                Vector3 from = m_BoardManager.GridToWorld(cmd.FromRow, cmd.Col);
                Vector3 to = m_BoardManager.GridToWorld(cmd.ToRow, cmd.Col);

                batch.AddOutstanding(1);
                cmd.Block.PlayRefillPush(from, to, m_RefillPushDuration, cmd.ToRow, m_RefillPushEase, batch.NotifyOneDone);
            }
        }
    }

    private void PlayRefillRiseAnimations(IReadOnlyList<ColumnRefillPacket> wave, AnimationCompletionBatch batch)
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady)
            return;

        float cellHeight = m_BoardManager.CellHeight;
        foreach (ColumnRefillPacket packet in wave)
        {
            if (packet.NewBlock == null)
                continue;

            batch.AddOutstanding(1);
            Vector3 target = m_BoardManager.GridToWorld(0, packet.Col);
            packet.NewBlock.PlayRiseFromBelow(target, cellHeight, m_RefillRiseDuration, 0, 0f, batch.NotifyOneDone);
        }
    }

    #endregion

    #region Merge View
    //hiển thị merge stack visual
    private void PlayMergeStackVisual(BlockManager source, BlockManager target)
    {
        if (target == null)
        {
            if (source != null)
                Destroy(source.gameObject);
            ClearMergeVisual();
            return;
        }

        target.UpdateTier2StageVisual(target.StackCount, target.Tier2MergeStage);

        if (source == null)
        {
            FinishTargetStackVisual(target);
            // RequestGravityOnly();
            return;
        }

        Vector3 absorbTarget = ResolveAbsorbTargetWorldPos(target);
        source.PlayAbsorbInto(absorbTarget, () =>
        {
            if (source != null)
                Destroy(source.gameObject);
            FinishTargetStackVisual(target);
        });
    }

    //resolve absorb target world pos
    private Vector3 ResolveAbsorbTargetWorldPos(BlockManager target)
    {
        if (target?.CurrentSlot == null || m_BoardManager == null || !m_BoardManager.IsGridReady)
            return target != null ? target.transform.position : Vector3.zero;

        Slot slot = target.CurrentSlot;
        int finalRow = m_BoardManager.PredictFinalRowAfterGravity(slot.Row, slot.Col);
        return m_BoardManager.GridToWorld(finalRow, slot.Col);
    }

    //finish target stack visual
    private void FinishTargetStackVisual(BlockManager target)
    {
        if (target == null)
            return;

        if (m_PendingTier2TripleClearTarget != null && m_PendingTier2TripleClearTarget == target)
        {
            target.PlayTier2ExplosionWindUp(CommitPendingTier2TripleClear);
            return;
        }

        bool impactHandled = false;
        void AfterImpact()
        {
            if (impactHandled)
                return;
            impactHandled = true;

            if (m_IsSettlementRunning)
                return;

            target.UpdateTier2StageVisual(target.StackCount, target.Tier2MergeStage);
            SnapTargetToCurrentSlot(target);
            // RequestGravityOnly();
        }

        target.PlayMergeImpact(AfterImpact);
        AudioManager.Ins.PlaySFX(AudioManager.BlockMerge);
    }

    //snap target to current slot
    private void SnapTargetToCurrentSlot(BlockManager target)
    {
        if (target == null || target.CurrentSlot == null)
            return;

        if (m_BoardManager == null || !m_BoardManager.IsGridReady)
            return;

        Slot slot = target.CurrentSlot;
        Vector3 gridPos = m_BoardManager.GridToWorld(slot.Row, slot.Col);
        target.SnapToGridCell(gridPos, slot.Row);
    }

    #endregion

    #region Session

    private void HandleLevelLoaded(LevelData _)
    {
        m_PendingTier2TripleClearTarget = null;
        m_PendingGravityAfterSettlement = false;
    }

    private void DestroyOrphanBlocks()
    {
        BlockManager[] blocks = FindObjectsByType<BlockManager>(FindObjectsSortMode.None);
        foreach (BlockManager block in blocks)
        {
            if (block != null)
                Destroy(block.gameObject);
        }
    }

    #endregion


    #region Utilities

    private bool IsInputBlocked()
    {
        if (m_IsInputLocked || m_IsDragging && m_IsSettlementRunning)
            return true;

        if (m_IsTurnProcessing || m_IsSettlementRunning)
            return true;

        if (m_OutcomeResolved)
            return true;

        return false;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 pos = m_MainCamera.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;
        return pos;
    }

    private BlockManager GetBlockAtPosition(Vector2 worldPos)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(worldPos, Vector2.zero, 0f, m_BlockLayer);
        if (hits.Length == 0)
            return null;

        Array.Sort(hits, (a, b) =>
            GetBlockSortingOrder(b.collider).CompareTo(GetBlockSortingOrder(a.collider)));

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null && hit.collider.TryGetComponent(out BlockManager block))
                return block;
        }

        return null;
    }

    private static int GetBlockSortingOrder(Collider2D collider)
    {
        if (collider == null)
            return 0;

        if (collider.TryGetComponent(out SortingGroup group))
            return group.sortingOrder;

        SpriteRenderer sprite = collider.GetComponentInChildren<SpriteRenderer>();
        return sprite != null ? sprite.sortingOrder : 0;
    }

    private Slot ResolveDropTargetSlot()
    {
        BlockManager target = ResolveHoverTarget(GetMouseWorldPosition());
        return target != null ? target.CurrentSlot : null;
    }

    /// <summary>
    /// Mặc định: block ở ô grid dưới chuột. Ghi đè: block cùng loại khi box overlap đủ ngưỡng.
    /// </summary>
    private BlockManager ResolveHoverTarget(Vector3 mouseWorldPos)
    {
        BlockManager defaultTarget = GetBlockUnderMouseGrid(mouseWorldPos);
        if (m_DraggedBlock == null)
            return defaultTarget;

        BlockManager sameTypeTarget = TryGetSameTypeOverlapOverride();
        return sameTypeTarget != null ? sameTypeTarget : defaultTarget;
    }

    private BlockManager GetBlockUnderMouseGrid(Vector3 mouseWorldPos)
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady)
            return null;

        Vector2Int grid = m_BoardManager.WorldToGrid(mouseWorldPos);
        Slot slot = m_BoardManager.GetSlot(grid.x, grid.y);
        if (slot == null || slot == m_SourceSlot || !slot.HasBlock)
            return null;

        return slot.CurrentBlock;
    }

    private BlockManager TryGetSameTypeOverlapOverride()
    {
        if (m_DraggedBlock == null || !m_DraggedBlock.TryGetComponent(out Collider2D draggedCollider))
            return null;

        Bounds draggedBounds = draggedCollider.bounds;
        float draggedArea = draggedBounds.size.x * draggedBounds.size.y;
        if (draggedArea <= Mathf.Epsilon)
            return null;

        var filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.SetLayerMask(m_BlockLayer);
        filter.useTriggers = true;

        Collider2D[] hits = new Collider2D[c_DragOverlapBufferSize];
        int count = draggedCollider.OverlapCollider(filter, hits);

        BlockManager best = null;
        float bestOverlapRatio = 0f;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == draggedCollider)
                continue;

            if (!hit.TryGetComponent(out BlockManager candidate))
                continue;

            if (candidate == m_DraggedBlock || candidate.CurrentSlot == m_SourceSlot)
                continue;

            if (!m_DraggedBlock.IsSameTypeAs(candidate))
                continue;

            float overlapArea = GetBoundsOverlapArea(draggedBounds, hit.bounds);
            float overlapRatio = overlapArea / draggedArea;
            if (overlapRatio < m_SameTypeOverlapThreshold || overlapRatio <= bestOverlapRatio)
                continue;

            bestOverlapRatio = overlapRatio;
            best = candidate;
        }

        return best;
    }

    private static float GetBoundsOverlapArea(Bounds a, Bounds b)
    {
        float overlapX = Mathf.Max(0f, Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x));
        float overlapY = Mathf.Max(0f, Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y));
        return overlapX * overlapY;
    }

    private void UpdateHoverTarget(Vector3 mouseWorldPos)
    {
        BlockManager next = ResolveHoverTarget(mouseWorldPos);

        if (next == m_HoverTargetBlock)
            return;

        if (m_HoverTargetBlock != null)
            m_HoverTargetBlock.ClearHoverTargetFeedback();

        m_HoverTargetBlock = next;

        if (next != null)
            next.PlayHoverTargetFeedback();
    }

    private void ClearHoverTarget()
    {
        if (m_HoverTargetBlock == null)
            return;

        m_HoverTargetBlock.ClearHoverTargetFeedback();
        m_HoverTargetBlock = null;
    }

    private void ReturnBlockToSource(BlockManager block, bool shakeReject)
    {
        if (block == null || m_SourceSlot == null || m_BoardManager == null || !m_BoardManager.IsGridReady)
            return;

        Vector3 originPos = m_BoardManager.GridToWorld(m_SourceSlot.Row, m_SourceSlot.Col);
        block.EndDragReturnTo(originPos, m_SourceSlot.Row, shakeReject);
    }

    private void ResetDragState()
    {
        m_DraggedBlock = null;
        m_SourceSlot = null;
        m_HoverSlot = null;
        m_IsDragging = false;
        m_DragOffset = Vector3.zero;
    }

    private bool TryValidateMove(Slot sourceSlot, Slot targetSlot)
    {
        if (sourceSlot == null || targetSlot == null || sourceSlot.IsEmpty || !targetSlot.HasBlock)
            return false;

        return CanMergeBlocks(sourceSlot.CurrentBlock, targetSlot.CurrentBlock);
    }

    private void AcceptMove()
    {
        ProcessTurn();
    }

    private void RejectMove(BlockManager block, bool shakeReject)
    {
        ReturnBlockToSource(block, shakeReject);
    }

    private static Dictionary<int, Queue<Tier2NeighborRestore>> BuildNeighborQueues(
        IReadOnlyList<Tier2NeighborRestore> neighborRestores)
    {
        var queues = new Dictionary<int, Queue<Tier2NeighborRestore>>();
        if (neighborRestores == null)
            return queues;

        foreach (Tier2NeighborRestore restore in neighborRestores)
        {
            if (string.IsNullOrEmpty(restore.TypeKey))
                continue;

            if (!queues.TryGetValue(restore.Col, out Queue<Tier2NeighborRestore> queue))
            {
                queue = new Queue<Tier2NeighborRestore>();
                queues[restore.Col] = queue;
            }

            queue.Enqueue(restore);
        }

        return queues;
    }

    private static bool HasPendingNeighborRestore(Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues)
    {
        foreach (Queue<Tier2NeighborRestore> queue in neighborQueues.Values)
        {
            if (queue.Count > 0)
                return true;
        }

        return false;
    }

    #endregion

    #region Merge Rules

    private enum MergeClearKind
    {
        None,
        NormalBurst,
        Tier2Triple
    }

    private readonly struct MergeResult
    {
        public bool IsValid { get; }
        public int TargetStackCount { get; }
        public int TargetTier2Stage { get; }
        public MergeClearKind ClearKind { get; }

        public bool ShouldClear => ClearKind != MergeClearKind.None;
        public bool NeedsWindUp => ClearKind == MergeClearKind.Tier2Triple;

        public static MergeResult Invalid => new MergeResult(false, 0, 0, MergeClearKind.None);

        public MergeResult(bool isValid, int targetStackCount, int targetTier2Stage, MergeClearKind clearKind)
        {
            IsValid = isValid;
            TargetStackCount = targetStackCount;
            TargetTier2Stage = targetTier2Stage;
            ClearKind = clearKind;
        }
    }

    /// <summary>Orchestrator API — BoardManager gọi khi cần kiểm tra merge.</summary>
    public bool CanMergeBlocks(BlockManager a, BlockManager b)
    {
        if (a == null || b == null || a.TypeId != b.TypeId)
            return false;

        int totalStack = a.StackCount + b.StackCount;

        if (a.StackCount == 1 && b.StackCount == 1)
            return totalStack <= 3;

        // SỬA TẠI ĐÂY: Thay vì luôn return true, hãy check Stage của chúng
        if (a.StackCount == 2 && b.StackCount == 2)
        {
            // Nếu cả 2 đều là Stage 2 (khối 4), CHẶN lại bằng cách trả về false
            if (a.Tier2MergeStage == 2 && b.Tier2MergeStage == 2)
                return false;

            // Các trường hợp 2 + 2 khác (ví dụ Stage 1 + Stage 1) thì vẫn cho gộp
            return true;
        }

        if (totalStack != 3)
            return false;

        BlockManager tier2Block = a.StackCount == 2 ? a : b;
        return tier2Block.Tier2MergeStage == 1;
    }
    //kiểm tra merge có hợp lệ không
    private MergeResult ResolveMerge(BlockManager source, BlockManager target)
    {
        // 1. Kiểm tra cơ bản
        if (!CanMergeBlocks(source, target))
            return MergeResult.Invalid;
        // Debug.Log($"Source: {source.StackCount}, Target: {target.StackCount}");

        // 3. Logic xử lý cho Stack 2 + Stack 2 cũ của bạn
        bool isTier2Pair = source.StackCount == 2 && target.StackCount == 2;
        if (isTier2Pair)
        {
            int combinedStage = target.Tier2MergeStage + source.Tier2MergeStage;
            MergeClearKind clearKind = combinedStage >= 3
                ? MergeClearKind.Tier2Triple
                : MergeClearKind.None;

            return new MergeResult(true, 2, combinedStage, clearKind);
        }

        // 4. Logic xử lý thông thường (1+1, hoặc các số khác)
        int newStack = target.StackCount + source.StackCount;
        int newStage = target.Tier2MergeStage;

        if (newStack == 2 && source.StackCount == 1 && target.StackCount == 1)
            newStage = 1;

        MergeClearKind stackClear = newStack >= 3
            ? MergeClearKind.NormalBurst
            : MergeClearKind.None;

        return new MergeResult(true, newStack, newStage, stackClear);
    }
    #endregion

    #region Merge Visual Context

    private BlockManager m_MergeVisualTarget;
    //bắt đầu hiển thị merge
    private void BeginMergeVisual(BlockManager source, BlockManager target)
        => m_MergeVisualTarget = target != null ? target : source;
    //xóa hiển thị merge
    public void ClearMergeVisual() => m_MergeVisualTarget = null;

    //kiểm tra block có phải là target merge không
    public bool IsMergeVisualTarget(BlockManager block)
        => block != null && block == m_MergeVisualTarget;

    #endregion

    #region Board Query

    //kiểm tra có move hợp lệ không
    public bool HasAvailableMove()
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady)
            return false;

        //kiểm tra từng cell trên bảng
        for (int r = 0; r < m_BoardManager.Rows; r++)
        {
            for (int c = 0; c < m_BoardManager.Columns; c++)
            {
                BlockManager a = m_BoardManager.GetBlock(r, c);
                if (a == null)
                    continue;

                //kiểm tra merge có hợp lệ không
                if (TryMergePairAt(r, c, r + 1, c, a))
                    return true;
                if (TryMergePairAt(r, c, r, c + 1, a))
                    return true;
            }
        }

        return false;
    }

    //kiểm tra merge có hợp lệ không
    private bool TryMergePairAt(int rowA, int colA, int rowB, int colB, BlockManager blockA)
    {
        BlockManager blockB = m_BoardManager.GetBlock(rowB, colB);
        return blockB != null && CanMergeBlocks(blockA, blockB);
    }

    #endregion

    //batch animation
    private sealed class AnimationCompletionBatch
    {
        private int m_Remaining;

        public void AddOutstanding(int count) => m_Remaining += count;

        public void NotifyOneDone() => m_Remaining--;

        public IEnumerator WaitRoutine(float timeoutSeconds, string label)
        {
            if (m_Remaining <= 0)
                yield break;

            float elapsed = 0f;
            while (m_Remaining > 0 && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (m_Remaining > 0)
                Debug.LogWarning($"[PlayManager] Timeout chờ {label} — còn {m_Remaining} anim.");
        }
    }

    //state refill
    private sealed class LevelRefillState
    {
        private static readonly LevelLoader s_CellParser = new LevelLoader();

        private Queue<BlockState>[] m_RuntimeQueues = System.Array.Empty<Queue<BlockState>>();
        private int m_ColumnCount;
        // Lấy các block từ level và đẩy vào các cột
        public void Reset(LevelData level, int boardRows, int boardColumns)
        {
            m_ColumnCount = Mathf.Max(0, boardColumns);

            if (m_RuntimeQueues.Length != m_ColumnCount)
                m_RuntimeQueues = new Queue<BlockState>[m_ColumnCount];

            int binStartRow = Mathf.Max(0, boardRows);
            var rowsByDepth = new Dictionary<int, LevelGridRow>();
            int maxDepth = -1;

            if (level?.Rows != null)
            {
                foreach (LevelGridRow row in level.Rows)
                {
                    rowsByDepth[row.Row] = row;
                    if (row.Row > maxDepth)
                        maxDepth = row.Row;
                }
            }

            for (int col = 0; col < m_ColumnCount; col++)
            {
                m_RuntimeQueues[col] = new Queue<BlockState>();

                for (int depth = binStartRow; depth <= maxDepth; depth++)
                {
                    if (!rowsByDepth.TryGetValue(depth, out LevelGridRow gridRow))
                        continue;

                    if (gridRow.ColBlockTypes == null || col >= gridRow.ColBlockTypes.Length)
                        continue;

                    if (s_CellParser.TryParseCell(gridRow.ColBlockTypes[col], out string typeKey, out int stack))
                        m_RuntimeQueues[col].Enqueue(BlockState.FromBinCell(typeKey, stack));
                }
            }
        }
        //lấy block từ cột
        public BlockState? TryDequeue(int col)
        {
            if (col < 0 || col >= m_ColumnCount)
                return null;

            Queue<BlockState> queue = m_RuntimeQueues[col];
            if (queue == null || queue.Count == 0)
                return null;

            return queue.Dequeue();
        }
        //kiểm tra có block còn lại không
        public bool HasRemainingBlocks()
        {
            for (int col = 0; col < m_ColumnCount; col++)
            {
                if (m_RuntimeQueues[col] != null && m_RuntimeQueues[col].Count > 0)
                    return true;
            }

            return false;
        }
        //lấy hết các block từ các cột
        public int[] DrainAllTo(List<BlockState> pool)
        {
            int[] counts = new int[m_ColumnCount];

            for (int col = 0; col < m_ColumnCount; col++)
            {
                Queue<BlockState> queue = m_RuntimeQueues[col];
                while (queue != null && queue.Count > 0)
                {
                    pool.Add(queue.Dequeue());
                    counts[col]++;
                }
            }

            return counts;
        }
        //đẩy hết các block vào pool
        public void RebuildFrom(List<BlockState> pool, int startIndex, int[] countsPerCol)
        {
            for (int col = 0; col < m_ColumnCount; col++)
            {
                Queue<BlockState> queue = m_RuntimeQueues[col] ?? new Queue<BlockState>();
                queue.Clear();

                for (int i = 0; i < countsPerCol[col]; i++)
                    queue.Enqueue(pool[startIndex++]);

                m_RuntimeQueues[col] = queue;
            }
        }

        public void AppendBinStates(List<BlockState> pool)
        {
            for (int col = 0; col < m_ColumnCount; col++)
            {
                Queue<BlockState> queue = m_RuntimeQueues[col];
                if (queue == null)
                    continue;

                foreach (BlockState state in queue)
                    pool.Add(state);
            }
        }

        public int TryTakeMatchingType(string typeKey, int count, List<BlockState> taken)
        {
            int found = 0;
            for (int col = 0; col < m_ColumnCount && found < count; col++)
            {
                Queue<BlockState> queue = m_RuntimeQueues[col];
                if (queue == null || queue.Count == 0)
                    continue;

                var kept = new Queue<BlockState>();
                while (queue.Count > 0)
                {
                    BlockState state = queue.Dequeue();
                    if (found < count && state.TypeKey == typeKey)
                    {
                        taken.Add(state);
                        found++;
                    }
                    else
                    {
                        kept.Enqueue(state);
                    }
                }

                m_RuntimeQueues[col] = kept;
            }

            return found;
        }
        public int TryTakeMatchingTypeWithStack(
                                       string typeKey,
                                       int count,
                                       int requiredStack,
                                       List<BlockState> taken)
        {
            int found = 0;
            for (int col = 0; col < m_ColumnCount && found < count; col++)
            {
                Queue<BlockState> queue = m_RuntimeQueues[col];
                if (queue == null || queue.Count == 0)
                    continue;

                var kept = new Queue<BlockState>();
                while (queue.Count > 0)
                {
                    BlockState state = queue.Dequeue();
                    if (found < count
                        && state.TypeKey == typeKey
                        && state.Stack == requiredStack)
                    {
                        taken.Add(state);
                        found++;
                    }
                    else
                    {
                        kept.Enqueue(state);
                    }
                }

                m_RuntimeQueues[col] = kept;
            }

            return found;
        }

        // Trả các state đã lấy ra ngược lại Bin (đưa lên đầu hàng đợi cột 0 để lấy lại trước).
        public void ReturnStates(List<BlockState> states)
        {
            if (states == null || states.Count == 0 || m_ColumnCount <= 0)
                return;

            var rebuilt = new Queue<BlockState>();
            for (int i = 0; i < states.Count; i++)
                rebuilt.Enqueue(states[i]);

            Queue<BlockState> existing = m_RuntimeQueues[0];
            if (existing != null)
            {
                foreach (BlockState state in existing)
                    rebuilt.Enqueue(state);
            }

            m_RuntimeQueues[0] = rebuilt;
        }
    }
    #region Boosters

    //sử dụng hint booster
    public bool TryHintBooster()
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady || IsInputBlocked())
        {
            Debug.Log($"[Hint] Bị chặn: board={m_BoardManager != null}, ready={m_BoardManager?.IsGridReady}, inputBlocked={IsInputBlocked()}");
            return false;
        }
        if (m_HintCoroutine != null)
        {
            Debug.Log("[Hint] Bị chặn: đang có hint chạy (m_HintCoroutine != null)");
            return false;
        }

        if (!TryFindHintBlocks(m_ActiveHintBlocks))
        {
            Debug.Log("[PlayManager] Không tìm thấy gợi ý.");
            return false;
        }

        foreach (BlockManager block in m_ActiveHintBlocks)
            block?.PlayHintHighlight(c_HintDuration);

        m_HintCoroutine = StartCoroutine(HintTimeoutRoutine());
        return true;
    }

    public void hintBooster()
    {
        TryHintBooster();
    }

    //timeout hint
    private IEnumerator HintTimeoutRoutine()
    {
        yield return new WaitForSeconds(c_HintDuration);
        ClearActiveHint();
        m_HintCoroutine = null;
    }

    //xóa hint active
    private void ClearActiveHint()
    {
        if (m_HintCoroutine != null)
        {
            StopCoroutine(m_HintCoroutine);
            m_HintCoroutine = null;
        }

        for (int i = 0; i < m_ActiveHintBlocks.Count; i++)
            m_ActiveHintBlocks[i]?.ClearHintHighlight();

        m_ActiveHintBlocks.Clear();
    }

    //tìm các block có thể merge
    private bool TryFindHintBlocks(List<BlockManager> result)
    {
        result.Clear();

        List<BlockManager> allBlocks = CollectAllBlocks();
        if (allBlocks.Count < 2)
            return false;

        //kiểm tra từng cặp block
        for (int i = 0; i < allBlocks.Count; i++)
        {
            BlockManager a = allBlocks[i];
            //kiểm tra từng cặp block
            for (int j = i + 1; j < allBlocks.Count; j++)
            {
                BlockManager b = allBlocks[j];
                //kiểm tra merge có hợp lệ không
                if (!CanMergeBlocks(a, b))
                    continue;

                MergeResult merge = ResolveMerge(a, b);
                //kiểm tra merge có hợp lệ không
                if (!merge.ShouldClear)
                    continue;

                //thêm block vào result
                AddUniqueHintBlock(result, a);
                //thêm block vào result
                AddUniqueHintBlock(result, b);
                //thêm block vào result
                TryAddThirdSameType(result, a.TypeId, allBlocks);
                //trả về true
                return true;
            }
        }

        //kiểm tra từng block
        Dictionary<int, List<BlockManager>> byType = new Dictionary<int, List<BlockManager>>();
        foreach (BlockManager block in allBlocks)
        {
            //kiểm tra block có type id khác 0 không
            if (block.TypeId == 0)
                continue;

            //kiểm tra block có type id trong byType không
            if (!byType.TryGetValue(block.TypeId, out List<BlockManager> list))
            {
                list = new List<BlockManager>();
                //thêm block vào byType
                byType[block.TypeId] = list;
            }

            //thêm block vào list
            list.Add(block);
        }

        foreach (KeyValuePair<int, List<BlockManager>> pair in byType)
        {
            if (pair.Value.Count < 3)
                continue;

            result.Add(pair.Value[0]);
            result.Add(pair.Value[1]);
            result.Add(pair.Value[2]);
            return true;
        }

        for (int i = 0; i < allBlocks.Count; i++)
        {
            BlockManager a = allBlocks[i];
            for (int j = i + 1; j < allBlocks.Count; j++)
            {
                BlockManager b = allBlocks[j];
                if (!CanMergeBlocks(a, b))
                    continue;

                result.Add(a);
                result.Add(b);
                TryAddThirdSameType(result, a.TypeId, allBlocks);
                return true;
            }
        }

        return false;
    }

    private List<BlockManager> CollectAllBlocks()
    {
        List<BlockManager> blocks = new List<BlockManager>();

        for (int r = 0; r < m_BoardManager.Rows; r++)
        {
            for (int c = 0; c < m_BoardManager.Columns; c++)
            {
                BlockManager block = m_BoardManager.GetBlock(r, c);
                if (block != null && !block.IsPendingDestroy)
                    blocks.Add(block);
            }
        }

        return blocks;
    }

    private static void AddUniqueHintBlock(List<BlockManager> list, BlockManager block)
    {
        if (block != null && !list.Contains(block))
            list.Add(block);
    }

    private static void TryAddThirdSameType(List<BlockManager> result, int typeId, List<BlockManager> allBlocks)
    {
        if (result.Count >= 3)
            return;

        foreach (BlockManager block in allBlocks)
        {
            if (block.TypeId != typeId || result.Contains(block))
                continue;

            result.Add(block);
            if (result.Count >= 3)
                return;
        }
    }
    /// <summary>
    /// Freeze the timer for 10 seconds and show the frost animation
    /// </summary>
    public bool TryFreezeTimer()
    {
        if (!HasTimeLimit || !m_IsTimerRunning || m_IsTimerFrozen || m_OutcomeResolved)
            return false;

        if (m_FreezeCoroutine != null)
            return false;

        if (m_IsFreezeAnimationPlaying)
            return false;

        if (m_FrostAnimation == null)
        {
            Debug.LogWarning("[PlayManager] Chưa gán FrostAnimation.");
            StartFreezeDuration();
            return true;
        }

        m_IsFreezeAnimationPlaying = true;

        m_FrostAnimation.Configure(
            m_FrostAnimation.startPos,
            m_FrostAnimation.endPos,
            () =>
            {
                m_IsFreezeAnimationPlaying = false;
                StartFreezeDuration();
            });

        return true;
    }

    public void FreezeTimer()
    {
        TryFreezeTimer();
    }

    private void StartFreezeDuration()
    {
        if (m_FreezeCoroutine != null)
            return;

        m_FreezeCoroutine = StartCoroutine(FreezeTimerRoutine());
    }

    private IEnumerator FreezeTimerRoutine()
    {
        m_IsTimerFrozen = true;
        RefreshTimeLimitText();

        yield return new WaitForSeconds(c_FreezeDuration);

        m_IsTimerFrozen = false;
        m_FreezeCoroutine = null;
        m_FrostAnimation?.OffFrostImageBG();
        RefreshTimeLimitText();
    }

    private void CancelFreeze()
    {
        if (m_FreezeCoroutine != null)
        {
            StopCoroutine(m_FreezeCoroutine);
            m_FreezeCoroutine = null;
        }

        m_IsTimerFrozen = false;
        m_FrostAnimation?.OffFrostImageBG();
        m_FrostAnimation?.ResetAnimation();
    }
    public bool TryShuffleBoard()
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady || m_BlockDatabase == null)
            return false;

        if (IsInputBlocked() || m_IsDragging)
            return false;

        ClearActiveHint();

        if (m_VisualBooster == null)
        {
            ExecuteShuffleBoardCore();
            return true;
        }

        CollectShuffleBlocks(m_ShuffleBlocks);
        if (m_ShuffleBlocks.Count < 2)
            return false;

        SetLockInput(true);
        m_VisualBooster.PlayShuffle(
            ExecuteShuffleBoardCore,
            HandleShuffleVisualCompleted,
            m_ShuffleBlocks);
        return true;
    }

    /// <summary>Xáo trộn toàn bộ block trên board và bin còn lại.</summary>
    public void ShuffleBoard()
    {
        TryShuffleBoard();
    }

    /// <summary>
    /// Chạy logic shuffle thuần, không chứa visual và không tự khóa gameplay.
    /// Hàm này được gọi từ peak flash của VisualBooster để giữ nguyên thuật toán hiện có.
    /// </summary>
    private void ExecuteShuffleBoardCore()
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady || m_BlockDatabase == null)
            return;

        var boardBlocks = new List<BlockManager>();
        var pool = new List<BlockState>();

        CollectBoardStatesForShuffle(boardBlocks, pool);
        int[] binCountsPerCol = m_RefillState.DrainAllTo(pool);

        if (pool.Count < 2)
            return;

        FisherYatesShuffle(pool);

        int index = 0;
        for (int i = 0; i < boardBlocks.Count; i++)
            ApplyBlockStateToBlock(boardBlocks[i], pool[index++]);

        m_RefillState.RebuildFrom(pool, index, binCountsPerCol);
    }

    /// <summary>
    /// Chỉ mở lại gameplay sau khi visual shuffle hoàn tất hoàn toàn.
    /// </summary>
    private void HandleShuffleVisualCompleted()
    {
        if (!m_OutcomeResolved)
            SetLockInput(false);
    }

    private void CollectBoardStatesForShuffle(List<BlockManager> boardBlocks, List<BlockState> pool)
    {
        for (int r = 0; r < m_BoardManager.Rows; r++)
        {
            for (int c = 0; c < m_BoardManager.Columns; c++)
            {
                BlockManager block = m_BoardManager.GetBlock(r, c);
                if (block == null || block.IsPendingDestroy)
                    continue;

                boardBlocks.Add(block);
                pool.Add(BlockState.FromBoard(block));
            }
        }
    }

    private static void FisherYatesShuffle(List<BlockState> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            BlockState tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
    /// <summary>
    /// Thu toàn bộ block hợp lệ đang nằm trên board vào list cache.
    /// Không tạo list mới để hạn chế GC Allocation trong gameplay.
    /// </summary>
    private void CollectShuffleBlocks(List<BlockManager> _result)
    {
        _result.Clear();

        if (m_BoardManager == null)
            return;

        for (int row = 0; row < m_BoardManager.Rows; row++)
        {
            for (int col = 0; col < m_BoardManager.Columns; col++)
            {
                BlockManager block = m_BoardManager.GetBlock(row, col);
                if (block == null || block.IsPendingDestroy)
                    continue;

                _result.Add(block);
            }
        }
    }
    /// <summary>
    /// Tự động gom đủ 3 block cùng TypeKey từ Board + Bin rồi merge bằng luật hiện có.
    /// </summary>

    //tự động gom đủ 3 block cùng TypeKey từ Board + Bin rồi merge bằng luật hiện có.
    public bool TryMagnetBooster()
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady || m_BlockDatabase == null)
            return false;

        if (IsInputBlocked() || m_IsDragging || m_MagnetCoroutine != null)
            return false;

        ClearActiveHint();

        if (!TryResolveMagnetPlan(
                out string typeKey,
                out List<BlockManager> boardBlocks,
                out int needFromBin,
                out int requiredBinStack,
                out bool isPairMode))
        {
            Debug.Log("[PlayManager] Magnet: không tìm thấy merge khả thi.");
            return false;
        }

        m_MagnetIsPairMode = isPairMode;
        var trio = new List<BlockManager>(boardBlocks);

        if (needFromBin > 0)
        {
            // Yêu cầu đủ ô trống: không sinh block "nửa vời" khi board đã đầy.
            IReadOnlyList<Vector2Int> emptySlots = m_BoardManager.GetEmptySlots();
            if (emptySlots == null || emptySlots.Count < needFromBin)
                return false;

            var takenFromBin = new List<BlockState>();
            int takenCount = requiredBinStack >= 0
                ? m_RefillState.TryTakeMatchingTypeWithStack(typeKey, needFromBin, requiredBinStack, takenFromBin)
                : m_RefillState.TryTakeMatchingType(typeKey, needFromBin, takenFromBin);
            if (takenCount < needFromBin)
            {
                m_RefillState.ReturnStates(takenFromBin);
                return false;
            }

            var spawnedBlocks = new List<BlockManager>(takenFromBin.Count);
            for (int i = 0; i < takenFromBin.Count; i++)
            {
                BlockManager spawned = SpawnBlockFromState(takenFromBin[i]);
                if (spawned == null)
                {
                    RollbackMagnetSpawn(spawnedBlocks, takenFromBin);
                    return false;
                }

                Vector2Int slot = emptySlots[i];
                m_BoardManager.PlaceBlock(spawned, slot.x, slot.y);

                spawnedBlocks.Add(spawned);
                trio.Add(spawned);
            }

            // GẮN CHẶT: nếu chuỗi gộp thực tế không dùng HẾT block vừa sinh, hủy toàn
            // bộ và trả block về Bin — tuyệt đối không để block thừa nằm lại trên board.
            if (!MagnetSelectionConsumesAll(trio, spawnedBlocks))
            {
                RollbackMagnetSpawn(spawnedBlocks, takenFromBin);
                return false;
            }
        }

        if (trio.Count < 2)
            return false;

        SetLockInput(true);
        m_MagnetCoroutine = StartCoroutine(MagnetMergeRoutine(trio));
        return true;
    }

    public void MagnetBooster()
    {
        TryMagnetBooster();
    }
    //tìm kiếm các block cùng TypeKey từ Board + Bin
    private bool TryResolveMagnetPlan(
        out string typeKey,
        out List<BlockManager> boardBlocks,
        out int needFromBin,
        out int requiredBinStack,
        out bool isPairMode)
    {
        // 1. KHỞI TẠO CÁC GIÁ TRỊ ĐẦU RA MẶC ĐỊNH
        typeKey = null;
        boardBlocks = new List<BlockManager>();
        needFromBin = 0;
        requiredBinStack = -1;
        isPairMode = false;

        var totalCounts = new Dictionary<string, int>();
        var binCounts = new Dictionary<string, int>();
        var binPool = new List<BlockState>();
        m_RefillState.AppendBinStates(binPool);

        // Đếm số lượng trên Board
        for (int r = 0; r < m_BoardManager.Rows; r++)
        {
            for (int c = 0; c < m_BoardManager.Columns; c++)
            {
                BlockManager block = m_BoardManager.GetBlock(r, c);
                if (block == null || block.IsPendingDestroy || string.IsNullOrEmpty(block.TypeKey))
                    continue;

                AddTypeKeyCount(totalCounts, block.TypeKey, 1);
            }
        }

        // Đếm số lượng trong Bin
        foreach (BlockState state in binPool)
        {
            if (string.IsNullOrEmpty(state.TypeKey))
                continue;

            AddTypeKeyCount(totalCounts, state.TypeKey, 1);
            AddTypeKeyCount(binCounts, state.TypeKey, 1);
        }

        string bestKey = null;
        int bestBoardCount = -1;

        // 2. DUYỆT QUA TỪNG LOẠI KHỐI ĐỂ TÌM PHƯƠNG ÁN
        foreach (KeyValuePair<string, int> pair in totalCounts)
        {
            // Tổng số lượng cả Board + Bin phải >= 3
            if (pair.Value < 3)
                continue;

            int onBoard = pair.Value - GetTypeKeyCount(binCounts, pair.Key);

            // Ưu tiên loại khối có số lượng trên bàn nhiều hơn
            if (onBoard <= bestBoardCount)
                continue;

            List<BlockManager> candidateBoard = CollectBoardBlocksByTypeKey(pair.Key, 3);

            var boardStates = new List<BlockState>(candidateBoard.Count);
            for (int i = 0; i < candidateBoard.Count; i++)
                boardStates.Add(BlockState.FromBoard(candidateBoard[i]));

            int binAvailable = GetTypeKeyCount(binCounts, pair.Key);

            // Tìm số block Bin TỐI THIỂU sao cho chuỗi merge vẫn nổ và MỌI block
            // lấy từ Bin đều được dùng — tránh sinh dư rồi để lại trên board.
            int chosenNeedFromBin = -1;
            for (int take = 0; take <= binAvailable; take++)
            {
                if (boardStates.Count + take < 2)
                    continue;

                var trialStates = new List<BlockState>(boardStates);
                trialStates.AddRange(CollectBinStatesByTypeKey(binPool, pair.Key, take));

                if (!MagnetSimClearsAndUsesAllBin(trialStates, boardStates.Count))
                    continue;

                chosenNeedFromBin = take;
                break;
            }

            if (chosenNeedFromBin < 0)
                continue;

            // Ghi nhận kế hoạch thành công (đã đảm bảo bin block sẽ được dùng hết)
            bestKey = pair.Key;
            bestBoardCount = onBoard;
            boardBlocks = candidateBoard;
            needFromBin = chosenNeedFromBin;
        }

        if (bestKey != null)
        {
            typeKey = bestKey;
            return true;
        }

        if (HasAnyTypeCountAtLeast(totalCounts, 3))
            return false;

        return TryResolveMagnetPairPlan(
            binPool,
            out typeKey,
            out boardBlocks,
            out needFromBin,
            out requiredBinStack,
            out isPairMode);
    }

    private static bool HasAnyTypeCountAtLeast(Dictionary<string, int> counts, int min)
    {
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value >= min)
                return true;
        }

        return false;
    }

    private bool TryResolveMagnetPairPlan(
        IReadOnlyList<BlockState> binPool,
        out string typeKey,
        out List<BlockManager> boardBlocks,
        out int needFromBin,
        out int requiredBinStack,
        out bool isPairMode)
    {
        typeKey = null;
        boardBlocks = new List<BlockManager>();
        needFromBin = 0;
        requiredBinStack = -1;
        isPairMode = false;

        string bestKey = null;
        int bestOnBoard = -1;
        List<BlockManager> bestBoardBlocks = null;
        int bestNeedFromBin = 0;
        int bestRequiredBinStack = -1;

        var typeKeys = new HashSet<string>();
        for (int r = 0; r < m_BoardManager.Rows; r++)
        {
            for (int c = 0; c < m_BoardManager.Columns; c++)
            {
                BlockManager block = m_BoardManager.GetBlock(r, c);
                if (block == null || block.IsPendingDestroy || string.IsNullOrEmpty(block.TypeKey))
                    continue;

                typeKeys.Add(block.TypeKey);
            }
        }

        foreach (BlockState state in binPool)
        {
            if (!string.IsNullOrEmpty(state.TypeKey))
                typeKeys.Add(state.TypeKey);
        }

        foreach (string key in typeKeys)
        {
            BlockManager boardStack2 = null;
            BlockManager boardStack1 = null;
            int onBoard = 0;

            for (int r = 0; r < m_BoardManager.Rows; r++)
            {
                for (int c = 0; c < m_BoardManager.Columns; c++)
                {
                    BlockManager block = m_BoardManager.GetBlock(r, c);
                    if (block == null || block.IsPendingDestroy || block.TypeKey != key)
                        continue;

                    onBoard++;

                    if (block.StackCount == 2 && block.Tier2MergeStage == 1)
                        boardStack2 = block;
                    else if (block.StackCount == 1)
                        boardStack1 = block;
                }
            }

            bool binHasStack1 = false;
            bool binHasStack2 = false;
            foreach (BlockState state in binPool)
            {
                if (state.TypeKey != key)
                    continue;

                if (state.Stack == 1)
                    binHasStack1 = true;
                else if (state.Stack == 2)
                    binHasStack2 = true;
            }

            if (boardStack2 != null && boardStack1 != null
                && CanPairDirectClear(boardStack2, boardStack1))
            {
                if (onBoard <= bestOnBoard)
                    continue;

                bestKey = key;
                bestOnBoard = onBoard;
                bestBoardBlocks = new List<BlockManager> { boardStack2, boardStack1 };
                bestNeedFromBin = 0;
                bestRequiredBinStack = -1;
                continue;
            }

            if (boardStack2 != null && boardStack1 == null && binHasStack1
                && CanPairDirectClearSim(
                    BlockState.FromBoard(boardStack2),
                    new BlockState { TypeKey = key, Stack = 1, Tier2MergeStage = 0 }))
            {
                if (onBoard <= bestOnBoard)
                    continue;

                bestKey = key;
                bestOnBoard = onBoard;
                bestBoardBlocks = new List<BlockManager> { boardStack2 };
                bestNeedFromBin = 1;
                bestRequiredBinStack = 1;
                continue;
            }

            if (boardStack1 != null && boardStack2 == null && binHasStack2
                && CanPairDirectClearSim(
                    new BlockState { TypeKey = key, Stack = 2, Tier2MergeStage = 1 },
                    BlockState.FromBoard(boardStack1)))
            {
                if (onBoard <= bestOnBoard)
                    continue;

                bestKey = key;
                bestOnBoard = onBoard;
                bestBoardBlocks = new List<BlockManager> { boardStack1 };
                bestNeedFromBin = 1;
                bestRequiredBinStack = 2;
            }
        }

        if (bestKey == null)
            return false;

        typeKey = bestKey;
        boardBlocks = bestBoardBlocks;
        needFromBin = bestNeedFromBin;
        requiredBinStack = bestRequiredBinStack;
        isPairMode = true;
        return true;
    }

    private bool CanPairDirectClear(BlockManager stack2, BlockManager stack1)
    {
        if (stack2 == null || stack1 == null)
            return false;

        if (stack2.StackCount != 2 || stack1.StackCount != 1)
            return false;

        if (stack2.Tier2MergeStage != 1)
            return false;

        return CanMergeBlocks(stack1, stack2) && ResolveMerge(stack1, stack2).ShouldClear;
    }

    private static bool CanPairDirectClearSim(BlockState stack2, BlockState stack1)
    {
        if (stack2.Stack != 2 || stack1.Stack != 1)
            return false;

        if (stack2.Tier2MergeStage != 1)
            return false;

        return CanMergeSim(stack1, stack2) && ResolveMergeSimClears(stack1, stack2);
    }

    private List<BlockManager> CollectBoardBlocksByTypeKey(string typeKey, int maxCount)
    {
        var result = new List<BlockManager>();

        for (int r = 0; r < m_BoardManager.Rows; r++)
        {
            for (int c = 0; c < m_BoardManager.Columns; c++)
            {
                BlockManager block = m_BoardManager.GetBlock(r, c);
                if (block == null || block.IsPendingDestroy || block.TypeKey != typeKey)
                    continue;

                result.Add(block);
                if (result.Count >= maxCount)
                    return result;
            }
        }

        return result;
    }


    private static void AddTypeKeyCount(Dictionary<string, int> counts, string key, int delta)
    {
        counts.TryGetValue(key, out int current);
        counts[key] = current + delta;
    }

    private static int GetTypeKeyCount(Dictionary<string, int> counts, string key)
    {
        return counts.TryGetValue(key, out int value) ? value : 0;
    }

    private static bool CanSimulateMagnetMerge(IReadOnlyList<BlockState> states)
    {
        if (states == null || states.Count < 3)
            return false;

        if (TryFindSimulatedDirectClear(states))
            return true;

        for (int i = 0; i < states.Count; i++)
        {
            for (int j = 0; j < states.Count; j++)
            {
                if (i == j || !CanMergeSim(states[i], states[j]))
                    continue;

                BlockState merged = ApplyMergeSim(states[i], states[j]);
                for (int k = 0; k < states.Count; k++)
                {
                    if (k == i || k == j)
                        continue;

                    if (CanMergeSim(states[k], merged) && ResolveMergeSimClears(states[k], merged))
                        return true;
                }
            }
        }

        return false;
    }
    //kiểm tra các block có thể merge không
    private static bool TryFindSimulatedDirectClear(IReadOnlyList<BlockState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            for (int j = 0; j < states.Count; j++)
            {
                if (i == j || !CanMergeSim(states[i], states[j]))
                    continue;

                if (ResolveMergeSimClears(states[i], states[j]))
                    return true;
            }
        }

        return false;
    }

    //kiểm tra các block có thể merge không
    private static bool CanMergeSim(BlockState a, BlockState b)
    {
        int totalStack = a.Stack + b.Stack;

        if (a.Stack == 1 && b.Stack == 1)
            return totalStack <= 3;

        if (a.Stack == 2 && b.Stack == 2)
        {
            if (a.Tier2MergeStage == 2 && b.Tier2MergeStage == 2)
                return false;

            return true;
        }

        if (totalStack != 3)
            return false;

        int tier2Stage = a.Stack == 2 ? a.Tier2MergeStage : b.Tier2MergeStage;
        return tier2Stage == 1;
    }
    //áp dụng merge sim
    private static BlockState ApplyMergeSim(BlockState source, BlockState target)
    {
        if (source.Stack == 2 && target.Stack == 2)
        {
            return new BlockState
            {
                TypeKey = target.TypeKey,
                Stack = 2,
                Tier2MergeStage = target.Tier2MergeStage + source.Tier2MergeStage
            };
        }

        int newStack = target.Stack + source.Stack;
        int newStage = target.Tier2MergeStage;
        if (newStack == 2 && source.Stack == 1 && target.Stack == 1)
            newStage = 1;

        return new BlockState
        {
            TypeKey = target.TypeKey,
            Stack = newStack,
            Tier2MergeStage = newStage
        };
    }
    //kiểm tra các block có thể merge không
    private static bool ResolveMergeSimClears(BlockState source, BlockState target)
    {
        if (source.Stack == 2 && target.Stack == 2)
            return target.Tier2MergeStage + source.Tier2MergeStage >= 3;

        BlockState merged = ApplyMergeSim(source, target);
        return merged.Stack >= 3;
    }

    // Lấy tối đa "count" block trong Bin theo TypeKey (giữ đúng thứ tự duyệt Bin).
    private static List<BlockState> CollectBinStatesByTypeKey(
        IReadOnlyList<BlockState> binPool,
        string typeKey,
        int count)
    {
        var result = new List<BlockState>();
        if (count <= 0 || binPool == null)
            return result;

        for (int i = 0; i < binPool.Count; i++)
        {
            if (binPool[i].TypeKey != typeKey)
                continue;

            result.Add(binPool[i]);
            if (result.Count >= count)
                break;
        }

        return result;
    }

    // Mô phỏng ĐÚNG cách MagnetMergeRoutine chọn block để gộp: ưu tiên direct-clear
    // (2 block), nếu không thì two-step (tối đa 3 block). Trả về tập index được dùng.
    private static bool TrySimulateMagnetSelection(
        IReadOnlyList<BlockState> states,
        List<int> consumedIndices,
        out bool clears)
    {
        consumedIndices.Clear();
        clears = false;

        if (states == null || states.Count < 2)
            return false;

        // Direct clear: cặp gộp đầu tiên đã nổ ngay.
        for (int i = 0; i < states.Count; i++)
        {
            for (int j = 0; j < states.Count; j++)
            {
                if (i == j)
                    continue;
                if (!CanMergeSim(states[i], states[j]))
                    continue;
                if (!ResolveMergeSimClears(states[i], states[j]))
                    continue;

                consumedIndices.Add(i);
                consumedIndices.Add(j);
                clears = true;
                return true;
            }
        }

        // Two-step: chọn cặp gộp đầu tiên (ưu tiên 1+1), rồi tới block thứ ba.
        int srcIdx = -1;
        int tgtIdx = -1;
        int fallbackSrc = -1;
        int fallbackTgt = -1;

        for (int i = 0; i < states.Count && srcIdx < 0; i++)
        {
            for (int j = 0; j < states.Count; j++)
            {
                if (i == j)
                    continue;
                if (!CanMergeSim(states[i], states[j]))
                    continue;

                if (states[i].Stack == 1 && states[j].Stack == 1)
                {
                    srcIdx = i;
                    tgtIdx = j;
                    break;
                }

                if (fallbackSrc < 0)
                {
                    fallbackSrc = i;
                    fallbackTgt = j;
                }
            }
        }

        if (srcIdx < 0)
        {
            srcIdx = fallbackSrc;
            tgtIdx = fallbackTgt;
        }

        if (srcIdx < 0)
            return false;

        consumedIndices.Add(srcIdx);
        consumedIndices.Add(tgtIdx);

        BlockState merged = ApplyMergeSim(states[srcIdx], states[tgtIdx]);

        // Runtime lấy block "thứ ba" là block ĐẦU TIÊN khác src/tgt, rồi mới kiểm tra gộp.
        for (int k = 0; k < states.Count; k++)
        {
            if (k == srcIdx || k == tgtIdx)
                continue;

            if (CanMergeSim(states[k], merged))
            {
                consumedIndices.Add(k);
                clears = ResolveMergeSimClears(states[k], merged);
            }

            break;
        }

        return true;
    }

    // Plan hợp lệ khi: chuỗi gộp nổ VÀ mọi block lấy từ Bin (index >= boardCount) đều được dùng.
    private static bool MagnetSimClearsAndUsesAllBin(IReadOnlyList<BlockState> states, int boardCount)
    {
        var consumed = new List<int>();
        if (!TrySimulateMagnetSelection(states, consumed, out bool clears))
            return false;

        if (!clears)
            return false;

        for (int idx = boardCount; idx < states.Count; idx++)
        {
            if (!consumed.Contains(idx))
                return false;
        }

        return true;
    }

    // Đảm bảo TẤT CẢ block sinh thêm sẽ được chuỗi gộp thực tế dùng tới, dựa trên
    // đúng logic chọn cặp của MagnetMergeRoutine — nếu không thì từ chối chạy.
    private bool MagnetSelectionConsumesAll(
        List<BlockManager> trio,
        List<BlockManager> mustConsume)
    {
        if (mustConsume == null || mustConsume.Count == 0)
            return true;

        var consumed = new HashSet<BlockManager>();

        if (TryFindDirectClearPair(trio, out BlockManager directSource, out BlockManager directTarget))
        {
            consumed.Add(directSource);
            consumed.Add(directTarget);
        }
        else if (TryFindFirstMergePair(trio, out BlockManager stepSource, out BlockManager stepTarget))
        {
            consumed.Add(stepSource);
            consumed.Add(stepTarget);

            BlockManager third = FindThirdMagnetBlock(trio, stepSource, stepTarget);
            if (third != null && CanMergeBlocks(third, stepTarget))
                consumed.Add(third);
        }
        else
        {
            return false;
        }

        for (int i = 0; i < mustConsume.Count; i++)
        {
            if (!consumed.Contains(mustConsume[i]))
                return false;
        }

        return true;
    }

    // Hủy block đã sinh dở + trả state đã lấy về Bin khi kế hoạch Magnet bị hủy.
    private void RollbackMagnetSpawn(List<BlockManager> spawnedBlocks, List<BlockState> takenFromBin)
    {
        if (spawnedBlocks != null)
        {
            for (int i = 0; i < spawnedBlocks.Count; i++)
            {
                BlockManager block = spawnedBlocks[i];
                if (block == null)
                    continue;

                Slot slot = block.CurrentSlot;
                if (slot != null)
                    m_BoardManager.ClearSlot(slot.Row, slot.Col);

                Destroy(block.gameObject);
            }
        }

        if (takenFromBin != null)
            m_RefillState.ReturnStates(takenFromBin);
    }
    //tự động gom đủ 3 block cùng TypeKey từ Board + Bin rồi merge bằng luật hiện có.
    private IEnumerator MagnetMergeRoutine(List<BlockManager> blocks)
    {
        try
        {
            bool hasDirectClear = TryFindDirectClearPair(
                blocks,
                out BlockManager directSource,
                out BlockManager directTarget);

            BlockManager stepSource = null;
            BlockManager stepTarget = null;
            bool hasTwoStep = !hasDirectClear
                && TryFindFirstMergePair(blocks, out stepSource, out stepTarget);

            if (!hasDirectClear && !hasTwoStep)
                yield break;

            ResetMagnetDeferredState();

            List<BlockManager> flyBlocks = BuildMagnetFlyBlocks(
                blocks,
                hasDirectClear,
                directSource,
                directTarget);

            bool visualFinished = false;
            bool hammerCommitted = false;
            bool mergeExecuted = false;

            Action onFlyArrived = () =>
            {
                if (mergeExecuted)
                    return;

                mergeExecuted = true;
                ExecuteMagnetDeferredMergeChain(
                    blocks,
                    hasDirectClear,
                    directSource,
                    directTarget,
                    stepSource,
                    stepTarget);

                BlockManager survivor = m_MagnetSurvivor;
                if (survivor != null && !survivor.IsPendingDestroy)
                    survivor.PlayMergeImpact();
            };

            Action onHammerHit = () =>
            {
                if (hammerCommitted)
                    return;

                hammerCommitted = true;
                CommitMagnetSurvivorClear();
            };

            if (m_VisualBooster == null)
            {
                onFlyArrived();
                onHammerHit();
            }
            else
            {
                Vector3 screenCenterWorldPos = ResolveMagnetScreenCenterWorldPos();

                m_VisualBooster.PlayMagnet(
                    flyBlocks,
                    screenCenterWorldPos,
                    onFlyArrived,
                    onHammerHit,
                    () => visualFinished = true);

                while (!visualFinished)
                    yield return null;
            }

            yield return WaitGameplayIdle();
        }
        finally
        {
            ResetMagnetDeferredState();
            m_MagnetCoroutine = null;
            if (!m_OutcomeResolved)
                SetLockInput(false);
        }
    }

    private void ResetMagnetDeferredState()
    {
        m_MagnetDeferSettlement = false;
        m_MagnetSurvivor = null;
        m_MagnetSurvivorClearKind = MergeClearKind.None;
    }

    private void ExecuteMagnetDeferredMergeChain(
        IReadOnlyList<BlockManager> blocks,
        bool hasDirectClear,
        BlockManager directSource,
        BlockManager directTarget,
        BlockManager stepSource,
        BlockManager stepTarget)
    {
        m_MagnetDeferSettlement = true;

        try
        {
            if (hasDirectClear)
            {
                TriggerMagnetMerge(directSource, directTarget);
            }
            else
            {
                TriggerMagnetMerge(stepSource, stepTarget);

                BlockManager third = FindThirdMagnetBlock(blocks, stepSource, stepTarget);
                if (third != null && CanMergeBlocks(third, stepTarget))
                    TriggerMagnetMerge(third, stepTarget);
            }
        }
        finally
        {
            m_MagnetDeferSettlement = false;
        }
    }

    private static List<BlockManager> BuildMagnetFlyBlocks(
        IReadOnlyList<BlockManager> blocks,
        bool hasDirectClear,
        BlockManager directSource,
        BlockManager directTarget)
    {
        var flyBlocks = new List<BlockManager>(3);

        if (hasDirectClear)
        {
            if (directSource != null && !directSource.IsPendingDestroy)
                flyBlocks.Add(directSource);

            if (directTarget != null
                && !directTarget.IsPendingDestroy
                && directTarget != directSource)
            {
                flyBlocks.Add(directTarget);
            }

            return flyBlocks;
        }

        int count = blocks.Count;
        for (int i = 0; i < count; i++)
        {
            BlockManager block = blocks[i];
            if (block == null || block.IsPendingDestroy)
                continue;

            flyBlocks.Add(block);
        }

        return flyBlocks;
    }

    /// <summary>
    /// Sau khi búa đập, destroy block survivor và mới bắt đầu gravity/settlement.
    /// </summary>
    private void CommitMagnetSurvivorClear()
    {
        if (m_PendingTier2TripleClearTarget != null)
        {
            CommitPendingTier2TripleClear();
            ResetMagnetDeferredState();
            return;
        }

        BlockManager survivor = m_MagnetSurvivor;
        MergeClearKind clearKind = m_MagnetSurvivorClearKind;
        ResetMagnetDeferredState();

        if (survivor == null)
        {
            RequestGravityOnly();
            return;
        }

        if (clearKind == MergeClearKind.NormalBurst)
        {
            ClearMergedBlock(survivor, isTier2TripleClear: false);
            return;
        }

        RequestGravityOnly();
    }

    /// <summary>
    /// Lấy tâm màn hình theo camera hiện tại, khớp hệ world-space của board (z = 0).
    /// </summary>
    private Vector3 ResolveMagnetScreenCenterWorldPos()
    {
        Camera cam = m_MainCamera != null ? m_MainCamera : Camera.main;
        if (cam == null)
            return Vector3.zero;

        const float boardPlaneZ = 0f;
        float planeDistance = Mathf.Abs(cam.transform.position.z - boardPlaneZ);
        Vector3 viewportCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, planeDistance));
        return new Vector3(viewportCenter.x, viewportCenter.y, boardPlaneZ);
    }
    // Nên đổi thành — merge trực tiếp
    private void TriggerMagnetMerge(BlockManager source, BlockManager target)
    {
        if (source == null || target == null) return;
        if (!CanMergeBlocks(source, target)) return;
        m_IsTurnProcessing = true;
        ExecuteMerge(source, target);
    }
    private IEnumerator WaitGameplayIdle()
    {
        while (m_IsTurnProcessing || m_IsSettlementRunning)
            yield return null;
    }
    //tìm kiếm các block có thể merge không 
    private bool TryFindDirectClearPair(
        IReadOnlyList<BlockManager> blocks,
        out BlockManager source,
        out BlockManager target)
    {
        source = null;
        target = null;

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockManager a = blocks[i];
            if (a == null || a.IsPendingDestroy)
                continue;

            for (int j = 0; j < blocks.Count; j++)
            {
                if (i == j)
                    continue;

                BlockManager b = blocks[j];
                if (b == null || b.IsPendingDestroy || !CanMergeBlocks(a, b))
                    continue;

                MergeResult merge = ResolveMerge(a, b);
                if (!merge.ShouldClear)
                    continue;

                source = a;
                target = b;
                return true;
            }
        }

        return false;
    }
    //tìm kiếm các block có thể merge không từ Board
    private bool TryFindFirstMergePair(
        IReadOnlyList<BlockManager> blocks,
        out BlockManager source,
        out BlockManager target)
    {
        source = null;
        target = null;
        BlockManager fallbackSource = null;
        BlockManager fallbackTarget = null;

        for (int i = 0; i < blocks.Count; i++)
        {
            BlockManager a = blocks[i];
            if (a == null || a.IsPendingDestroy)
                continue;

            for (int j = 0; j < blocks.Count; j++)
            {
                if (i == j)
                    continue;

                BlockManager b = blocks[j];
                if (b == null || b.IsPendingDestroy || !CanMergeBlocks(a, b))
                    continue;

                if (a.StackCount == 1 && b.StackCount == 1)
                {
                    source = a;
                    target = b;
                    return true;
                }

                if (fallbackSource == null)
                {
                    fallbackSource = a;
                    fallbackTarget = b;
                }
            }
        }

        if (fallbackSource == null)
            return false;

        source = fallbackSource;
        target = fallbackTarget;
        return true;
    }
    //tìm kiếm các block có thể merge không từ Board + Bin
    private static BlockManager FindThirdMagnetBlock(
        IReadOnlyList<BlockManager> blocks,
        BlockManager firstSource,
        BlockManager survivor)
    {
        foreach (BlockManager block in blocks)
        {
            if (block == null || block.IsPendingDestroy)
                continue;

            if (block == firstSource || block == survivor)
                continue;

            return block;
        }

        return null;
    }
    #endregion Boosters
    [Button("Spawn Vfx Block")]
    public void SpawnVfxBlock(Vector3 position)
    {
        m_CurrentBlockVfx = m_BlockPool.Spawn(
            position,
            Quaternion.identity,
            m_BlockPool.transform
        );
        m_BlockPool.Despawn(m_CurrentBlockVfx, 1f);
    }

    [Button("Despawn Vfx Block")]
    public void DespawnVfxBlock()
    {
        if (m_CurrentBlockVfx != null)
        {
            m_BlockPool.Despawn(m_CurrentBlockVfx);
            m_CurrentBlockVfx = null;
        }
    }
    [Button("Reset Save")]
    public void OnResetSaveClicked()
    {
        SaveManager.DeleteAll(); // hoặc DeleteAll()

        if (BoosterManager.Exists())
            BoosterManager.Ins.Initialized(); // load lại booster từ PlayerPrefs mới (rỗng)

        // Tuỳ chọn: refresh UI nếu đang mở GamePlayUI
        if (UIManager.Ins.IsOpened<GamePlayUI>())
            UIManager.Ins.GetUI<GamePlayUI>().RefreshBoosterUI();
    }
    #region Cheat

    public void CheatGoToLevel(int levelId)
    {
        if (m_Catalog == null)
        {
            Debug.LogWarning("[Cheat] Chưa có LevelCatalog.");
            return;
        }

        LevelCatalogEntry entry = m_Catalog.FindById(levelId);
        if (entry.LevelAsset == null)
        {
            Debug.LogWarning($"[Cheat] Level {levelId} không tồn tại.");
            return;
        }

        SaveManager.CurrentLevel = levelId;

        UIManager.Ins.CloseUI<WinUI>();
        UIManager.Ins.CloseUI<LoseUI>();

        LevelData level = m_LevelLoader.LoadFromEntry(entry, m_BlockDatabase);
        if (level != null)
            LoadLevel(level);


    }

    public void CheatSetTime(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        m_RemainingSeconds = seconds;

        if (m_TimeLimitSeconds <= 0 && seconds > 0)
            m_TimeLimitSeconds = seconds;

        m_IsTimerRunning = seconds > 0 && !m_OutcomeResolved;
        RefreshTimeLimitText();
    }

    public void CheatWin()
    {
        if (m_OutcomeResolved || m_CurrentLevel == null)
            return;

        EnterWin();
    }

    public void CheatLose()
    {
        if (m_OutcomeResolved || m_CurrentLevel == null)
            return;

        EnterLose();
    }

    public void CheatAddBoosters()
    {
        if (!BoosterManager.Exists())
            return;

        BoosterManager.Ins.CheatAddBooster();

        if (UIManager.Ins.IsOpened<GamePlayUI>())
            UIManager.Ins.GetUI<GamePlayUI>().RefreshBoosterUI();
    }

    public void CheatResetSave()
    {
        OnResetSaveClicked();
        SaveManager.ResetLevel();
    }

    #endregion
}

#region Settlement Structs

public struct GravityMoveCommand
{
    public BlockManager Block;
    public int FromRow;
    public int FromCol;
    public int ToRow;
    public int ToCol;
    public int DropDistance;
}

public struct ColumnPushCommand
{
    public BlockManager Block;
    public int FromRow;
    public int ToRow;
    public int Col;
}

public struct ColumnRefillPacket
{
    public int Col;
    public List<ColumnPushCommand> PushCommands;
    public BlockManager NewBlock;
}

public struct Tier2NeighborRestore
{
    public int Col;
    public string TypeKey;
    public int Stack;
    public int Tier2MergeStage;
}

public struct BlockState
{
    public string TypeKey;
    public int Stack;
    public int Tier2MergeStage;

    public static BlockState FromBoard(BlockManager block)
    {
        return new BlockState
        {
            TypeKey = block.TypeKey,
            Stack = block.StackCount,
            Tier2MergeStage = block.Tier2MergeStage
        };
    }

    public static BlockState FromBinCell(string typeKey, int stack)
    {
        return new BlockState
        {
            TypeKey = typeKey,
            Stack = stack,
            Tier2MergeStage = stack == 2 ? 1 : 0
        };
    }
}

#endregion