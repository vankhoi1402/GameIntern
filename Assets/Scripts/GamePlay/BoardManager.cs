using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Trung tâm trạng thái và logic bàn cờ — grid, mutate, query, gravity.
/// </summary>
public class BoardManager : MonoBehaviour
{
    #region Constants

    private const float c_DefaultGravityAnimTimeout = 5f;

    #endregion

    #region References

    [Header("Board Settings")]
    [SerializeField] private int rows = 7;
    [SerializeField] private int columns = 5;

    [Header("Slot Size")]
    [SerializeField] private float cellWidth = 0.93f;
    [SerializeField] private float cellHeight = 1.15f;

    [Header("Grid Origin")]
    [SerializeField] private Transform originTransform;

    [Header("Screen Layout")]
    [SerializeField] private bool centerOnScreen = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 screenCenterOffset;

    [Header("Gravity Animation")]
    [SerializeField] private float m_GravityDurationPerSqrtCell = 0.17f;
    [SerializeField] private float m_GravityRowStagger = 0.035f;
    [SerializeField] private Ease m_GravityEase = Ease.OutSine;

    [Header("Gameplay")]
    [SerializeField] private PlayManager m_PlayManager;

    #endregion

    #region Grid State

    private Slot[,] m_Slots;
    private float m_GridStartX;
    private float m_GridStartY;

    #endregion

    #region Events

    public event Action<BlockManager, int, int> OnBlockPlaced;
    public event Action<BlockManager, int, int, int, int> OnBlockMoved;
    public event Action<BlockManager, int, int> OnBlockRemoved;

    #endregion

    #region Properties

    public bool IsGridReady { get; private set; }
    public int Rows => rows;
    public int Columns => columns;
    public float CellWidth => cellWidth;
    public float CellHeight => cellHeight;

    public Vector2 Origin
    {
        get
        {
            Transform t = originTransform != null ? originTransform : transform;
            return t.position;
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (m_PlayManager == null)
            m_PlayManager = FindObjectOfType<PlayManager>();

        InitGrid();
        if (centerOnScreen)
            CenterBoardOnScreen();
        RefreshGridMetrics();
        IsGridReady = true;
    }

    #endregion

    #region Grid & Coordinates

    private Transform OriginTransform => originTransform != null ? originTransform : transform;

    private void InitGrid()
    {
        m_Slots = new Slot[rows, columns];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
                m_Slots[r, c] = new Slot(r, c);
        }
    }

    private void CenterBoardOnScreen()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return;

