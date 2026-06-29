using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Điều phối spawn — level fill, refill entity, delegate VFX burst.</summary>
public class SpawnSystem : MonoBehaviour
{
    [System.Serializable]
    public struct TestSpawnCell
    {
        public int row;
        public int col;
        public string blockType;
        public int stack;
    }

    [Header("Providers")]
    [SerializeField] private BlockFactory m_BlockFactory;
    [SerializeField] private ClearBurstVfxManager m_BurstVfx;

    [Header("References")]
    [FormerlySerializedAs("boardManager")]
    [SerializeField] private BoardManager m_BoardManager;
    [FormerlySerializedAs("boardView")]
    [SerializeField] private BoardView m_BoardView;
    [FormerlySerializedAs("stackBackgroundConfig")]
    [SerializeField] private StackBackgroundConfig m_StackBackgroundConfig;
    [FormerlySerializedAs("levelManager")]
    [SerializeField] private LevelManager m_LevelManager;
    [FormerlySerializedAs("targetCamera")]
    [SerializeField] private Camera m_TargetCamera;

    [Header("Factory Fallback")]
    [FormerlySerializedAs("blockPrefab")]
    [SerializeField] private Block m_BlockPrefab;
    [FormerlySerializedAs("blockDatabase")]
    [SerializeField] private BlockDatabase m_BlockDatabase;

    [Header("Level Fallback")]
    [FormerlySerializedAs("useTestLayout")]
    [SerializeField] private bool m_UseTestLayout;
    [FormerlySerializedAs("testCells")]
    [SerializeField] private TestSpawnCell[] m_TestCells;

    [Header("Burst Fall Off (T3 Clear)")]
    [FormerlySerializedAs("burstExplodeDuration")]
    [SerializeField] private float m_BurstExplodeDuration = 0.22f;
    [FormerlySerializedAs("burstFallDuration")]
    [SerializeField] private float m_BurstFallDuration = 0.8f;
    [FormerlySerializedAs("burstFallWorldDistance")]
    [SerializeField] private float m_BurstFallWorldDistance = 6f;
    [FormerlySerializedAs("burstStaggerDelay")]
    [SerializeField] private float m_BurstStaggerDelay = 0.05f;
    [FormerlySerializedAs("burstExplodeHorizontal")]
    [SerializeField, Range(0.2f, 0.8f)] private float m_BurstExplodeHorizontal = 0.45f;
    [FormerlySerializedAs("burstExplodeUp")]
    [SerializeField, Range(0.2f, 0.8f)] private float m_BurstExplodeUp = 0.4f;

    public BlockDatabase BlockDatabase =>
        m_BlockFactory != null ? m_BlockFactory.Database : m_BlockDatabase;

    private void Awake()
    {
        if (m_BoardManager == null)
            m_BoardManager = FindObjectOfType<BoardManager>();
        if (m_BoardView == null)
            m_BoardView = FindObjectOfType<BoardView>();
        if (m_StackBackgroundConfig == null && m_BoardView != null)
            m_StackBackgroundConfig = m_BoardView.StackBackgroundConfig;
        if (m_LevelManager == null)
            m_LevelManager = FindObjectOfType<LevelManager>();

        EnsureProviders();
    }

    private void EnsureProviders()
    {
        if (m_BlockFactory == null)
            m_BlockFactory = GetComponent<BlockFactory>();
        if (m_BlockFactory == null)
            m_BlockFactory = gameObject.AddComponent<BlockFactory>();

        m_BlockFactory.EnsureConfigured(m_BlockDatabase, m_BlockPrefab);

        if (m_BurstVfx == null)
            m_BurstVfx = GetComponent<ClearBurstVfxManager>();
        if (m_BurstVfx == null)
            m_BurstVfx = gameObject.AddComponent<ClearBurstVfxManager>();

        m_BurstVfx.EnsureConfigured(
            m_BoardManager,
            m_BoardView,
            m_BlockFactory,
            m_StackBackgroundConfig,
            m_TargetCamera != null ? m_TargetCamera : Camera.main);

        m_BurstVfx.ApplyBurstSettings(
            m_BurstExplodeDuration,
            m_BurstFallDuration,
            m_BurstFallWorldDistance,
            m_BurstStaggerDelay,
            m_BurstExplodeHorizontal,
            m_BurstExplodeUp);
    }

    private void Start()
    {
        Invoke(nameof(GenerateInitialBoard), 0.1f);
    }

    public void GenerateInitialBoard()
    {
        EnsureProviders();

        if (m_BoardManager == null || m_BlockFactory == null)
        {
            Debug.LogError("[SpawnSystem] Thiếu BoardManager hoặc Block Prefab!");
            return;
        }

        if (m_BlockFactory.Database == null && m_BlockPrefab == null)
        {
            Debug.LogError("[SpawnSystem] Thiếu BoardManager hoặc Block Prefab!");
            return;
        }

        if (m_LevelManager != null && !m_LevelManager.HasLevel)
            m_LevelManager.LoadStartLevel();

        if (m_LevelManager != null && m_LevelManager.HasLevel)
            return;

        if (m_UseTestLayout)
            GenerateFromTestLayout();
        else
            GenerateRandomFullBoard();
    }

