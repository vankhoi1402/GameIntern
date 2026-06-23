using System.Collections.Generic;
using UnityEngine;

namespace BlockSystem.Data
{
    [CreateAssetMenu(fileName = "BlockDatabase", menuName = "Grid Game/Block Database")]
    public class BlockDatabase : ScriptableObject
    {
        [SerializeField] private List<BlockData> normalBlocks;

        // Cache Dictionary để tìm kiếm đạt hiệu năng O(1)
        private Dictionary<BlockType, BlockData> _lookupTable;

        private void OnEnable()
        {
            BuildLookupTable();
        }

        public void BuildLookupTable()
        {
            _lookupTable = new Dictionary<BlockType, BlockData>();
            if (normalBlocks == null) return;

            foreach (var data in normalBlocks)
            {
                if (data != null && !_lookupTable.ContainsKey(data.BlockType))
                {
                    _lookupTable.Add(data.BlockType, data);
                }
            }
        }

        public BlockData GetRandomNormalBlock()
        {
            if (normalBlocks == null || normalBlocks.Count == 0) return null;
            return normalBlocks[Random.Range(0, normalBlocks.Count)];
        }

        public BlockData GetBlockData(BlockType type)
        {
            if (_lookupTable == null) BuildLookupTable();

            if (_lookupTable.TryGetValue(type, out var data))
            {
                return data;
            }

            Debug.LogWarning($"Không tìm thấy BlockType: {type} trong Database!");
            return null;
        }
    }
}