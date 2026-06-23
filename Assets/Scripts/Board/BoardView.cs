using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// Tầng View của bàn cờ — đồng bộ vị trí/hiệu ứng block theo event từ BoardManager và DragController.
/// </summary>
public class BoardView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private BoardArea boardArea;
    [SerializeField] private DragController dragController;
    [SerializeField] private StackBackgroundConfig stackBackgroundConfig;

    public StackBackgroundConfig StackBackgroundConfig => stackBackgroundConfig;

    /// <summary>Lưu sortingOrder gốc khi kéo block lên trên cùng.</summary>
    private readonly Dictionary<Block, int> _sortingCache = new Dictionary<Block, int>();

    public BoardLayout Layout => boardManager != null ? boardManager.Layout : null;

    /// <summary>Đăng ký lắng nghe sự kiện kéo thả từ DragController.</summary>
    private void OnEnable()
    {
        if (dragController != null)
        {
            dragController.OnBlockSelected += HandleBlockSelected;
            dragController.OnBlockVisualDragged += HandleBlockVisualDragging;
            dragController.OnBlockVisualReset += HandleBlockVisualReset;
        }
    }

    /// <summary>Hủy đăng ký sự kiện kéo thả.</summary>
    private void OnDisable()
    {
        if (dragController != null)
        {
            dragController.OnBlockSelected -= HandleBlockSelected;
            dragController.OnBlockVisualDragged -= HandleBlockVisualDragging;
            dragController.OnBlockVisualReset -= HandleBlockVisualReset;
        }
    }

    /// <summary>Đăng ký lắng nghe event đặt/di chuyển/xóa block từ BoardManager.</summary>
    private void Start()
    {
        boardManager.OnBlockPlaced += HandleBlockPlaced;
        boardManager.OnBlockMoved += HandleBlockMoved;
        boardManager.OnBlockRemoved += HandleBlockRemoved;
    }

    /// <summary>Hủy đăng ký event BoardManager khi object bị destroy.</summary>
    private void OnDestroy()
    {
        if (boardManager != null)
        {
            boardManager.OnBlockPlaced -= HandleBlockPlaced;
            boardManager.OnBlockMoved -= HandleBlockMoved;
            boardManager.OnBlockRemoved -= HandleBlockRemoved;
        }
    }

    /// <summary>Khi block mới spawn — gán parent, khởi tạo BlockView và đặt vào ô.</summary>
    private void HandleBlockPlaced(Block block, int row, int col)
    {
        if (block == null || Layout == null) return;

        block.transform.SetParent(this.transform);

        if (block.TryGetComponent<BlockView>(out var view))
        {
            Vector3 worldPos = Layout.GetWorldPosition(row, col);
            view.SetBackgroundConfig(stackBackgroundConfig);
            view.Initialize(block.Data, Layout.CellWidth, Layout.CellHeight);
            view.MoveToPosition(worldPos);
        }

        block.gameObject.name = $"Block_At_[{row},{col}] ({block.Type})";
    }

    /// <summary>
    /// Khi block di chuyển trên lưới — tween tới ô đích.
    /// Bỏ qua teleport khi đang gravity (BlockAnimationManager xử lý).
    /// </summary>
    private void HandleBlockMoved(Block block, int fromRow, int fromCol, int toRow, int toCol)
    {
        if (block == null || Layout == null) return;

        if (BoardStateManager.Instance != null &&
            BoardStateManager.Instance.CurrentState == BoardState.ApplyingGravity)
        {
            block.gameObject.name = $"Block_At_[{toRow},{toCol}] ({block.Type})";
            return;
        }

        if (block.TryGetComponent<BlockView>(out var view))
        {
            Vector3 targetPos = Layout.GetWorldPosition(toRow, toCol);
            view.PlayMoveToSlot(targetPos);
        }

        if (_sortingCache.TryGetValue(block, out int originalOrder))
        {
            if (block.TryGetComponent<SpriteRenderer>(out var sRenderer))
                sRenderer.sortingOrder = originalOrder;
            _sortingCache.Remove(block);
        }

        block.gameObject.name = $"Block_At_[{toRow},{toCol}] ({block.Type})";
    }

    /// <summary>Xóa GameObject block khi BoardManager.RemoveBlock được gọi.</summary>
    private void HandleBlockRemoved(Block block, int row, int col)
    {
        if (block != null)
        {
            if (_sortingCache.ContainsKey(block)) _sortingCache.Remove(block);
            Destroy(block.gameObject);
        }
    }

    #region INPUT VISUAL HANDLERS

    /// <summary>Khi bắt đầu kéo — nâng sortingOrder và phát hiệu ứng nhấc block.</summary>
    private void HandleBlockSelected(Block block)
    {
        if (block == null) return;

        if (block.TryGetComponent<SpriteRenderer>(out var sRenderer))
        {
            if (!_sortingCache.ContainsKey(block))
                _sortingCache[block] = sRenderer.sortingOrder;
            sRenderer.sortingOrder = 99;
        }

        if (block.TryGetComponent<BlockView>(out var view))
            view.PlayPickupFeedback();
    }

    /// <summary>Cập nhật vị trí world của block theo chuột khi đang kéo.</summary>
    private void HandleBlockVisualDragging(Block block, Vector3 targetPos)
    {
        if (block != null) block.transform.position = targetPos;
    }

    /// <summary>Khi thả không hợp lệ — reset scale/rotation và tween về ô gốc.</summary>
    private void HandleBlockVisualReset(Block block, Slot sourceSlot)
    {
        if (block == null || sourceSlot == null || Layout == null) return;

        Vector3 originWorldPos = Layout.GetWorldPosition(sourceSlot.Row, sourceSlot.Col);

        if (block.TryGetComponent<BlockView>(out var view))
        {
            view.PlayReleaseFeedback();
            block.transform.DOMove(originWorldPos, 0.2f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => RestoreSortingAfterReset(block));
        }
        else
        {
            block.transform.DOMove(originWorldPos, 0.2f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => RestoreSortingAfterReset(block));
        }
    }

    /// <summary>Khôi phục sortingOrder sau khi block trở về ô gốc.</summary>
    private void RestoreSortingAfterReset(Block block)
    {
        if (block == null) return;

        if (_sortingCache.TryGetValue(block, out int originalOrder))
        {
            if (block.TryGetComponent<SpriteRenderer>(out var sRenderer))
                sRenderer.sortingOrder = originalOrder;
            _sortingCache.Remove(block);
        }
    }

    #endregion

    #region DEBUG VISUALIZATION

    /// <summary>Vẽ lưới ô và tâm từng cell trong Scene view (debug).</summary>
    private void OnDrawGizmos()
    {
        if (boardArea == null || boardManager == null) return;

        Bounds bounds = boardArea.GetBounds(boardManager.Columns, boardManager.Rows);
        float cellWidth = boardArea.CellWidth;
        float cellHeight = boardArea.CellHeight;

        Gizmos.color = Color.green;

        for (int c = 0; c <= boardManager.Columns; c++)
        {
            float x = bounds.min.x + c * cellWidth;
            Gizmos.DrawLine(new Vector3(x, bounds.min.y, 0), new Vector3(x, bounds.max.y, 0));
        }

        for (int r = 0; r <= boardManager.Rows; r++)
        {
            float y = bounds.min.y + r * cellHeight;
            Gizmos.DrawLine(new Vector3(bounds.min.x, y, 0), new Vector3(bounds.max.x, y, 0));
        }

        Gizmos.color = Color.red;
        for (int r = 0; r < boardManager.Rows; r++)
        {
            for (int c = 0; c < boardManager.Columns; c++)
            {
                float centerX = bounds.min.x + (c * cellWidth) + (cellWidth * 0.5f);
                float centerY = bounds.min.y + (r * cellHeight) + (cellHeight * 0.5f);
                Gizmos.DrawSphere(new Vector3(centerX, centerY, 0), 0.1f);
            }
        }
    }

    #endregion
}
