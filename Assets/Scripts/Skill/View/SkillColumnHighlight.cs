using UnityEngine;

/// <summary>Highlight nhẹ các block trong cột đang chọn (pre-select).</summary>
public class SkillColumnHighlight : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private Color highlightTint = new Color(1f, 1f, 0.65f, 1f);
    [SerializeField] private Color normalColor = Color.white;

    private int m_HighlightedCol = -1;

    private void Awake()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
    }

    private void OnEnable()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.OnColumnSelected += HandleColumnSelected;
            SkillManager.Instance.OnColumnSelectionCleared += HandleCleared;
        }
    }

    private void OnDisable()
    {
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.OnColumnSelected -= HandleColumnSelected;
            SkillManager.Instance.OnColumnSelectionCleared -= HandleCleared;
        }

        ClearHighlight();
    }

    private void HandleColumnSelected(int col)
    {
        ApplyHighlight(col);
    }

    private void HandleCleared()
    {
        ClearHighlight();
    }

    private void ApplyHighlight(int col)
    {
        ClearHighlight();
        if (boardManager == null || col < 0 || col >= boardManager.Columns)
            return;

        m_HighlightedCol = col;
        for (int row = 0; row < boardManager.Rows; row++)
        {
            Slot slot = boardManager.GetSlot(row, col);
            if (slot == null || !slot.HasBlock)
                continue;

            Block block = slot.CurrentBlock;
            if (block == null || !block.TryGetComponent<BlockView>(out var view))
                continue;

            if (view._mainRenderer != null)
                view._mainRenderer.color = highlightTint;
        }
    }

    private void ClearHighlight()
    {
        if (m_HighlightedCol < 0 || boardManager == null)
            return;

        int col = m_HighlightedCol;
        m_HighlightedCol = -1;

        for (int row = 0; row < boardManager.Rows; row++)
        {
            Slot slot = boardManager.GetSlot(row, col);
            if (slot == null || !slot.HasBlock)
                continue;

            Block block = slot.CurrentBlock;
            if (block == null || !block.TryGetComponent<BlockView>(out var view))
                continue;

            if (view._mainRenderer != null && block.Data != null)
                view._mainRenderer.color = block.Data.DebugColor;
            else if (view._mainRenderer != null)
                view._mainRenderer.color = normalColor;
        }
    }
}
