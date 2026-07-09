using UnityEngine;

public static class SaveManager
{
    #region Keys

    private const string CoinKey = "Coin";
    private const string CurrentLevelKey = "CurrentLevel";

    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SfxVolume";
    private const string MusicEnableKey = "MusicEnable";
    private const string SfxEnableKey = "SfxEnable";

    private const int DefaultLevel = 1;

    #endregion

    #region Level

    /// <summary>Level sẽ load khi bấm Play (mặc định = 1).</summary>
    public static int CurrentLevel
    {
        get => PlayerPrefs.GetInt(CurrentLevelKey, DefaultLevel);
        set => PlayerPrefs.SetInt(CurrentLevelKey, Mathf.Max(1, value));
    }

    /// <summary>Gọi khi thắng level — advance sang level tiếp theo.</summary>
    public static void CompleteLevel(int completedLevelId)
    {
        CurrentLevel = completedLevelId + 1;
        
        Save();
    }

    /// <summary>Reset progress (cheat / debug).</summary>
    public static void ResetLevel()
    {
        CurrentLevel = DefaultLevel;
        Save();
    }

    #endregion

    #region Player

    public static int Coin
    {
        get => PlayerPrefs.GetInt(CoinKey, 0);
        set => PlayerPrefs.SetInt(CoinKey, Mathf.Max(0, value));
    }

    public static void AddCoin(int amount)
    {
        Coin += amount;
        Save();
    }

    #endregion

    #region Audio

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
    }

    public static bool MusicEnable
    {
        get => PlayerPrefs.GetInt(MusicEnableKey, 1) == 1;
        set => PlayerPrefs.SetInt(MusicEnableKey, value ? 1 : 0);
    }

    public static bool SfxEnable
    {
        get => PlayerPrefs.GetInt(SfxEnableKey, 1) == 1;
        set => PlayerPrefs.SetInt(SfxEnableKey, value ? 1 : 0);
    }

    #endregion

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    public static void DeleteAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
