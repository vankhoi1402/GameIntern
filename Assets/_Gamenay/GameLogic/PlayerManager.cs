using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>
/// Trung tâm điều phối gameplay — input, merge, settlement, spawn refill.
/// </summary>
public class PlayManager : MonoBehaviour
{
    #region Constants

    private const float c_GravityAnimTimeout = 5f;
    private const float c_SettlementTimeout = 8f;
    private const float c_ClearFallDistance = 8f;
    private const float c_ClearFallDuration = 0.45f;
    private const float c_Tier2ExplodeDuration = 0.15f;
    private const float c_Tier2FallDuration = 0.4f;
    private const float c_HintDuration = 3f;
    private const float c_FreezeDuration = 10f;

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
    [SerializeField] private VictoryUIController m_VictoryUI;
    [SerializeField] private DefeatUIController m_DefeatUI;
    [SerializeField] private TextMeshProUGUI m_TimeLimitText;

    [Header("Input")]
    [SerializeField] private Camera m_MainCamera;
    [SerializeField] private LayerMask m_BlockLayer;

    [Header("Refill Animation")]
    [SerializeField] private float m_RefillRiseDuration = 0.16f;
    [SerializeField] private float m_RefillPushDuration = 0.15f;
    [SerializeField] private Ease m_RefillPushEase = Ease.OutSine;

    [Header("Boosters")]
    [SerializeField] private FrostAnimation m_FrostAnimation;

    #endregion

    #region Runtime State

    private readonly LevelLoader m_LevelLoader = new LevelLoader();
    private readonly LevelRefillState m_RefillState = new LevelRefillState();

    private LevelData m_CurrentLevel;
    private bool m_OutcomeResolved;

    private float m_RemainingSeconds;
    private int m_TimeLimitSeconds;
    private bool m_IsTimerRunning;

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

    private bool m_IsTimerFrozen;

    #endregion

    #region Timer State

    public float RemainingSeconds => m_RemainingSeconds;
    public int TimeLimitSeconds => m_TimeLimitSeconds;
    public bool IsTimerRunning => m_IsTimerRunning;
    public bool IsTimerFrozen => m_IsTimerFrozen;
    public bool HasTimeLimit => m_TimeLimitSeconds > 0;
    public bool IsTimerExpired => HasTimeLimit && m_RemainingSeconds <= 0f;
    private bool m_IsFreezeAnimationPlaying;
    public event Action OnTimerExpired;

    #endregion

    #region Properties

    public LevelData CurrentLevel => m_CurrentLevel;
    public bool HasLevel => m_CurrentLevel != null;
    public int CurrentLevelId => m_CurrentLevel != null ? m_CurrentLevel.LevelId : m_StartLevelId;
    public bool IsOutcomeResolved => m_OutcomeResolved;

    #endregion

    #region Events

    public event Action<bool> OnSettlementCompleted;
    public event Action<LevelData> OnLevelLoaded;

