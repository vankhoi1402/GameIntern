using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    public enum AnalyticType
    {
        None,
        Firebase,
        IronSource,
    }

    public class AnalyticParameter : IDisposable
    {
        internal string Name { get; set; }

        internal object Value { get; set; }

        public AnalyticParameter(string parameterName, string parameterValue)
        {
            Name = parameterName;
            Value = parameterValue;
        }

        public AnalyticParameter(string parameterName, long parameterValue)
        {
            Name = parameterName;
            Value = parameterValue;
        }

        public AnalyticParameter(string parameterName, double parameterValue)
        {
            Name = parameterName;
            Value = parameterValue;
        }

        public AnalyticParameter(string parameterName, IDictionary<string, object> parameterValue)
        {
            Name = parameterName;
            Value = parameterValue;
        }

        public AnalyticParameter(string parameterName, IEnumerable<IDictionary<string, object>> parameterValue)
        {
            Name = parameterName;
            Value = parameterValue;
        }

        [Obsolete("No longer needed, will be removed in the future.")]
        public void Dispose()
        {
        }

        [Obsolete("No longer needed, will be removed in the future.")]
        public void Dispose(bool disposing)
        {
        }
    }

    public class AnalyticManager : Singleton<AnalyticManager>
    {
        public AnalyticType AnalyticType;

        public void Initialize()
        {
            if (AnalyticType == AnalyticType.Firebase)
                FirebaseManager.Ins.Initialize();
        }

        public void LogEvent(string name, AnalyticParameter[] param = null)
        {
            if (Application.isEditor || Debug.isDebugBuild) return;
            if (AnalyticType == AnalyticType.Firebase && FirebaseManager.Ins.DoneInitFirebase)
            {
                FirebaseManager.Ins.LogEvent(name, param);
            }
            else
                Debug.LogWarning("Firebase not init done, can't send log event");

        }
    }
