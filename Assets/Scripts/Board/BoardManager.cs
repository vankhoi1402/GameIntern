using System;
using UnityEngine;

/// <summary>
/// Quản lý lưới logic (Slot[,]) — đặt/di chuyển/xóa block và phát event cho tầng View.
/// Không xử lý animation hay input.
/// </summary>
public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField] private int rows = 7;
    [SerializeField] private int columns = 5;

    [Header("Area References")]
    [SerializeField] private BoardArea boardArea;

    [Header("Screen Layout")]
    [SerializeField] private bool centerOnScreen = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 screenCenterOffset;

    private Slot[,] slots;

    /// <summary>Bộ tính tọa độ ô — View và animation dùng chung.</summary>
    public BoardLayout Layout { get; set; }

    public event Action<Block, int, int> OnBlockPlaced;
    public event Action<Block, int, int, int, int> OnBlockMoved;
    public event Action<Block, int, int> OnBlockRemoved;

    public int Rows => rows;
    public int Columns => columns;
    public float CellWidth => Layout?.CellWidth ?? 0f;
    public float CellHeight => Layout?.CellHeight ?? 0f;

    /// <summary>Khởi tạo lưới, căn giữa bàn và tạo BoardLayout.</summary>
    private void Awake()
    {
        InitBoard();

        if (boardArea == null)
        {
            Debug.LogError("[BoardManager] Chưa gán BoardArea trong Inspector.");
            return;
        }

        if (centerOnScreen)
            CenterBoardOnScreen();

        InitLayout();
    }

    /// <summary>Đặt BoardArea sao cho tâm bàn trùng giữa màn hình.</summary>
    private void CenterBoardOnScreen()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        float planeDistance = Mathf.Abs(cam.transform.position.z - boardArea.transform.position.z);
        Vector3 viewportCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, planeDistance));
        Vector2 worldCenter = new Vector2(viewportCenter.x, viewportCenter.y) + screenCenterOffset;
        boardArea.CenterAt(worldCenter, columns, rows);
    }

    /// <summary>Tạo BoardLayout từ BoardArea hiện tại.</summary>
    private void InitLayout()
    {
        Layout = new BoardLayout(boardArea, rows, columns);
    }

    /// <summary>Lấy vị trí world tại tâm ô (row, col).</summary>
    public Vector3 GetSlotWorldPosition(int row, int col)
        => Layout?.GetWorldPosition(row, col) ?? Vector3.zero;

    /// <summary>Tạo mảng Slot[,] rỗng theo rows x columns.</summary>
    private void InitBoard()
    {
        slots = new Slot[rows, columns];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
                slots[r, c] = new Slot(r, c);
        }
    }

    /// <summary>Kiểm tra (row, col) có nằm trong biên bàn cờ.</summary>
    public bool IsValidPosition(int row, int col) => row >= 0 && row < rows && col >= 0 && col < columns;

    /// <summary>Lấy Slot tại (row, col); null nếu ngoài biên.</summary>
    public Slot GetSlot(int row, int col) => !IsValidPosition(row, col) ? null : slots[row, col];

    /// <summary>Tìm vị trí lưới của block (quét toàn bàn).</summary>
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

    /// <summary>Đặt block vào ô và phát OnBlockPlaced cho View.</summary>
    public void PlaceBlock(Block block, int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || block == null) return;

        slot.SetBlock(block);
        OnBlockPlaced?.Invoke(block, row, col);
    }

    /// <summary>Xóa block khỏi ô trên lưới (không Destroy GameObject).</summary>
    public void ClearSlot(int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || slot.IsEmpty) return;
        slot.Clear();
    }

    /// <summary>Xóa block khỏi ô và phát OnBlockRemoved (View sẽ Destroy).</summary>
    public void RemoveBlock(int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || slot.IsEmpty) return;

        Block blockToRemove = slot.CurrentBlock;
        slot.Clear();
        OnBlockRemoved?.Invoke(blockToRemove, row, col);
    }

    /// <summary>True khi không còn block nào trên lưới.</summary>
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

    /// <summary>Xóa toàn bộ block trên bàn (logic + Destroy view).</summary>
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

    /// <summary>Gán block vào ô không phát OnBlockPlaced (dùng refill shift trước animation).</summary>
    internal void SetBlockSilent(Block block, int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || block == null) return;
        slot.SetBlock(block);
    }

    /// <summary>Xóa ô không phát OnBlockRemoved.</summary>
    internal void ClearSlotSilent(int row, int col)
    {
        Slot slot = GetSlot(row, col);
        if (slot == null || slot.IsEmpty) return;
        slot.Clear();
    }

    /// <summary>Di chuyển block giữa hai ô trống/đích hợp lệ.</summary>
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

    /// <summary>Hoán đổi hai block giữa hai ô (chưa dùng trong gameplay hiện tại).</summary>
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
}
