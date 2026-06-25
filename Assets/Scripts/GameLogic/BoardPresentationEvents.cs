using System;
using System.Collections.Generic;

/// <summary>Event presentation — gameplay không phụ thuộc BlockAnimationManager.</summary>
public static class BoardPresentationEvents
{
    public static event Action<IReadOnlyList<ColumnRefillPacket>, Action> RefillWaveRequested;

    public static void RequestRefillWaveAnimation(IReadOnlyList<ColumnRefillPacket> wave, Action onComplete)
    {
        if (RefillWaveRequested != null)
            RefillWaveRequested.Invoke(wave, onComplete);
        else
            onComplete?.Invoke();
    }
}
