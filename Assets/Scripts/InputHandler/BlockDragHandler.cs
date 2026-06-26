using UnityEngine;

/// <summary>
/// Cách kéo thả thay thế qua OnMouse — gắn trên prefab Block (dùng song song hoặc thay InputManager).
/// </summary>
[RequireComponent(typeof(Block))]
public class BlockDragHandler : MonoBehaviour
{
    private Block _block;
    private Camera _mainCamera;

    /// <summary>Cache Block và camera.</summary>
    private void Awake()
    {
        _block = GetComponent<Block>();
        _mainCamera = Camera.main;
    }

    /// <summary>Unity gọi khi click — phát DragStarted qua bus.</summary>
    private void OnMouseDown()
    {
        if (!CanAcceptBoardInput())
            return;

        if (InputEventBus.Instance != null)
            InputEventBus.Instance.RaiseDragStarted(_block);
    }

    /// <summary>Unity gọi khi kéo — phát Dragging với vị trí chuột world.</summary>
    private void OnMouseDrag()
    {
        if (!CanAcceptBoardInput())
            return;

        if (_mainCamera == null || InputEventBus.Instance != null == false) return;

        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        InputEventBus.Instance.RaiseDragging(_block, mouseWorldPos);
    }

    /// <summary>Unity gọi khi thả chuột — phát DragEnded.</summary>
    private void OnMouseUp()
    {
        if (InputEventBus.Instance != null)
            InputEventBus.Instance.RaiseDragEnded(_block);
    }

    private static bool CanAcceptBoardInput()
    {
        return BoardStateManager.Instance == null || BoardStateManager.Instance.CanAcceptInput();
    }
}