    public void ClearBoard()
    {
        if (m_BoardManager != null)
            m_BoardManager.ClearAllBlocks();
    }

    public void SpawnFromLevel(LevelData level)
    {
        if (level == null || level.Rows == null)
            return;

        foreach (LevelGridRow gridRow in level.Rows)
        {
            if (gridRow.Row >= level.VisibleRows)
                continue;

            if (gridRow.ColBlockTypes == null)
                continue;

            for (int col = 0; col < gridRow.ColBlockTypes.Length; col++)
            {
                if (!LevelCellParser.TryParseCell(gridRow.ColBlockTypes[col], out string typeKey, out int stack))
                    continue;

                SpawnBlockAt(gridRow.Row, col, typeKey, stack);
            }
        }
    }

    public Block SpawnBlockAtWithoutPlace(string typeKey, int stack = 1)
    {
        EnsureProviders();

        if (m_BlockFactory == null)
            return null;

        Block block = m_BlockFactory.TryCreateByTypeKey(typeKey, stack, out BlockData data);
        if (block == null)
        {
            Debug.LogError($"[SpawnSystem] block_type '{typeKey}' không có trong BlockDatabase!");
            return null;
        }

        m_BlockFactory.AttachToBoardView(block, m_BoardView != null ? m_BoardView.transform : null);
        m_BlockFactory.SetupGameplayView(block, data, m_BoardManager, m_StackBackgroundConfig);
        return block;
    }

    public void GenerateFromTestLayout()
    {
        if (m_BlockFactory == null || m_BlockFactory.Database == null)
        {
            Debug.LogError("[SpawnSystem] useTestLayout bật nhưng chưa gán BlockDatabase!");
            return;
        }

        if (m_TestCells == null || m_TestCells.Length == 0)
        {
            Debug.LogWarning("[SpawnSystem] testCells rỗng — bàn sẽ trống.");
            return;
        }

        foreach (TestSpawnCell cell in m_TestCells)
        {
            if (string.IsNullOrWhiteSpace(cell.blockType))
                continue;

            SpawnBlockAt(cell.row, cell.col, cell.blockType.Trim(), cell.stack);
        }
    }

    public void GenerateRandomFullBoard()
    {
        for (int r = 0; r < m_BoardManager.Rows; r++)
        {
            for (int c = 0; c < m_BoardManager.Columns; c++)
                SpawnRandomBlock(r, c);
        }
    }

    public void SpawnBlockAt(int row, int col, string typeKey, int stack = 1)
    {
        EnsureProviders();

        if (m_BlockFactory == null)
        {
            Debug.LogError("[SpawnSystem] Chưa gán BlockFactory!");
            return;
        }

        Block block = m_BlockFactory.TryCreateByTypeKey(typeKey, stack, out _);
        if (block == null)
        {
            Debug.LogError($"[SpawnSystem] block_type '{typeKey}' không có trong BlockDatabase!");
            return;
        }

        if (!m_BoardManager.IsValidPosition(row, col))
        {
            Debug.LogError($"[SpawnSystem] Vị trí ({row},{col}) ngoài biên bàn!");
            Destroy(block.gameObject);
            return;
        }

        m_BoardManager.PlaceBlock(block, row, col);
    }

    public void SpawnBlockAt(int row, int col, int blockId, int stack = 1)
        => SpawnBlockAt(row, col, blockId.ToString(), stack);

    public void SpawnBurstFallOff(int row, int col, BlockData clearedData, Action onExplodeComplete)
    {
        EnsureProviders();
        m_BurstVfx.PlayBurstFallOff(row, col, clearedData, onExplodeComplete);
    }

    public IEnumerator PlayCenterBurstRoutine(int row, int col, BlockData clearedData, float explodeScale = 1f)
    {
        EnsureProviders();
        return m_BurstVfx.PlayCenterBurstRoutine(row, col, clearedData, explodeScale);
    }

    public IEnumerator PlayRadialBurstRoutine(int row, int col, BlockData clearedData, float explodeScale = 1.6f)
    {
        EnsureProviders();
        return m_BurstVfx.PlayRadialBurstRoutine(row, col, clearedData, explodeScale);
    }

    private void SpawnRandomBlock(int row, int col)
    {
        if (m_BlockFactory == null)
        {
            Debug.LogError("[SpawnSystem] Chưa gán BlockFactory!");
            return;
        }

        BlockData randomData = m_BlockFactory.GetRandomData();
        if (randomData == null)
        {
            Debug.LogError("[SpawnSystem] BlockDatabase không có block nào để spawn ngẫu nhiên!");
            return;
        }

        Block block = m_BlockFactory.Create(randomData);
        m_BoardManager.PlaceBlock(block, row, col);
    }
}
