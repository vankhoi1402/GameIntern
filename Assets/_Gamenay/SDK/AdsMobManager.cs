//using AppsFlyerSDK;
using DG.Tweening;
using Firebase.Analytics;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using static MaxSdkBase;
public class AdsMobManager : Singleton<AdsMobManager>
{
    // These ad units are configured to always serve test ads.
#if UNITY_ANDROID
    // Toàn bộ đã được chuyển sang ID TEST an toàn của Google AdMob
    private string AOA_ID = "ca-app-pub-3940256099942544/9257395921";
    private string banner_ID = "ca-app-pub-3940256099942544/6300978111";
    private string reward_ID = "ca-app-pub-3940256099942544/5224354917";
    private string inter_ID = "ca-app-pub-3940256099942544/1033173712";
    private string native_ID = "ca-app-pub-3940256099942544/2247696110";


#elif UNITY_IPHONE || UNITY_IOS
    private string AOA_ID = "unused";
    private string banner_ID = "unused";
    private string inter_ID = "ca-app-pub-9819920607806935/1584404395";
    private string reward_ID = "unused";
    private string native_ID = "unused";
#else
    private string AOA_ID = "unused";
    private string banner_ID = "unused";
    private string inter_ID = "unused";
    private string reward_ID = "unused";
    private string native_ID = "unused";
#endif
    // App open ads can be preloaded for up to 4 hours.
    private readonly TimeSpan TIMEOUT = TimeSpan.FromHours(4);
    private DateTime _expireTime;
    private bool isAppOpenAdLoaded = false;
    public bool isAOAAdShowing = false;
    private bool isDropAOA = false;
    // Thoi gian doi aoa load neu lau hon thi bo di
    public float AOAWaitTime = 5f;

    //  [SerializeField] private LocalizedString falseString;

    // Data for analytic
    public string interPlacement;
    public string levelInter;

    public string levelReward;
    public string button_name;
    public string reward_name;
    public string reward_type;
    public int value;
    public string rewardPlacement;

    private void OnEnable()
    {
        EventManager.AddListener<SendInterData>(SendInterDataCallBack);
        EventManager.AddListener<SendRewardData>(SendRewardDataCallBack);
    }

    private void OnDisable()
    {
        EventManager.RemoveListener<SendInterData>(SendInterDataCallBack);
        EventManager.RemoveListener<SendRewardData>(SendRewardDataCallBack);
    }

    private void SendInterDataCallBack(SendInterData e)
    {
        interPlacement = e.interPlacement;
        levelInter = e.levelInter;
    }

    private void SendRewardDataCallBack(SendRewardData e)
    {
        levelReward = e.levelReward;
        button_name = e.button_name;
        reward_name = e.reward_name;
        reward_type = e.reward_type;
        value = e.value;
        rewardPlacement = e.rewardPlacement;
    }

    private void Awake()
    {
        // Use the AppStateEventNotifier to listen to application open/close events.
        // This is used to launch the loaded ad when we open the app.
    }
    // private void OnEnable()
    // {
    //     // Use the AppStateEventNotifier to listen to application open/close events.
    //     // This is used to launch the loaded ad when we open the app.
    //     AppStateEventNotifier.AppStateChanged += OnAppStateChanged;
    // }


    private void OnDestroy()
    {
        // Always unlisten to events when complete.
        AppStateEventNotifier.AppStateChanged -= OnAppStateChanged;
    }

    public void Initialize()
    {
        // Initialize the Google Mobile Ads SDK.
        MobileAds.Initialize(initStatus =>
        {
            // This callback is called once the MobileAds SDK is initialized.
            Debug.Log("[Block Escape] Google Mobile Ads Initialized.");
            AppStateEventNotifier.AppStateChanged += OnAppStateChanged;
            // LoadAppOpenAd();
            LoadInterstitialAd();
            LoadRewardedAd();
            LoadBannerAd();

        });
    }

    // -------------- AOA --------------- //
    private AppOpenAd appOpenAd;

    public bool IsAdAvailable
    {
        get
        {
            return appOpenAd != null;
        }
    }

