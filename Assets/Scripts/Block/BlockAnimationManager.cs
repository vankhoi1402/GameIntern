using System.Collections.Generic;
using UnityEngine;
using DG.Tweening; // Đảm bảo dự án đã import DOTween
using Game.Logic;
using BlockSystem.View; // Để gọi sang BlockView nếu cần
using Game.Grid;

namespace Game.View
{
    public class BlockAnimationManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardManager boardManager; // Kéo thả BoardManager vào đây ngoài Inspector

        private void OnEnable()
        {
            // Lắng nghe sự kiện từ hệ thống Trọng lực mới
            GravitySystem.OnGravityAnimationRequested += HandleGravityAnimation;
        }

        private void OnDisable()
        {
            GravitySystem.OnGravityAnimationRequested -= HandleGravityAnimation;
        }

        private void HandleGravityAnimation(List<GravityMoveCommand> commands, System.Action onCompleteCallback)
        {
            // 🛡️ CHỐT CHẶN 1: Kiểm tra xem đã kéo thả BoardManager chưa
            if (boardManager == null)
            {
                Debug.LogError("<color=red>[BUG CHÍ MẠNG]</color> Ông quên chưa kéo thả BoardManager vào BlockAnimationManager ngoài Inspector kìa! Kéo vào ngay ông ơi!");
                onCompleteCallback?.Invoke(); // Giải phóng để game không bị treo
                return;
            }

            // 🛡️ CHỐT CHẶN 2: Kiểm tra xem bộ tính tọa độ Layout đã được nạp chưa
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
                // 🛡️ CHỐT CHẶN 3: Kiểm tra viên gạch có bị "bốc hơi" trước đó không
                if (cmd.Block == null)
                {
                    Debug.LogWarning($"[CẢNH BÁO] Ô cờ báo có gạch rơi từ [{cmd.FromRow},{cmd.FromCol}] nhưng biến Block lại bị NULL!");
                    runningTweens--;
                    if (runningTweens <= 0) onCompleteCallback?.Invoke();
                    continue;
                }

                // Chạy tính toán vị trí an toàn sau khi đã qua 3 lớp bảo hiểm
                Vector3 targetWorldPos = boardManager.Layout.GetWorldPosition(cmd.ToRow, cmd.ToCol);
                float duration = cmd.DropDistance * 0.08f;

                cmd.Block.transform.DOMove(targetWorldPos, duration)
                    .SetEase(Ease.OutBounce)
                    .OnComplete(() =>
                    {
                        runningTweens--;
                        if (runningTweens <= 0)
                        {
                            onCompleteCallback?.Invoke();
                        }
                    });
            }
        }
    }
}