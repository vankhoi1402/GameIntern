using System;
using System.Collections;
using UnityEngine;
using Game.Grid;
using BlockSystem.Runtime;

namespace Game.Logic
{
    public class MergeSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardManager boardManager;

        public event Action<Block, Block> OnBlockStacked;
        public event Action<Block, int, int> OnBlockMaxedOut;

        public void PreceptMerge(Block sourceBlock, Block targetBlock)
        {
            if (sourceBlock == null || targetBlock == null) return;
            if (targetBlock.IsPendingDestroy || sourceBlock.IsPendingDestroy) return;

            BoardStateManager.Instance.ChangeState(BoardState.ResolvingMerges);
            StartCoroutine(MergeRoutine(sourceBlock, targetBlock));
        }

        private IEnumerator MergeRoutine(Block sourceBlock, Block targetBlock)
        {
            sourceBlock.IsPendingDestroy = true;

            // FIX LỖI TOÁN HỌC: Cộng dồn lũy kế giá trị nội tại (Kéo 2 vào 1 thì lên thẳng 3)
            int sourceWeight = sourceBlock.StackCount;
            targetBlock.AddStack(sourceWeight);

            Slot sourceSlot = sourceBlock.CurrentSlot;
            if (sourceSlot != null)
            {
                boardManager.RemoveBlock(sourceSlot.Row, sourceSlot.Col);
            }

            OnBlockStacked?.Invoke(sourceBlock, targetBlock);

            // Chờ View chạy xong animation gộp cơ bản (có thể thay bằng callback nếu muốn tối ưu sâu hơn)
            yield return new WaitForSeconds(0.25f);

            if (targetBlock.StackCount >= 3)
            {
                Slot targetSlot = targetBlock.CurrentSlot;
                if (targetSlot != null)
                {
                    targetBlock.IsPendingDestroy = true;

                    OnBlockMaxedOut?.Invoke(targetBlock, targetSlot.Row, targetSlot.Col);
                    boardManager.RemoveBlock(targetSlot.Row, targetSlot.Col);

                    yield return new WaitForSeconds(0.2f);
                }
            }

            // BÀN GIAO CHO GRAVITY: Rơi dồn gạch ngay khi có khoảng trống nổ
            if (GravitySystem.Instance != null)
            {
                GravitySystem.Instance.RunGravity();
            }
            else
            {
                BoardStateManager.Instance.ChangeState(BoardState.Idle);
            }
        }
    }
}