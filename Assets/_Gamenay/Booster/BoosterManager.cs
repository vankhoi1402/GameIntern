using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;
using TMPro;

public class BoosterManager : Singleton<BoosterManager>
{
    [SerializeField] private BoosterDataBase[] dataList;

    [ShowInInspector] public List<Booster> Boosters { get; private set; } = new List<Booster>();

    [SerializeField] private TextMeshProUGUI boosterFreeText;

    public int LevelUnlockBoosterHint;
    public int LevelUnlockBoosterReveal;
    public int LevelUnlockBoosterFrostTime;
    public int LevelUnlockBoosterHammer;
    public int BoosterFree = 100;

    public void Initialized()
    {
        Boosters.Clear();

        for (int i = 0; i < dataList.Length; i++)
        {
            Booster booster = new Booster();
            booster.BoosterType = dataList[i].BoosterType;
            booster.BoosterPlace = dataList[i].BoosterPlace;
            booster.BoosterIcon = dataList[i].BoosterIcon;
            booster.BoosterName = dataList[i].BoosterName;
            booster.BoosterDescription = dataList[i].BoosterDescription;
            booster.BoosterPrice = dataList[i].BoosterPrice;
            booster.BoosterLevelUnlock = dataList[i].BoosterLevelUnlock;
            booster.BoosterDescriptionUnlock = dataList[i].BoosterDescriptionUnlock;
            booster.Unlock = PlayerPrefs.GetInt($"Unlock_{booster.BoosterType}", defaultValue: 0) == 1;
            booster.FirstUse = PlayerPrefs.GetInt($"FirstUse_{booster.BoosterType}", defaultValue: 1) == 1;
            booster.Amount = PlayerPrefs.GetInt($"Amount_{booster.BoosterType}", defaultValue: 0);
            booster.WaitToPay = false;
            Boosters.Add(booster);
        }

        LevelUnlockBoosterHint = Boosters.Find(b => b.BoosterType == BoosterType.Hint).BoosterLevelUnlock;
        LevelUnlockBoosterReveal = Boosters.Find(b => b.BoosterType == BoosterType.Shuffle).BoosterLevelUnlock;
        LevelUnlockBoosterFrostTime = Boosters.Find(b => b.BoosterType == BoosterType.FrostTime).BoosterLevelUnlock;
        LevelUnlockBoosterHammer = Boosters.Find(b => b.BoosterType == BoosterType.Hammer).BoosterLevelUnlock;
        BindRemoteConfig();

        for (int i = 0; i < Boosters.Count; i++)
        {
            if (Boosters[i].BoosterLevelUnlock <= 0 && !Boosters[i].Unlock)
                UnlockBooster(Boosters[i].BoosterType);
        }

        CheckUnlockByLevel(SaveManager.CurrentLevel);
    }

    private void OnDisable()
    {
        UnbindRemoteConfig();
    }

    private void BindRemoteConfig()
    {
        if (!FirebaseManager.Exists())
            return;

        FirebaseManager.Ins.OnRemoteConfigReady -= HandleRemoteConfigReady;
        FirebaseManager.Ins.OnRemoteConfigReady += HandleRemoteConfigReady;

        if (FirebaseManager.Ins.DoneRemoteConfig)
            HandleRemoteConfigReady();
        else
            BoosterFree = FirebaseManager.Ins.BoosterFree;
    }

    private void UnbindRemoteConfig()
    {
        if (!FirebaseManager.Exists())
            return;

        FirebaseManager.Ins.OnRemoteConfigReady -= HandleRemoteConfigReady;
    }

    private void HandleRemoteConfigReady()
    {
        BoosterFree = FirebaseManager.Ins.BoosterFree;
        //Debug.Log($"[Booster] BoosterFree updated from Remote Config: {BoosterFree}");
    }


    public void CheckUnlockByLevel(int completedLevelId)
    {
        for (int i = 0; i < Boosters.Count; i++)
        {
            Booster booster = Boosters[i];
            if (booster.Unlock || booster.BoosterLevelUnlock <= 0)
                continue;

            if (completedLevelId >= booster.BoosterLevelUnlock)
                UnlockBooster(booster.BoosterType);
        }
    }

    public bool CanUseBooster(Booster booster)
    {
        if (booster == null || !booster.Unlock)
            return false;

        if (booster.FirstUse)
            return true;

        return booster.Amount > 0;
    }

    public bool TryUseBooster(BoosterType boosterType, Func<bool> useAction)
    {
        Booster booster = GetBooster(boosterType);
        if (!CanUseBooster(booster))
            return false;

        if (useAction == null || !useAction())
            return false;

        DirectPayBooster(booster);
        return true;
    }

    public void UnlockBooster(BoosterType boosterType)
    {
        for (int i = 0; i < Boosters.Count; i++)
        {
            if (Boosters[i].BoosterType == boosterType && Boosters[i].Unlock == false)
            {
                Boosters[i].Unlock = true;
                Boosters[i].FirstUse = true;
                Boosters[i].Amount += BoosterFree;
                Save(Boosters[i]);
                break;
            }
        }

    }

    public void AddBoosterAmount(Booster booster, int amount = 1)
    {
        for (int i = 0; i < Boosters.Count; i++)
        {
            if (booster.BoosterType == Boosters[i].BoosterType)
            {
                Boosters[i].Amount += amount;
                Save(Boosters[i]);
                break;
            }
        }
    }

