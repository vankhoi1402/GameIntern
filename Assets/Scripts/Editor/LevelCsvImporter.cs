using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Import level.csv (bin cột) → LevelData.asset trong cùng folder level và gán catalog.</summary>
public class LevelCsvImporter : EditorWindow
{
    private const string c_LevelsFolder = LevelCsvEditorPaths.LevelsFolder;

    private LevelCatalog _catalog;
    private BlockDatabase _database;
    private string _levelsFolder = c_LevelsFolder;
    private int _visibleRows = LevelCsvParser.DefaultVisibleRows;

    [MenuItem("Grid Game/Import Levels From CSV")]
    public static void ShowWindow()
    {
        GetWindow<LevelCsvImporter>("Level CSV Importer");
    }

    [MenuItem("Grid Game/Validate Level CSVs (log only)")]
    public static void ValidateAllMenu()
    {
        LevelCsvEditorPaths.ValidateAllInCatalog(
            LevelCsvEditorPaths.LoadDefaultCatalog(),
            LevelCsvEditorPaths.LoadDefaultDatabase());
    }

    private void OnEnable()
    {
        if (_catalog == null)
            _catalog = LevelCsvEditorPaths.LoadDefaultCatalog();
        if (_database == null)
            _database = LevelCsvEditorPaths.LoadDefaultDatabase();
    }

    private void OnGUI()
    {
        GUILayout.Label("Import level.csv — mỗi level một folder Level_XXX/", EditorStyles.boldLabel);
        _catalog = (LevelCatalog)EditorGUILayout.ObjectField("Level Catalog", _catalog, typeof(LevelCatalog), false);
        _database = (BlockDatabase)EditorGUILayout.ObjectField("Block Database", _database, typeof(BlockDatabase), false);
        _levelsFolder = EditorGUILayout.TextField("Levels Folder", _levelsFolder);
        _visibleRows = EditorGUILayout.IntField("Visible Rows (bàn ban đầu)", _visibleRows);

        if (GUILayout.Button("Validate All (chỉ log Console)"))
            ValidateAllFromCatalog();

        if (GUILayout.Button("Import All Entries In Catalog"))
            ImportAllFromCatalog();

        if (GUILayout.Button("Create Sample Level 1"))
            CreateSampleLevel1();

        if (GUILayout.Button("Create Catalog + Import Level 1"))
            CreateCatalogAndImportLevel1();
    }

    public static string GetLevelFolder(string levelsFolder, int levelId)
        => $"{levelsFolder}/Level_{levelId:D3}";

    public static string GetDefaultCsvPath(string levelsFolder, int levelId)
        => $"{GetLevelFolder(levelsFolder, levelId)}/level.csv";

    public static string GetLevelAssetPath(string levelsFolder, int levelId)
        => $"{GetLevelFolder(levelsFolder, levelId)}/Level_{levelId:D3}.asset";

    public static string GetLevelAssetPathFromCsv(TextAsset csv, int levelId)
    {
        if (csv == null)
            return GetLevelAssetPath("Assets/Data/Levels", levelId);

        string csvPath = AssetDatabase.GetAssetPath(csv);
        if (string.IsNullOrEmpty(csvPath))
            return GetLevelAssetPath("Assets/Data/Levels", levelId);

        string dir = Path.GetDirectoryName(csvPath)?.Replace('\\', '/');
        return $"{dir}/Level_{levelId:D3}.asset";
    }

    private void CreateCatalogAndImportLevel1()
    {
        string levelPath = GetDefaultCsvPath(_levelsFolder, 1);
        if (!File.Exists(levelPath))
            CreateSampleLevel1();

        var levelCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(levelPath);

        string catalogPath = $"{_levelsFolder}/LevelCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }

        LevelData saved = ImportEntry(1, levelCsv);
        catalog.Levels = new[]
        {
            new LevelCatalogEntry
            {
                LevelId = 1,
                LevelCsv = levelCsv,
                LevelAsset = saved
            }
        };

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LevelCsvImporter] LevelCatalog + Level_001 sẵn sàng.");
    }

    private void ImportAllFromCatalog()
    {
        if (_catalog == null || _catalog.Levels == null)
        {
            Debug.LogError("[LevelCsvImporter] Gán LevelCatalog trước.");
            return;
        }

        var updated = new List<LevelCatalogEntry>(_catalog.Levels.Length);

        foreach (LevelCatalogEntry entry in _catalog.Levels)
        {
            if (entry.LevelCsv == null)
            {
                Debug.LogWarning($"[LevelCsvImporter] Level {entry.LevelId} thiếu level.csv.");
                updated.Add(entry);
                continue;
            }

            LevelData saved = ImportEntry(entry.LevelId, entry.LevelCsv);
            updated.Add(new LevelCatalogEntry
            {
                LevelId = entry.LevelId,
                LevelCsv = entry.LevelCsv,
                LevelAsset = saved
            });
        }

        _catalog.Levels = updated.ToArray();
        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LevelCsvImporter] Import xong — đã cập nhật LevelAsset trong catalog.");
    }

    private void ValidateAllFromCatalog()
    {
        if (_catalog == null)
            _catalog = LevelCsvEditorPaths.LoadDefaultCatalog();
        if (_database == null)
            _database = LevelCsvEditorPaths.LoadDefaultDatabase();

        LevelCsvEditorPaths.ValidateAllInCatalog(_catalog, _database);
    }

    private LevelData ImportEntry(int levelId, TextAsset csv)
    {
        LevelData data = LevelCsvParser.ParseLevelFromCsv(levelId, csv.text, _database, _visibleRows);
        LevelCsvValidator.LogReport(levelId, data.Rows, _database);

        string assetPath = GetLevelAssetPathFromCsv(csv, levelId);
        LevelCsvParser.SaveLevelAsset(data, assetPath);
        Debug.Log($"[LevelCsvImporter] Imported → {assetPath}");

        return AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
    }

    private void CreateSampleLevel1()
    {
        string dir = GetLevelFolder(_levelsFolder, 1);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText($"{dir}/level.csv",
            "row,col0,col1,col2,col3,col4\n" +
            "0,1,2,1,3,2\n" +
            "1,2,1,3,1,2\n" +
            "2,1,3,2,2,1\n" +
            "3,3,2,1,1,3\n" +
            "4,2,1,2,3,1\n" +
            "5,1,2,3,2,2\n" +
            "6,3,1,1,2,3\n" +
            "7,1,2,3,1,2\n" +
            "8,2,3,1,2,3\n" +
            "9,3,1,2,3,1\n" +
            "10,1,3,2,1,2\n" +
            "11,2,1,3,2,3\n" +
            "12,3,2,1,3,1\n" +
            "13,1,2,3,1,2\n" +
            "14,2,3,1,2,3\n" +
            "15,3,1,2,3,1\n" +
            "16,1,3,2,1,2\n" +
            "17,2,1,3,2,3\n" +
            "18,3,2,1,3,1\n" +
            "19,1,2,3,1,2\n" +
            "20,2,3,1,2,3\n" +
            "21,3,1,2,3,1\n" +
            "22,1,3,2,1,2\n" +
            "23,2,1,3,2,3\n" +
            "24,3,2,1,3,1\n");

        AssetDatabase.Refresh();
        Debug.Log($"[LevelCsvImporter] Tạo sample tại {dir}/level.csv");
    }
}
