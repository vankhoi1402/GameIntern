using UnityEngine;

/// <summary>
/// Tầng View của bàn cờ — đồng bộ vị trí/hiệu ứng block theo event từ BoardManager.
/// </summary>
public class BoardView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private StackBackgroundConfig stackBackgroundConfig;
    [SerializeField] private BoardIntroAnimator m_IntroAnimator;

    public StackBackgroundConfig StackBackgroundConfig => stackBackgroundConfig;
    public BoardIntroAnimator IntroAnimator => m_IntroAnimator;

    private void Awake()
    {
        if (m_IntroAnimator == null)
            m_IntroAnimator = GetComponent<BoardIntroAnimator>();

        if (m_IntroAnimator == null)
            m_IntroAnimator = FindObjectOfType<BoardIntroAnimator>();

        if (m_IntroAnimator == null)
            m_IntroAnimator = gameObject.AddComponent<BoardIntroAnimator>();

        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();

        if (boardManager != null)
        {
            boardManager.OnBlockPlaced += HandleBlockPlaced;
            boardManager.OnBlockMoved += HandleBlockMoved;
            boardManager.OnBlockRemoved += HandleBlockRemoved;
        }
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
        if (block == null || boardManager == null || !boardManager.IsGridReady) return;

        block.transform.SetParent(this.transform);

        if (block.TryGetComponent<BlockView>(out var view))
        {
            Vector3 worldPos = boardManager.GridToWorld(row, col);
            view.SetBackgroundConfig(stackBackgroundConfig);
            view.Initialize(block.Data, boardManager.CellWidth, boardManager.CellHeight);
            view.UpdateTier2StageVisual(block.StackCount, block.Tier2MergeStage);
            view.ApplyGridSorting(row);

            if (m_IntroAnimator != null && m_IntroAnimator.IsIntroActive)
            {
                m_IntroAnimator.TrackBlock(block, row, col);
                view.PrepareForIntroRise();
            }
            else
            {
                view.MoveToPosition(worldPos);
            }
        }

        block.gameObject.name = $"Block_At_[{row},{col}] (type:{block.TypeKey})";
    }

    /// <summary>
    /// Khi block di chuyển trên lưới — tween tới ô đích.
    /// Bỏ qua teleport khi đang gravity (BlockAnimationManager xử lý).
    /// </summary>
    private void HandleBlockMoved(Block block, int fromRow, int fromCol, int toRow, int toCol)
    {
        if (block == null || boardManager == null || !boardManager.IsGridReady) return;

        if (BoardStateManager.Instance != null &&
            (BoardStateManager.Instance.CurrentState == BoardState.ApplyingGravity ||
             BoardStateManager.Instance.CurrentState == BoardState.ApplyingRefill))
        {
            if (block.TryGetComponent<BlockView>(out var gravityView))
                gravityView.ApplyGridSorting(toRow);
            block.gameObject.name = $"Block_At_[{toRow},{toCol}] (type:{block.TypeKey})";
            return;
        }

        if (block.TryGetComponent<BlockView>(out var view))
        {
            Vector3 targetPos = boardManager.GridToWorld(toRow, toCol);
            view.PlayMoveToSlot(targetPos);
            view.ApplyGridSorting(toRow);
            view.SetDragSorting(false);
        }

        block.gameObject.name = $"Block_At_[{toRow},{toCol}] (type:{block.TypeKey})";
    }

    /// <summary>Xóa GameObject block khi BoardManager.RemoveBlock được gọi.</summary>
    private void HandleBlockRemoved(Block block, int row, int col)
    {
        if (block == null) return;
        Destroy(block.gameObject);
    }
}
