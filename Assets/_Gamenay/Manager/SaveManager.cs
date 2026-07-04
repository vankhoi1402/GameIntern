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

    private static string BoosterUnlockKey(BoosterType type)
        => $"Booster_{type}_Unlock";

    private static string BoosterAmountKey(BoosterType type)
        => $"Booster_{type}_Amount";

    private static string BoosterFirstUseKey(BoosterType type)
        => $"Booster_{type}_FirstUse";

    #endregion

    #region Player

    public static int Coin
    {
        get => PlayerPrefs.GetInt(CoinKey, 0);
        set => PlayerPrefs.SetInt(CoinKey, value);
    }

    public static int CurrentLevel
    {
        get => PlayerPrefs.GetInt(CurrentLevelKey, 1);
        set => PlayerPrefs.SetInt(CurrentLevelKey, value);
    }

    #endregion

    #region Booster

    public static bool GetBoosterUnlock(BoosterType type)
    {
        return PlayerPrefs.GetInt(BoosterUnlockKey(type), 0) == 1;
    }

    public static void SetBoosterUnlock(BoosterType type, bool value)
    {
        PlayerPrefs.SetInt(BoosterUnlockKey(type), value ? 1 : 0);
    }

    public static int GetBoosterAmount(BoosterType type)
    {
        return PlayerPrefs.GetInt(BoosterAmountKey(type), 0);
    }

    public static void SetBoosterAmount(BoosterType type, int amount)
    {
        PlayerPrefs.SetInt(BoosterAmountKey(type), amount);
    }

    public static bool GetBoosterFirstUse(BoosterType type)
    {
        return PlayerPrefs.GetInt(BoosterFirstUseKey(type), 1) == 1;
    }

    public static void SetBoosterFirstUse(BoosterType type, bool value)
    {
        PlayerPrefs.SetInt(BoosterFirstUseKey(type), value ? 1 : 0);
    }

    #endregion

    #region Audio

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(MusicVolumeKey, value);
    }

    #endregion

    public static void Save()
    {
        PlayerPrefs.Save();
    }
}