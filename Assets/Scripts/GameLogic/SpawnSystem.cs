using System;
using System.Collections;
using UnityEngine;


/// <summary>
/// Sinh block — fill bàn lúc start và burst 3 block rơi khỏi màn hình khi T3 nổ.
/// </summary>
public class SpawnSystem : MonoBehaviour
{
    private const int c_BurstSpawnCount = 3;

    [System.Serializable]
    public struct TestSpawnCell
    {
        public int row;
        public int col;
        public string blockType;
        public int stack;
    }

    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private BoardView boardView;
    [SerializeField] private Block blockPrefab;
    [SerializeField] private StackBackgroundConfig stackBackgroundConfig;
    [SerializeField] private Camera targetCamera;

    [Header("Level")]
    [SerializeField] private LevelManager levelManager;

    [Header("Level Fallback")]
    [SerializeField] private bool useTestLayout = false;
    [SerializeField] private BlockDatabase blockDatabase;
    [SerializeField] private TestSpawnCell[] testCells;

    [Header("Burst Fall Off (T3 Clear)")]
    [SerializeField] private float burstExplodeDuration = 0.22f;
    [SerializeField] private float burstFallDuration = 0.8f;
    [SerializeField] private float burstFallWorldDistance = 6f;
    [SerializeField] private float burstStaggerDelay = 0.05f;
    [SerializeField, Range(0.2f, 0.8f)] private float burstExplodeHorizontal = 0.45f;
    [SerializeField, Range(0.2f, 0.8f)] private float burstExplodeUp = 0.4f;