    public void LoadAppOpenAd()
    {
        // Clean up the old ad before loading a new one.
        if (appOpenAd != null)
        {
            DestroyAppOpenAd();
        }

        Debug.Log("Loading the app open ad.");

        // Create our request used to load the ad.
        AdRequest adRequest = new AdRequest();

        // send the request to load the ad.
        AppOpenAd.Load(AOA_ID, adRequest,
            (AppOpenAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("app open ad failed to load an ad " +
                                   "with error : " + error);
                    isAppOpenAdLoaded = false;
                    return;
                }

                Debug.Log("App open ad loaded with response : "
                          + ad.GetResponseInfo());

                appOpenAd = ad;
                isAppOpenAdLoaded = true;
                RegisterEventHandlers(ad);
                // App open ads can be preloaded for up to 4 hours.
                _expireTime = DateTime.Now + TIMEOUT;
            });
    }

    private void RegisterEventHandlers(AppOpenAd ad)
    {
        // Raised when the ad is estimated to have earned money.
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Dictionary<string, string> additionalParams = new Dictionary<string, string>();
            // additionalParams.Add(AdRevenueScheme.COUNTRY, MaxSdk.GetSdkConfiguration().CountryCode);
            // additionalParams.Add(AdRevenueScheme.AD_UNIT, ad.GetAdUnitID());
            //additionalParams.Add(AdRevenueScheme.AD_TYPE, "AOA");
            //  additionalParams.Add(AdRevenueScheme.PLACEMENT, "AOAplacement");
            // var logRevenue = new AFAdRevenueData("monetizationAOAGoogleAds", MediationNetwork.GoogleAdMob, adValue.CurrencyCode, adValue.Value);
            // AppsFlyer.logAdRevenue(logRevenue, additionalParams);

            Debug.Log(String.Format("App open ad paid {0} {1}.",
                adValue.Value,
                adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("App open ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        ad.OnAdClicked += () =>
        {
            Debug.Log("App open ad was clicked.");
        };
        // Raised when an ad opened full screen content.
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("App open ad full screen content opened.");
        };
        // Raised when the ad closed full screen content.
        ad.OnAdFullScreenContentClosed += () =>
        {
            isAppOpenAdLoaded = false;
            isAOAAdShowing = false;
            LoadAppOpenAd();
            Debug.Log("App open ad full screen content closed.");
        };
        // Raised when the ad failed to open full screen content.
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            isAOAAdShowing = false;
            Debug.LogError("App open ad failed to open full screen content " +
                           "with error : " + error);
            isAppOpenAdLoaded = false;
            LoadAppOpenAd();
        };
    }

    private void RegisterReloadHandler(AppOpenAd ad)
    {
        // Raised when the ad closed full screen content.
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("App open ad full screen content closed.");

            // Reload the ad so that we can show another as soon as possible.
            //   LoadAppOpenAd();
        };
        // Raised when the ad failed to open full screen content.
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("App open ad failed to open full screen content " +
                           "with error : " + error);

            // Reload the ad so that we can show another as soon as possible.
            //    LoadAppOpenAd();
        };
    }

    private Coroutine coroutineAOA;
    public void ShowAppOpenAd()
    {
        //if (IAPManager.Ins.RemoveAds) return;
        if (coroutineAOA != null) StopCoroutine(coroutineAOA);
        coroutineAOA = StartCoroutine(I_ShowAOA());
    }
    private void ShowAOAAdmob()
    {
        if (appOpenAd != null && appOpenAd.CanShowAd() && DateTime.Now < _expireTime)
        {
            Debug.Log("Showing app open ad.");
            isAOAAdShowing = true;
            appOpenAd.Show();
            isAppOpenAdLoaded = false;
        }
        else
        {
            Debug.LogError("App open ad is not ready yet.");
        }
    }
    IEnumerator I_ShowAOA()
    {
        isDropAOA = false;
        DOVirtual.DelayedCall(AOAWaitTime, () =>
        {
            isDropAOA = true;
        }).SetUpdate(true);
        yield return new WaitUntil(() => appOpenAd != null || isDropAOA);
        Debug.LogWarning("[CakeLand]Check Drop AOA: " + isDropAOA);
        if (!isDropAOA)
            ShowAOAAdmob();
    }
    public void DestroyAppOpenAd()
    {
        if (appOpenAd != null)
        {
            Debug.Log("Destroying app open ad.");
            appOpenAd.Destroy();
            appOpenAd = null;
        }
        isAppOpenAdLoaded = false;
    }

    private void OnAppStateChanged(AppState state)
    {
        Debug.Log("App State changed to : " + state);

        // if the app is Foregrounded and the ad is available, show it.
        if (state == AppState.Foreground)
        {
            //if (!MaxManager.Ins.isShowingAd)
            {
                if (IsAdAvailable)
                {
                    ShowAppOpenAd();
                }
            }
        }
    }

    // -------------- Banner --------------- //
    BannerView bannerView;

    /// <summary>
    /// Creates a 320x50 banner view at top of the screen.
    /// </summary>

    private bool isBannerLoading = false; // Cờ chặn việc load trùng lặp

    public void LoadBannerAd()
    {
        // Nếu đang trong quá trình load, không cho phép gọi trùng dòng nữa
        if (isBannerLoading) return;
        isBannerLoading = true;

        Debug.Log("--- [BANNER] Bắt đầu luồng LoadBannerAd an toàn ---");

        // Nếu chưa có view thì tạo mới
        if (bannerView == null)
        {
            CreateBannerView();
        }

        var adRequest = new AdRequest();
        Debug.Log("--- [BANNER] Đang gửi request lên Google... ---");

        bannerView.LoadAd(adRequest);
    }

    public void CreateBannerView()
    {
        Debug.Log("--- [BANNER] Khởi tạo Banner View mới ---");

        // SỬA: Dùng kích thước Banner chuẩn của AdMob để test trên Editor cho an toàn
        bannerView = new BannerView(banner_ID, AdSize.Banner, AdPosition.Bottom);

        // Đăng ký sự kiện để giải phóng cờ loading khi xong
        bannerView.OnBannerAdLoaded += () =>
        {
            isBannerLoading = false;
            Debug.Log("--- [BANNER] Quảng cáo hiển thị THÀNH CÔNG! ---");
        };
        bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            isBannerLoading = false;
            Debug.LogError("--- [BANNER] Thất bại: " + error);
        };

        // Gọi hàm lắng nghe các sự kiện khác của bạn
        ListenToAdEvents();
    }

    private void ListenToAdEvents()
    {
        // Raised when an ad is loaded into the banner view.
        bannerView.OnBannerAdLoaded += () =>
        {
            Debug.Log("Banner view loaded an ad with response : "
                + bannerView.GetResponseInfo());
        };
        // Raised when an ad fails to load into the banner view.
        bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
        {
            Debug.LogError("Banner view failed to load an ad with error : "
                + error);
        };
        // Raised when the ad is estimated to have earned money.
        bannerView.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Banner view paid {0} {1}.",
                adValue.Value,
                adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        bannerView.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Banner view recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        bannerView.OnAdClicked += () =>
        {
            Debug.Log("Banner view was clicked.");
        };
        // Raised when an ad opened full screen content.
        bannerView.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Banner view full screen content opened.");
        };
        // Raised when the ad closed full screen content.
        bannerView.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Banner view full screen content closed.");
        };
    }

    public void DestroyBannerAds()
    {
        if (bannerView != null)
        {
            Debug.Log("Destroying banner view.");
            bannerView.Destroy();
            bannerView = null;
        }
    }

    // -------------- Reward ------------ //
    private RewardedAd _rewardedAd;

    /// <summary>
    /// Loads the rewarded ad.
    /// </summary>
    public void LoadRewardedAd()
    {
        // Clean up the old ad before loading a new one.
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        Debug.Log("Loading the rewarded ad.");

        // create our request used to load the ad.
        var adRequest = new AdRequest();

        // send the request to load the ad.
        RewardedAd.Load(reward_ID, adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("Rewarded ad failed to load an ad " +
                                   "with error : " + error);
                    return;
                }

                Debug.Log("Rewarded ad loaded with response : "
                          + ad.GetResponseInfo());

                _rewardedAd = ad;
                RegisterEventHandlers(ad);
            });
    }

    // Action rewardAction;
    // public void ShowRewardedAd(Action action)
    // {
    //     rewardAction = action;

    //     const string rewardMsg =
    //         "Rewarded ad rewarded the user. Type: {0}, amount: {1}.";

    //     if (_rewardedAd != null && _rewardedAd.CanShowAd())
    //     {

    //         _rewardedAd.Show((GoogleMobileAds.Api.Reward reward) =>
    //         {
    //             rewardAction?.Invoke();
    //             // TODO: Reward the user.
    //             Debug.Log(String.Format(rewardMsg, reward.Type, reward.Amount));
    //             EventManager.Trigger(new OnWatchAds());
    //         });
    //        // AppsflyerEvent.LogEventAf("af_rewarded_logicgame");

    //     }
    //     else
    //     {
    //         OnNotice e = new OnNotice();
    //         e.Content =  "Ad is not ready yet, please try again later.";
    //         EventManager.Trigger(e);
    //     }

    // }
    Action rewardAction;
    public void ShowRewardedAd(Action action)
    {
        rewardAction = action;

        const string rewardMsg =
            "Rewarded ad rewarded the user. Type: {0}, amount: {1}.";

        if (_rewardedAd != null && _rewardedAd.CanShowAd())
        {
            _rewardedAd.Show((GoogleMobileAds.Api.Reward reward) =>
            {
                // === CHỈ CHẠY KHI XEM ĐỦ GIÂY ===
                rewardAction?.Invoke();
                rewardAction = null; // BỔ SUNG: Dọn dẹp bộ nhớ ngay sau khi dùng xong

                Debug.Log(String.Format(rewardMsg, reward.Type, reward.Amount));

                // DI CHUYỂN VÀO ĐÂY: Chỉ tính là xem thành công khi xem ĐỦ GIÂY
                EventManager.Trigger(new OnWatchAds());
            });

            // AppsflyerEvent.LogEventAf("af_rewarded_logicgame");
        }
        else
        {
            OnNotice e = new OnNotice();
            e.Content = "Ad is not ready yet, please try again later.";
            EventManager.Trigger(e);
        }
    }

    private void RegisterEventHandlers(RewardedAd ad)
    {
        // Raised when the ad is estimated to have earned money.
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Rewarded ad paid {0} {1}.",
                adValue.Value,
                adValue.CurrencyCode));

            Dictionary<string, string> additionalParams = new Dictionary<string, string>();
            // additionalParams.Add(AdRevenueScheme.COUNTRY, MaxSdk.GetSdkConfiguration().CountryCode);
            // additionalParams.Add(AdRevenueScheme.AD_UNIT, ad.GetAdUnitID());
            // additionalParams.Add(AdRevenueScheme.AD_TYPE, "Reward");
            // additionalParams.Add(AdRevenueScheme.PLACEMENT, "RewardPlace");
            // var logRevenue = new AFAdRevenueData("monetizationGoogleAdMob", MediationNetwork.GoogleAdMob, adValue.CurrencyCode, adValue.Value);
            // AppsFlyer.logAdRevenue(logRevenue, additionalParams);

        };
        // Raised when an impression is recorded for an ad.
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Rewarded ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        ad.OnAdClicked += () =>
        {
            Debug.Log("Rewarded ad was clicked.");
        };
        // Raised when an ad opened full screen content.
        ad.OnAdFullScreenContentOpened += () =>
        {
            // AppsflyerEvent.LogEventAf("af_rewarded_displayed");
            Debug.Log("Rewarded ad full screen content opened.");
        };
        // Raised when the ad closed full screen content.
        ad.OnAdFullScreenContentClosed += () =>
        {
            LoadRewardedAd();
            var parameter = new[] {
                    new AnalyticParameter("level", levelReward),
                    new AnalyticParameter("button_name", button_name),
                    new AnalyticParameter("reward_name", reward_name),
                    new AnalyticParameter("reward_type", reward_type),
                    new AnalyticParameter("value", value),
                    new AnalyticParameter("placement", rewardPlacement),
            };
            AnalyticManager.Ins.LogEvent("ads_reward_complete", parameter);
            Debug.Log("Rewarded ad full screen content closed.");
        };
        // Raised when the ad failed to open full screen content.
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            LoadRewardedAd();
            Debug.LogError("Rewarded ad failed to open full screen content " +
                           "with error : " + error);
        };
    }
    private void RegisterReloadHandler(RewardedAd ad)
    {
        // Raised when the ad closed full screen content.
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Rewarded Ad full screen content closed.");

            // Reload the ad so that we can show another as soon as possible.
            LoadRewardedAd();
        };
        // Raised when the ad failed to open full screen content.
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Rewarded ad failed to open full screen content " +
                           "with error : " + error);

            // Reload the ad so that we can show another as soon as possible.
            LoadRewardedAd();
        };
    }


    // -------------- Inter --------------- //
    private InterstitialAd interstitialAd;

    public void LoadInterstitialAd()
    {
        // Clean up the old ad before loading a new one.
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        Debug.Log("Loading the interstitial ad.");

        // create our request used to load the ad.
        var adRequest = new AdRequest();

        // send the request to load the ad.
        InterstitialAd.Load(inter_ID, adRequest,
            (InterstitialAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("interstitial ad failed to load an ad " +
                                   "with error : " + error);
                    return;
                }

                Debug.Log("Interstitial ad loaded with response : "
                          + ad.GetResponseInfo());

                interstitialAd = ad;
                // Register to ad events to extend functionality.
                RegisterEventHandlers(ad);
            });
    }

    public void ShowInterstitialAd()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            Debug.Log("Showing interstitial ad.");
            interstitialAd.Show();
            // AppsflyerEvent.LogEventAf("af_inters_logicgame");
        }
        else
        {
            Debug.LogError("Interstitial ad is not ready yet.");
        }
    }

    Action successAction;
    Action failAction;
    public void FirstShowInterstitialAd(Action successAction, Action failAction)
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            Debug.Log("Showing interstitial ad.");
            interstitialAd.Show();
            this.successAction = successAction;
            // AppsflyerEvent.LogEventAf("af_inters_logicgame");
        }
        else
        {
            failAction?.Invoke();
            Debug.LogWarning("Interstitial ad is not ready yet.");
        }
    }

    private void RegisterEventHandlers(InterstitialAd interstitialAd)
    {
        // Raised when the ad is estimated to have earned money.
        interstitialAd.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Interstitial ad paid {0} {1}.",
                adValue.Value,
                adValue.CurrencyCode));

            Dictionary<string, string> additionalParams = new Dictionary<string, string>();
            // additionalParams.Add(AdRevenueScheme.COUNTRY, MaxSdk.GetSdkConfiguration().CountryCode);
            // additionalParams.Add(AdRevenueScheme.AD_UNIT, interstitialAd.GetAdUnitID());
            // additionalParams.Add(AdRevenueScheme.AD_TYPE, "Inter");
            // additionalParams.Add(AdRevenueScheme.PLACEMENT, "InterPlace");
            // var logRevenue = new AFAdRevenueData("monetizationGoogleAdMob", MediationNetwork.GoogleAdMob, adValue.CurrencyCode, adValue.Value);
            // AppsFlyer.logAdRevenue(logRevenue, additionalParams);
        };
        // Raised when an impression is recorded for an ad.
        interstitialAd.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Interstitial ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        interstitialAd.OnAdClicked += () =>
        {
            Debug.Log("Interstitial ad was clicked.");
        };
        // Raised when an ad opened full screen content.
        interstitialAd.OnAdFullScreenContentOpened += () =>
        {
            var parameter = new[] {
                new AnalyticParameter("placement", interPlacement),
                new AnalyticParameter("level", levelInter),
            };
            AnalyticManager.Ins.LogEvent("ad_inter_show", parameter);

            //AppsflyerEvent.LogEventAf("af_inters_displayed");

            Debug.Log("Interstitial ad full screen content opened.");
        };
        // Raised when the ad closed full screen content.
        interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            LoadInterstitialAd();
            successAction?.Invoke();
            successAction = null;
            Debug.Log("Interstitial ad full screen content closed.");
        };
        // Raised when the ad failed to open full screen content.
        interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Interstitial ad failed to open full screen content " +
                           "with error : " + error);
        };
    }

    private void RegisterReloadHandler(InterstitialAd interstitialAd)
    {
        // Raised when the ad closed full screen content.
        interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Interstitial Ad full screen content closed.");

            // Reload the ad so that we can show another as soon as possible.
            LoadInterstitialAd();
        };
        // Raised when the ad failed to open full screen content.
        interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Interstitial ad failed to open full screen content " +
                           "with error : " + error);

            // Reload the ad so that we can show another as soon as possible.
            LoadInterstitialAd();
        };
    }

    public void DestroyInterADs()
    {
        interstitialAd.Destroy();
    }

    // -------------- Reward --------------- //


    // -------------- Native --------------- //

    public NativeAdOptions Option = new NativeAdOptions
    {
        AdChoicesPlacement = AdChoicesPlacement.BottomLeftCorner,
        MediaAspectRatio = MediaAspectRatio.Portrait,
    };

    public NativeTemplateStyle Style = new NativeTemplateStyle
    {
        TemplateId = NativeTemplateId.Medium,
    };

    private NativeOverlayAd nativeOverlayAd;

    public void LoadNativeAd()
    {
        // Clean up the old ad before loading a new one.
        if (nativeOverlayAd != null)
        {
            DestroyNativeAd();
        }

        Debug.Log("Loading native overlay ad.");

        // Create our request used to load the ad.
        var adRequest = new AdRequest();

        // Send the request to load the ad.
        NativeOverlayAd.Load(native_ID, adRequest, Option,
            (NativeOverlayAd ad, LoadAdError error) =>
            {
                // If the operation failed with a reason.
                if (error != null)
                {
                    Debug.LogError("Native Overlay ad failed to load an ad with error : " + error);
                    return;
                }
                // If the operation failed for unknown reasons.
                // This is an unexpected error, please report this bug if it happens.
                if (ad == null)
                {
                    Debug.LogError("Unexpected error: Native Overlay ad load event fired with " +
                        " null ad and null error.");
                    return;
                }

                // The operation completed successfully.
                Debug.Log("Native Overlay ad loaded with response : " + ad.GetResponseInfo());
                nativeOverlayAd = ad;

                // Register to ad events to extend functionality.
                RegisterEventHandlers(ad);
            });
    }

    private void RegisterEventHandlers(NativeOverlayAd ad)
    {
        // Raised when the ad is estimated to have earned money.
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Native Overlay ad paid {0} {1}.",
                adValue.Value,
                adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Native Overlay ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        ad.OnAdClicked += () =>
        {
            Debug.Log("Native Overlay ad was clicked.");
        };
        // Raised when the ad opened full screen content.
        ad.OnAdFullScreenContentOpened += () =>
        {
            LoadNativeAd();
            Debug.Log("Native Overlay ad full screen content opened.");
        };
        // Raised when the ad closed full screen content.
        ad.OnAdFullScreenContentClosed += () =>
        {
            LoadNativeAd();
            Debug.Log("Native Overlay ad full screen content closed.");
        };
    }

    public void ShowNativeAd()
    {
        if (nativeOverlayAd != null)
        {
            Debug.Log("Showing Native Overlay ad.");
            nativeOverlayAd.Show();
        }
    }

    public void HideNativeAd()
    {
        if (nativeOverlayAd != null)
        {
            Debug.Log("Hiding Native Overlay ad.");
            nativeOverlayAd.Hide();
        }
    }

    public void RenderNativeAd()
    {
        if (nativeOverlayAd != null)
        {
            Debug.Log("Rendering Native Overlay ad.");

            // Renders a native overlay ad at the default size
            // and anchored to the bottom of the screne.
            nativeOverlayAd.RenderTemplate(Style, AdPosition.Bottom);
        }
    }
    public void DestroyNativeAd()
    {
        if (nativeOverlayAd != null)
        {
            Debug.Log("Destroying Native Overlay ad.");
            nativeOverlayAd.Destroy();
            nativeOverlayAd = null;
        }
    }

    public void LogResponseInfo()
    {
        if (nativeOverlayAd != null)
        {
            var responseInfo = nativeOverlayAd.GetResponseInfo();
            if (responseInfo != null)
            {
                Debug.Log(responseInfo);
            }
        }
    }


}
