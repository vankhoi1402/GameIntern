using UnityEngine;

/// <summary>
/// Config nền chung cho block — 2 mức: mặc định (stack 1) và merge (stack ≥ 2).
/// </summary>
[CreateAssetMenu(fileName = "StackBackgroundConfig", menuName = "Grid Game/Stack Background Config")]
public class StackBackgroundConfig : ScriptableObject
{
    [SerializeField] private Sprite bgStack1;
    [SerializeField] private Sprite bgStack2;

    /// <summary>Stack visual khi T3 nổ — 3 block rơi khỏi màn hình dùng nền merge.</summary>
    public const int BurstFallVisualStack = 2;

    /// <summary>Trả sprite nền: stack 1 → mặc định, stack ≥ 2 → merge.</summary>
    public Sprite GetBackground(int stackCount)
    {
        if (stackCount >= 2)
            return bgStack2 != null ? bgStack2 : bgStack1;

        return bgStack1;
    }
}
