using System;
using UnityEngine;

/// <summary>
/// Hiệu ứng View khi block bị xóa — rơi ra ngoài màn hình rồi Destroy (gọi thủ công nếu cần).
/// </summary>
public class BlockRemoveViewHandler : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    [Header("Fall Off Screen")]
    [SerializeField] private float fallDuration = 0.55f;
    [SerializeField] private float fallWorldDistance = 6f;

    /// <summary>Chạy animation rơi và Destroy block.</summary>
    public void PlayFallOff(Block block, Action onComplete)
    {
        if (block == null)
        {
            onComplete?.Invoke();
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (block.TryGetComponent<BlockView>(out var view))
        {
            view.PlayFallOffScreen(cam, fallWorldDistance, fallDuration, () =>
            {
                if (block != null)
                    Destroy(block.gameObject);
                onComplete?.Invoke();
            });
        }
        else
        {
            Destroy(block.gameObject);
            onComplete?.Invoke();
        }
    }
}
