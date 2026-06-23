using System;
using UnityEngine;

/// <summary>
/// Trạng thái bàn cờ — dùng để khóa input khi đang xử lý merge/gravity.
/// </summary>
public enum BoardState { Idle, ProcessingMove, ResolvingMerges, ApplyingGravity, ApplyingRefill }

/// <summary>
/// Singleton quản lý state machine bàn cờ. Chỉ cho input khi Idle.
/// </summary>
public class BoardStateManager : MonoBehaviour
{
    public static BoardStateManager Instance { get; private set; }

    [SerializeField] private BoardState currentState = BoardState.Idle;
    public BoardState CurrentState => currentState;

    public event Action<BoardState> OnStateChanged;

    /// <summary>Đăng ký singleton khi Awake.</summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>Chuyển trạng thái bàn cờ và phát event.</summary>
    public void ChangeState(BoardState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        OnStateChanged?.Invoke(currentState);
    }

    /// <summary>True khi người chơi được phép kéo-thả block.</summary>
    public bool CanAcceptInput() => currentState == BoardState.Idle;
}
