using UnityEngine;

/// <summary>
/// Chuyển đổi giữa tọa độ lưới (row, col) và tọa độ world.
/// Dùng chung cho đặt block, raycast thả chuột, animation.
/// </summary>
public class BoardLayout
{
    private readonly BoardArea _boardArea;
    private readonly int _rows;
    private readonly int _columns;

    private readonly float _cellWidth;
    private readonly float _cellHeight;
    private readonly float _startX;
    private readonly float _startY;

    public float CellWidth => _cellWidth;
    public float CellHeight => _cellHeight;

    /// <summary>Khởi tạo layout từ BoardArea và kích thước lưới.</summary>
    public BoardLayout(BoardArea boardArea, int rows, int columns)
    {
        _boardArea = boardArea;
        _rows = rows;
        _columns = columns;

        _cellWidth = boardArea.CellWidth;
        _cellHeight = boardArea.CellHeight;

        Vector2 origin = boardArea.Origin;
        _startX = origin.x + _cellWidth * 0.5f;
        _startY = origin.y + _cellHeight * 0.5f;
    }

    /// <summary>Chuyển (row, col) → vị trí world tại tâm ô.</summary>
    public Vector3 GetWorldPosition(int row, int col)
    {
        return new Vector3(
            _startX + col * _cellWidth,
            _startY + row * _cellHeight,
            0);
    }

    /// <summary>Chuyển vị trí world → (row, col) trên lưới (có clamp biên).</summary>
    public Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        Vector2 origin = _boardArea.Origin;

        float localX = worldPosition.x - origin.x;
        float localY = worldPosition.y - origin.y;

        int col = Mathf.FloorToInt(localX / _cellWidth);
        int row = Mathf.FloorToInt(localY / _cellHeight);

        row = Mathf.Clamp(row, 0, _rows - 1);
        col = Mathf.Clamp(col, 0, _columns - 1);

        return new Vector2Int(row, col);
    }
}
