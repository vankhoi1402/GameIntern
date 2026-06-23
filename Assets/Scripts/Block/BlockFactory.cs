using UnityEngine;
using BlockSystem.Data;
using BlockSystem.Runtime;

namespace BlockSystem.Factory
{
    public class BlockFactory : MonoBehaviour
    {
        [Header("Assets Configuration")]
        [SerializeField] private BlockDatabase database;
        [SerializeField] private GameObject blockPrefab; // Prefab chỉ chứa script Block và BlockView độc lập

        /// <summary>
        /// Hàm cốt lõi: Chỉ đúc thực thể dựa trên dữ liệu, không quan tâm vị trí hay cha mẹ.
        /// </summary>
        public Block CreateBlock(BlockData data)
        {
            if (data == null || blockPrefab == null)
            {
                Debug.LogError("Factory chưa được cấu hình Database hoặc Prefab!");
                return null;
            }

            // Sinh ra ở vị trí gốc mặc định (0,0,0)
            GameObject obj = Instantiate(blockPrefab, Vector3.zero, Quaternion.identity);
            Block block = obj.GetComponent<Block>();

            if (block != null)
            {
                block.Init(data); // Nhà máy chỉ khai sinh và nạp Dữ liệu gốc
            }

            return block;
        }

        /// <summary>
        /// Tạo nhanh một Block với dữ liệu ngẫu nhiên từ Database
        /// </summary>
        public Block CreateRandomBlock()
        {
            BlockData randomData = GetRandomData();
            return CreateBlock(randomData);
        }

        /// <summary>
        /// Tạo nhanh một Block theo đúng loại (Type) yêu cầu
        /// </summary>
        public Block CreateBlockByType(BlockType type)
        {
            BlockData data = GetDataByType(type);
            return CreateBlock(data);
        }

        // --- CÁC HÀM BỔ TRỢ ĐỂ TẦNG LOGIC TRUY VẤN DỮ LIỆU ---

        public BlockData GetRandomData()
        {
            return database != null ? database.GetRandomNormalBlock() : null;
        }

        public BlockData GetDataByType(BlockType type)
        {
            return database != null ? database.GetBlockData(type) : null;
        }
    }
}