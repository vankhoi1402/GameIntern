using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using System;
using System.Collections.Generic;
using UnityEngine;

public class FirebaseManager : Singleton<FirebaseManager>
{
    private FirebaseApp app;

    public float CappingInter = 30;
    public float CappingReward = 30;
    public int LevelShowInter = 7;
    public int BoosterFree = 2;

    public bool DoneInitFirebase { get; private set; } = false;
    public bool DoneRemoteConfig { get; private set; } = false;

    public event Action OnRemoteConfigReady;

    public void Initialize()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"[Firebase] Dependency error: {task.Exception}");
                return;
            }

            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                app = FirebaseApp.DefaultInstance;
                DoneInitFirebase = true;
                Debug.Log("[FIREBASE] Init Done");
                InitializeFirebaseRemoteConfig();
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        });
    }

    async void InitializeFirebaseRemoteConfig()
    {
        try
        {
            FirebaseRemoteConfig remoteConfig = FirebaseRemoteConfig.DefaultInstance;

            // Apply zero-cache settings before Fetch so Editor always hits the server.
            ConfigSettings settings = new ConfigSettings
            {
                MinimumFetchIntervalInMilliseconds = 0
            };
            await remoteConfig.SetConfigSettingsAsync(settings);

            Dictionary<string, object> defaults = new Dictionary<string, object>
            {
                { "booster_free", BoosterFree },
                { "level_show_inter", LevelShowInter },
                { "capping_time_inter", CappingInter },
                { "capping_time_reward", CappingReward },
            };
            await remoteConfig.SetDefaultsAsync(defaults);
            Debug.Log("[Firebase RC] Defaults set.");

            await remoteConfig.FetchAsync(TimeSpan.Zero);
            bool activated = await remoteConfig.ActivateAsync();
            Debug.Log($"[Firebase RC] Activate result: {activated}");

            LevelShowInter = (int)remoteConfig.GetValue("level_show_inter").LongValue;
            CappingInter = (float)remoteConfig.GetValue("capping_time_inter").DoubleValue;
            CappingReward = (float)remoteConfig.GetValue("capping_time_reward").DoubleValue;
            BoosterFree = (int)remoteConfig.GetValue("booster_free").LongValue;

            Debug.Log($"[Firebase RC] LevelShowInter={LevelShowInter}, CappingInter={CappingInter}, CappingReward={CappingReward}, BoosterFree={BoosterFree}");

            DoneRemoteConfig = true;
            OnRemoteConfigReady?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Firebase RC] Failed: {ex}");
        }
    }

    public void LogEvent(string name, AnalyticParameter[] analyticParam = null)
    {
        try
        {
            if (DoneInitFirebase)
            {
                if (analyticParam == null || analyticParam.Length == 0)
                {
                    FirebaseAnalytics.LogEvent(name);
                    return;
                }
                else
                {
                    List<Parameter> parameterList = new List<Parameter>();
                    for (int i = 0; i < analyticParam.Length; i++)
                    {
                        switch (analyticParam[i].Value)
                        {
                            case string strValue:
                                parameterList.Add(new Parameter(analyticParam[i].Name, strValue));
                                break;
                            case int intValue:
                                parameterList.Add(new Parameter(analyticParam[i].Name, intValue));
                                break;
                            case float floatValue:
                                parameterList.Add(new Parameter(analyticParam[i].Name, floatValue));
                                break;
                            case double doubleValue:
                                parameterList.Add(new Parameter(analyticParam[i].Name, doubleValue));
                                break;
                            case long longValue:
                                parameterList.Add(new Parameter(analyticParam[i].Name, longValue));
                                break;
                            default:
                                Debug.LogWarning($"Unsupported parameter type for {analyticParam[i].Name}: {analyticParam[i].Value.GetType()}");
                                parameterList.Add(new Parameter(analyticParam[i].Name, analyticParam[i].Value.ToString()));
                                break;
                        }
                    }

                    FirebaseAnalytics.LogEvent(name, parameterList.ToArray());
                }
            }
            else
            {
                Debug.LogWarning("Firebase not init done, can't send log event");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Firebase Event Error : " + e.ToString());
        }
    }
}
