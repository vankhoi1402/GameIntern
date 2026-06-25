/// <summary>Đánh giá thắng/thua — logic thuần, không UI.</summary>
public class LevelOutcomeEvaluator
{
    private readonly BoardManager m_BoardManager;
    private readonly LevelRefillState m_RefillState;
    private readonly LevelTimer m_Timer;

    public LevelOutcomeEvaluator(
        BoardManager boardManager,
        LevelRefillState refillState,
        LevelTimer timer = null)
    {
        m_BoardManager = boardManager;
        m_RefillState = refillState;
        m_Timer = timer;
    }

    public LevelOutcome Evaluate()
    {
        if (IsLevelCleared())
            return LevelOutcome.Won;

        if (IsTimeExpired())
            return LevelOutcome.Lost;

        return LevelOutcome.Playing;
    }

    public bool IsLevelCleared()
    {
        if (m_BoardManager == null || m_RefillState == null)
            return false;

        return m_BoardManager.IsBoardEmpty() && !m_RefillState.HasRemainingBlocks();
    }

    public bool IsTimeExpired()
        => m_Timer != null && m_Timer.IsExpired;
}
