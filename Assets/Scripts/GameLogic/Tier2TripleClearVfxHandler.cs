using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// VFX clear 2+2+2: ring + burst + neighbor blast — clear logic neighbor rồi settlement (gravity → refill).
/// </summary>
public class Tier2TripleClearVfxHandler : MonoBehaviour
{
    private struct NeighborCell
    {
        public Block Block;
        public BlockView View;
        public int Row;
        public int Col;
        public Vector3 HomePos;
        public Vector3 BlastPos;
        public float StaggerDelay;
    }

    private static readonly int[] s_DeltaRow = { -1, -1, -1, 0, 0, 1, 1, 1 };
    private static readonly int[] s_DeltaCol = { -1, 0, 1, -1, 1, -1, 0, 1 };

    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private StackBackgroundConfig stackBackgroundConfig;
    [SerializeField] private Camera shakeCamera;

    [Header("Center Explosion")]
    [SerializeField] private float centerBurstScale = 1.65f;
    [SerializeField] private float ringExpandDuration = 0.22f;
    [SerializeField] private float ringEndScaleMultiplier = 2.4f;
    [SerializeField] private float cameraShakeDuration = 0.14f;
    [SerializeField] private float cameraShakeStrength = 0.14f;

    [Header("Neighbor Blast (8 directions)")]
    [SerializeField] private float blastRadiusMultiplier = 0.52f;
    [SerializeField] private float diagonalBlastBonus = 1.12f;
    [SerializeField] private float blastDuration = 0.14f;
    [SerializeField] private float dissolveDuration = 0.16f;
    [SerializeField] private float radialStagger = 0.022f;
    [SerializeField] private float holdAfterDissolve = 0.06f;

