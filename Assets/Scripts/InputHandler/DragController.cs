using UnityEngine;
using System;
using Game.Grid;
using Game.Logic;
using Game.View;
using BlockSystem.Runtime;

namespace Game.InputSystem
{
    public enum DragState { Idle, Dragging }

    public class DragController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private MoveSystem moveSystem;
        [SerializeField] private Camera targetCamera;

        private Slot _sourceSlot;
        private DragState _currentState = DragState.Idle;
        private Vector3 _dragOffset;

        public event Action<Block, Vector3> OnBlockVisualDragged;
        public event Action<Block, Slot> OnBlockVisualReset;
        public event Action<Block> OnBlockSelected;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void Start()
        {
            if (InputEventBus.Instance != null)
            {
                InputEventBus.Instance.OnDragStarted += HandleDragStart;
                InputEventBus.Instance.OnDragging += HandleDragging;
                InputEventBus.Instance.OnDragEnded += HandleDragEnd;
            }
        }

        private void OnDestroy()
        {
            if (InputEventBus.Instance != null)
            {
                InputEventBus.Instance.OnDragStarted -= HandleDragStart;
                InputEventBus.Instance.OnDragging -= HandleDragging;
                InputEventBus.Instance.OnDragEnded -= HandleDragEnd;
            }
        }

        private void HandleDragStart(Block block)
        {
            if (_currentState != DragState.Idle || block == null) return;

            _sourceSlot = block.CurrentSlot;
            if (_sourceSlot != null)
            {
                _currentState = DragState.Dragging;

                // Tính toán độ lệch tâm chuột (UX chuẩn thương mại)
                Vector3 mousePos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
                mousePos.z = 0;
                _dragOffset = block.transform.position - mousePos;

                OnBlockSelected?.Invoke(block);
            }
        }

        private void HandleDragging(Block block, Vector3 mouseWorldPos)
        {
            if (_currentState != DragState.Dragging || block == null) return;

            Vector3 targetVisualPos = mouseWorldPos + _dragOffset;
            OnBlockVisualDragged?.Invoke(block, targetVisualPos);
        }

        private void HandleDragEnd(Block block)
        {
            if (_currentState != DragState.Dragging || block == null) return;
            _currentState = DragState.Idle;

            var layout = boardView.Layout;
            if (layout != null && targetCamera != null)
            {
                Vector3 dropWorldPos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
                dropWorldPos.z = 0;

                Vector2Int gridPos = layout.GetGridPosition(dropWorldPos);
                Slot targetSlot = boardManager.GetSlot(gridPos.x, gridPos.y);

                if (targetSlot != null && targetSlot != _sourceSlot)
                {
                    moveSystem.ProcessMove(_sourceSlot, targetSlot);
                    return;
                }
            }

            ReturnBlockToSource(block);
        }

        private void ReturnBlockToSource(Block block)
        {
            if (_sourceSlot != null)
            {
                OnBlockVisualReset?.Invoke(block, _sourceSlot);
            }
        }
    }
}