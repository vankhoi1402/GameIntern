using UnityEngine;

/// <summary>
/// ScriptableObject chứa dữ liệu tĩnh của một loại block (icon, id, type key, điểm).
/// typeKey dạng string số: "1", "6", "101" — khớp CSV / level design.
/// </summary>
[CreateAssetMenu(fileName = "NewBlockData", menuName = "Grid Game/Block Data")]
public class BlockData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private int blockId;
    [SerializeField] private string typeKey;

    [Header("Visual")]
    [SerializeField] private Sprite visualSprite;
    [SerializeField] private Color debugColor = Color.white;
    [SerializeField] private int scoreValue = 10;

    public int BlockId => blockId;

    /// <summary>Key string cho CSV — ví dụ "1", "6". Mặc định = BlockId.ToString().</summary>
    public string TypeKey => string.IsNullOrWhiteSpace(typeKey) ? blockId.ToString() : typeKey.Trim();

    public Sprite VisualSprite => visualSprite;
    public Color DebugColor => debugColor;
    public int ScoreValue => scoreValue;
}
