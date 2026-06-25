/// <summary>Đánh giá thắng/thua — logic thuần, không UI.</summary>
public class LevelOutcomeEvaluator
{
    private readonly BoardManager m_BoardManager;
    private readonly LevelRefillState m_RefillState;

    public LevelOutcomeEvaluator(BoardManager boardManager, LevelRefillState refillState)
    {
        m_BoardManager = boardManager;
        m_RefillState = refillState;
    }

    public LevelOutcome Evaluate()
    {
        if (IsLevelCleared())
            return LevelOutcome.Won;

        return LevelOutcome.Playing;
    }

    public bool IsLevelCleared()
    {
        if (m_BoardManager == null || m_RefillState == null)
            return false;

        return m_BoardManager.IsBoardEmpty() && !m_RefillState.HasRemainingBlocks();
    }
}
