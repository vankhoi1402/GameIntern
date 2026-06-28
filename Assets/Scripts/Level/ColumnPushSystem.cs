using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Lệnh dịch block lên 1 ô trong cột (refill push).</summary>
public struct ColumnPushCommand
{
    public Block Block;
    public int FromRow;
    public int ToRow;
    public int Col;
}

/// <summary>Một cột được refill trong một wave — shift + block mới row 0.</summary>
public struct ColumnRefillPacket
{
    public int Col;
    public List<ColumnPushCommand> PushCommands;
    public Block NewBlock;
}

/// <summary>Logic đẩy cột lên — shift block + chỗ trống row 0 cho spawn mới.</summary>
public class ColumnPushSystem : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;

    private void Awake()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
    }

    /// <summary>
    /// Dịch toàn bộ block trong cột lên 1 hàng. Trả false nếu cột đầy (không đủ chỗ).
    /// </summary>
    public bool TryShiftColumnUp(int col, out List<ColumnPushCommand> commands)
    {
        commands = new List<ColumnPushCommand>();
        if (boardManager == null) return false;

        var blocksInColumn = new List<(Block block, int row)>();
        for (int row = 0; row < boardManager.Rows; row++)
        {
            Slot slot = boardManager.GetSlot(row, col);
            if (slot != null && slot.HasBlock)
                blocksInColumn.Add((slot.CurrentBlock, row));
        }

        if (blocksInColumn.Count >= boardManager.Rows)
            return false;

        int topRow = boardManager.Rows - 1;
        foreach (var (_, row) in blocksInColumn)
        {
            if (row + 1 > topRow)
                return false;
        }

        for (int i = blocksInColumn.Count - 1; i >= 0; i--)
        {
            Block block = blocksInColumn[i].block;
            int fromRow = blocksInColumn[i].row;
            int toRow = fromRow + 1;
            boardManager.ClearSlotSilent(fromRow, col);
            boardManager.SetBlockSilent(block, toRow, col);
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
}
