using System;
using UnityEngine;

/// <summary>
/// Xử lý hiệu ứng View khi block đạt stack max — rơi ra ngoài màn hình rồi Destroy.
/// </summary>
public class BlockRemoveViewHandler : MonoBehaviour
{
    [SerializeField] private MergeSystem mergeSystem;
    [SerializeField] private Camera targetCamera;

    [Header("Fall Off Screen")]
    [SerializeField] private float fallDuration = 0.55f;
    [SerializeField] private float fallWorldDistance = 6f;

    /// <summary>Tự tìm MergeSystem trên cùng GameObject nếu chưa gán.</summary>
    private void Awake()
    {
        if (mergeSystem == null)
            mergeSystem = GetComponent<MergeSystem>();
    }

    /// <summary>Lắng nghe OnBlockMaxedOut (fallback nếu không gọi trực tiếp).</summary>
    private void OnEnable()
    {
        if (mergeSystem != null)
            mergeSystem.OnBlockMaxedOut += HandleMaxedOut;
    }

    /// <summary>Hủy đăng ký event.</summary>
    private void OnDisable()
    {
        if (mergeSystem != null)
            mergeSystem.OnBlockMaxedOut -= HandleMaxedOut;
    }

    /// <summary>Chạy animation rơi và Destroy block — có thể gọi trực tiếp từ MergeSystem.</summary>
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

    /// <summary>Handler cho event OnBlockMaxedOut — ủy quyền sang PlayFallOff.</summary>
    private void HandleMaxedOut(Block block, int row, int col, Action onComplete)
    {
        PlayFallOff(block, onComplete);
    }
}
