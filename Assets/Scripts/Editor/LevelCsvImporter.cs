using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Import level.csv (bin cột) → LevelData.asset và gán vào LevelCatalog.</summary>
public class LevelCsvImporter : EditorWindow
{
    private LevelCatalog _catalog;
    private BlockDatabase _database;
    private string _levelsFolder = "Assets/Data/Levels";
    private int _visibleRows = LevelCsvParser.DefaultVisibleRows;

    [MenuItem("Grid Game/Import Levels From CSV")]
    public static void ShowWindow()
    {
        GetWindow<LevelCsvImporter>("Level CSV Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Import level.csv — mỗi cột là bin block từ đáy lên", EditorStyles.boldLabel);
        _catalog = (LevelCatalog)EditorGUILayout.ObjectField("Level Catalog", _catalog, typeof(LevelCatalog), false);
        _database = (BlockDatabase)EditorGUILayout.ObjectField("Block Database", _database, typeof(BlockDatabase), false);
        _levelsFolder = EditorGUILayout.TextField("Levels Folder", _levelsFolder);
        _visibleRows = EditorGUILayout.IntField("Visible Rows (bàn ban đầu)", _visibleRows);

        if (GUILayout.Button("Import All Entries In Catalog"))
            ImportAllFromCatalog();

        if (GUILayout.Button("Create Sample Level 1"))
            CreateSampleLevel1();

        if (GUILayout.Button("Create Catalog + Import Level 1"))
            CreateCatalogAndImportLevel1();
    }

    private void CreateCatalogAndImportLevel1()
    {
        string levelPath = $"{_levelsFolder}/Level_001/level.csv";
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

        LevelData data = LevelCsvParser.ParseLevelFromCsv(1, levelCsv.text, _database, _visibleRows);
        string levelAssetPath = $"{_levelsFolder}/Level_001.asset";
        LevelCsvParser.SaveLevelAsset(data, levelAssetPath);

        LevelData saved = AssetDatabase.LoadAssetAtPath<LevelData>(levelAssetPath);
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

        foreach (var entry in _catalog.Levels)
        {
            if (entry.LevelCsv == null)
            {
                Debug.LogWarning($"[LevelCsvImporter] Level {entry.LevelId} thiếu level.csv.");
                continue;
            }

            LevelData data = LevelCsvParser.ParseLevelFromCsv(
                entry.LevelId,
                entry.LevelCsv.text,
                _database,
                _visibleRows);

            string assetPath = $"{_levelsFolder}/Level_{entry.LevelId:D3}.asset";
            LevelCsvParser.SaveLevelAsset(data, assetPath);
            Debug.Log($"[LevelCsvImporter] Imported → {assetPath}");
        }

        AssetDatabase.Refresh();
        Debug.Log("[LevelCsvImporter] Import xong.");
    }

    private void CreateSampleLevel1()
    {
        string dir = $"{_levelsFolder}/Level_001";
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
