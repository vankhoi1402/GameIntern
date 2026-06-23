using UnityEngine;
using System;
using BlockSystem.Runtime;

namespace Game.InputSystem
{
    public class InputEventBus : MonoBehaviour
    {
        public static InputEventBus Instance { get; private set; }

        public event Action<Block> OnDragStarted;
        public event Action<Block, Vector3> OnDragging;
        public event Action<Block> OnDragEnded;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // Giữ qua các Scene nếu cần
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void RaiseDragStarted(Block block) => OnDragStarted?.Invoke(block);
        public void RaiseDragging(Block block, Vector3 mouseWorldPos) => OnDragging?.Invoke(block, mouseWorldPos);
        public void RaiseDragEnded(Block block) => OnDragEnded?.Invoke(block);
    }
}