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
    [SerializeField] private Transform m_FreezeButtonTransform;

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

    public LevelBinBlock? TryDequeueBin(int col) => m_RefillState.TryDequeue(col);

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

        if (restore.Stack == 2 && restore.Tier2MergeStage > 0)
        {
            block.SetTier2MergeStage(restore.Tier2MergeStage);
            block.UpdateTier2StageVisual(block.StackCount, block.Tier2MergeStage);
        }

        return block;
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

            LevelBinBlock? binBlock = null;
            if (!restore.HasValue && includeBin)
                binBlock = TryDequeueBin(col);

            if (!restore.HasValue && !binBlock.HasValue)
                continue;

            if (!m_BoardManager.TryShiftColumnUp(col, out List<ColumnPushCommand> commands))
            {
                if (restore.HasValue && queue != null)
                    queue.Enqueue(restore.Value);
                continue;
            }

            BlockManager newBlock = restore.HasValue
                ? SpawnRestoredNeighbor(restore.Value)
                : SpawnBlockInstance(binBlock.Value.BlockType, binBlock.Value.Stack);

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

    private Vector3 ResolveAbsorbTargetWorldPos(BlockManager target)
    {
        if (target?.CurrentSlot == null || m_BoardManager == null || !m_BoardManager.IsGridReady)
            return target != null ? target.transform.position : Vector3.zero;

        Slot slot = target.CurrentSlot;
        int finalRow = m_BoardManager.PredictFinalRowAfterGravity(slot.Row, slot.Col);
        return m_BoardManager.GridToWorld(finalRow, slot.Col);
    }

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

        if (a.StackCount == 2 && b.StackCount == 2)
            return true;

        if (totalStack != 3)
            return false;

        BlockManager tier2Block = a.StackCount == 2 ? a : b;
        return tier2Block.Tier2MergeStage == 1;
    }

    private MergeResult ResolveMerge(BlockManager source, BlockManager target)
    {
        if (!CanMergeBlocks(source, target))
            return MergeResult.Invalid;

        bool isTier2Pair = source.StackCount == 2 && target.StackCount == 2;

        if (isTier2Pair)
        {
            int combinedStage = target.Tier2MergeStage + source.Tier2MergeStage;
            MergeClearKind clearKind = combinedStage >= 3
                ? MergeClearKind.Tier2Triple
                : MergeClearKind.None;

            return new MergeResult(true, 2, combinedStage, clearKind);
        }

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

    private void BeginMergeVisual(BlockManager source, BlockManager target)
        => m_MergeVisualTarget = target != null ? target : source;

    public void ClearMergeVisual() => m_MergeVisualTarget = null;

    public bool IsMergeVisualTarget(BlockManager block)
        => block != null && block == m_MergeVisualTarget;

    #endregion

    #region Board Query

    public bool HasAvailableMove()
    {
        if (m_BoardManager == null || !m_BoardManager.IsGridReady)
            return false;

        for (int r = 0; r < m_BoardManager.Rows; r++)
        {
            for (int c = 0; c < m_BoardManager.Columns; c++)
            {
                BlockManager a = m_BoardManager.GetBlock(r, c);
                if (a == null)
                    continue;

                if (TryMergePairAt(r, c, r + 1, c, a))
                    return true;
                if (TryMergePairAt(r, c, r, c + 1, a))
                    return true;
            }
        }

        return false;
    }

    private bool TryMergePairAt(int rowA, int colA, int rowB, int colB, BlockManager blockA)
    {
        BlockManager blockB = m_BoardManager.GetBlock(rowB, colB);
        return blockB != null && CanMergeBlocks(blockA, blockB);
    }

    #endregion

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

    private sealed class LevelRefillState
    {
        private static readonly LevelLoader s_CellParser = new LevelLoader();

        private readonly System.Collections.Generic.Dictionary<int, LevelGridRow> m_RowsByDepth =
            new System.Collections.Generic.Dictionary<int, LevelGridRow>();

        private int[] m_NextDepthByCol = System.Array.Empty<int>();
        private int m_MaxDepth = -1;
        private int m_BinStartRow;
        private int m_ColumnCount;

        public void Reset(LevelData level, int boardRows, int boardColumns)
        {
            m_RowsByDepth.Clear();
            m_MaxDepth = -1;
            m_BinStartRow = Mathf.Max(0, boardRows);
            m_ColumnCount = Mathf.Max(0, boardColumns);

            if (m_NextDepthByCol.Length != m_ColumnCount)
                m_NextDepthByCol = new int[m_ColumnCount];

            for (int col = 0; col < m_ColumnCount; col++)
                m_NextDepthByCol[col] = m_BinStartRow;

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
            if (col < 0 || col >= m_ColumnCount)
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
            for (int col = 0; col < m_ColumnCount; col++)
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
    #region Boosters

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

    private IEnumerator HintTimeoutRoutine()
    {
        yield return new WaitForSeconds(c_HintDuration);
        ClearActiveHint();
        m_HintCoroutine = null;
    }

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

    private bool TryFindHintBlocks(List<BlockManager> result)
    {
        result.Clear();

        List<BlockManager> allBlocks = CollectAllBlocks();
        if (allBlocks.Count < 2)
            return false;

        for (int i = 0; i < allBlocks.Count; i++)
        {
            BlockManager a = allBlocks[i];
            for (int j = i + 1; j < allBlocks.Count; j++)
            {
                BlockManager b = allBlocks[j];
                if (!CanMergeBlocks(a, b))
                    continue;

                MergeResult merge = ResolveMerge(a, b);
                if (!merge.ShouldClear)
                    continue;

                AddUniqueHintBlock(result, a);
                AddUniqueHintBlock(result, b);
                TryAddThirdSameType(result, a.TypeId, allBlocks);
                return true;
            }
        }

        Dictionary<int, List<BlockManager>> byType = new Dictionary<int, List<BlockManager>>();
        foreach (BlockManager block in allBlocks)
        {
            if (block.TypeId == 0)
                continue;

            if (!byType.TryGetValue(block.TypeId, out List<BlockManager> list))
            {
                list = new List<BlockManager>();
                byType[block.TypeId] = list;
            }

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

        if (m_FrostAnimation == null)
        {
            Debug.LogWarning("[PlayManager] Chưa gán FrostAnimation.");
            StartFreezeDuration();
            return;
        }

        Vector3 start = m_FrostAnimation.startPos;
        Vector3 end = m_FrostAnimation.endPos;

        if (m_FreezeButtonTransform != null &&
            TryGetWorldPosition(m_FreezeButtonTransform, out Vector3 startWorld))
            start = startWorld;

        if (m_TimeLimitText != null &&
            TryGetWorldPosition(m_TimeLimitText.rectTransform, out Vector3 endWorld))
            end = endWorld;

        m_FrostAnimation.Configure(start, end, StartFreezeDuration);
    }

    private static bool TryGetWorldPosition(Transform target, out Vector3 worldPos)
    {
        worldPos = default;
        if (target == null)
            return false;

        if (target is RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : Camera.main;

            if (camera == null)
                return false;

            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(
                canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera,
                rect.position);
            worldPos = camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(camera.transform.position.z)));
            worldPos.z = 0f;
            return true;
        }

        worldPos = target.position;
        worldPos.z = 0f;
        return true;
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
    /// <summary>
    /// Xáo trộn các block trên bảng
    /// </summary>  
    public void ShuffleBoard()
    {
        

        
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

#endregion