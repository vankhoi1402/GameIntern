using System;
using UnityEngine;
using BlockSystem.Runtime;

namespace Game.Grid
{
    public class BoardManager : MonoBehaviour
    {
        [Header("Board Settings")]
        [SerializeField] private int rows = 8;
        [SerializeField] private int columns = 6;

        // 🔥 THÊM Ô NÀY ngoài Inspector để kéo thả vùng hiển thị bàn cờ vào
        [Header("Area References")]
        [SerializeField] private BoardArea boardArea;

        private Slot[,] slots;

        // Hệ thống giữ bộ tính toán tọa độ cho tầng View mượn
        public BoardLayout Layout { get; set; }

        // HỆ THỐNG EVENT THUẦN LOGIC
        public event Action<Block, int, int> OnBlockPlaced;
        public event Action<Block, int, int, int, int> OnBlockMoved;
        public event Action<Block, int, int> OnBlockRemoved;

        public int Rows => rows;
        public int Columns => columns;

        private void Awake()
        {
            InitBoard();
        }

        private void Start()
        {
            // 🔥 TỰ ĐỘNG KHỞI TẠO TẠI ĐÂY: Vừa vào game là tự tạo Layout luôn, không sợ bị NULL nữa!
            if (boardArea != null)
            {
                Layout = new BoardLayout(boardArea, rows, columns);
                Debug.Log("<color=green>[INIT SUCCESS]</color> Đã tự động tạo và nạp BoardLayout thành công!");
            }
            else
            {
                Debug.LogError("<color=red>[THIẾU SỐ LIỆU]</color> BoardManager chưa được kéo thả BoardArea ngoài Inspector kìa ông ơi!");
            }
        }

        private void InitBoard()
        {
            slots = new Slot[rows, columns];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    slots[r, c] = new Slot(r, c);
                }
            }
        }

        public bool IsValidPosition(int row, int col) => row >= 0 && row < rows && col >= 0 && col < columns;

        public Slot GetSlot(int row, int col) => !IsValidPosition(row, col) ? null : slots[row, col];

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

        public void RemoveBlock(int row, int col)
        {
            Slot slot = GetSlot(row, col);
            if (slot == null || slot.IsEmpty) return;

            Block blockToRemove = slot.CurrentBlock;
            slot.Clear();
            OnBlockRemoved?.Invoke(blockToRemove, row, col);
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
    }
}