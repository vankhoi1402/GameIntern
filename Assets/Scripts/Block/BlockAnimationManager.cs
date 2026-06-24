using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Chạy animation DOTween khi block rơi do gravity / refill push.
/// </summary>
public class BlockAnimationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;

    [Header("Timing")]
    [SerializeField] private float refillPushDuration = 0.12f;

    private void OnEnable()
    {
        GravitySystem.OnGravityAnimationRequested += HandleGravityAnimation;
        ColumnPushSystem.OnRefillWaveAnimationRequested += HandleRefillWaveAnimation;
    }

    private void OnDisable()
    {
        GravitySystem.OnGravityAnimationRequested -= HandleGravityAnimation;
        ColumnPushSystem.OnRefillWaveAnimationRequested -= HandleRefillWaveAnimation;
    }

    private void HandleGravityAnimation(List<GravityMoveCommand> commands, Action onCompleteCallback)
    {
        if (boardManager == null || boardManager.Layout == null)
        {
            onCompleteCallback?.Invoke();
            return;
        }

        var batch = new TweenCompletionBatch(commands.Count, onCompleteCallback);

        foreach (GravityMoveCommand cmd in commands)
        {
            if (cmd.Block == null)
            {
                batch.NotifyOneDone();
                continue;
            }

            Vector3 targetWorldPos = boardManager.Layout.GetWorldPosition(cmd.ToRow, cmd.ToCol);
            float duration = Mathf.Sqrt(cmd.DropDistance) * 0.12f;
            int toRow = cmd.ToRow;
            Block block = cmd.Block;

            var gate = new SingleTweenGate();
            void FinishTween()
            {
                gate.TryRun(() =>
                {
                    if (block != null && block.TryGetComponent<BlockView>(out var view))
                    {
                        view.ResetVisualState();
                        view.ApplyGridSorting(toRow);
                    }
                    batch.NotifyOneDone();
                });
            }

            block.transform.DOMove(targetWorldPos, duration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    gate.TryRun(() =>
                    {
                        if (block != null && block.TryGetComponent<BlockView>(out var view))
                        {
                            view.ResetVisualState();
                            view.ApplyGridSorting(toRow);
                        }
                        batch.NotifyOneDone();
                    });
                })
                .OnKill(FinishTween);
        }
    }

    private void HandleRefillWaveAnimation(IReadOnlyList<ColumnRefillPacket> wave, Action onComplete)
    {
        if (boardManager == null || boardManager.Layout == null)
        {
            onComplete?.Invoke();
            return;
        }

        int commandCount = 0;
        foreach (ColumnRefillPacket packet in wave)
            commandCount += packet.PushCommands?.Count ?? 0;

        var batch = new TweenCompletionBatch(commandCount, onComplete);

        foreach (ColumnRefillPacket packet in wave)
        {
            if (packet.PushCommands == null)
                continue;

            foreach (ColumnPushCommand cmd in packet.PushCommands)
            {
                if (cmd.Block == null)
                {
                    batch.NotifyOneDone();
                    continue;
                }

                Vector3 from = boardManager.Layout.GetWorldPosition(cmd.FromRow, cmd.Col);
                Vector3 to = boardManager.Layout.GetWorldPosition(cmd.ToRow, cmd.Col);

                int toRow = cmd.ToRow;
                Block block = cmd.Block;

                if (block != null && block.TryGetComponent<BlockView>(out var pushView))
                {
                    pushView.ResetVisualState();
                    block.transform.position = from;
                }
                else
                {
                    cmd.Block.transform.position = from;
                }

                var gate = new SingleTweenGate();
                void FinishTween()
                {
                    gate.TryRun(() =>
                    {
                        if (block != null && block.TryGetComponent<BlockView>(out var view))
                            view.ApplyGridSorting(toRow);
                        batch.NotifyOneDone();
                    });
                }

                block.transform.DOMove(to, refillPushDuration)
                    .SetEase(Ease.OutCubic)
                    .OnComplete(FinishTween)
                    .OnKill(FinishTween);
            }
        }
    }

    private sealed class SingleTweenGate
    {
        private bool m_Done;

        public void TryRun(Action action)
        {
            if (m_Done) return;
            m_Done = true;
            action?.Invoke();
        }
    }

    /// <summary>Đếm tween hoàn tất.</summary>
    private sealed class TweenCompletionBatch
    {
        private int m_Remaining;
        private bool m_Invoked;
        private readonly Action m_OnComplete;

        public TweenCompletionBatch(int count, Action onComplete)
        {
            m_Remaining = count;
            m_OnComplete = onComplete;
            if (m_Remaining <= 0)
                InvokeOnce();
        }

        public void NotifyOneDone()
        {
            m_Remaining--;
            if (m_Remaining <= 0)
                InvokeOnce();
        }

        private void InvokeOnce()
        {
            if (m_Invoked) return;
            m_Invoked = true;
            m_OnComplete?.Invoke();
        }
    }
}
