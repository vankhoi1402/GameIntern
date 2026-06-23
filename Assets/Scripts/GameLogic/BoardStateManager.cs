using UnityEngine;
using System;

namespace Game.Logic
{
    public enum BoardState { Idle, ProcessingMove, ResolvingMerges, ApplyingGravity }

    public class BoardStateManager : MonoBehaviour
    {
        public static BoardStateManager Instance { get; private set; }

        [SerializeField] private BoardState currentState = BoardState.Idle;
        public BoardState CurrentState => currentState;

        public event Action<BoardState> OnStateChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void ChangeState(BoardState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            OnStateChanged?.Invoke(currentState);
            Debug.Log($"[BOARD STATE] -> {currentState} (Frame: {Time.frameCount})");
        }

        public bool CanAcceptInput() => currentState == BoardState.Idle;
    }
}