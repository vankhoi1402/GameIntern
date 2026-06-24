using UnityEngine;

/// <summary>Load level từ catalog, giữ refill state runtime.</summary>
public class LevelManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelCatalog catalog;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private SkillManager skillManager;
    [SerializeField] private int startLevelId = 1;

    private LevelData _currentLevel;
    private readonly LevelRefillState _refillState = new LevelRefillState();

    public LevelData CurrentLevel => _currentLevel;
    public bool HasLevel => _currentLevel != null;

    private void Awake()
    {
        if (spawnSystem == null)
            spawnSystem = FindObjectOfType<SpawnSystem>();
        if (skillManager == null)
            skillManager = FindObjectOfType<SkillManager>();
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
        spawnSystem.ClearBoard();
        spawnSystem.SpawnFromLevel(level);

        if (skillManager != null)
            skillManager.ApplyLoadout(level.SkillLoadout);

        Debug.Log($"[LevelManager] Loaded level {level.LevelId}");
    }

    public LevelBinBlock? TryDequeueBin(int col) => _refillState.TryDequeue(col);
}
