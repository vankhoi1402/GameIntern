using UnityEngine;
using Game.Grid;
using Game.InputSystem;
using BlockSystem.Runtime;
using BlockSystem.View;
using DG.Tweening;
using System.Collections.Generic;

namespace Game.View
{
    public class BoardView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private BoardArea boardArea;
        [SerializeField] private DragController dragController;

        private BoardLayout _layout;
        private readonly Dictionary<Block, int> _sortingCache = new Dictionary<Block, int>();

        public BoardLayout Layout => _layout;

        private void OnEnable()
        {
            if (dragController != null)
            {
                dragController.OnBlockSelected += HandleBlockSelected;
                dragController.OnBlockVisualDragged += HandleBlockVisualDragging;
                dragController.OnBlockVisualReset += HandleBlockVisualReset;
            }
        }

        private void OnDisable()
        {
            if (dragController != null)
            {
                dragController.OnBlockSelected -= HandleBlockSelected;
                dragController.OnBlockVisualDragged -= HandleBlockVisualDragging;
                dragController.OnBlockVisualReset -= HandleBlockVisualReset;
            }
        }

        private void Start()
        {
            _layout = new BoardLayout(boardArea, boardManager.Rows, boardManager.Columns);

            boardManager.OnBlockPlaced += HandleBlockPlaced;
            boardManager.OnBlockMoved += HandleBlockMoved;
            boardManager.OnBlockRemoved += HandleBlockRemoved;
        }

        private void OnDestroy()
        {
            if (boardManager != null)
            {
                boardManager.OnBlockPlaced -= HandleBlockPlaced;
                boardManager.OnBlockMoved -= HandleBlockMoved;
                boardManager.OnBlockRemoved -= HandleBlockRemoved;
            }
        }

        private void HandleBlockPlaced(Block block, int row, int col)
        {
            if (block == null) return;

            block.transform.SetParent(this.transform);

            if (block.TryGetComponent<BlockView>(out var view))
            {
                Vector3 worldPos = _layout.GetWorldPosition(row, col);
                view.Initialize(block.Data, _layout.CellWidth, _layout.CellHeight);
                view.MoveToPosition(worldPos);
            }

            block.gameObject.name = $"Block_At_[{row},{col}] ({block.Type})";
        }

        private void HandleBlockMoved(Block block, int fromRow, int fromCol, int toRow, int toCol)
        {
            if (block == null) return;

            if (block.TryGetComponent<BlockView>(out var view))
            {
                Vector3 targetPos = _layout.GetWorldPosition(toRow, toCol);
                view.MoveToPosition(targetPos);
            }

            // Đồng bộ trả lại Sorting Order gốc từ cache nếu block này vừa di chuyển xong từ trạng thái kéo thả thành công
            if (_sortingCache.TryGetValue(block, out int originalOrder))
            {
                if (block.TryGetComponent<SpriteRenderer>(out var sRenderer))
                {
                    sRenderer.sortingOrder = originalOrder;
                }
                _sortingCache.Remove(block);
            }

            block.gameObject.name = $"Block_At_[{toRow},{toCol}] ({block.Type})";
        }

        private void HandleBlockRemoved(Block block, int row, int col)
        {
            if (block != null)
            {
                if (_sortingCache.ContainsKey(block)) _sortingCache.Remove(block);
                Destroy(block.gameObject);
            }
        }

        #region INPUT VISUAL HANDLERS

        // Cache an toàn: Lưu lại sortingOrder gốc của RIÊNG viên gạch đó rồi mới đẩy lên lớp 99
        private void HandleBlockSelected(Block block)
        {
            if (block != null && block.TryGetComponent<SpriteRenderer>(out var sRenderer))
            {
                if (!_sortingCache.ContainsKey(block))
                {
                    _sortingCache[block] = sRenderer.sortingOrder;
                }
                sRenderer.sortingOrder = 99;
            }
        }

        private void HandleBlockVisualDragging(Block block, Vector3 targetPos)
        {
            if (block != null) block.transform.position = targetPos;
        }

        private void HandleBlockVisualReset(Block block, Slot sourceSlot)
        {
            if (block == null || sourceSlot == null) return;

            Vector3 originWorldPos = _layout.GetWorldPosition(sourceSlot.Row, sourceSlot.Col);

            block.transform.DOMove(originWorldPos, 0.2f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    // Khi tween chạy xong về chỗ cũ an toàn mới bốc từ bộ nhớ cache ra trả lại, triệt tiêu bug hiển thị
                    if (_sortingCache.TryGetValue(block, out int originalOrder))
                    {
                        if (block.TryGetComponent<SpriteRenderer>(out var sRenderer))
                        {
                            sRenderer.sortingOrder = originalOrder;
                        }
                        _sortingCache.Remove(block);
                    }
                });
        }

        #endregion

        #region DEBUG VISUALIZATION (Vẽ Grid lên Scene)

        private void OnDrawGizmos()
        {
            if (boardArea == null || boardManager == null) return;

            Bounds bounds = boardArea.Bounds;
            float cellWidth = bounds.size.x / boardManager.Columns;
            float cellHeight = bounds.size.y / boardManager.Rows;

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
}