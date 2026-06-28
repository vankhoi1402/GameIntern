using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Read-only query layer — quan sát BoardManager và LevelRefillState, không mutate.
/// </summary>
public sealed class BoardAnalyzer
{
    private readonly BoardManager m_Board;
    private readonly LevelRefillState m_RefillState;

    public BoardAnalyzer(BoardManager board, LevelRefillState refillState)
    {
        m_Board = board;
        m_RefillState = refillState;
    }

    public int CountBlocks()
    {
        if (!IsBoardReadable())
            return 0;

        int count = 0;
        for (int row = 0; row < m_Board.Rows; row++)
        {
            for (int col = 0; col < m_Board.Columns; col++)
            {
                Slot slot = m_Board.GetSlot(row, col);
                if (slot != null && slot.HasBlock)
                    count++;
            }
        }

        return count;
    }

    public int CountEmptySlots()
    {
        if (!IsBoardReadable())
            return 0;

        return m_Board.Rows * m_Board.Columns - CountBlocks();
    }

    public bool HasBlocksOnBoard() => CountBlocks() > 0;

    public bool IsBoardEmpty() => !HasBlocksOnBoard();

    public bool HasRemainingBinSupply()
    {
        return m_RefillState != null &&
               m_RefillState.HasRemainingBlocks();
    }

    /// <summary>Cột chưa đủ block để lấp đầy grid.</summary>
    public bool ColumnNeedsRefill(int col)
    {
        if (!IsBoardReadable() || col < 0 || col >= m_Board.Columns)
            return false;

        return CountBlocksInColumn(col) < m_Board.Rows;
    }

    public IReadOnlyList<Vector2Int> GetEmptySlots()
    {
        var empty = new List<Vector2Int>();
        if (!IsBoardReadable())
            return empty;

        for (int row = 0; row < m_Board.Rows; row++)
        {
            for (int col = 0; col < m_Board.Columns; col++)
            {
                Slot slot = m_Board.GetSlot(row, col);
                if (slot != null && slot.IsEmpty)
                    empty.Add(new Vector2Int(row, col));
            }
        }

        return empty;
    }

    private int CountBlocksInColumn(int col)
    {
        int count = 0;
        for (int row = 0; row < m_Board.Rows; row++)
        {
            Slot slot = m_Board.GetSlot(row, col);
            if (slot != null && slot.HasBlock)
                count++;
        }

        return count;
    }

    private bool IsBoardReadable()
        => m_Board != null && m_Board.IsGridReady;
}
