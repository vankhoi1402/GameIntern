using DG.Tweening;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using System.Collections;

public class IAPManager : Singleton<IAPManager>
{
    // --- 1. QUẢN LÝ TRẠNG THÁI GÓI MUA MỘT LẦN ---
    [field: SerializeField] public bool RemoveAds { get; private set; } = false;
    [field: SerializeField] public bool BoughtBeginnerPack { get; private set; } = false;

    [SerializeField] private ParticleSystem[] celebrateVFXs;

    // Biến lưu tạm phục vụ Analytics
    public int level;
    public string pack_id;
    public string placement;

    private StoreController m_StoreController;

    public void Awake()
    {
        RemoveAds = SaveManager.RemoveAds;
        BoughtBeginnerPack = SaveManager.BoughtBeginnerPack;
       // GameManager.Ins.RemoveAds = RemoveAds;
        // CHỌC VÀO ĐÂY: Nếu đã từng mua Remove Ads rồi thì ép hệ thống Ads về None luôn
        if (RemoveAds)
        {
            AdsManager.Ins.AdsType = AdsType.None;
        }

        InitializeIAP();
    }

    private void OnEnable()
    {
        EventManager.AddListener<OnIAPPurchase>(OnIAPPurchaseCallBack);
    }

    private void OnDisable()
    {
        // ĐÃ SỬA BUG: Đổi thành RemoveListener để tránh trùng lặp sự kiện
        EventManager.RemoveListener<OnIAPPurchase>(OnIAPPurchaseCallBack);
    }

    // --- 2. CALLBACK KHI MUA THÀNH CÔNG (NHẬN TỪ EVENT) ---
    private void OnIAPPurchaseCallBack(OnIAPPurchase e)
    {
        // Kích hoạt phát thưởng vật phẩm
        GrantProduct(e.Product.definition.id);

        // Lưu dữ liệu để Log Analytics
        this.level = e.Level;
        this.pack_id = e.Pack_id;
        this.placement = e.Placement;

        // Log dữ liệu lên Firebase/Analytic của bạn
        var parameters = new AnalyticParameter[]
        {
            new AnalyticParameter("level", level),
            new AnalyticParameter("pack_id", pack_id),
            new AnalyticParameter("placement", placement)
        };
        AnalyticManager.Ins.LogEvent("iapPurchased_confirmed", parameters);
    }

