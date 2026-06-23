using UnityEngine;
using UnityEngine.EventSystems;
using BlockSystem.Runtime;
using System;
using Game.Logic;

namespace Game.InputSystem
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private LayerMask blockLayer;
        [SerializeField] private Camera targetCamera;

        private Block _currentDraggedBlock;
        private bool _isInputLocked = false;

        public void SetLockInput(bool isLocked) => _isInputLocked = isLocked;
        public bool IsInputLocked => _isInputLocked;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (targetCamera == null) targetCamera = Camera.main;
        }

        private void Update()
        {
            // ĐÃ NÂNG CẤP: Chỉ cho phép click khi bàn cờ hoàn toàn IDLE (Rảnh rỗi)
            if (BoardStateManager.Instance == null || !BoardStateManager.Instance.CanAcceptInput()) return;
            if (targetCamera == null || InputEventBus.Instance == null) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (Input.GetMouseButtonDown(0)) return;
            }

            HandlePointerInput();
        }

        private void HandlePointerInput()
        {
            Vector3 mouseWorldPos = targetCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;

            if (Input.GetMouseButtonDown(0))
            {
                Block hitBlock = GetBlockAtPosition(mouseWorldPos);
                if (hitBlock != null)
                {
                    _currentDraggedBlock = hitBlock;
                    InputEventBus.Instance.RaiseDragStarted(_currentDraggedBlock);
                }
            }

            if (Input.GetMouseButton(0) && _currentDraggedBlock != null)
            {
                InputEventBus.Instance.RaiseDragging(_currentDraggedBlock, mouseWorldPos);
            }

            if (Input.GetMouseButtonUp(0) && _currentDraggedBlock != null)
            {
                InputEventBus.Instance.RaiseDragEnded(_currentDraggedBlock);
                _currentDraggedBlock = null;
            }
        }

        // Kiến trúc Supercell: Raycast xuyên thấu và sắp xếp độ ưu tiên hiển thị chuẩn tuyệt đối
        private Block GetBlockAtPosition(Vector2 pos)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(pos, Vector2.zero, 0f, blockLayer);
            if (hits.Length == 0) return null;

            // Sắp xếp các vật thể va chạm theo độ cao Sorting Order trước, nếu bằng nhau thì tính theo Z vật lý
            Array.Sort(hits, (a, b) =>
            {
                var sA = a.collider.GetComponent<SpriteRenderer>();
                var sB = b.collider.GetComponent<SpriteRenderer>();
                if (sA != null && sB != null) return sB.sortingOrder.CompareTo(sA.sortingOrder);
                return b.transform.position.z.CompareTo(a.transform.position.z);
            });

            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider.TryGetComponent<Block>(out var block))
                {
                    return block;
                }
            }
            return null;
        }
    }
}