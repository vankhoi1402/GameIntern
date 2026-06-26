using UnityEngine;
using System;

/// <summary>Trạng thái kéo block — Idle hoặc đang Dragging.</summary>
public enum DragState { Idle, Dragging }

/// <summary>
/// Điều phối logic kéo thả — nhận input từ InputEventBus, gọi MoveSystem hoặc reset về ô gốc.
/// </summary>
public class DragController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardView boardView;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private MoveSystem moveSystem;
    [SerializeField] private Camera targetCamera;

    private Slot _sourceSlot;
    private Block _draggedBlock;
    private Block _hoverTargetBlock;
    private DragState _currentState = DragState.Idle;
    private Vector3 _dragOffset;

    public event Action<Block, Vector3> OnBlockVisualDragged;
    public event Action<Block, Slot> OnBlockVisualReset;
    public event Action<Block> OnBlockSelected;
    public event Action<Block> OnHoverTargetChanged;

    /// <summary>Lấy camera mặc định nếu chưa gán.</summary>
    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
    }

    /// <summary>Đăng ký lắng nghe sự kiện kéo từ InputEventBus.</summary>
    private void Start()
    {
        if (InputEventBus.Instance != null)
        {
            InputEventBus.Instance.OnDragStarted += HandleDragStart;
            InputEventBus.Instance.OnDragging += HandleDragging;
            InputEventBus.Instance.OnDragEnded += HandleDragEnd;
        }
    }

    /// <summary>Hủy đăng ký event khi destroy.</summary>
    private void OnDestroy()
    {
        if (InputEventBus.Instance != null)
        {
            InputEventBus.Instance.OnDragStarted -= HandleDragStart;
            InputEventBus.Instance.OnDragging -= HandleDragging;
            InputEventBus.Instance.OnDragEnded -= HandleDragEnd;
        }
    }

    /// <summary>Bắt đầu kéo — lưu ô gốc, tính offset chuột và phát OnBlockSelected.</summary>
    private void HandleDragStart(Block block)
    {
        if (InputManager.Instance != null && InputManager.Instance.IsInputLocked)
            return;

        if (BoardStateManager.Instance != null && !BoardStateManager.Instance.CanAcceptInput())
            return;

        if (_currentState != DragState.Idle || block == null) return;

        _sourceSlot = block.CurrentSlot;
        if (_sourceSlot != null)
        {
            _currentState = DragState.Dragging;
            _draggedBlock = block;

            Vector3 mousePos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            _dragOffset = block.transform.position - mousePos;

            OnBlockSelected?.Invoke(block);
        }
    }

    /// <summary>Cập nhật vị trí visual block theo chuột + offset.</summary>
    private void HandleDragging(Block block, Vector3 mouseWorldPos)
    {
        if (InputManager.Instance != null && InputManager.Instance.IsInputLocked)
            return;

        if (BoardStateManager.Instance != null && !BoardStateManager.Instance.CanAcceptInput())
            return;

        if (_currentState != DragState.Dragging || block == null) return;

        Vector3 targetVisualPos = mouseWorldPos + _dragOffset;
        OnBlockVisualDragged?.Invoke(block, targetVisualPos);
        UpdateHoverTarget(mouseWorldPos);
    }

    private void UpdateHoverTarget(Vector3 mouseWorldPos)
    {
        Block next = null;
        var layout = boardView != null ? boardView.Layout : null;

        if (layout != null && boardManager != null)
        {
            Vector2Int grid = layout.GetGridPosition(mouseWorldPos);
            Slot slot = boardManager.GetSlot(grid.x, grid.y);
            if (slot != null && slot != _sourceSlot && slot.HasBlock)
                next = slot.CurrentBlock;
        }

        if (next == _hoverTargetBlock)
            return;

        _hoverTargetBlock = next;
        OnHoverTargetChanged?.Invoke(next);
    }

    private void ClearHoverTarget()
    {
        if (_hoverTargetBlock == null)
            return;

        _hoverTargetBlock = null;
        OnHoverTargetChanged?.Invoke(null);
    }

    /// <summary>
    /// Kết thúc kéo — chuyển world pos sang ô lưới, gọi ProcessMove hoặc trả block về ô gốc.
    /// </summary>
    private void HandleDragEnd(Block block)
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

        var layout = boardView.Layout;
        if (layout != null && targetCamera != null)
        {
            Vector3 dropWorldPos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            dropWorldPos.z = 0;

            Vector2Int gridPos = layout.GetGridPosition(dropWorldPos);
            Slot targetSlot = boardManager.GetSlot(gridPos.x, gridPos.y);

            if (targetSlot != null && targetSlot != _sourceSlot)
            {
                if (!moveSystem.ProcessMove(_sourceSlot, targetSlot))
                    ReturnBlockToSource(block);
                return;
            }
        }

        ReturnBlockToSource(block);
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
            OnBlockVisualReset?.Invoke(block, _sourceSlot);

        _sourceSlot = null;
        _draggedBlock = null;
    }

    /// <summary>Thả không hợp lệ — tween block về ô ban đầu.</summary>
    private void ReturnBlockToSource(Block block)
    {
        if (_sourceSlot != null)
            OnBlockVisualReset?.Invoke(block, _sourceSlot);
    }
}
