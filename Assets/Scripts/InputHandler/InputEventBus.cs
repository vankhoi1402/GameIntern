using UnityEngine;
using System;

/// <summary>
/// Bus sự kiện input — tách InputManager khỏi DragController (pub/sub pattern).
/// </summary>
public class InputEventBus : MonoBehaviour
{
    public static InputEventBus Instance { get; private set; }

    public event Action<Block> OnDragStarted;
    public event Action<Block, Vector3> OnDragging;
    public event Action<Block> OnDragEnded;

    /// <summary>Đăng ký singleton, giữ qua scene nếu cần.</summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>Phát sự kiện bắt đầu kéo block.</summary>
    public void RaiseDragStarted(Block block) => OnDragStarted?.Invoke(block);

    /// <summary>Phát sự kiện đang kéo — kèm vị trí chuột world.</summary>
    public void RaiseDragging(Block block, Vector3 mouseWorldPos) => OnDragging?.Invoke(block, mouseWorldPos);

    /// <summary>Phát sự kiện thả chuột.</summary>
    public void RaiseDragEnded(Block block) => OnDragEnded?.Invoke(block);
}