    private void Awake()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
        if (spawnSystem == null)
            spawnSystem = FindObjectOfType<SpawnSystem>();
        if (shakeCamera == null)
            shakeCamera = Camera.main;
    }

    public void Play(
        int centerRow,
        int centerCol,
        BlockData clearedData,
        Action<IReadOnlyList<Tier2NeighborRestore>> onComplete)
    {
        StartCoroutine(PlayRoutine(centerRow, centerCol, clearedData, onComplete));
    }

    private IEnumerator PlayRoutine(
        int centerRow,
        int centerCol,
        BlockData clearedData,
        Action<IReadOnlyList<Tier2NeighborRestore>> onComplete)
    {
        if (boardManager == null || !boardManager.IsGridReady)
        {
            onComplete?.Invoke(Array.Empty<Tier2NeighborRestore>());
            yield break;
        }

        Vector3 centerPos = boardManager.GridToWorld(centerRow, centerCol);
        float cellWidth = boardManager.CellWidth;
        float cellHeight = boardManager.CellHeight;
        float cellRadius = Mathf.Min(cellWidth, cellHeight);

        List<NeighborCell> neighbors = CollectNeighbors(centerRow, centerCol, centerPos, cellRadius);

        int parallelDone = 0;
        const int parallelTotal = 2;

        void OnParallelPhaseDone() => parallelDone++;

        StartCoroutine(CenterExplosionPhase(centerRow, centerCol, clearedData, centerPos, cellRadius, OnParallelPhaseDone));
        StartCoroutine(NeighborBlastPhase(neighbors, OnParallelPhaseDone));

        yield return new WaitUntil(() => parallelDone >= parallelTotal);

        if (holdAfterDissolve > 0f)
            yield return new WaitForSeconds(holdAfterDissolve);

        List<Tier2NeighborRestore> restores = CommitNeighborClears(neighbors);
        onComplete?.Invoke(restores);
    }

    private List<Tier2NeighborRestore> CommitNeighborClears(List<NeighborCell> neighbors)
    {
        var restores = new List<Tier2NeighborRestore>(neighbors.Count);

        foreach (NeighborCell cell in neighbors)
        {
            Block block = cell.Block;
            if (block == null)
                continue;

            restores.Add(new Tier2NeighborRestore(block, cell.Col));
            block.IsPendingDestroy = true;
            boardManager.ClearSlot(cell.Row, cell.Col);
            Destroy(block.gameObject);
        }

        return restores;
    }

    private IEnumerator CenterExplosionPhase(
        int centerRow,
        int centerCol,
        BlockData clearedData,
        Vector3 centerPos,
        float cellRadius,
        Action onComplete)
    {
        PlayExplosionRing(centerPos, cellRadius);
        PlayCameraShake();

        if (spawnSystem != null && clearedData != null)
            yield return spawnSystem.PlayRadialBurstRoutine(centerRow, centerCol, clearedData, centerBurstScale);

        onComplete?.Invoke();
    }

    private IEnumerator NeighborBlastPhase(List<NeighborCell> neighbors, Action onComplete)
    {
        if (neighbors.Count == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        int pending = neighbors.Count;
        bool done = false;

        void OnOneDone()
        {
            pending--;
            if (pending <= 0)
                done = true;
        }

        foreach (NeighborCell cell in neighbors)
        {
            cell.View.PlayTier2NeighborBlast(
                cell.HomePos,
                cell.BlastPos,
                blastDuration,
                dissolveDuration,
                cell.StaggerDelay,
                OnOneDone);
        }

        float maxWait = blastDuration + dissolveDuration + radialStagger * 8f + 0.4f;
        float elapsed = 0f;
        while (!done && elapsed < maxWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        onComplete?.Invoke();
    }

    private List<NeighborCell> CollectNeighbors(
        int centerRow,
        int centerCol,
        Vector3 centerPos,
        float cellRadius)
    {
        var neighbors = new List<NeighborCell>(8);

        for (int i = 0; i < s_DeltaRow.Length; i++)
        {
            int dr = s_DeltaRow[i];
            int dc = s_DeltaCol[i];
            int row = centerRow + dr;
            int col = centerCol + dc;
            if (!boardManager.IsValidPosition(row, col))
                continue;

            Slot slot = boardManager.GetSlot(row, col);
            if (slot == null || !slot.HasBlock)
                continue;

            Block block = slot.CurrentBlock;
            if (block == null || block.IsPendingDestroy)
                continue;
            if (!block.TryGetComponent<BlockView>(out BlockView view))
                continue;

            Vector3 homePos = boardManager.GridToWorld(row, col);
            Vector3 pushDir = homePos - centerPos;
            if (pushDir.sqrMagnitude < 0.0001f)
                pushDir = Vector3.up;
            else
                pushDir.Normalize();

            bool isDiagonal = dr != 0 && dc != 0;
            float blastDist = cellRadius * blastRadiusMultiplier * (isDiagonal ? diagonalBlastBonus : 1f);
            Vector3 blastPos = homePos + pushDir * blastDist;

            neighbors.Add(new NeighborCell
            {
                Block = block,
                View = view,
                Row = row,
                Col = col,
                HomePos = homePos,
                BlastPos = blastPos,
                StaggerDelay = i * radialStagger
            });
        }

        return neighbors;
    }

    private void PlayExplosionRing(Vector3 centerPos, float cellRadius)
    {
        Sprite ringSprite = stackBackgroundConfig != null
            ? stackBackgroundConfig.GetBackground(1)
            : null;

        if (ringSprite == null)
            return;

        var ringGo = new GameObject("Tier2ExplosionRing");
        ringGo.transform.position = centerPos;
        ringGo.transform.localScale = Vector3.one * (cellRadius * 0.35f);

        var renderer = ringGo.AddComponent<SpriteRenderer>();
        renderer.sprite = ringSprite;
        renderer.color = new Color(1f, 1f, 1f, 0.85f);
        renderer.sortingOrder = BlockView.DragSortingOrder + 50;

        float endScale = cellRadius * ringEndScaleMultiplier;
        Sequence seq = DOTween.Sequence().SetLink(ringGo);
        seq.Append(ringGo.transform.DOScale(Vector3.one * endScale, ringExpandDuration).SetEase(Ease.OutExpo));
        seq.Join(renderer.DOFade(0f, ringExpandDuration * 1.05f));
        seq.OnComplete(() =>
        {
            if (ringGo != null)
                Destroy(ringGo);
        });
    }

    private void PlayCameraShake()
    {
        if (shakeCamera == null)
            return;

        shakeCamera.transform.DOKill(complete: true);
        shakeCamera.transform.DOShakePosition(
            cameraShakeDuration,
            cameraShakeStrength,
            vibrato: 18,
            randomness: 80f,
            fadeOut: true);
    }
}
