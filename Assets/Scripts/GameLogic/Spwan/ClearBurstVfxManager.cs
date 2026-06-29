using System;
using System.Collections;
using UnityEngine;

/// <summary>Block giả + burst VFX khi T3 clear — không ảnh hưởng logic board.</summary>
public class ClearBurstVfxManager : MonoBehaviour
{
    private const int c_BurstSpawnCount = 3;

    [Header("References")]
    [SerializeField] private BoardManager m_BoardManager;
    [SerializeField] private BoardView m_BoardView;
    [SerializeField] private BlockFactory m_BlockFactory;
    [SerializeField] private StackBackgroundConfig m_StackBackgroundConfig;
    [SerializeField] private Camera m_TargetCamera;

    [Header("Burst Fall Off (T3 Clear)")]
    [SerializeField] private float m_BurstExplodeDuration = 0.22f;
    [SerializeField] private float m_BurstFallDuration = 0.8f;
    [SerializeField] private float m_BurstFallWorldDistance = 6f;
    [SerializeField] private float m_BurstStaggerDelay = 0.05f;
    [SerializeField, Range(0.2f, 0.8f)] private float m_BurstExplodeHorizontal = 0.45f;
    [SerializeField, Range(0.2f, 0.8f)] private float m_BurstExplodeUp = 0.4f;

    private void Awake()
    {
        if (m_BoardManager == null)
            m_BoardManager = FindObjectOfType<BoardManager>();
        if (m_BoardView == null)
            m_BoardView = FindObjectOfType<BoardView>();
        if (m_BlockFactory == null)
            m_BlockFactory = GetComponent<BlockFactory>();
        if (m_StackBackgroundConfig == null && m_BoardView != null)
            m_StackBackgroundConfig = m_BoardView.StackBackgroundConfig;
        if (m_TargetCamera == null)
            m_TargetCamera = Camera.main;
    }

    public void EnsureConfigured(
        BoardManager boardManager,
        BoardView boardView,
        BlockFactory blockFactory,
        StackBackgroundConfig stackBackgroundConfig,
        Camera targetCamera)
    {
        if (m_BoardManager == null)
            m_BoardManager = boardManager;
        if (m_BoardView == null)
            m_BoardView = boardView;
        if (m_BlockFactory == null)
            m_BlockFactory = blockFactory;
        if (m_StackBackgroundConfig == null)
            m_StackBackgroundConfig = stackBackgroundConfig;
        if (m_TargetCamera == null)
            m_TargetCamera = targetCamera;
    }

    public void ApplyBurstSettings(
        float explodeDuration,
        float fallDuration,
        float fallWorldDistance,
        float staggerDelay,
        float explodeHorizontal,
        float explodeUp)
    {
        m_BurstExplodeDuration = explodeDuration;
        m_BurstFallDuration = fallDuration;
        m_BurstFallWorldDistance = fallWorldDistance;
        m_BurstStaggerDelay = staggerDelay;
        m_BurstExplodeHorizontal = explodeHorizontal;
        m_BurstExplodeUp = explodeUp;
    }

    /// <summary>T3 nổ: sinh 3 block tóe ra rồi rơi khỏi màn hình.</summary>
    public void PlayBurstFallOff(int row, int col, BlockData clearedData, Action onExplodeComplete)
    {
        StartCoroutine(PlayCenterBurstRoutine(row, col, clearedData, 1f, onExplodeComplete));
    }

    public IEnumerator PlayCenterBurstRoutine(int row, int col, BlockData clearedData, float explodeScale = 1f)
    {
        yield return PlayCenterBurstRoutine(row, col, clearedData, explodeScale, null);
    }

    public IEnumerator PlayRadialBurstRoutine(int row, int col, BlockData clearedData, float explodeScale = 1.6f)
    {
        yield return RadialBurstFallOffRoutine(row, col, clearedData, explodeScale, null);
    }

