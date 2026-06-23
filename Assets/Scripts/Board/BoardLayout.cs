using UnityEngine;

public class BoardLayout 
{
    private readonly BoardArea _boardArea;
    private readonly int _rows;
    private readonly int _columns;

    // Tách riêng rộng và cao cho hình chữ nhật
    private readonly float _cellWidth;
    private readonly float _cellHeight;

    private readonly float _startX;
    private readonly float _startY;

    public float CellWidth => _cellWidth;
    public float CellHeight => _cellHeight;

    public BoardLayout(BoardArea boardArea, int rows, int columns)
    {
        _boardArea = boardArea;
        _rows = rows;
        _columns = columns;

        // BỎ Mathf.Min: Tự động chia đều cho vừa khít chiều rộng và chiều cao
        _cellWidth = boardArea.Size.x / columns;
        _cellHeight = boardArea.Size.y / rows;

        Bounds bounds = _boardArea.Bounds;

        // KHÔNG CẦN OFFSET: Vì ô đã tự giãn vừa khít 100% Collider
        // Tọa độ bắt đầu chỉ cần tính từ góc min + nửa kích thước ô đầu tiên
        _startX = bounds.min.x + _cellWidth * 0.5f;
        _startY = bounds.min.y + _cellHeight * 0.5f;
    }

    public Vector3 GetWorldPosition(int row, int col)
    {
        // Tính vị trí dựa trên Width (cột) và Height (hàng) riêng biệt
        return new Vector3(
            _startX + col * _cellWidth,
            _startY + row * _cellHeight,
            0);
    }

    /// <summary>
    /// ĐÃ THÊM: Dịch từ tọa độ chuột (World Vector3) ngược về tọa độ ô cờ (Row, Col) cho DragController dùng
    /// </summary>
    public Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        Bounds bounds = _boardArea.Bounds;

        // Tính khoảng cách local so với góc dưới bên trái của bàn chơi
        float localX = worldPosition.x - bounds.min.x;
        float localY = worldPosition.y - bounds.min.y;

        // Chia cho kích thước ô để tìm ra cột và hàng
        int col = Mathf.FloorToInt(localX / _cellWidth);
        int row = Mathf.FloorToInt(localY / _cellHeight);

        // Giới hạn an toàn (Clamp) không cho vượt quá biên ma trận data
        row = Mathf.Clamp(row, 0, _rows - 1);
        col = Mathf.Clamp(col, 0, _columns - 1);

        // Trả về kết quả: x đại diện cho Row, y đại diện cho Col giống DragController mong đợi
        return new Vector2Int(row, col);
    }
}