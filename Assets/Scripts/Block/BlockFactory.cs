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

    /// <summary>Tạo block theo BlockType yêu cầu.</summary>
    public Block CreateBlockByType(BlockType type)
    {
        BlockData data = GetDataByType(type);
        return CreateBlock(data);
    }

    /// <summary>Lấy BlockData ngẫu nhiên từ database.</summary>
    public BlockData GetRandomData()
    {
        return database != null ? database.GetRandomNormalBlock() : null;
    }

    /// <summary>Lấy BlockData theo BlockType.</summary>
    public BlockData GetDataByType(BlockType type)
    {
        return database != null ? database.GetBlockData(type) : null;
    }
}