        Transform origin = OriginTransform;
        float planeDistance = Mathf.Abs(cam.transform.position.z - origin.position.z);
        Vector3 viewportCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, planeDistance));
        Vector2 worldCenter = new Vector2(viewportCenter.x, viewportCenter.y) + screenCenterOffset;
        Vector2 size = new Vector2(cellWidth * columns, cellHeight * rows);
        Vector2 originPos = worldCenter - size * 0.5f;
        origin.position = new Vector3(originPos.x, originPos.y, origin.position.z);
    }

    private void RefreshGridMetrics()
    {
        Vector2 o = Origin;
        m_GridStartX = o.x + cellWidth * 0.5f;
        m_GridStartY = o.y + cellHeight * 0.5f;
    }

    public Vector3 GridToWorld(int row, int col)
    {
        return new Vector3(
            m_GridStartX + col * cellWidth,
            m_GridStartY + row * cellHeight,
            0f);
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector2 o = Origin;
        int col = Mathf.Clamp(Mathf.FloorToInt((worldPosition.x - o.x) / cellWidth), 0, columns - 1);
        int row = Mathf.Clamp(Mathf.FloorToInt((worldPosition.y - o.y) / cellHeight), 0, rows - 1);
        return new Vector2Int(row, col);
    }

    public bool IsInside(int row, int col) => row >= 0 && row < rows && col >= 0 && col < columns;
    public bool IsValidPosition(int row, int col) => IsInside(row, col);
    public Vector3 GetSlotWorldPosition(int row, int col) => GridToWorld(row, col);

    #endregion

    #region Slot Access

    public Slot GetSlot(int row, int col) => !IsInside(row, col) ? null : m_Slots[row, col];

    public BlockManager GetBlock(int row, int col)
    {
        Slot slot = GetSlot(row, col);
        return slot != null && slot.HasBlock ? slot.CurrentBlock : null;
    }

    public Vector2Int GetBlockPosition(BlockManager block)
    {
        if (block == null)
            return new Vector2Int(-1, -1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (m_Slots[r, c].CurrentBlock == block)
                    return new Vector2Int(r, c);
            }
        }

        return new Vector2Int(-1, -1);
    }

    #endregion

    #region Block Mutations

    public void PlaceBlock(BlockManager block, int row, int col)
{
    Slot slot = GetSlot(row, col);
    if (slot == null || block == null)
        return;

    slot.SetBlock(block);

    Vector3 pos = GridToWorld(row, col);
    block.SnapToGridCell(pos, row);

    OnBlockPlaced?.Invoke(block, row, col);
}

    public void ClearSlot(int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || slot.IsEmpty)
            return;

        slot.Clear();
    }

    public void RemoveBlock(int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || slot.IsEmpty)
            return;

        BlockManager block = slot.CurrentBlock;
        slot.Clear();
        OnBlockRemoved?.Invoke(block, row, col);
    }

    public void ClearAllBlocks()
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Slot slot = GetSlot(r, c);
                if (slot != null && slot.HasBlock)
                    RemoveBlock(r, c);
            }
        }
    }

    public bool MoveBlock(int fromRow, int fromCol, int toRow, int toCol)
    {
        Slot fromSlot = GetSlot(fromRow, fromCol);
        Slot toSlot = GetSlot(toRow, toCol);

        if (fromSlot == null || toSlot == null || fromSlot.IsEmpty || !toSlot.IsEmpty)
            return false;

        BlockManager block = fromSlot.CurrentBlock;
        fromSlot.Clear();
        toSlot.SetBlock(block);
        OnBlockMoved?.Invoke(block, fromRow, fromCol, toRow, toCol);
        return true;
    }

    public void SwapBlock(int rowA, int colA, int rowB, int colB)
    {
        Slot slotA = GetSlot(rowA, colA);
        Slot slotB = GetSlot(rowB, colB);
        if (slotA == null || slotB == null || slotA.IsEmpty || slotB.IsEmpty)
            return;

        BlockManager blockA = slotA.CurrentBlock;
        BlockManager blockB = slotB.CurrentBlock;
        slotA.SetBlock(blockB);
        slotB.SetBlock(blockA);
        OnBlockMoved?.Invoke(blockA, rowA, colA, rowB, colB);
        OnBlockMoved?.Invoke(blockB, rowB, colB, rowA, colA);
    }

    /// <summary>Dịch toàn bộ block trong cột lên 1 ô — phục vụ refill.</summary>
    public bool TryShiftColumnUp(int col, out List<ColumnPushCommand> commands)
    {
        commands = new List<ColumnPushCommand>();
        var blocksInColumn = new List<(BlockManager block, int row)>();

        for (int row = 0; row < rows; row++)
        {
            Slot slot = GetSlot(row, col);
            if (slot != null && slot.HasBlock)
                blocksInColumn.Add((slot.CurrentBlock, row));
        }

        if (blocksInColumn.Count >= rows)
            return false;

        int topRow = rows - 1;
        foreach (var (_, row) in blocksInColumn)
        {
            if (row + 1 > topRow)
                return false;
        }

        for (int i = blocksInColumn.Count - 1; i >= 0; i--)
        {
            BlockManager block = blocksInColumn[i].block;
            int fromRow = blocksInColumn[i].row;
            int toRow = fromRow + 1;

            SetBlockSilent(block, toRow, col, clearFromRow: fromRow);
            commands.Add(new ColumnPushCommand
            {
                Block = block,
                FromRow = fromRow,
                ToRow = toRow,
                Col = col
            });
        }

        return true;
    }

    private void SetBlockSilent(BlockManager block, int row, int col, int clearFromRow)
    {
        Slot fromSlot = GetSlot(clearFromRow, col);
        if (fromSlot != null && !fromSlot.IsEmpty)
            fromSlot.Clear();

        Slot toSlot = GetSlot(row, col);
        if (toSlot == null || block == null)
            return;

        toSlot.SetBlock(block);
    }

    #endregion

    #region Board Query

    public int CountBlocks()
    {
        if (!IsGridReady)
            return 0;

        int count = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (m_Slots[r, c].HasBlock)
                    count++;
            }
        }

        return count;
    }

    public int CountEmptySlots() => rows * columns - CountBlocks();

    public bool HasBlocksOnBoard() => CountBlocks() > 0;

    public bool IsBoardEmpty() => !HasBlocksOnBoard();

    public bool ColumnNeedsRefill(int col)
    {
        if (!IsGridReady || col < 0 || col >= columns)
            return false;

        return CountBlocksInColumn(col) < rows;
    }

    public IReadOnlyList<Vector2Int> GetEmptySlots()
    {
        var empty = new List<Vector2Int>();
        if (!IsGridReady)
            return empty;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (m_Slots[r, c].IsEmpty)
                    empty.Add(new Vector2Int(r, c));
            }
        }

        return empty;
    }

    private int CountBlocksInColumn(int col)
    {
        int count = 0;
        for (int row = 0; row < rows; row++)
        {
            if (m_Slots[row, col].HasBlock)
                count++;
        }

        return count;
    }

    public bool HasAvailableMove()
        => m_PlayManager != null && m_PlayManager.HasAvailableMove();

    #endregion

    #region Gravity

    public int PredictFinalRowAfterGravity(int row, int col)
    {
        int emptyRowsCount = 0;
        for (int r = 0; r < row; r++)
        {
            Slot slot = GetSlot(r, col);
            if (slot != null && slot.IsEmpty)
                emptyRowsCount++;
        }

        return row - emptyRowsCount;
    }

    public IEnumerator ApplyGravity(float animTimeoutSeconds = c_DefaultGravityAnimTimeout)
    {
        while (true)
        {
            List<GravityMoveCommand> commands = CollectGravityCommands();
            if (commands.Count == 0)
                break;

            foreach (GravityMoveCommand cmd in commands)
                MoveBlock(cmd.FromRow, cmd.FromCol, cmd.ToRow, cmd.ToCol);

            bool animDone = false;
            HandleGravityAnimation(commands, () => animDone = true);
            yield return WaitUntilOrTimeout(() => animDone, animTimeoutSeconds, "gravity animation");
        }

        ClearMergeVisualContext();
    }

    public void ApplyGravitySilentForColumn(int col)
    {
        if (col < 0 || col >= columns)
            return;

        foreach (GravityMoveCommand cmd in CollectGravityCommandsForColumn(col))
            MoveBlock(cmd.FromRow, cmd.FromCol, cmd.ToRow, cmd.ToCol);
    }

    private List<GravityMoveCommand> CollectGravityCommands()
    {
        var commands = new List<GravityMoveCommand>();
        for (int col = 0; col < columns; col++)
            commands.AddRange(CollectGravityCommandsForColumn(col));
        return commands;
    }

    private List<GravityMoveCommand> CollectGravityCommandsForColumn(int col)
    {
        var commands = new List<GravityMoveCommand>();
        int emptyRowsCount = 0;

        for (int row = 0; row < rows; row++)
        {
            Slot slot = GetSlot(row, col);
            if (slot == null)
                continue;

            if (slot.IsEmpty)
            {
                emptyRowsCount++;
            }
            else if (emptyRowsCount > 0)
            {
                commands.Add(new GravityMoveCommand
                {
                    Block = slot.CurrentBlock,
                    FromRow = row,
                    FromCol = col,
                    ToRow = row - emptyRowsCount,
                    ToCol = col,
                    DropDistance = emptyRowsCount
                });
            }
        }

        return commands;
    }

    private void HandleGravityAnimation(List<GravityMoveCommand> commands, Action onCompleteCallback)
    {
        if (!IsGridReady || commands == null || commands.Count == 0)
        {
            onCompleteCallback?.Invoke();
            return;
        }

        var batch = new GravityTweenBatch(commands.Count, () =>
        {
            ClearMergeVisualContext();
            onCompleteCallback?.Invoke();
        });

        foreach (GravityMoveCommand cmd in commands)
        {
            if (cmd.Block == null)
            {
                batch.NotifyOneDone();
                continue;
            }

            Vector3 fromWorldPos = GridToWorld(cmd.FromRow, cmd.FromCol);
            Vector3 targetWorldPos = GridToWorld(cmd.ToRow, cmd.ToCol);
            float duration = Mathf.Sqrt(cmd.DropDistance) * m_GravityDurationPerSqrtCell;
            float delay = cmd.FromRow * m_GravityRowStagger;
            int toRow = cmd.ToRow;
            BlockManager block = cmd.Block;
            bool isMergeTarget = m_PlayManager != null && m_PlayManager.IsMergeVisualTarget(block);

            if (!isMergeTarget)
            {
                block.PlayGravity(
                    fromWorldPos,
                    targetWorldPos,
                    duration,
                    delay,
                    toRow,
                    m_GravityEase,
                    preserveCurrentPosition: false,
                    batch.NotifyOneDone);
            }
            else if (isMergeTarget)
            {
                fromWorldPos = block.transform.position;
                block.transform
                    .DOMove(targetWorldPos, duration)
                    .SetEase(m_GravityEase)
                    .SetDelay(delay)
                    .SetLink(block.gameObject)
                    .OnComplete(batch.NotifyOneDone)
                    .OnKill(batch.NotifyOneDone);
            }
            else
            {
                block.transform.position = fromWorldPos;
                block.transform
                    .DOMove(targetWorldPos, duration)
                    .SetEase(m_GravityEase)
                    .SetDelay(delay)
                    .SetLink(block.gameObject)
                    .OnComplete(batch.NotifyOneDone)
                    .OnKill(batch.NotifyOneDone);
            }
        }
    }

    private void ClearMergeVisualContext()
        => m_PlayManager?.ClearMergeVisual();

    #endregion

    #region Utilities

    private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds, string label)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!condition())
            Debug.LogWarning($"[BoardManager] Timeout chờ {label} ({timeoutSeconds}s).");
    }

    private sealed class GravityTweenBatch
    {
        private int m_Remaining;
        private bool m_Invoked;
        private readonly Action m_OnComplete;

        public GravityTweenBatch(int count, Action onComplete)
        {
            m_Remaining = count;
            m_OnComplete = onComplete;
            if (m_Remaining <= 0)
                InvokeOnce();
        }

        public void NotifyOneDone()
        {
            m_Remaining--;
            if (m_Remaining <= 0)
                InvokeOnce();
        }

        private void InvokeOnce()
        {
            if (m_Invoked)
                return;
            m_Invoked = true;
            m_OnComplete?.Invoke();
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (rows <= 0 || columns <= 0)
            return;

        Vector2 o = originTransform != null ? (Vector2)originTransform.position : (Vector2)transform.position;
        float startX = o.x + cellWidth * 0.5f;
        float startY = o.y + cellHeight * 0.5f;

        Gizmos.color = Color.green;
        for (int c = 0; c <= columns; c++)
        {
            float x = o.x + c * cellWidth;
            Gizmos.DrawLine(new Vector3(x, o.y, 0f), new Vector3(x, o.y + rows * cellHeight, 0f));
        }

        for (int r = 0; r <= rows; r++)
        {
            float y = o.y + r * cellHeight;
            Gizmos.DrawLine(new Vector3(o.x, y, 0f), new Vector3(o.x + columns * cellWidth, y, 0f));
        }

        Gizmos.color = Color.red;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Gizmos.DrawSphere(new Vector3(startX + c * cellWidth, startY + r * cellHeight, 0f), 0.05f);
            }
        }
    }
#endif
}
