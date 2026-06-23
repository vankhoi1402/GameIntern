using UnityEngine;

/// <summary>
/// Nhà máy tạo block từ prefab + BlockData. Chỉ sinh entity, không đặt lên bàn.
/// </summary>
public class BlockFactory : MonoBehaviour
{
    [Header("Assets Configuration")]
    [SerializeField] private BlockDatabase database;
    [SerializeField] private GameObject blockPrefab;

    /// <summary>Tạo block từ BlockData cụ thể tại vị trí (0,0,0).</summary>
    public Block CreateBlock(BlockData data)
    {
        if (data == null || blockPrefab == null)
        {
            Debug.LogError("Factory chưa được cấu hình Database hoặc Prefab!");
            return null;
        }

        GameObject obj = Instantiate(blockPrefab, Vector3.zero, Quaternion.identity);
        Block block = obj.GetComponent<Block>();

        if (block != null)
            block.Init(data);

        return block;
    }

    /// <summary>Tạo block với BlockData ngẫu nhiên từ database.</summary>
    public Block CreateRandomBlock()
    {
        BlockData randomData = GetRandomData();
        return CreateBlock(randomData);
    }

    /// <summary>Tạo block theo typeKey string — ví dụ "1", "6".</summary>
    public Block CreateBlockByTypeKey(string typeKey, int stack = 1)
    {
        BlockData data = database != null ? database.GetBlockData(typeKey) : null;
        if (data == null)
        {
            Debug.LogError($"Factory không tìm thấy typeKey: {typeKey}");
            return null;
        }

        return CreateBlockWithStack(data, stack);
    }

    /// <summary>Tạo block theo blockId số.</summary>
    public Block CreateBlockById(int blockId, int stack = 1)
    {
        BlockData data = database != null ? database.GetBlockData(blockId) : null;
        if (data == null)
        {
            Debug.LogError($"Factory không tìm thấy blockId: {blockId}");
            return null;
        }

        return CreateBlockWithStack(data, stack);
    }

    private Block CreateBlockWithStack(BlockData data, int stack)
    {
        GameObject obj = Instantiate(blockPrefab, Vector3.zero, Quaternion.identity);
        Block block = obj.GetComponent<Block>();
        if (block != null)
            block.Init(data, stack);

        return block;
    }

    /// <summary>Lấy BlockData ngẫu nhiên từ database.</summary>
    public BlockData GetRandomData()
    {
        return database != null ? database.GetRandomNormalBlock() : null;
    }
}