    // --- 3. LOGIC PHÁT THƯỞNG CHO 3 GÓI ---
    public void GrantProduct(string productId)
    {
        switch (productId)
        {
            case "com.piti.gameintern.removeads": // 1. Gói Xóa Ads
                RemoveAds = true;
                SaveManager.RemoveAds = true;
                GameManager.Ins.RemoveAds = true;
                // CHỌC VÀO ĐÂY: Chuyển loại Ads về None để tắt toàn bộ quảng cáo luôn!
                AdsManager.Ins.AdsType = AdsType.None;
                AdsManager.Ins.HindBanner(); // Ẩn Banner Ads luôn

                // Làm mới lại UI Home và UI Shop để cập nhật trạng thái mới
                if (GameManager.Ins.GameState == GameState.Play)
                {
                    UIManager.Ins.GetUI<GamePlayUI>().Configure2();
                  //  UIManager.Ins.GetUI<ShopUI>().Configure();
                }
                break;

            case "com.piti.gameintern.beginnerpack": // 2. Gói Tân Thủ
                BoughtBeginnerPack = true;
                SaveManager.BoughtBeginnerPack = true;
                SaveManager.RemoveAds = true;

                // CHỌC VÀO ĐÂY: Chuyển loại Ads về None để tắt toàn bộ quảng cáo luôn!
                AdsManager.Ins.AdsType = AdsType.None;
                AdsManager.Ins.HindBanner(); // Ẩn Banner Ads luôn

                // Thưởng gói tân thủ: Ví dụ cộng 2000 Vàng và các Booster
                // CurrencyManager.Ins.AddCoin(new Vector2(0, 100), 2000);
                BoosterManager.Ins.AddBoosterAmount(BoosterType.Hint, 20);
                BoosterManager.Ins.AddBoosterAmount(BoosterType.FrostTime, 20);
                BoosterManager.Ins.AddBoosterAmount(BoosterType.Hammer, 20);
                BoosterManager.Ins.AddBoosterAmount(BoosterType.Shuffle, 20);
                //BoosterManager.Ins.AddBoosterAmount(BoosterType.Reveal, 1);

                // Cập nhật lại UI để ẩn gói này đi (vì chỉ mua được 1 lần)
                if (GameManager.Ins.GameState == GameState.Home)
                {
                    UIManager.Ins.GetUI<HomeUI>().Configure();
                    UIManager.Ins.GetUI<GamePlayUI>()?.RefreshBoosterUI();
                   // UIManager.Ins.GetUI<ShopUI>().Configure();
                   // UIManager.Ins.GetUI<ShopUI>().FixTopSize();
                }
                GrantRewardInGamePlay(); // Tự hồi sinh nếu đang ở trạng thái Lose
                break;

            case "com.piti.gameintern.booster30": 
                BoosterManager.Ins.AddBoosterAmount(BoosterType.Hint, 30);
                BoosterManager.Ins.AddBoosterAmount(BoosterType.FrostTime, 30);
                BoosterManager.Ins.AddBoosterAmount(BoosterType.Hammer, 30);
                BoosterManager.Ins.AddBoosterAmount(BoosterType.Shuffle, 30);
                UIManager.Ins.GetUI<GamePlayUI>()?.RefreshBoosterUI();
                break;
        }

        // Chạy hiệu ứng hạt ăn mừng
        if (celebrateVFXs != null)
        {
            for (int i = 0; i < celebrateVFXs.Length; i++) celebrateVFXs[i].Play();
        }

        // Hiển thị Popup thông báo thành công sau 1.5 giây
        DOVirtual.DelayedCall(1.5f, () =>
        {
            OnNotice e = new OnNotice();
            e.Content = "Purchase successful! Thank you for your support!";
            EventManager.Trigger(e);
        });
    }

    // Logic hỗ trợ người chơi mua gói cứu trợ khi chuẩn bị Thua màn chơi
    public void GrantRewardInGamePlay()
    {
        if (GameManager.Ins.GameState == GameState.Play)
        {
            // if (PlayManager.Ins.CurrentState == GamePlayState.Lose)
            // {
            //     StartCoroutine(Sequence());
            //     IEnumerator Sequence()
            //     {
            //         yield return new WaitForSeconds(2f);
            //         UIManager.Ins.GetUI<OutOfTimeUI>().Close(0.25f);
            //         PlayManager.Ins.OnContinuePlay(); // Hồi sinh chơi tiếp
            //     }
            // }
        }
    }

    // --- 4. KHỞI TẠO HỆ THỐNG ENGINE (GIỮ NGUYÊN) ---
    async void InitializeIAP()
    {
        m_StoreController = UnityIAPServices.StoreController();
        await m_StoreController.Connect();
        m_StoreController.OnPurchasesFetched += OnPurchasesFetched;

        var initialProductsToFetch = new List<ProductDefinition>();
        var catalog = ProductCatalog.LoadDefaultCatalog();

        foreach (var product in catalog.allValidProducts)
        {
            initialProductsToFetch.Add(new ProductDefinition(product.id, product.type));
        }
        m_StoreController.FetchProducts(initialProductsToFetch);
    }

    void OnPurchasesFetched(Orders orders)
    {
        foreach (var order in orders.ConfirmedOrders)
        {
            foreach (var item in order.CartOrdered.Items())
            {
                GrantProduct(item.Product.definition.id);
            }
        }
    }
}