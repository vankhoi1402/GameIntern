using UnityEditor;
using UnityEngine;

/// <summary>Gọi AssetDatabase.Refresh — menu, batchmode, hoặc tự chạy khi Unity recompile.</summary>
public static class ForceRefreshAssets
{
    private const string c_PendingMarker = "Assets/Data/Levels/.refresh_pending";

    [InitializeOnLoadMethod]
    private static void OnEditorLoad()
    {
        EditorApplication.delayCall += TryAutoRefresh;
    }

    [MenuItem("Grid Game/Force Refresh Assets")]
    public static void RefreshFromMenu()
    {
        Refresh();
        Debug.Log("[ForceRefresh] AssetDatabase.Refresh xong.");
    }

    /// <summary>Unity -batchmode -executeMethod ForceRefreshAssets.RefreshFromBatch</summary>
    public static void RefreshFromBatch()
    {
        Refresh();
        Debug.Log("[ForceRefresh] Batch refresh xong.");
        EditorApplication.Exit(0);
    }

    /// <summary>Ghi marker — Unity recompile sẽ tự refresh (khi Editor đang mở).</summary>
    public static void RequestAutoRefresh()
    {
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(Application.dataPath, "Data/Levels/.refresh_pending"),
            System.DateTime.UtcNow.ToString("O"));
        AssetDatabase.Refresh();
    }

    private static void TryAutoRefresh()
    {
        string path = c_PendingMarker;
        if (!System.IO.File.Exists(path))
            return;

        try { System.IO.File.Delete(path); }
        catch { /* ignore */ }

        Refresh();
        Debug.Log("[ForceRefresh] Auto refresh Level_009 / catalog xong.");
    }

    private static void Refresh()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
    }
}
