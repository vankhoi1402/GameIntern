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
    private DragState _currentState = DragState.Idle;
    private Vector3 _dragOffset;

    public event Action<Block, Vector3> OnBlockVisualDragged;
    public event Action<Block, Slot> OnBlockVisualReset;
    public event Action<Block> OnBlockSelected;

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
        if (_currentState != DragState.Idle || block == null) return;

        _sourceSlot = block.CurrentSlot;
        if (_sourceSlot != null)
        {
            _currentState = DragState.Dragging;

            Vector3 mousePos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            _dragOffset = block.transform.position - mousePos;

            OnBlockSelected?.Invoke(block);
        }
    }

    /// <summary>Cập nhật vị trí visual block theo chuột + offset.</summary>
    private void HandleDragging(Block block, Vector3 mouseWorldPos)
    {
        if (_currentState != DragState.Dragging || block == null) return;

        Vector3 targetVisualPos = mouseWorldPos + _dragOffset;
        OnBlockVisualDragged?.Invoke(block, targetVisualPos);
    }

    /// <summary>
    /// Kết thúc kéo — chuyển world pos sang ô lưới, gọi ProcessMove hoặc trả block về ô gốc.
    /// </summary>
    private void HandleDragEnd(Block block)
    {
        if (_currentState != DragState.Dragging || block == null) return;
        _currentState = DragState.Idle;

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

    /// <summary>Thả không hợp lệ — tween block về ô ban đầu.</summary>
    private void ReturnBlockToSource(Block block)
    {
        if (_sourceSlot != null)
            OnBlockVisualReset?.Invoke(block, _sourceSlot);
    }
}
