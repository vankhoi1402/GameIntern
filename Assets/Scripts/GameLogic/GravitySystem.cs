using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Grid;
using BlockSystem.Runtime;

namespace Game.Logic
{
    public struct GravityMoveCommand
    {
        public Block Block;
        public int FromRow, FromCol;
        public int ToRow, ToCol;
        public int DropDistance;
    }

    public class GravitySystem : MonoBehaviour
    {
        public static GravitySystem Instance { get; private set; }

        [Header("References")]
        [SerializeField] private BoardManager boardManager;

        // Tín hiệu phát lệnh cho tầng View chạy Animation. Khi View chạy xong PHẢI gọi callback Action.
        public static event Action<List<GravityMoveCommand>, Action> OnGravityAnimationRequested;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RunGravity()
        {
            BoardStateManager.Instance.ChangeState(BoardState.ApplyingGravity);
            StartCoroutine(GravityPipelineRoutine());
        }

        private IEnumerator GravityPipelineRoutine()
        {
            while (true)
            {
                // Bước 1: Thu thập lệnh (Scan sạch, không mutate grid lúc này)
                List<GravityMoveCommand> commands = CollectGravityCommands();

                if (commands.Count == 0) break;

                // Bước 2: Cập nhật Model đồng loạt (Instant Data Mutation)
                foreach (var cmd in commands)
                {
                    boardManager.MoveBlock(cmd.FromRow, cmd.FromCol, cmd.ToRow, cmd.ToCol);
                }

                // Bước 3: Đợi View phản hồi đồng bộ (Deterministic Sync)
                bool isAnimationComplete = false;
                OnGravityAnimationRequested?.Invoke(commands, () =>
                {
                    isAnimationComplete = true;
                });

                yield return new WaitUntil(() => isAnimationComplete);
            }

            // Kết thúc toàn bộ chuỗi Cascade rơi rụng -> Trả lại quyền chơi
            BoardStateManager.Instance.ChangeState(BoardState.Idle);
        }

        private List<GravityMoveCommand> CollectGravityCommands()
        {
            List<GravityMoveCommand> commands = new List<GravityMoveCommand>();

            for (int col = 0; col < boardManager.Columns; col++)
            {
                int emptyRowsCount = 0;

                for (int row = 0; row < boardManager.Rows; row++)
                {
                    Slot currentSlot = boardManager.GetSlot(row, col);
                    if (currentSlot == null) continue;

                    if (currentSlot.IsEmpty)
                    {
                        emptyRowsCount++;
                    }
                    else if (emptyRowsCount > 0)
                    {
                        commands.Add(new GravityMoveCommand
                        {
                            Block = currentSlot.CurrentBlock,
                            FromRow = row,
                            FromCol = col,
                            ToRow = row - emptyRowsCount,
                            ToCol = col,
                            DropDistance = emptyRowsCount
                        });
                    }
                }
            }
            return commands;
        }
    }
}