    private void Awake()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
        if (boardView == null)
            boardView = FindObjectOfType<BoardView>();
        if (stackBackgroundConfig == null && boardView != null)
            stackBackgroundConfig = boardView.StackBackgroundConfig;
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (levelManager == null)
            levelManager = FindObjectOfType<LevelManager>();
    }

    /// <summary>Trì hoãn nhẹ để BoardManager kịp khởi tạo Layout.</summary>
    private void Start()
    {
        Invoke(nameof(GenerateInitialBoard), 0.1f);
    }

    /// <summary>Spawn bàn lúc start — level CSV, test layout hoặc random.</summary>
    public void GenerateInitialBoard()
    {
        if (boardManager == null || blockPrefab == null)
        {
            Debug.LogError("[SpawnSystem] Thiếu BoardManager hoặc Block Prefab!");
            return;
        }

        if (levelManager != null && levelManager.HasLevel == false)
            levelManager.LoadStartLevel();

        if (levelManager != null && levelManager.HasLevel)
            return;

        if (useTestLayout)
            GenerateFromTestLayout();
        else
            GenerateRandomFullBoard();
    }

    /// <summary>Xóa toàn bộ block trên bàn.</summary>
    public void ClearBoard()
    {
        if (boardManager != null)
            boardManager.ClearAllBlocks();
    }

    /// <summary>Spawn VisibleRows hàng đáy của mỗi bin cột.</summary>
    public void SpawnFromLevel(LevelData level)
    {
        if (level == null || level.Rows == null) return;

        foreach (var gridRow in level.Rows)
        {
            if (gridRow.Row >= level.VisibleRows)
                continue;

            if (gridRow.ColBlockTypes == null) continue;

            for (int col = 0; col < gridRow.ColBlockTypes.Length; col++)
            {
                if (!LevelCellParser.TryParseCell(gridRow.ColBlockTypes[col], out string typeKey, out int stack))
                    continue;

                SpawnBlockAt(gridRow.Row, col, typeKey, stack);
            }
        }
    }

    /// <summary>Tạo block entity chưa đặt lên lưới (refill row 0).</summary>
    public Block SpawnBlockAtWithoutPlace(string typeKey, int stack = 1)
    {
        if (blockDatabase == null || blockPrefab == null)
            return null;

        BlockData data = blockDatabase.GetBlockData(typeKey);
        if (data == null)
        {
            Debug.LogError($"[SpawnSystem] block_type '{typeKey}' không có trong BlockDatabase!");
            return null;
        }

        Block block = Instantiate(blockPrefab);
        block.Init(data, stack);

        if (boardView != null)
            block.transform.SetParent(boardView.transform);

        if (block.TryGetComponent<BlockView>(out var view) && boardManager != null && boardManager.Layout != null)
        {
            if (stackBackgroundConfig != null)
                view.SetBackgroundConfig(stackBackgroundConfig);
            view.Initialize(data, boardManager.Layout.CellWidth, boardManager.Layout.CellHeight);
            view.UpdateStackVisual(stack);
        }

        return block;
    }

    /// <summary>Spawn chỉ các ô khai báo trong testCells (block_type string + stack).</summary>
    public void GenerateFromTestLayout()
    {
        if (blockDatabase == null)
        {
            Debug.LogError("[SpawnSystem] useTestLayout bật nhưng chưa gán BlockDatabase!");
            return;
        }

        if (testCells == null || testCells.Length == 0)
        {
            Debug.LogWarning("[SpawnSystem] testCells rỗng — bàn sẽ trống.");
            return;
        }

        foreach (var cell in testCells)
        {
            if (string.IsNullOrWhiteSpace(cell.blockType) || cell.blockType.Trim() == "0")
                continue;

            SpawnBlockAt(cell.row, cell.col, cell.blockType, cell.stack);
        }
    }

    /// <summary>Lấp toàn bộ lưới bằng block ngẫu nhiên từ BlockDatabase.</summary>
    public void GenerateRandomFullBoard()
    {
        for (int r = 0; r < boardManager.Rows; r++)
        {
            for (int c = 0; c < boardManager.Columns; c++)
                SpawnRandomBlock(r, c);
        }
    }

    /// <summary>Tạo block theo typeKey string ("1", "6") và đặt vào ô (row, col).</summary>
    public void SpawnBlockAt(int row, int col, string typeKey, int stack = 1)
    {
        if (blockDatabase == null)
        {
            Debug.LogError("[SpawnSystem] Chưa gán BlockDatabase!");
            return;
        }

        BlockData data = blockDatabase.GetBlockData(typeKey);
        if (data == null)
        {
            Debug.LogError($"[SpawnSystem] block_type '{typeKey}' không có trong BlockDatabase!");
            return;
        }

        if (!boardManager.IsValidPosition(row, col))
        {
            Debug.LogError($"[SpawnSystem] Vị trí ({row},{col}) ngoài biên bàn!");
            return;
        }

        Block block = Instantiate(blockPrefab);
        block.Init(data, stack);
        boardManager.PlaceBlock(block, row, col);
    }

    /// <summary>Tạo block theo blockId số — tiện khi đã parse từ CSV.</summary>
    public void SpawnBlockAt(int row, int col, int blockId, int stack = 1)
        => SpawnBlockAt(row, col, blockId.ToString(), stack);

    /// <summary>
    /// T3 nổ: sinh 3 block tóe ra rồi rơi khỏi màn hình.
    /// onExplodeComplete gọi khi cả 3 đã nổ xong (trước khi rơi) — dùng để refill/gravity.
    /// </summary>
    public void SpawnBurstFallOff(int row, int col, BlockData clearedData, Action onExplodeComplete)
    {
        StartCoroutine(SpawnBurstFallOffRoutine(row, col, clearedData, onExplodeComplete));
    }

    private IEnumerator SpawnBurstFallOffRoutine(int row, int col, BlockData clearedData, Action onExplodeComplete)
    {
        if (clearedData == null || boardManager == null || blockPrefab == null)
        {
            onExplodeComplete?.Invoke();
            yield break;
        }

        if (boardManager.Layout == null)
        {
            Debug.LogError("[SpawnSystem] BoardManager.Layout chưa sẵn sàng!");
            onExplodeComplete?.Invoke();
            yield break;
        }

        Vector3 centerPos = boardManager.Layout.GetWorldPosition(row, col);
        float cellWidth = boardManager.Layout.CellWidth;
        float cellHeight = boardManager.Layout.CellHeight;
        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        Vector3[] explodeOffsets =
        {
            new Vector3(-cellWidth * burstExplodeHorizontal, cellHeight * burstExplodeUp, 0f),
            new Vector3(0f, cellHeight * burstExplodeUp * 1.15f, 0f),
            new Vector3(cellWidth * burstExplodeHorizontal, cellHeight * burstExplodeUp, 0f)
        };

        int explodeRemaining = c_BurstSpawnCount;
        bool pipelineFired = false;

        for (int i = 0; i < c_BurstSpawnCount; i++)
        {
            Block block = CreateBurstBlock(clearedData, centerPos);
            if (block == null)
            {
                explodeRemaining--;
                continue;
            }

            int index = i;
            if (block.TryGetComponent<BlockView>(out var view))
            {
                view.PlayBurstExplodeThenFall(
                    explodeOffsets[index],
                    cam,
                    burstExplodeDuration,
                    burstFallDuration,
                    burstFallWorldDistance,
                    index * burstStaggerDelay,
                    () =>
                    {
                        explodeRemaining--;
                        if (explodeRemaining <= 0 && !pipelineFired)
                        {
                            pipelineFired = true;
                            onExplodeComplete?.Invoke();
                        }
                    },
                    () =>
                    {
                        if (block != null)
                            Destroy(block.gameObject);
                    });
            }
            else
            {
                Destroy(block.gameObject);
                explodeRemaining--;
                if (explodeRemaining <= 0 && !pipelineFired)
                {
                    pipelineFired = true;
                    onExplodeComplete?.Invoke();
                }
            }
        }

        yield return new WaitUntil(() => pipelineFired || explodeRemaining <= 0);
        if (!pipelineFired)
            onExplodeComplete?.Invoke();
    }

    private Block CreateBurstBlock(BlockData data, Vector3 worldPos)
    {
        Block block = Instantiate(blockPrefab, worldPos, Quaternion.identity);
        block.Init(data);

        Transform parent = boardView != null ? boardView.transform : transform;
        block.transform.SetParent(parent);

        if (block.TryGetComponent<BlockView>(out var view) && boardManager.Layout != null)
        {
            if (stackBackgroundConfig != null)
                view.SetBackgroundConfig(stackBackgroundConfig);
            view.InitializeBurstFallOff(data, boardManager.Layout.CellWidth, boardManager.Layout.CellHeight);
        }

        return block;
    }

    /// <summary>Tạo một block ngẫu nhiên và đặt vào ô (row, col).</summary>
    private void SpawnRandomBlock(int row, int col)
    {
        if (blockDatabase == null)
        {
            Debug.LogError("[SpawnSystem] Chưa gán BlockDatabase!");
            return;
        }

        BlockData randomData = blockDatabase.GetRandomNormalBlock();
        if (randomData == null)
        {
            Debug.LogError("[SpawnSystem] BlockDatabase không có block nào để spawn ngẫu nhiên!");
            return;
        }

        Block newBlock = Instantiate(blockPrefab);
        newBlock.Init(randomData);
        boardManager.PlaceBlock(newBlock, row, col);
    }
}
