using UnityEngine;

/// <summary>
/// Tạo block gameplay từ prefab + BlockData. Không đặt lên bàn, không chạy VFX burst.
/// </summary>
public class BlockFactory : MonoBehaviour
{
    [SerializeField] private BlockDatabase m_Database;
    [SerializeField] private Block m_BlockPrefab;

    public BlockDatabase Database => m_Database;

    public void EnsureConfigured(BlockDatabase database, Block blockPrefab)
    {
        if (m_Database == null)
            m_Database = database;
        if (m_BlockPrefab == null)
            m_BlockPrefab = blockPrefab;
    }

    public BlockData GetBlockData(string typeKey)
    {
        if (m_Database == null || string.IsNullOrEmpty(typeKey))
            return null;

        return m_Database.GetBlockData(typeKey);
    }

    public BlockData GetRandomData()
    {
        return m_Database != null ? m_Database.GetRandomNormalBlock() : null;
    }

    public Block Create(BlockData data, int stack = 1)
    {
        if (data == null || m_BlockPrefab == null)
            return null;

        Block block = Instantiate(m_BlockPrefab);
        block.Init(data, stack);
        return block;
    }

    public Block CreateAt(BlockData data, Vector3 worldPos)
    {
        if (data == null || m_BlockPrefab == null)
            return null;

        Block block = Instantiate(m_BlockPrefab, worldPos, Quaternion.identity);
        block.Init(data);
        return block;
    }

    public Block TryCreateByTypeKey(string typeKey, int stack, out BlockData data)
    {
        data = GetBlockData(typeKey);
        if (data == null)
            return null;

        return Create(data, stack);
    }

    public void AttachToBoardView(Block block, Transform boardViewParent)
    {
        if (block == null || boardViewParent == null)
            return;

        block.transform.SetParent(boardViewParent);
    }

    public void SetupGameplayView(
        Block block,
        BlockData data,
        BoardManager boardManager,
        StackBackgroundConfig stackBackgroundConfig)
    {
        if (block == null || data == null || boardManager == null || !boardManager.IsGridReady)
            return;

        if (!block.TryGetComponent<BlockView>(out var view))
            return;

        if (stackBackgroundConfig != null)
            view.SetBackgroundConfig(stackBackgroundConfig);

        view.Initialize(data, boardManager.CellWidth, boardManager.CellHeight);
        view.UpdateTier2StageVisual(block.StackCount, block.Tier2MergeStage);
    }

    public void SetupBurstView(
        Block block,
        BlockData data,
        BoardManager boardManager,
        StackBackgroundConfig stackBackgroundConfig)
    {
        if (block == null || data == null || boardManager == null || !boardManager.IsGridReady)
            return;

        if (!block.TryGetComponent<BlockView>(out var view))
            return;

        if (stackBackgroundConfig != null)
            view.SetBackgroundConfig(stackBackgroundConfig);

        view.InitializeBurstFallOff(data, boardManager.CellWidth, boardManager.CellHeight);
    }
}
