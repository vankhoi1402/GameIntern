using UnityEngine;

/// <summary>
/// Định nghĩa vùng bàn cờ và kích thước từng ô (cellWidth x cellHeight).
/// transform.position = góc dưới-trái ô [0,0].
/// </summary>
public class BoardArea : MonoBehaviour
{
    [Header("Slot Size")]
    [SerializeField] private float cellWidth = 1f;
    [SerializeField] private float cellHeight = 1.21f;

    public float CellWidth => cellWidth;
    public float CellHeight => cellHeight;

    /// <summary>Góc dưới-trái của ô [0,0] trong world space.</summary>
    public Vector2 Origin => transform.position;

    /// <summary>Tính tổng kích thước bàn cờ theo số cột và hàng.</summary>
    public Vector2 GetBoardSize(int columns, int rows)
        => new Vector2(cellWidth * columns, cellHeight * rows);

    /// <summary>Trả Bounds bao quanh toàn bộ lưới (dùng cho Gizmos/debug).</summary>
    public Bounds GetBounds(int columns, int rows)
    {
        Vector2 size = GetBoardSize(columns, rows);
        Vector2 center = Origin + size * 0.5f;
        return new Bounds(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0f));
    }

    /// <summary>Đặt vị trí sao cho tâm bàn cờ trùng worldCenter.</summary>
    public void CenterAt(Vector2 worldCenter, int columns, int rows)
    {
        Vector2 size = GetBoardSize(columns, rows);
        Vector2 origin = worldCenter - size * 0.5f;
        transform.position = new Vector3(origin.x, origin.y, transform.position.z);
    }
}
