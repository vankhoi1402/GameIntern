using UnityEngine;
using BlockSystem.Data;

namespace BlockSystem.View
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class BlockView : MonoBehaviour
    {
        private SpriteRenderer _mainRenderer; // Ảnh icon chính (nằm trên chính nó)
        private SpriteRenderer _bgRenderer;   // Ảnh nền background (nằm ở Object con)

        private void Awake()
        {
            // 1. Tự lấy SpriteRenderer của chính GameObject này (Icon chính)
            _mainRenderer = GetComponent<SpriteRenderer>();

            // 2. TỰ ĐỘNG TÌM CON: Tìm Object con tên là "Background" ngoài Hierarchy để lấy component
            Transform bgTransform = transform.Find("Background");
            if (bgTransform != null)
            {
                _bgRenderer = bgTransform.GetComponent<SpriteRenderer>();
            }
            else
            {
                Debug.LogWarning($"[BlockView] Không tìm thấy Object con nào tên là 'Background' dưới {gameObject.name} đâu ông ơi!");
            }
        }

        public void Initialize(BlockData data, float cellWidth, float cellHeight)
        {
            if (_mainRenderer == null) _mainRenderer = GetComponent<SpriteRenderer>();

            _mainRenderer.sprite = data.VisualSprite;
            _mainRenderer.color = data.DebugColor;

            // Kích hoạt hàm co giãn thông minh
            ScaleToFitCell(cellWidth, cellHeight);
        }

        private void ScaleToFitCell(float cellWidth, float cellHeight)
        {
            // 👉 BƯỚC 1: Scale cục CHA trước (Chứa Icon chính) để vừa khít ô cờ (thụt lề 85% cho đẹp)
            if (_mainRenderer.sprite != null)
            {
                float spriteWidth = _mainRenderer.sprite.bounds.size.x;
                float spriteHeight = _mainRenderer.sprite.bounds.size.y;

                float targetScaleX = (cellWidth / spriteWidth) * 0.85f; // Nhân 0.85 để icon lọt lòng
                float targetScaleY = (cellHeight / spriteHeight) * 0.85f;

                transform.localScale = new Vector3(targetScaleX, targetScaleY, 1f);
            }

            // 👉 BƯỚC 2: Xử lý cục CON (Background) để nó vừa khít 100% ô cờ mà KHÔNG bị méo theo cha
            if (_bgRenderer != null && _bgRenderer.sprite != null)
            {
                float bgWidth = _bgRenderer.sprite.bounds.size.x;
                float bgHeight = _bgRenderer.sprite.bounds.size.y;

                // Toán học triệt tiêu tỷ lệ thừa: Lấy kích thước ô cờ chia cho kích thước ảnh nền, 
                // sau đó CHIA TIẾP cho scale của cha để triệt tiêu lực kéo biến dạng từ cha truyền xuống con!
                float bgScaleX = (cellWidth / bgWidth) / transform.localScale.x;
                float bgScaleY = (cellHeight / bgHeight) / transform.localScale.y;

                _bgRenderer.transform.localScale = new Vector3(bgScaleX * 0.98f, bgScaleY * 0.98f, 1f);
            }
        }

        public void MoveToPosition(Vector3 targetWorldPos)
        {
            transform.position = targetWorldPos;
        }
    }
}