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
    }

    /// <summary>Hủy đăng ký khi disable.</summary>
    private void OnDisable()
    {
        GravitySystem.OnGravityAnimationRequested -= HandleGravityAnimation;
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
                    runningTweens--;
                    if (runningTweens <= 0)
                        onCompleteCallback?.Invoke();
                });
        }
    }
}
