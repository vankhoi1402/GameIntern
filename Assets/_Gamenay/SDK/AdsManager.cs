using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AdsType
{
    None,
    Max,
    Google,
}

public class AdsManager : Singleton<AdsManager>
{
    public AdsType AdsType;
    public float interTimer;
    public float rewardTimer;
    public float InterCappingTimer = 60;
    public float RewardCappingTimer = 90;
    public int LevelShowInter = 5;
    public bool FirstShowInter = false;


    public void Initialize(Action action)
    {
        DontDestroyOnLoad(this.gameObject);
        if (AdsType == AdsType.None)
        {
            action?.Invoke();
            return;
        }

       // MaxManager.Ins.Initialize(action);
        AdsMobManager.Ins.Initialize();
        interTimer = 0;
        rewardTimer = 0;
        FirstShowInter = PlayerPrefs.GetInt("FirstShowInter", 0) == 1;
    }

    public void GetLevelShowInter(int level) => LevelShowInter = level;
    public void GetInterCappingTime(float time) => InterCappingTimer = time;
    public void GetRewardCappingTime(float time) => RewardCappingTimer = time;

    public void ShowBanner()
    {
        // if (AdsType == AdsType.Max)
        // {
        //     //MaxManager.Ins.ShowBanner();
        // }
        // else if (AdsType == AdsType.Google)
        //// {
            
        //}
        if (AdsType == AdsType.None) return;
        AdsMobManager.Ins.LoadBannerAd();
    }

    public void HindBanner()
    {
        if (AdsType == AdsType.Max)
        {
            //MaxManager.Ins.HindBanner();
        }
        AdsMobManager.Ins.DestroyBannerAds();
    }

    public void ShowInterAds()
    {
        if (AdsType == AdsType.None) return;
        if (GameManager.Ins.IsCheat)
        {
            EventManager.Trigger(new OnWatchAds());
            return;
        }
        if (GameManager.Ins.RemoveAds) return;

        if (AdsType == AdsType.Max)
        {
            //MaxManager.Ins.ShowInter();
        }
        else if (AdsType == AdsType.Google)
        {
            AdsMobManager.Ins.ShowInterstitialAd();
        }
    }

    public void ShowRewardAds(Action rewardAction)
    {
        // rewardTimer = 0;
        // if (GameManager.Ins.IsCheat)
        // {
        //     rewardAction?.Invoke();
        //     EventManager.Trigger(new OnWatchAds());
        //     return;
        // }
        // // if (AdsType == AdsType.Max)
        // // {
        // //     MaxManager.Ins.ShowReward(rewardAction);
        // // }
        // // else 
        // if (AdsType == AdsType.Google)
        // {
            AdsMobManager.Ins.ShowRewardedAd(rewardAction);
        //}
    }

    // Inter capping
    public void TryShowInter()
    {               
         ShowInterAds();
         return;

        if (SaveManager.CurrentLevel < LevelShowInter)
            return;

        if (rewardTimer > RewardCappingTimer)
        {
            if (interTimer > InterCappingTimer)
            {
                interTimer = 0;
                rewardTimer = 0;
                ShowInterAds();
            }
        }

    }

    public void FirstShowInterFirstTime(Action successAction, Action failAction)
    {
        if (SaveManager.CurrentLevel < LevelShowInter)
            return;
        interTimer = 0;
        rewardTimer = 0;

        if (GameManager.Ins.RemoveAds)
        {
            FirstShowInter = true;
            PlayerPrefs.SetInt("FirstShowInter", 1);
            PlayerPrefs.Save();
            failAction?.Invoke();
            return;
        }
        else if (GameManager.Ins.IsCheat)
        {
            FirstShowInter = true;
            PlayerPrefs.SetInt("FirstShowInter", 1);
            PlayerPrefs.Save();
            successAction?.Invoke();
            return;
        }
        else
        {
            // if (AdsType == AdsType.Max)
            // {
            //     MaxManager.Ins.FirstShowInter(successAction, failAction);
            // }
            // else 
            if (AdsType == AdsType.Google)
            {
                AdsMobManager.Ins.FirstShowInterstitialAd(successAction, failAction);
            }
        }
    }

    private void Update()
    {
        interTimer += Time.deltaTime;
        rewardTimer += Time.deltaTime;
        if(Input.GetKeyDown(KeyCode.Space))
        {
            ShowInterAds();
        }
    }
}