    private IEnumerator PlayCenterBurstRoutine(
        int row,
        int col,
        BlockData clearedData,
        float explodeScale,
        Action onExplodeComplete)
    {
        if (clearedData == null || m_BoardManager == null || m_BlockFactory == null)
        {
            onExplodeComplete?.Invoke();
            yield break;
        }

        if (!m_BoardManager.IsGridReady)
        {
            Debug.LogError("[ClearBurstVfxManager] BoardManager chưa sẵn sàng!");
            onExplodeComplete?.Invoke();
            yield break;
        }

        Vector3 centerPos = m_BoardManager.GridToWorld(row, col);
        float cellWidth = m_BoardManager.CellWidth;
        float cellHeight = m_BoardManager.CellHeight;
        Camera cam = m_TargetCamera != null ? m_TargetCamera : Camera.main;

        float scale = Mathf.Max(0.5f, explodeScale);
        Vector3[] explodeOffsets =
        {
            new Vector3(-cellWidth * m_BurstExplodeHorizontal * scale, cellHeight * m_BurstExplodeUp * scale, 0f),
            new Vector3(0f, cellHeight * m_BurstExplodeUp * 1.15f * scale, 0f),
            new Vector3(cellWidth * m_BurstExplodeHorizontal * scale, cellHeight * m_BurstExplodeUp * scale, 0f)
        };

        float explodeDuration = m_BurstExplodeDuration * scale;

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
                    explodeDuration,
                    m_BurstFallDuration,
                    m_BurstFallWorldDistance,
                    index * m_BurstStaggerDelay,
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

    private IEnumerator RadialBurstFallOffRoutine(
        int row,
        int col,
        BlockData clearedData,
        float explodeScale,
        Action onExplodeComplete)
    {
        if (clearedData == null || m_BoardManager == null || m_BlockFactory == null)
        {
            onExplodeComplete?.Invoke();
            yield break;
        }

        if (!m_BoardManager.IsGridReady)
        {
            onExplodeComplete?.Invoke();
            yield break;
        }

        Vector3 centerPos = m_BoardManager.GridToWorld(row, col);
        float cellWidth = m_BoardManager.CellWidth;
        float cellHeight = m_BoardManager.CellHeight;
        float cellRadius = Mathf.Min(cellWidth, cellHeight);
        Camera cam = m_TargetCamera != null ? m_TargetCamera : Camera.main;
        float scale = Mathf.Max(0.5f, explodeScale);
        float explodeDuration = m_BurstExplodeDuration * scale;

        Vector3[] radialDirs =
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 1f, 0f).normalized,
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, -1f, 0f).normalized,
            new Vector3(0f, -1f, 0f),
            new Vector3(-1f, -1f, 0f).normalized,
            new Vector3(-1f, 0f, 0f),
            new Vector3(-1f, 1f, 0f).normalized
        };

        int explodeRemaining = radialDirs.Length;
        bool pipelineFired = false;

        for (int i = 0; i < radialDirs.Length; i++)
        {
            Vector3 dir = radialDirs[i];
            Vector3 explodeOffset = new Vector3(
                dir.x * cellRadius * m_BurstExplodeHorizontal * scale,
                dir.y * cellRadius * m_BurstExplodeHorizontal * scale,
                0f);

            Block block = CreateBurstBlock(clearedData, centerPos);
            if (block == null)
            {
                explodeRemaining--;
                continue;
            }

            if (block.TryGetComponent<BlockView>(out var view))
            {
                view.PlayBurstExplodeThenFall(
                    explodeOffset,
                    cam,
                    explodeDuration,
                    m_BurstFallDuration,
                    m_BurstFallWorldDistance,
                    i * m_BurstStaggerDelay * 0.5f,
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
            }
        }

        yield return new WaitUntil(() => pipelineFired || explodeRemaining <= 0);
        if (!pipelineFired)
            onExplodeComplete?.Invoke();
    }

    private Block CreateBurstBlock(BlockData data, Vector3 worldPos)
    {
        Block block = m_BlockFactory.CreateAt(data, worldPos);
        if (block == null)
            return null;

        Transform parent = m_BoardView != null ? m_BoardView.transform : transform;
        block.transform.SetParent(parent);
        m_BlockFactory.SetupBurstView(block, data, m_BoardManager, m_StackBackgroundConfig);
        return block;
    }
}
