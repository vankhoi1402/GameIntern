using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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

    #endregion

    #region References

    [Header("Board")]
    [SerializeField] private BoardManager m_BoardManager;
    [SerializeField] private StackBackgroundConfig m_StackBackgroundConfig;

    [Header("Blocks")]
    [SerializeField] private BlockManager m_BlockPrefab;
    [SerializeField] private BlockDatabase m_BlockDatabase;
    [SerializeField] private Transform m_BlockParent;

    [Header("Level")]
    [SerializeField] private LevelManager2 m_LevelManager;

    [Header("Input")]
    [SerializeField] private Camera m_MainCamera;
    [SerializeField] private LayerMask m_BlockLayer;

    [Header("Refill Animation")]
    [SerializeField] private float m_RefillRiseDuration = 0.16f;
    [SerializeField] private float m_RefillPushDuration = 0.15f;
    [SerializeField] private Ease m_RefillPushEase = Ease.OutSine;

    #endregion

    #region Runtime State

    private readonly LevelLoader m_LevelLoader = new LevelLoader();

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

    #endregion

    #region Events

    public event Action<bool> OnSettlementCompleted;

    public bool IsSettlementRunning => m_IsSettlementRunning;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (m_LevelManager != null)
            m_LevelManager.OnLevelLoaded += HandleLevelLoaded;
    }

    private void OnDisable()
    {
        if (m_LevelManager != null)
            m_LevelManager.OnLevelLoaded -= HandleLevelLoaded;
    }

    private void Update()
    {
        HandleInput();
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
            RequestFullSettlement();
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

            if (isTier2TripleClear)
                RequestFullSettlement();
            else
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
                    RequestFullSettlement();
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
                RequestFullSettlement();
            });
    }

    private void RequestFullSettlement(IReadOnlyList<Tier2NeighborRestore> neighborRestores = null)
    {
        ClearMergeVisual();
        StartSettlement(includeRefill: true, neighborRestores);
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
        StartSettlement(includeRefill: false, neighborRestores: null);
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
        StartSettlement(includeRefill: true, neighborRestores: null);
    }

    private void StartSettlement(bool includeRefill, IReadOnlyList<Tier2NeighborRestore> neighborRestores)
    {
        if (m_SettlementCoroutine != null)
            StopCoroutine(m_SettlementCoroutine);

        m_SettlementCoroutine = StartCoroutine(SettlementRoutine(includeRefill, neighborRestores));
    }

    private IEnumerator SettlementRoutine(
        bool includeRefill,
        IReadOnlyList<Tier2NeighborRestore> neighborRestores)
    {
        if (m_IsSettlementRunning)
            yield break;

        m_IsSettlementRunning = true;

        try
        {
            if (m_BoardManager != null)
                yield return m_BoardManager.ApplyGravity(c_GravityAnimTimeout);

            Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues =
                BuildNeighborQueues(neighborRestores);

            bool hasNeighborRefill = HasPendingNeighborRestore(neighborQueues);

            if (includeRefill || hasNeighborRefill)
                yield return RunRefillPhase(includeRefill, neighborQueues);
        }
        finally
        {
            m_IsSettlementRunning = false;
            m_IsTurnProcessing = false;
            m_SettlementCoroutine = null;
            OnSettlementCompleted?.Invoke(includeRefill);
        }
    }

    private IEnumerator RunRefillPhase(
        bool includeBin,
        Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues)
    {
        while (true)
        {
            List<ColumnRefillPacket> wave = CollectFillWave(includeBin, neighborQueues);
            if (wave.Count == 0)
                break;

            var batch = new AnimationCompletionBatch();
            PlayRefillPushAnimations(wave, batch);
            PlayRefillRiseAnimations(wave, batch);

            yield return batch.WaitRoutine(c_SettlementTimeout, "refill wave");
        }
    }

    #endregion

    #region Spawn Level Layout

    /// <summary>Xóa bàn và spawn layout ban đầu từ level data.</summary>
    public void SpawnLevelBoard(LevelData level)
    {
        if (level == null || m_BoardManager == null)
            return;

        m_BoardManager.ClearAllBlocks();

        if (level.Rows == null)
            return;

        foreach (LevelGridRow gridRow in level.Rows)
        {
            if (gridRow.Row >= level.VisibleRows || gridRow.ColBlockTypes == null)
                continue;

            for (int col = 0; col < gridRow.ColBlockTypes.Length; col++)
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

        if (m_StackBackgroundConfig != null)
            block.SetBackgroundConfig(m_StackBackgroundConfig);

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
            if (!restore.HasValue && includeBin && m_LevelManager != null)
                binBlock = m_LevelManager.TryDequeueBin(col);

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

        target.SetBackgroundConfig(m_StackBackgroundConfig);
        target.UpdateTier2StageVisual(target.StackCount, target.Tier2MergeStage);

        if (source == null)
        {
            FinishTargetStackVisual(target);
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

            SnapTargetToCurrentSlot(target);
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

    #endregion

    #region Utilities

    private bool IsInputBlocked()
    {
        if (m_IsInputLocked || m_IsDragging && m_IsSettlementRunning)
            return true;

        if (m_IsTurnProcessing || m_IsSettlementRunning)
            return true;

        if (m_LevelManager != null && m_LevelManager.IsOutcomeResolved)
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
        if (a == null || b == null || a.TypeId == 0 || b.TypeId == 0 || a.TypeId != b.TypeId)
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
