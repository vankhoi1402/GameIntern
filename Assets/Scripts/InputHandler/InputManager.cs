using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using System;

/// <summary>
/// Đọc input chuột — raycast block, phát sự kiện kéo qua InputEventBus.
/// Chỉ hoạt động khi BoardState = Idle.
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private LayerMask blockLayer;
    [SerializeField] private Camera targetCamera;

    private Block _currentDraggedBlock;
    private bool _isInputLocked = false;

    /// <summary>Khóa/mở input thủ công (ngoài BoardState).</summary>
    public void SetLockInput(bool isLocked)
    {
        _isInputLocked = isLocked;
        if (isLocked)
            _currentDraggedBlock = null;
    }

    public bool IsInputLocked => _isInputLocked;

    /// <summary>Đăng ký singleton và camera mặc định.</summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (targetCamera == null) targetCamera = Camera.main;
    }

    /// <summary>Mỗi frame — xử lý click/kéo/thả nếu được phép nhận input.</summary>
    private void Update()
    {
        if (BoardStateManager.Instance == null || !BoardStateManager.Instance.CanAcceptInput()) return;
        if (_isInputLocked) return;
        if (targetCamera == null || InputEventBus.Instance == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetMouseButtonDown(0)) return;
        }

        HandlePointerInput();
    }

    /// <summary>Phát DragStarted / Dragging / DragEnded theo trạng thái nút chuột.</summary>
    private void HandlePointerInput()
    {
        Vector3 mouseWorldPos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        if (Input.GetMouseButtonDown(0))
        {
            Block hitBlock = GetBlockAtPosition(mouseWorldPos);
            if (hitBlock != null)
            {
                _currentDraggedBlock = hitBlock;
                InputEventBus.Instance.RaiseDragStarted(_currentDraggedBlock);
            }
        }

        if (Input.GetMouseButton(0) && _currentDraggedBlock != null)
            InputEventBus.Instance.RaiseDragging(_currentDraggedBlock, mouseWorldPos);

        if (Input.GetMouseButtonUp(0) && _currentDraggedBlock != null)
        {
            InputEventBus.Instance.RaiseDragEnded(_currentDraggedBlock);
            _currentDraggedBlock = null;
        }
    }

    /// <summary>Raycast tất cả block tại vị trí — ưu tiên SortingGroup order cao hơn.</summary>
    private Block GetBlockAtPosition(Vector2 pos)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(pos, Vector2.zero, 0f, blockLayer);
        if (hits.Length == 0) return null;

        Array.Sort(hits, (a, b) =>
        {
            int orderA = GetBlockSortingOrder(a.collider);
            int orderB = GetBlockSortingOrder(b.collider);
            return orderB.CompareTo(orderA);
        });

        foreach (var hit in hits)
        {
            if (hit.collider != null && hit.collider.TryGetComponent<Block>(out var block))
                return block;
        }
        return null;
    }

    private static int GetBlockSortingOrder(Collider2D collider)
    {
        if (collider == null) return 0;

        if (collider.TryGetComponent<SortingGroup>(out var group))
            return group.sortingOrder;

        SpriteRenderer sprite = collider.GetComponentInChildren<SpriteRenderer>();
        return sprite != null ? sprite.sortingOrder : 0;
    }
}
