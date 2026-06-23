using System;
using System.Collections;
using UnityEngine;
using Game.Grid;
using BlockSystem.Runtime;

namespace Game.Logic
{
    public class MoveSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private MergeSystem mergeSystem;

        public event Action<Slot, Slot> OnMoveRejected;

        public void ProcessMove(Slot sourceSlot, Slot targetSlot)
        {
            if (sourceSlot == null || targetSlot == null || sourceSlot.IsEmpty) return;

            // KIỂM TRA KHÓA: Đang rơi hoặc đang gộp thì cấm di chuyển tiếp
            if (BoardStateManager.Instance != null && !BoardStateManager.Instance.CanAcceptInput()) return;

            Block sourceBlock = sourceSlot.CurrentBlock;
            Block targetBlock = targetSlot.CurrentBlock;

            // 1. Kéo vào ô trống hợp lệ
            if (targetSlot.CanPlace(sourceBlock))
            {
                StartCoroutine(MoveAndGravityRoutine(sourceSlot, targetSlot));
                return;
            }

            // 2. Hai gạch đập vào nhau gộp cấp số
            if (targetBlock != null && sourceBlock.Type == targetBlock.Type)
            {
                mergeSystem.PreceptMerge(sourceBlock, targetBlock);
            }
            else
            {
                OnMoveRejected?.Invoke(sourceSlot, targetSlot);
            }
        }

        private IEnumerator MoveAndGravityRoutine(Slot sourceSlot, Slot targetSlot)
        {
            BoardStateManager.Instance.ChangeState(BoardState.ProcessingMove);

            boardManager.MoveBlock(sourceSlot.Row, sourceSlot.Col, targetSlot.Row, targetSlot.Col);

            // Đợi hiệu ứng kéo thả hoàn tất trên View
            yield return new WaitForSeconds(0.15f);

            // Kích hoạt trọng lực lấp khoảng trống ô cũ vừa bỏ lại
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