    public bool IsSettlementRunning => m_IsSettlementRunning;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        BindPopupEvents(true);
        OnSettlementCompleted += HandleSettlementCompleted;
    }

    private void OnDisable()
    {
        BindPopupEvents(false);
        OnSettlementCompleted -= HandleSettlementCompleted;
    }

    private void Update()
    {
        HandleInput();
        TickTimer();
    }

    #endregion

    #region Level Management

    private void Start()
    {
        LoadStartLevel();
    }

    public void LoadStartLevel()
    {
        if (m_Catalog == null || m_Catalog.Levels == null || m_Catalog.Levels.Length == 0)
        {
            Debug.LogWarning("[PlayManager] Chưa có LevelCatalog.");
            return;
        }

        LevelCatalogEntry entry = m_Catalog.FindById(m_StartLevelId);
        LevelData level = m_LevelLoader.LoadFromEntry(entry, m_BlockDatabase);

        if (level == null)
        {
            Debug.LogError($"[PlayManager] Level {m_StartLevelId} chưa có dữ liệu. Chạy Import CSV trước.");
            return;
        }

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
        if (m_TimeLimitText == null)
            return;

        if (!HasTimeLimit)
        {
            m_TimeLimitText.gameObject.SetActive(false);
            return;
        }

        m_TimeLimitText.gameObject.SetActive(true);

        int totalSeconds = Mathf.CeilToInt(m_RemainingSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        m_TimeLimitText.text = $"{minutes:00}:{seconds:00}";
    }

    private bool IsTimerPaused()
    {
        return m_IsInputLocked || m_IsSettlementRunning || m_IsTimerFrozen;
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
        CancelActiveDrag();
        SetLockInput(true);
        m_DefeatUI?.Hide(animated: false);

        int levelId = CurrentLevelId;
        bool hasNext = HasNextLevel();
        m_VictoryUI?.Show(levelId, hasNext);
    }

    private void EnterLose()
    {
        m_OutcomeResolved = true;
        StopTimer();
        CancelActiveDrag();
        SetLockInput(true);
        m_VictoryUI?.Hide(animated: false);
        m_DefeatUI?.Show(CurrentLevelId);
    }

    #endregion

    #region Refill

    public BlockState? TryDequeueBin(int col) => m_RefillState.TryDequeue(col);

    #endregion

    #region Session

    private void ResetOutcomeSession()
    {
        m_OutcomeResolved = false;
        m_VictoryUI?.Hide(animated: false);
        m_DefeatUI?.Hide(animated: false);
        SetLockInput(false);
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

        if (isTier2TripleClear)
        {
            targetBlock.PlayBurstExplodeThenFall(
                Vector3.up * 0.3f,
                m_MainCamera,
                c_Tier2ExplodeDuration,
                c_Tier2FallDuration,
                c_ClearFallDistance,
                0f,
                null,
                () =>
                {
                    if (targetBlock != null)
                        Destroy(targetBlock.gameObject);
                    RequestGravityOnly();
                });
            return;
        }

        targetBlock.PlayFallOffScreen(
            m_MainCamera,
            c_ClearFallDistance,
            c_ClearFallDuration,
            () =>
            {
                if (targetBlock != null)
                    Destroy(targetBlock.gameObject);
                RequestGravityOnly();
            });
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

        if (m_SettlementCoroutine != null)
        {
            StopCoroutine(m_SettlementCoroutine);
            m_SettlementCoroutine = null;
        }

        m_IsSettlementRunning = false;
        m_IsTurnProcessing = false;
        m_PendingTier2TripleClearTarget = null;
        m_PendingGravityAfterSettlement = false;
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

            int colCount = Mathf.Min(boardCols, gridRow.ColBlockTypes.Length);
            for (int col = 0; col < colCount; col++)
            {
                if (!m_LevelLoader.TryParseCell(gridRow.ColBlockTypes[col], out string typeKey, out int stack))
                    continue;

                SpawnBlockAt(gridRow.Row, col, typeKey, stack);
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
        if (m_BoardManager == null || !m_BoardManager.IsGridReady)
            return null;

        Vector2Int grid = m_BoardManager.WorldToGrid(GetMouseWorldPosition());
        return m_BoardManager.GetSlot(grid.x, grid.y);
    }

    private void UpdateHoverTarget(Vector3 mouseWorldPos)
    {
        BlockManager next = null;

        if (m_BoardManager != null && m_BoardManager.IsGridReady)
        {
            Vector2Int grid = m_BoardManager.WorldToGrid(mouseWorldPos);
            Slot slot = m_BoardManager.GetSlot(grid.x, grid.y);
            if (slot != null && slot != m_SourceSlot && slot.HasBlock)
                next = slot.CurrentBlock;
        }

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
        Debug.Log($"Source: {source.StackCount}, Target: {target.StackCount}");

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
    }
    #region Boosters

    //sử dụng hint booster
    public void hintBooster()
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady || IsInputBlocked())
            return;

        ClearActiveHint();

        if (!TryFindHintBlocks(m_ActiveHintBlocks))
        {
            Debug.Log("[PlayManager] Không tìm thấy gợi ý.");
            return;
        }

        foreach (BlockManager block in m_ActiveHintBlocks)
            block?.PlayHintHighlight(c_HintDuration);

        m_HintCoroutine = StartCoroutine(HintTimeoutRoutine());
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
    public void FreezeTimer()
    {
        if (!HasTimeLimit || !m_IsTimerRunning || m_IsTimerFrozen || m_OutcomeResolved)
            return;

        if (m_FreezeCoroutine != null)
            return;

        if (m_IsFreezeAnimationPlaying)
            return;

        if (m_FrostAnimation == null)
        {
            Debug.LogWarning("[PlayManager] Chưa gán FrostAnimation.");
            StartFreezeDuration();
            return;
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
    /// <summary>Xáo trộn toàn bộ block trên board và bin còn lại.</summary>
    public void ShuffleBoard()
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady || m_BlockDatabase == null)
            return;

        if (IsInputBlocked() || m_IsDragging)
            return;

        ClearActiveHint();

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
    /// Tự động gom đủ 3 block cùng TypeKey từ Board + Bin rồi merge bằng luật hiện có.
    /// </summary>

    //tự động gom đủ 3 block cùng TypeKey từ Board + Bin rồi merge bằng luật hiện có.
    public void MagnetBooster()
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady || m_BlockDatabase == null)
            return;

        if (IsInputBlocked() || m_IsDragging || m_MagnetCoroutine != null)
            return;

        ClearActiveHint();

        if (!TryResolveMagnetPlan(
                out string typeKey,
                out List<BlockManager> boardBlocks,
                out int needFromBin))
        {
            Debug.Log("[PlayManager] Magnet: không tìm thấy merge khả thi.");
            return;
        }

        var trio = new List<BlockManager>(boardBlocks);

        if (needFromBin > 0)
        {
            // Lấy danh sách ô trống hiện tại
            IReadOnlyList<Vector2Int> emptySlots = m_BoardManager.GetEmptySlots();

            var takenFromBin = new List<BlockState>();
            int takenCount = m_RefillState.TryTakeMatchingType(typeKey, needFromBin, takenFromBin);
            if (takenCount < needFromBin)
                return;

            for (int i = 0; i < takenFromBin.Count; i++)
            {
                BlockManager spawned = SpawnBlockFromState(takenFromBin[i]);
                if (spawned == null)
                    return;

                // ================= LOGIC SỬA ĐỔI Ô TRỐNG THÔNG MINH =================
                if (emptySlots != null && i < emptySlots.Count)
                {
                    // Nếu bàn CÒN ô trống, đặt khối vào ô trống như bình thường
                    Vector2Int slot = emptySlots[i];
                    m_BoardManager.PlaceBlock(spawned, slot.x, slot.y);
                }
                else
                {
                    // NẾU BÀN ĐẦY KÍN (0 ô trống): 
                    // Đặt tạm vị trí khối này trùng với viên khối đầu tiên trên Board 
                    // để tí nữa hàm MagnetMergeRoutine hút tụi nó nhập vào nhau tạo hiệu ứng nổ.
                    if (boardBlocks.Count > 0 && boardBlocks[0] != null)
                    {
                        spawned.transform.position = boardBlocks[0].transform.position;
                    }

                    // Chú ý: Không gọi PlaceBlock vì bàn đầy, khối này sẽ tồn tại ở trạng thái tự do 
                    // và được nạp ngay vào danh sách trio để chuẩn bị biến mất lập tức khi gộp.
                }
                // ===================================================================

                trio.Add(spawned);
            }
        }

        if (trio.Count < 3)
            return;

        m_MagnetCoroutine = StartCoroutine(MagnetMergeRoutine(trio));
    }
    //tìm kiếm các block cùng TypeKey từ Board + Bin
    private bool TryResolveMagnetPlan(
        out string typeKey,
        out List<BlockManager> boardBlocks,
        out int needFromBin)
    {
        // 1. KHỞI TẠO CÁC GIÁ TRỊ ĐẦU RA MẶC ĐỊNH
        typeKey = null;
        boardBlocks = new List<BlockManager>();
        needFromBin = 0;

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
            int candidateNeedFromBin = Mathf.Max(0, 3 - candidateBoard.Count);

            // Kiểm tra trong Bin có đủ số lượng viên đang thiếu hay không
            if (GetTypeKeyCount(binCounts, pair.Key) < candidateNeedFromBin)
                continue;

            // ======================================================================
            // ĐÃ GỠ BỎ HOÀN TOÀN ĐOẠN CODE KIỂM TRA Ô TRỐNG (GetEmptySlots) Ở ĐÂY!
            // ======================================================================

            // Tiến hành đóng gói dữ liệu nạp từ Bin
            var candidateBinStates = new List<BlockState>();
            int collected = 0;
            foreach (BlockState state in binPool)
            {
                if (state.TypeKey != pair.Key)
                    continue;

                candidateBinStates.Add(state);
                collected++;
                if (collected >= candidateNeedFromBin)
                    break;
            }

            // Gom đủ 3 khối (Board + Bin) vào danh sách giả lập ảo
            var planStates = new List<BlockState>(3);
            for (int i = 0; i < candidateBoard.Count; i++)
                planStates.Add(BlockState.FromBoard(candidateBoard[i]));
            planStates.AddRange(candidateBinStates);

            // Chạy qua phòng thí nghiệm ảo CanSimulateMagnetMerge (Bắt buộc gộp phải nổ)
            if (planStates.Count < 3 || !CanSimulateMagnetMerge(planStates))
                continue;

            // Nếu vượt qua tất cả các bộ lọc logic gộp, ghi nhận kế hoạch thành công
            bestKey = pair.Key;
            bestBoardCount = onBoard;
            boardBlocks = candidateBoard;
            needFromBin = candidateNeedFromBin;
        }

        // 3. KẾT LUẬN LUỒNG CHẠY
        if (bestKey == null)
            return false;

        typeKey = bestKey;
        return true;
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
    //tự động gom đủ 3 block cùng TypeKey từ Board + Bin rồi merge bằng luật hiện có.
    private IEnumerator MagnetMergeRoutine(List<BlockManager> blocks)
    {
        try
        {
            if (TryFindDirectClearPair(blocks, out BlockManager directSource, out BlockManager directTarget))
            {
                TriggerMagnetMerge(directSource, directTarget);
                yield break;
            }

            if (!TryFindFirstMergePair(blocks, out BlockManager stepSource, out BlockManager stepTarget))
                yield break;

            TriggerMagnetMerge(stepSource, stepTarget);
            yield return WaitGameplayIdle();

            if (stepTarget == null || stepTarget.IsPendingDestroy)
                yield break;

            BlockManager third = FindThirdMagnetBlock(blocks, stepSource, stepTarget);
            if (third == null || !CanMergeBlocks(third, stepTarget))
                yield break;

            TriggerMagnetMerge(third, stepTarget);
        }
        finally
        {
            m_MagnetCoroutine = null;
        }
    }
    //trigger magnet merge
    private void TriggerMagnetMerge(BlockManager source, BlockManager target)
    {
        m_SourceSlot = source.CurrentSlot;
        m_HoverSlot = target.CurrentSlot;
        ProcessTurn();
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