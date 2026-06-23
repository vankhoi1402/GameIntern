using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Chạy animation DOTween khi block rơi do gravity — lắng nghe GravitySystem.
/// </summary>
public class BlockAnimationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;

    /// <summary>Đăng ký nhận lệnh animation gravity.</summary>
    private void OnEnable()
    {
        GravitySystem.OnGravityAnimationRequested += HandleGravityAnimation;
        ColumnPushSystem.OnRefillWaveAnimationRequested += HandleRefillWaveAnimation;
    }

    /// <summary>Hủy đăng ký khi disable.</summary>
    private void OnDisable()
    {
        GravitySystem.OnGravityAnimationRequested -= HandleGravityAnimation;
        ColumnPushSystem.OnRefillWaveAnimationRequested -= HandleRefillWaveAnimation;
    }

    /// <summary>
    /// Nhận danh sách lệnh rơi, tween từng block tới ô đích.
    /// Gọi onCompleteCallback khi tất cả tween xong.
    /// </summary>
    private void HandleGravityAnimation(List<GravityMoveCommand> commands, System.Action onCompleteCallback)
    {
        if (boardManager == null)
        {
            Debug.LogError("<color=red>[BUG CHÍ MẠNG]</color> Ông quên chưa kéo thả BoardManager vào BlockAnimationManager ngoài Inspector kìa! Kéo vào ngay ông ơi!");
            onCompleteCallback?.Invoke();
            return;
        }

        if (boardManager.Layout == null)
        {
            Debug.LogError("<color=yellow>[BUG KHỞI TẠO]</color> boardManager.Layout đang bị NULL! Ông chưa tạo 'new BoardLayout()' hoặc chưa gán nó vào 'boardManager.Layout' lúc game bắt đầu rồi.");
            onCompleteCallback?.Invoke();
            return;
        }

        int runningTweens = commands.Count;
        if (runningTweens == 0)
        {
            onCompleteCallback?.Invoke();
            return;
        }

        foreach (var cmd in commands)
        {
            if (cmd.Block == null)
            {
                Debug.LogWarning($"[CẢNH BÁO] Ô cờ báo có gạch rơi từ [{cmd.FromRow},{cmd.FromCol}] nhưng biến Block lại bị NULL!");
                runningTweens--;
                if (runningTweens <= 0) onCompleteCallback?.Invoke();
                continue;
            }

            Vector3 targetWorldPos = boardManager.Layout.GetWorldPosition(cmd.ToRow, cmd.ToCol);

            // Thời gian rơi tỷ lệ căn bậc hai số ô rơi — càng xa càng nhanh dứt khoát
            float duration = Mathf.Sqrt(cmd.DropDistance) * 0.12f;

            cmd.Block.transform.DOMove(targetWorldPos, duration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    if (cmd.Block.TryGetComponent<BlockView>(out var view))
                        view.ApplyGridSorting(cmd.ToRow);

                    runningTweens--;
                    if (runningTweens <= 0)
                        onCompleteCallback?.Invoke();
                });
        }
    }

    private void HandleRefillWaveAnimation(IReadOnlyList<ColumnRefillPacket> wave, System.Action onComplete)
    {
        if (boardManager == null || boardManager.Layout == null)
        {
            onComplete?.Invoke();
            return;
        }

        int running = 0;
        foreach (ColumnRefillPacket packet in wave)
            running += packet.PushCommands?.Count ?? 0;

        if (running <= 0)
        {
            onComplete?.Invoke();
            return;
        }

        const float duration = 0.12f;
        foreach (ColumnRefillPacket packet in wave)
        {
            if (packet.PushCommands == null)
                continue;

            foreach (ColumnPushCommand cmd in packet.PushCommands)
            {
                if (cmd.Block == null)
                {
                    running--;
                    if (running <= 0)
                        onComplete?.Invoke();
                    continue;
                }

                Vector3 from = boardManager.Layout.GetWorldPosition(cmd.FromRow, cmd.Col);
                Vector3 to = boardManager.Layout.GetWorldPosition(cmd.ToRow, cmd.Col);
                cmd.Block.transform.position = from;

                cmd.Block.transform.DOMove(to, duration)
                    .SetEase(Ease.OutCubic)
                    .OnComplete(() =>
                    {
                        if (cmd.Block.TryGetComponent<BlockView>(out var view))
                            view.ApplyGridSorting(cmd.ToRow);
                        running--;
                        if (running <= 0)
                            onComplete?.Invoke();
                    });
            }
        }
    }
}
