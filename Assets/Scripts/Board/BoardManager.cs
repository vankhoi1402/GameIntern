using System;
using UnityEngine;

/// <summary>
/// Bàn cờ thống nhất — lưới logic, tọa độ grid/world, đặt/di chuyển/xóa block.
/// </summary>
public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField] private int rows = 7;
    [SerializeField] private int columns = 5;

    [Header("Slot Size")]
    [SerializeField] private float cellWidth = 0.93f;
    [SerializeField] private float cellHeight = 1.15f;

    [Header("Grid Origin")]
    [Tooltip("Góc dưới-trái ô [0,0]. Để trống = dùng transform của object này.")]
    [SerializeField] private Transform originTransform;

    [Header("Screen Layout")]
    [SerializeField] private bool centerOnScreen = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 screenCenterOffset;

    private Slot[,] slots;
    private float m_GridStartX;
    private float m_GridStartY;

    public bool IsGridReady { get; private set; }

    public event Action<Block, int, int> OnBlockPlaced;
    public event Action<Block, int, int, int, int> OnBlockMoved;
    public event Action<Block, int, int> OnBlockRemoved;

    public int Rows => rows;
    public int Columns => columns;
    public float CellWidth => cellWidth;
    public float CellHeight => cellHeight;

    /// <summary>Góc dưới-trái ô [0,0] trong world space.</summary>
    public Vector2 Origin
    {
        get
        {
            Transform t = originTransform != null ? originTransform : transform;
            return t.position;
        }
    }

    private void Awake()
    {
        InitBoard();

        if (centerOnScreen)
            CenterBoardOnScreen();

        RefreshGridMetrics();
        IsGridReady = true;
    }

    private Transform OriginTransform => originTransform != null ? originTransform : transform;

    private void CenterBoardOnScreen()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

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

    /// <summary>(row, col) → tâm ô trong world.</summary>
    public Vector3 GridToWorld(int row, int col)
    {
        return new Vector3(
            m_GridStartX + col * cellWidth,
            m_GridStartY + row * cellHeight,
            0f);
    }

    /// <summary>World → (row, col), clamp trong biên bàn.</summary>
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector2 o = Origin;
        float localX = worldPosition.x - o.x;
        float localY = worldPosition.y - o.y;

        int col = Mathf.FloorToInt(localX / cellWidth);
        int row = Mathf.FloorToInt(localY / cellHeight);

        row = Mathf.Clamp(row, 0, rows - 1);
        col = Mathf.Clamp(col, 0, columns - 1);

        return new Vector2Int(row, col);
    }

    public bool IsInside(int row, int col) => row >= 0 && row < rows && col >= 0 && col < columns;

    /// <summary>Alias — giữ tương thích call site cũ.</summary>
    public bool IsValidPosition(int row, int col) => IsInside(row, col);

    /// <summary>Alias của GridToWorld.</summary>
    public Vector3 GetSlotWorldPosition(int row, int col) => GridToWorld(row, col);

    private void InitBoard()
    {
        slots = new Slot[rows, columns];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
                slots[r, c] = new Slot(r, c);
        }
    }

    public Slot GetSlot(int row, int col) => !IsInside(row, col) ? null : slots[row, col];

    public Vector2Int GetBlockPosition(Block block)
    {
        if (block == null) return new Vector2Int(-1, -1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                if (slots[r, c].CurrentBlock == block)
                    return new Vector2Int(r, c);
            }
        }

        return new Vector2Int(-1, -1);
    }

    public void PlaceBlock(Block block, int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || block == null) return;

        slot.SetBlock(block);
        OnBlockPlaced?.Invoke(block, row, col);
    }

    public void ClearSlot(int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || slot.IsEmpty) return;
        slot.Clear();
    }

    public void RemoveBlock(int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || slot.IsEmpty) return;

        Block blockToRemove = slot.CurrentBlock;
        slot.Clear();
        OnBlockRemoved?.Invoke(blockToRemove, row, col);
    }

    public bool IsBoardEmpty()
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Slot slot = GetSlot(r, c);
                if (slot != null && slot.HasBlock)
                    return false;
            }
        }

        return true;
    }

    public void ClearAllBlocks()
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Slot slot = GetSlot(r, c);
                if (slot == null || slot.IsEmpty) continue;
                RemoveBlock(r, c);
            }
        }
    }

    internal void SetBlockSilent(Block block, int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || block == null) return;
        slot.SetBlock(block);
    }

    internal void ClearSlotSilent(int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || slot.IsEmpty) return;
        slot.Clear();
    }

    public bool MoveBlock(int fromRow, int fromCol, int toRow, int toCol)
    {
        Slot fromSlot = GetSlot(fromRow, fromCol);
        Slot toSlot = GetSlot(toRow, toCol);

        if (fromSlot == null || toSlot == null || fromSlot.IsEmpty || !toSlot.IsEmpty)
            return false;

        Block block = fromSlot.CurrentBlock;
        fromSlot.Clear();
        toSlot.SetBlock(block);

        OnBlockMoved?.Invoke(block, fromRow, fromCol, toRow, toCol);
        return true;
    }

    public void SwapBlock(int rowA, int colA, int rowB, int colB)
    {
        Slot slotA = GetSlot(rowA, colA);
        Slot slotB = GetSlot(rowB, colB);

        if (slotA == null || slotB == null || slotA.IsEmpty || slotB.IsEmpty) return;

        Block blockA = slotA.CurrentBlock;
        Block blockB = slotB.CurrentBlock;

        slotA.SetBlock(blockB);
        slotB.SetBlock(blockA);

        OnBlockMoved?.Invoke(blockA, rowA, colA, rowB, colB);
        OnBlockMoved?.Invoke(blockB, rowB, colB, rowA, colA);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (rows <= 0 || columns <= 0) return;

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
                float cx = startX + c * cellWidth;
                float cy = startY + r * cellHeight;
                Gizmos.DrawSphere(new Vector3(cx, cy, 0f), 0.05f);
            }
        }
    }
#endif
}
