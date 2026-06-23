using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Database chứa danh sách BlockData — tra cứu theo BlockType hoặc random spawn.
/// </summary>
[CreateAssetMenu(fileName = "BlockDatabase", menuName = "Grid Game/Block Database")]
public class BlockDatabase : ScriptableObject
{
    [SerializeField] private List<BlockData> normalBlocks;

    private Dictionary<BlockType, BlockData> _lookupTable;

    /// <summary>Unity gọi khi load asset — build bảng tra cứu.</summary>
    private void OnEnable()
    {
        BuildLookupTable();
    }

    /// <summary>Xây Dictionary BlockType → BlockData để tra cứu O(1).</summary>
    public void BuildLookupTable()
    {
        _lookupTable = new Dictionary<BlockType, BlockData>();
        if (normalBlocks == null) return;

        foreach (var data in normalBlocks)
        {
            if (data != null && !_lookupTable.ContainsKey(data.BlockType))
                _lookupTable.Add(data.BlockType, data);
        }
    }

    /// <summary>Lấy ngẫu nhiên một BlockData từ danh sách.</summary>
    public BlockData GetRandomNormalBlock()
    {
        if (normalBlocks == null || normalBlocks.Count == 0) return null;
        return normalBlocks[Random.Range(0, normalBlocks.Count)];
    }

    /// <summary>Lấy BlockData theo loại cụ thể.</summary>
    public BlockData GetBlockData(BlockType type)
    {
        if (_lookupTable == null) BuildLookupTable();

        if (_lookupTable.TryGetValue(type, out var data))
            return data;

        Debug.LogWarning($"Không tìm thấy BlockType: {type} trong Database!");
        return null;
    }
}
