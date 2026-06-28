using UnityEngine;

/// <summary>Trạng thái kéo block — Idle hoặc đang Dragging.</summary>
public enum DragState { Idle, Dragging }

/// <summary>
/// Điều phối logic kéo thả — nhận thông báo từ InputManager, gọi MoveSystem hoặc reset về ô gốc.
/// </summary>
public class DragController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private MoveSystem moveSystem;
    [SerializeField] private Camera targetCamera;

    private Slot _sourceSlot;
    private Block _draggedBlock;
    private Block _hoverTargetBlock;
    private DragState _currentState = DragState.Idle;
    private Vector3 _dragOffset;

    /// <summary>Lấy camera mặc định nếu chưa gán.</summary>
    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    /// <summary>Bắt đầu kéo — lưu ô gốc, tính offset chuột và bắt đầu visual drag.</summary>
    public void NotifyDragStarted(Block block)
    {
        if (InputManager.Instance != null && InputManager.Instance.IsInputLocked)
            return;

        if (BoardStateManager.Instance != null && !BoardStateManager.Instance.CanAcceptInput())
            return;

        if (_currentState != DragState.Idle || block == null) return;

        _sourceSlot = block.CurrentSlot;
        if (_sourceSlot == null) return;

        _currentState = DragState.Dragging;
        _draggedBlock = block;

        Vector3 mousePos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        _dragOffset = block.transform.position - mousePos;

        if (block.TryGetComponent<BlockView>(out var view))
            view.BeginDrag();
    }

    /// <summary>Cập nhật vị trí visual block theo chuột + offset.</summary>
    public void NotifyDragging(Block block, Vector3 mouseWorldPos)
    {
        if (InputManager.Instance != null && InputManager.Instance.IsInputLocked)
            return;

        if (BoardStateManager.Instance != null && !BoardStateManager.Instance.CanAcceptInput())
            return;

        if (_currentState != DragState.Dragging || block == null) return;

        Vector3 targetVisualPos = mouseWorldPos + _dragOffset;
        if (block.TryGetComponent<BlockView>(out var view))
            view.FollowDragPosition(targetVisualPos);

        UpdateHoverTarget(mouseWorldPos);
    }

    /// <summary>
    /// Kết thúc kéo — chuyển world pos sang ô lưới, gọi ProcessMove hoặc trả block về ô gốc.
    /// </summary>
    public void NotifyDragEnded(Block block)
    {
        if (InputManager.Instance != null && InputManager.Instance.IsInputLocked)
        {
            CancelActiveDrag();
            return;
        }

        if (_currentState != DragState.Dragging || block == null) return;

        ClearHoverTarget();
        _currentState = DragState.Idle;
        _draggedBlock = null;

        if (boardManager != null && boardManager.IsGridReady && targetCamera != null)
        {
            Vector3 dropWorldPos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            dropWorldPos.z = 0;

            Vector2Int gridPos = boardManager.WorldToGrid(dropWorldPos);
            Slot targetSlot = boardManager.GetSlot(gridPos.x, gridPos.y);

            if (targetSlot != null && targetSlot != _sourceSlot)
            {
                bool shakeReject = targetSlot.HasBlock;
                if (!moveSystem.ProcessMove(_sourceSlot, targetSlot))
                    ReturnBlockToSource(block, shakeReject);
                return;
            }
        }

        ReturnBlockToSource(block, false);
    }

    /// <summary>Hủy kéo đang dở — trả block về ô gốc (gọi khi thua / khóa input).</summary>
    public void CancelActiveDrag()
    {
        if (_currentState != DragState.Dragging)
            return;

        Block block = _draggedBlock != null ? _draggedBlock : _sourceSlot?.CurrentBlock;
        ClearHoverTarget();
        _currentState = DragState.Idle;

        if (block != null && _sourceSlot != null)
            ReturnBlockToSource(block, false);

        _sourceSlot = null;
        _draggedBlock = null;
    }

    private void UpdateHoverTarget(Vector3 mouseWorldPos)
    {
        Block next = null;

        if (boardManager != null && boardManager.IsGridReady)
        {
            Vector2Int grid = boardManager.WorldToGrid(mouseWorldPos);
            Slot slot = boardManager.GetSlot(grid.x, grid.y);
            if (slot != null && slot != _sourceSlot && slot.HasBlock)
                next = slot.CurrentBlock;
        }

        if (next == _hoverTargetBlock)
            return;

        if (_hoverTargetBlock != null && _hoverTargetBlock.TryGetComponent<BlockView>(out var prev))
            prev.ClearHoverTargetFeedback();

        _hoverTargetBlock = next;

        if (next != null && next.TryGetComponent<BlockView>(out var view))
            view.PlayHoverTargetFeedback();
    }

    private void ClearHoverTarget()
    {
        if (_hoverTargetBlock == null)
            return;

        if (_hoverTargetBlock.TryGetComponent<BlockView>(out var view))
            view.ClearHoverTargetFeedback();

        _hoverTargetBlock = null;
    }

    private void ReturnBlockToSource(Block block, bool shakeReject)
    {
        if (block == null || _sourceSlot == null || boardManager == null || !boardManager.IsGridReady)
            return;

        Vector3 originWorldPos = boardManager.GridToWorld(_sourceSlot.Row, _sourceSlot.Col);
        if (block.TryGetComponent<BlockView>(out var view))
            view.EndDragReturnTo(originWorldPos, _sourceSlot.Row, shakeReject);
    }
}
