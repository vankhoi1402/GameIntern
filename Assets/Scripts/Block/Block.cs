using UnityEngine;
using BlockSystem.Data;
using Game.Grid;

namespace BlockSystem.Runtime
{
    public class Block : MonoBehaviour
    {
        public BlockData Data { get; private set; }
        public BlockType Type => Data != null ? Data.BlockType : BlockType.None;

        public Slot CurrentSlot { get; private set; }
        public int StackCount { get; private set; } = 1;

        // Cờ tử hình: Chặn đứng mọi tương tác khi gạch chuẩn bị biến mất
        public bool IsPendingDestroy { get; set; } = false;

        public void Init(BlockData data)
        {
            Data = data;
            StackCount = 1;
            IsPendingDestroy = false;
        }

        public void AddStack(int amount = 1)
        {
            if (IsPendingDestroy) return;
            StackCount += amount;
        }

        internal void SetCurrentSlot(Slot slot)
        {
            if (CurrentSlot == slot) return;
            CurrentSlot = slot;
        }
    }
}