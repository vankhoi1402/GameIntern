using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Database chứa danh sách BlockData — tra cứu theo typeKey string ("1") hoặc blockId int.
/// </summary>
[CreateAssetMenu(fileName = "BlockDatabase", menuName = "Grid Game/Block Database")]
public class BlockDatabase : ScriptableObject
{
    [SerializeField] private List<BlockData> normalBlocks;

    private Dictionary<int, BlockData> _byId;
    private Dictionary<string, BlockData> _byKey;

    /// <summary>Unity gọi khi load asset — build bảng tra cứu.</summary>
    private void OnEnable()
    {
        BuildLookupTable();
    }

    /// <summary>Xây Dictionary id/key → BlockData để tra cứu O(1).</summary>
    public void BuildLookupTable()
    {
        _byId = new Dictionary<int, BlockData>();
        _byKey = new Dictionary<string, BlockData>();
        if (normalBlocks == null) return;

        foreach (var data in normalBlocks)
        {
            if (data == null) continue;

            int id = data.BlockId;
            if (id <= 0)
            {
                Debug.LogWarning($"[BlockDatabase] BlockData '{data.name}' có BlockId không hợp lệ.");
                continue;
            }

            if (!_byId.ContainsKey(id))
                _byId.Add(id, data);
            else
                Debug.LogWarning($"[BlockDatabase] Trùng blockId {id} — asset '{data.name}' bị bỏ qua.");

            string key = data.TypeKey;
            if (string.IsNullOrEmpty(key)) continue;

            if (!_byKey.ContainsKey(key))
                _byKey.Add(key, data);
            else
                Debug.LogWarning($"[BlockDatabase] Trùng typeKey '{key}' — asset '{data.name}' bị bỏ qua.");
        }
    }

    /// <summary>Lấy ngẫu nhiên một BlockData từ danh sách.</summary>
    public BlockData GetRandomNormalBlock()
    {
        if (normalBlocks == null || normalBlocks.Count == 0) return null;
        return normalBlocks[Random.Range(0, normalBlocks.Count)];
    }

    /// <summary>Lấy BlockData theo typeKey string — ví dụ "1", "6".</summary>
    public BlockData GetBlockData(string typeKey)
    {
        if (string.IsNullOrWhiteSpace(typeKey)) return null;

        if (_byId == null) BuildLookupTable();

        string key = typeKey.Trim();
        if (_byKey.TryGetValue(key, out var data))
            return data;

        if (int.TryParse(key, out int id))
            return GetBlockData(id);

        Debug.LogWarning($"[BlockDatabase] Không tìm thấy typeKey: '{typeKey}'");
        return null;
    }

    /// <summary>Lấy BlockData theo blockId số.</summary>
    public BlockData GetBlockData(int blockId)
    {
        if (blockId <= 0) return null;

        if (_byId == null) BuildLookupTable();

        if (_byId.TryGetValue(blockId, out var data))
            return data;

        Debug.LogWarning($"[BlockDatabase] Không tìm thấy blockId: {blockId}");
        return null;
    }
}
