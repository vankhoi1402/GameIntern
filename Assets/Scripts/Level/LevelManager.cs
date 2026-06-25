using System;
using UnityEngine;

/// <summary>Load level từ catalog, giữ refill state runtime.</summary>
public class LevelManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelCatalog catalog;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private SkillManager skillManager;
    [SerializeField] private BlockDatabase blockDatabase;
    [SerializeField] private BoardIntroAnimator m_IntroAnimator;
    [SerializeField] private int startLevelId = 1;

    private LevelData _currentLevel;
    private readonly LevelRefillState _refillState = new LevelRefillState();

    public LevelData CurrentLevel => _currentLevel;
    public bool HasLevel => _currentLevel != null;
    public int CurrentLevelId => _currentLevel != null ? _currentLevel.LevelId : startLevelId;
    public LevelRefillState RefillState => _refillState;

    public event Action<LevelData> OnLevelLoaded;

    private void Awake()
    {
        if (spawnSystem == null)
            spawnSystem = FindObjectOfType<SpawnSystem>();
        if (skillManager == null)
            skillManager = FindObjectOfType<SkillManager>();
        if (blockDatabase == null && spawnSystem != null)
            blockDatabase = spawnSystem.BlockDatabase;

        if (m_IntroAnimator == null)
            m_IntroAnimator = FindObjectOfType<BoardIntroAnimator>();
    }

    /// <summary>Gọi từ SpawnSystem sau khi board layout sẵn sàng.</summary>
    public void LoadStartLevel()
    {
        if (catalog == null || catalog.Levels == null || catalog.Levels.Length == 0)
        {
            Debug.LogWarning("[LevelManager] Chưa có LevelCatalog — SpawnSystem dùng random/test.");
            return;
        }

        LevelCatalogEntry entry = catalog.FindById(startLevelId);
        if (entry.LevelAsset == null)
        {
            Debug.LogError($"[LevelManager] Level {startLevelId} chưa có LevelAsset. Chạy Import CSV trước.");
            return;
        }

        LoadLevel(entry.LevelAsset);
    }

    public void LoadLevel(LevelData level)
    {
        if (level == null || spawnSystem == null) return;

        _currentLevel = level;
        _refillState.Reset(level);

        if (blockDatabase != null)
            LevelCsvValidator.LogReport(level.LevelId, level.Rows, blockDatabase);
        else
            Debug.LogWarning("[LevelManager] Chưa có BlockDatabase — bỏ qua validate level.");

        ResolveIntroAnimator();
        SetInputLocked(true);

        void CompleteLoad()
        {
            SetInputLocked(false);

            if (skillManager != null)
                skillManager.ApplyLoadout(level.SkillLoadout);

            Debug.Log($"[LevelManager] Loaded level {level.LevelId}");
            OnLevelLoaded?.Invoke(level);
        }

        if (m_IntroAnimator != null && m_IntroAnimator.IsEnabled)
        {
            m_IntroAnimator.BeginIntro(level.VisibleRows, CompleteLoad);
            spawnSystem.ClearBoard();
            spawnSystem.SpawnFromLevel(level);
            m_IntroAnimator.PlayWave();
            return;
        }

        spawnSystem.ClearBoard();
        spawnSystem.SpawnFromLevel(level);
        CompleteLoad();
    }

    private void ResolveIntroAnimator()
    {
        if (m_IntroAnimator != null)
            return;

        BoardView boardView = FindObjectOfType<BoardView>();
        if (boardView != null)
            m_IntroAnimator = boardView.IntroAnimator;

        if (m_IntroAnimator == null)
            m_IntroAnimator = FindObjectOfType<BoardIntroAnimator>();
    }

    private static void SetInputLocked(bool locked)
    {
        if (InputManager.Instance != null)
            InputManager.Instance.SetLockInput(locked);
    }

    public bool HasNextLevel()
    {
        return catalog != null && catalog.TryGetNextAfter(CurrentLevelId, out _);
    }

    public bool TryLoadNextLevel()
    {
        if (catalog == null || !catalog.TryGetNextAfter(CurrentLevelId, out LevelCatalogEntry next))
            return false;

        LoadLevel(next.LevelAsset);
        return true;
    }

    public void ReloadCurrentLevel()
    {
        if (_currentLevel != null)
            LoadLevel(_currentLevel);
    }

    public LevelBinBlock? TryDequeueBin(int col) => _refillState.TryDequeue(col);
}
