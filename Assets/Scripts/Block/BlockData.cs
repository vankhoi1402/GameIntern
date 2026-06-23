using UnityEngine;

namespace BlockSystem.Data
{
    [CreateAssetMenu(fileName = "NewBlockData", menuName = "Grid Game/Block Data")]
    public class BlockData : ScriptableObject
    {
        [SerializeField] private BlockType blockType;
        [SerializeField] private Sprite visualSprite;
        [SerializeField] private Color debugColor = Color.white;
        [SerializeField] private int scoreValue = 10;

        public BlockType BlockType => blockType;
        public Sprite VisualSprite => visualSprite;
        public Color DebugColor => debugColor;
        public int ScoreValue => scoreValue;
    }
}