    public void AddBoosterAmount(BoosterType boosterType, int amount = 1)
    {
        for (int i = 0; i < Boosters.Count; i++)
        {
            if (Boosters[i].BoosterType == boosterType)
            {
                Boosters[i].Amount += amount;
                Save(Boosters[i]);
                return;
            }
        }
    }

    public void AddAllBooster(int amount)
    {
        for (int i = 0; i < Boosters.Count; i++)
        {
            Boosters[i].Amount += amount;
            Save(Boosters[i]);
        }
    }

    public void SetBoosterPay(Booster booster)
    {
        if (Boosters.Contains(booster))
        {
            booster.WaitToPay = true;
        }
    }

    public void SetFreeUseBooster(Booster booster)
    {
        if (Boosters.Contains(booster))
        {
            booster.WaitToPay = true;
        }
    }

    public void ResetBoosterPay(Booster booster)
    {
        if (Boosters.Contains(booster))
        {
            booster.WaitToPay = false;
        }
    }

    public void BoosterPay()
    {
        for (int i = 0; i < Boosters.Count; i++)
        {
            if (Boosters[i].WaitToPay)
            {
                if (Boosters[i].FirstUse)
                {
                    Boosters[i].FirstUse = false;
                    switch (Boosters[i].BoosterType)
                    {
                        case BoosterType.Hint:
                            break;
                        case BoosterType.Shuffle:
                            break;
                        case BoosterType.FrostTime:
                            break;
                        case BoosterType.Hammer:
                            break;
                    }
                }
                else
                {
                    Boosters[i].Amount--;
                }

                Boosters[i].WaitToPay = false;
                Save(Boosters[i]);
            }
        }
    }

    public void PayReset()
    {
        for (int i = 0; i < Boosters.Count; i++)
        {
            Boosters[i].WaitToPay = false;
        }
    }

    public void DirectPayBooster(Booster booster) //Only use for in  booster
    {
        if (Boosters.Contains(booster))
        {
            if (booster.FirstUse)
            {
                booster.FirstUse = false;
            }
            else
            {
                booster.Amount--;
            }

            Save(booster);
        }
    }


    private void Save(Booster booster)
    {
        if (booster == null || booster.BoosterType == BoosterType.None)
            return;

        PlayerPrefs.SetInt("Unlock_" + booster.BoosterType, booster.Unlock ? 1 : 0);
        PlayerPrefs.SetInt("Amount_" + booster.BoosterType, booster.Amount);
        PlayerPrefs.SetInt("FirstUse_" + booster.BoosterType, booster.FirstUse ? 1 : 0);
        PlayerPrefs.Save();
    }

    public Booster GetBooster(BoosterType boosterType)
    {
        return Boosters.Find(b => b.BoosterType == boosterType);
    }

    public Sprite GetBoosterSprite(BoosterType boosterType)
    {
        return Boosters.Find(b => b.BoosterType == boosterType).BoosterIcon;
    }

    public Booster GetRandomBooster()
    {
        Booster booster = Boosters[0];

        int randomBooster = Random.Range(0, 4);
        switch (randomBooster)
        {
            case 0:
                booster = Boosters[0];
                break;
            case 1:
                booster = Boosters[1];
                break;
            case 2:
                booster = Boosters[2];
                break;
            case 3:
                booster = Boosters[3];
                break;
        }

        return booster;
    }

    [Button(ButtonSizes.Gigantic)]
    public void CheatAddBooster()
    {
        for (int i = 0; i < Boosters.Count; i++)
        {

            Boosters[i].Amount += 100;
            Save(Boosters[i]);
        }
    }

    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            for (int i = 0; i < Boosters.Count; i++)
            {
                Save(Boosters[i]);
            }
        }
    }

    void OnApplicationQuit()
    {
        for (int i = 0; i < Boosters.Count; i++)
        {
            Save(Boosters[i]);
        }
    }

}

[Serializable]
public class Booster
{
    public BoosterType BoosterType;
    public BoosterPlace BoosterPlace;
    public int Amount;
    public Sprite BoosterIcon;
    public string BoosterName;
    public string BoosterDescription;
    public string BoosterDescriptionUnlock;
    public int BoosterPrice;
    public int BoosterLevelUnlock;
    public bool WaitToPay;
    public bool FirstUse;
    public bool Unlock;
}

[Serializable]
public struct BoosterDataBase
{
    public BoosterType BoosterType;
    public BoosterPlace BoosterPlace;
    public Sprite BoosterIcon;
    public string BoosterDescriptionUnlock;
    public int BoosterPrice;
    public int BoosterLevelUnlock;
    public string BoosterName;
    public string BoosterDescription;
}

public enum BoosterType
{
    Hint, // Name : "Hint", Description : "Gợi ý 3 block trên board có cùng topic", Icon : BoosterHintIcon
    Shuffle, // Name : "Shuffle", Description : "Xếp lại các lá bài trên board", Icon : BoosterShuffleIcon
    FrostTime, // Name : "FrostTime", Description : "Đóng băng thời gian", Icon : BoosterFrostTimeIcon
    Hammer, // Name : "Hammer", Description : "Đập hạ bài", Icon : BoosterHammerIcon
    None
}

public enum BoosterPlace
{
    In,
    Out
}