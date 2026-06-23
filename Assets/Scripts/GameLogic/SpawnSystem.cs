using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Sinh block — fill bàn lúc start và burst 3 block rơi khỏi màn hình khi T3 nổ.
/// </summary>
public class SpawnSystem : MonoBehaviour
{
    private const int c_BurstSpawnCount = 3;

    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private BoardView boardView;
    [SerializeField] private Block blockPrefab;
    [SerializeField] private StackBackgroundConfig stackBackgroundConfig;
    [SerializeField] private Camera targetCamera;

    [Header("Spawn Settings")]
    [SerializeField] private BlockData[] possibleBlockData;

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
    }

    /// <summary>Trì hoãn nhẹ để BoardManager kịp khởi tạo Layout.</summary>
    private void Start()
    {
        Invoke(nameof(GenerateInitialBoard), 0.1f);
    }

    /// <summary>Lấp toàn bộ lưới bằng block ngẫu nhiên từ possibleBlockData.</summary>
    public void GenerateInitialBoard()
    {
        if (boardManager == null || blockPrefab == null)
        {
            Debug.LogError("[SpawnSystem] Thiếu BoardManager hoặc Block Prefab!");
            return;
        }

        for (int r = 0; r < boardManager.Rows; r++)
        {
            for (int c = 0; c < boardManager.Columns; c++)
                SpawnRandomBlock(r, c);
        }
    }

    /// <summary>
    /// T3 nổ: sinh 3 block cùng loại tại ô vừa clear, tóe ra rồi rơi khỏi màn hình.
    /// </summary>
    public void SpawnBurstFallOff(int row, int col, BlockData clearedData, Action onComplete)
    {
        StartCoroutine(SpawnBurstFallOffRoutine(row, col, clearedData, onComplete));
    }

    private IEnumerator SpawnBurstFallOffRoutine(int row, int col, BlockData clearedData, Action onComplete)
    {
        if (clearedData == null || boardManager == null || blockPrefab == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        if (boardManager.Layout == null)
        {
            Debug.LogError("[SpawnSystem] BoardManager.Layout chưa sẵn sàng!");
            onComplete?.Invoke();
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

        int remaining = c_BurstSpawnCount;

        for (int i = 0; i < c_BurstSpawnCount; i++)
        {
            Block block = CreateBurstBlock(clearedData, centerPos);
            if (block == null)
            {
                remaining--;
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
                        if (block != null)
                            Destroy(block.gameObject);
                        remaining--;
                    });
            }
            else
            {
                Destroy(block.gameObject);
                remaining--;
            }
        }

        yield return new WaitUntil(() => remaining <= 0);
        onComplete?.Invoke();
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
        Block newBlock = Instantiate(blockPrefab);

        if (possibleBlockData != null && possibleBlockData.Length > 0)
        {
            BlockData randomData = possibleBlockData[UnityEngine.Random.Range(0, possibleBlockData.Length)];
            newBlock.Init(randomData);
        }
        else
        {
            Debug.LogError("[SpawnSystem] Chưa gán BlockData vào mảng Possible Block Data!");
        }

        boardManager.PlaceBlock(newBlock, row, col);
    }
}
