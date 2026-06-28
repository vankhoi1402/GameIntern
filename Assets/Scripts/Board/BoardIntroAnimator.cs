using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Column wave rise khi load level — stagger theo cột và hàng.</summary>
public class BoardIntroAnimator : MonoBehaviour
{
    #region Constants
    private const float c_DefaultSpawnDepthMultiplier = 1.25f;
    private const float c_DefaultIntroStartScale = 0.92f;
    #endregion

    #region Serialized Fields
    [SerializeField] private BoardManager m_BoardManager;
    [SerializeField] private bool m_EnableIntro = true;
    [SerializeField] private float m_RiseDuration = 0.42f;
    [SerializeField] private float m_ColStagger = 0.035f;
    [SerializeField] private float m_RowStagger = 0f;
    [SerializeField] private float m_SpawnDepthMultiplier = c_DefaultSpawnDepthMultiplier;
    [SerializeField, Range(0.8f, 1f)] private float m_IntroStartScale = c_DefaultIntroStartScale;
    [SerializeField] private float m_FadeInDuration = 0.18f;
    #endregion

    #region Private Fields
    private readonly List<IntroEntry> m_Entries = new List<IntroEntry>();
    private bool m_IsIntroActive;
    private int m_VisibleRows = LevelCsvParser.DefaultVisibleRows;
    private Action m_OnComplete;
    #endregion

    #region Public Properties
    public bool IsIntroActive => m_IsIntroActive;
    public bool IsEnabled => m_EnableIntro;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_BoardManager == null)
            m_BoardManager = FindObjectOfType<BoardManager>();
    }
    #endregion

    #region Public Methods
    public void BeginIntro(int visibleRows, Action onComplete)
    {
        if (!m_EnableIntro)
        {
            onComplete?.Invoke();
            return;
        }

        m_IsIntroActive = true;
        m_VisibleRows = Mathf.Max(1, visibleRows);
        m_OnComplete = onComplete;
        m_Entries.Clear();
    }

    public void TrackBlock(Block block, int row, int col)
    {
        if (!m_IsIntroActive || block == null)
            return;

        m_Entries.Add(new IntroEntry
        {
            Block = block,
            Row = row,
            Col = col
        });
    }

    public void PlayWave()
    {
        if (!m_EnableIntro || !m_IsIntroActive)
            return;

        if (m_BoardManager == null || !m_BoardManager.IsGridReady)
        {
            FinishIntro();
            return;
        }

        if (m_Entries.Count == 0)
        {
            FinishIntro();
            return;
        }

        float cellHeight = m_BoardManager.CellHeight;
        var batch = new TweenCompletionBatch(m_Entries.Count, FinishIntro);

        foreach (IntroEntry entry in m_Entries)
        {
            if (entry.Block == null || !entry.Block.TryGetComponent<BlockView>(out BlockView view))
            {
                batch.NotifyOneDone();
                continue;
            }

            Vector3 target = m_BoardManager.GridToWorld(entry.Row, entry.Col);
            PlayRise(view, target, cellHeight, entry.Row, entry.Col, batch.NotifyOneDone);
        }
    }

    public Vector3 GetSpawnBelowPosition(Vector3 targetWorldPos, float cellHeight)
        => targetWorldPos + Vector3.down * cellHeight * m_SpawnDepthMultiplier;

    public void PlayRise(BlockView view, Vector3 targetWorldPos, float cellHeight, int row, int col, Action onComplete)
    {
        if (view == null)
        {
            onComplete?.Invoke();
            return;
        }

        Vector3 spawnPos = GetSpawnBelowPosition(targetWorldPos, cellHeight);
        float delay = GetStaggerDelay(col, row);
        view.PlayIntroRise(
            spawnPos,
            targetWorldPos,
            m_RiseDuration,
            delay,
            row,
            m_IntroStartScale,
            m_FadeInDuration,
            onComplete);
    }
    #endregion

    #region Private Methods
    private float GetStaggerDelay(int col, int row)
        => col * m_ColStagger + (m_VisibleRows - 1 - row) * m_RowStagger;

    private void FinishIntro()
    {
        m_IsIntroActive = false;
        m_Entries.Clear();

        Action callback = m_OnComplete;
        m_OnComplete = null;
        callback?.Invoke();
    }
    #endregion

    #region Nested Types
    private struct IntroEntry
    {
        public Block Block;
        public int Row;
        public int Col;
    }

    private sealed class TweenCompletionBatch
    {
        private int m_Remaining;
        private bool m_Invoked;
        private readonly Action m_OnComplete;

        public TweenCompletionBatch(int count, Action onComplete)
        {
            m_Remaining = count;
            m_OnComplete = onComplete;
            if (m_Remaining <= 0)
                InvokeOnce();
        }

        public void NotifyOneDone()
        {
            m_Remaining--;
            if (m_Remaining <= 0)
                InvokeOnce();
        }

        private void InvokeOnce()
        {
            if (m_Invoked)
                return;

            m_Invoked = true;
            m_OnComplete?.Invoke();
        }
    }
    #endregion
}
