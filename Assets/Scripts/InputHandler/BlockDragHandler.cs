using UnityEngine;
using BlockSystem.Runtime;

namespace Game.InputSystem
{
    [RequireComponent(typeof(Block))]
    public class BlockDragHandler : MonoBehaviour
    {
        private Block _block;
        private Camera _mainCamera;

        private void Awake()
        {
            _block = GetComponent<Block>();
            _mainCamera = Camera.main;
        }

        private void OnMouseDown()
        {
            Debug.Log($"Đã chạm vào: {_block.name}");
            if (InputEventBus.Instance != null)
                InputEventBus.Instance.RaiseDragStarted(_block);
        }

        private void OnMouseDrag()
        {
            if (_mainCamera == null || InputEventBus.Instance != null == false) return;

            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;
            Debug.Log($"Đang di chạm vào: {_block.name}");

            InputEventBus.Instance.RaiseDragging(_block, mouseWorldPos);
        }

        private void OnMouseUp()
        {
            if (InputEventBus.Instance != null)
                InputEventBus.Instance.RaiseDragEnded(_block);
        }
    }
}