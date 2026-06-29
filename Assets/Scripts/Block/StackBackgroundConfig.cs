using UnityEngine;

[CreateAssetMenu(fileName = "StackBackgroundConfig", menuName = "Grid Game/Stack Background Config")]
public class StackBackgroundConfig : ScriptableObject
{
    public const int BurstFallVisualStack = -1;

    [SerializeField] private Sprite m_Stack1Background;
    [SerializeField] private Sprite m_Stack2Background;

    public Sprite GetBackground(int stackCount)
    {
        if (stackCount == 2 && m_Stack2Background != null)
            return m_Stack2Background;
        return m_Stack1Background;
    }
}
