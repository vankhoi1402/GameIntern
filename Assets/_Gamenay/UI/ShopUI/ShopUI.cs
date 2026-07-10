using UnityEngine;
using UnityEngine.UI;

public class ShopUI : UICanvas
{
    [Header("UI Components")]
    [SerializeField] private GameObject shopPanel; // Bảng giao diện Shop (để bật/tắt)
    [SerializeField] private Button closeBtn;       // Nút đóng Shop

    [Header("IAP System")]
   
    [SerializeField] private IAPContainer[] iapContainers; // Mảng chứa đúng 3 ô gói nạp

    private void Awake()
    {
        if (closeBtn)
            closeBtn.onClick.AddListener(CloseShop);
    }

    private void Start()
    {
        // Khởi tạo toàn bộ Shop khi game bắt đầu
        InitShop();
    }

    public void OpenShop()
    {
       // shopPanel.SetActive(true);
        
        // Kích hoạt hiệu ứng bay vào/nảy của từng ô vật phẩm (DOTween)
        for (int i = 0; i < iapContainers.Length; i++)
        {
            if (iapContainers[i].gameObject.activeSelf)
            {
                // Cho các ô xuất hiện lệch nhau 0.05 giây nhìn cho đẹp (Delay)
                iapContainers[i].OpenAnimation(i * 0.05f); 
            }
        }
    }

    public void CloseShop()
    {
       UIManager.Ins.CloseUI<ShopUI>();
       PlayManager.Ins.OnResumeGame();
    }

    private void InitShop()
    {
        // 1. Kích hoạt IAP trên từng nút bấm của 3 gói
        for (int i = 0; i < iapContainers.Length; i++)
        {
            if (iapContainers[i] != null)
            {
                iapContainers[i].Initialize();
            }
        }

        // 2. Cấu hình phân giải màn hình cho thanh cuộn IAPScroll
        // (Hàm Awake của IAPScroll đã tự động chạy, nhưng gọi thêm ở đây cho chắc chắn nếu cần)
    }
}