using BlockSystem.Runtime;

namespace Game.Grid
{
    public class Slot
    {
        public int Row { get; }
        public int Col { get; }
        public Block CurrentBlock { get; private set; }

        public bool IsEmpty => CurrentBlock == null;
        public bool HasBlock => CurrentBlock != null;

        public Slot(int row, int col)
        {
            Row = row;
            Col = col;
        }

        // Điểm cộng lớn: Giúp mở rộng ô Băng, ô Khóa sau này mà không nát MoveSystem
        public bool CanPlace(Block block)
        {
            return IsEmpty;
        }

        public void SetBlock(Block block)
        {
            CurrentBlock = block;
            if (block != null)
            {
                block.SetCurrentSlot(this);
            }
        }

        public void Clear()
        {
            if (CurrentBlock != null)
            {
                CurrentBlock.SetCurrentSlot(null);
            }
            CurrentBlock = null;
        }
    }
}