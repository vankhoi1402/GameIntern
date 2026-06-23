using UnityEngine;
using Game.Grid;
using BlockSystem.Runtime;
using BlockSystem.Data; // Bổ sung namespace chứa BlockData của ông

namespace Game.Logic
{
    public class SpawnSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private Block blockPrefab;

        [Header("Spawn Settings")]
        //[SerializeField, Range(1, 8)] private int initialRowsToFill = 8;

        // ĐÃ MỞ KHÓA: Mảng này dùng để chứa danh sách các loại BlockData (ví dụ: khối số 2, số 4, số 8...)
        [SerializeField] private BlockData[] possibleBlockData;

        private void Start()
        {
            Invoke(nameof(GenerateInitialBoard), 0.1f);
        }

        public void GenerateInitialBoard()
        {
            if (boardManager == null || blockPrefab == null)
            {
                Debug.LogError("SpawnSystem: Thiếu Reference kìa ông ơi!");
                return;
            }

            // Sửa chỗ này: Chạy từ 0 đến boardManager.Rows thay vì biến initialRowsToFill
            for (int r = 0; r < boardManager.Rows; r++)
            {
                for (int c = 0; c < boardManager.Columns; c++)
                {
                    SpawnRandomBlock(r, c);
                }
            }
        }

        private void SpawnRandomBlock(int row, int col)
        {
            // 1. Đẻ ra cái xác (GameObject)
            Block newBlock = Instantiate(blockPrefab);

            // 2. ĐÃ SỬA: Gọi đúng tên hàm Init() có trong Block.cs của ông
            if (possibleBlockData != null && possibleBlockData.Length > 0)
            {
                BlockData randomData = possibleBlockData[Random.Range(0, possibleBlockData.Length)];

                // Đổi Initialize thành Init
                newBlock.Init(randomData);
            }
            else
            {
                Debug.LogError("SpawnSystem: Chưa kéo thả BlockData vào mảng Possible Block Data!");
            }

            // 3. Ném vào BoardManager để quản lý logic
            boardManager.PlaceBlock(newBlock, row, col);
        }
    }
}