using System;
using System.Collections;
using UnityEngine;

/// <summary>Sau T3: ủy quyền gravity + refill bin cho BoardSettlementService.</summary>
public class DesignRefillController : MonoBehaviour
{
    [SerializeField] private BoardSettlementService settlementService;

    private bool m_IsRefilling;

    private void Awake()
    {
        if (settlementService == null)
            settlementService = FindObjectOfType<BoardSettlementService>();
    }

    public void OnT3Cleared(Action onComplete)
    {
        if (m_IsRefilling)
        {
            Debug.LogWarning("[DesignRefill] Pipeline refill đang chạy — bỏ qua lệnh trùng.");
            return;
        }

        if (settlementService == null)
        {
            Debug.LogError("[DesignRefill] Chưa gán BoardSettlementService.");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(T3RefillRoutine(onComplete));
    }

    private IEnumerator T3RefillRoutine(Action onComplete)
    {
        m_IsRefilling = true;

        try
        {
            yield return settlementService.RunGravityThenRefillRoutine(null);
        }
        finally
        {
            m_IsRefilling = false;
            onComplete?.Invoke();
        }
    }
}
