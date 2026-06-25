using System.Collections.Generic;

/// <summary>Runtime — con trỏ độ sâu bin từng cột (FIFO từ VisibleRows trở lên).</summary>
public class LevelRefillState
{
    private const int c_ColumnCount = LevelCsvParser.GridColumnCount;

    private readonly Dictionary<int, LevelGridRow> _rowsByDepth = new Dictionary<int, LevelGridRow>();
    private readonly int[] _nextDepthByCol = new int[c_ColumnCount];
    private int _maxDepth = -1;
    private int _visibleRows;

    public void Reset(LevelData level)
    {
        _rowsByDepth.Clear();
        _maxDepth = -1;
        _visibleRows = level != null ? level.VisibleRows : 7;

        for (int col = 0; col < c_ColumnCount; col++)
            _nextDepthByCol[col] = _visibleRows;

        if (level?.Rows == null)
            return;

        foreach (LevelGridRow row in level.Rows)
        {
            _rowsByDepth[row.Row] = row;
            if (row.Row > _maxDepth)
                _maxDepth = row.Row;
        }
    }

    /// <summary>Lấy block tiếp theo trong bin cột; null nếu hết.</summary>
    public LevelBinBlock? TryDequeue(int col)
    {
        if (col < 0 || col >= c_ColumnCount)
            return null;

        while (_nextDepthByCol[col] <= _maxDepth)
        {
            int depth = _nextDepthByCol[col]++;

            if (!_rowsByDepth.TryGetValue(depth, out LevelGridRow row))
                continue;

            if (row.ColBlockTypes == null || col >= row.ColBlockTypes.Length)
                continue;

            if (LevelCellParser.TryParseCell(row.ColBlockTypes[col], out string typeKey, out int stack))
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

    /// <summary>Còn block chờ dequeue trong bin cột.</summary>
    public bool HasRemainingBlocks()
    {
        for (int col = 0; col < c_ColumnCount; col++)
        {
            if (HasRemainingInColumn(col))
                return true;
        }

        return false;
    }

    private bool HasRemainingInColumn(int col)
    {
        int savedDepth = _nextDepthByCol[col];

        while (_nextDepthByCol[col] <= _maxDepth)
        {
            int depth = _nextDepthByCol[col]++;

            if (!_rowsByDepth.TryGetValue(depth, out LevelGridRow row))
                continue;

            if (row.ColBlockTypes == null || col >= row.ColBlockTypes.Length)
                continue;

            if (LevelCellParser.TryParseCell(row.ColBlockTypes[col], out _, out _))
            {
                _nextDepthByCol[col] = savedDepth;
                return true;
            }
        }

        _nextDepthByCol[col] = savedDepth;
        return false;
    }
}
