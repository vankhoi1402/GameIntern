using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

    public class IAPContainer : MonoBehaviour
    {

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private CodelessIAPButton btn;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI realPriceText;

        [SerializeField] private bool isCoinPack = false;
        [SerializeField] private int coinNumber;
        [SerializeField] private TextMeshProUGUI coinNameText;
        private Product product;

        [Header("Tutorial")]
        [SerializeField] private BaseButton vipInfoBtn;
        [SerializeField] private GameObject infoTab;
        private Tween infoOpenTween;

        private void Awake()
        {
            if(infoTab)
                infoTab.gameObject.SetActive(false);

            if(vipInfoBtn)
                vipInfoBtn.AddListener(TryOpenInfo);
        }

        private void OnEnable()
        {
            if (isCoinPack)
                coinNameText.text = coinNumber.ToString();
        }

        public void OpenAnimation(float delay)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.25f).SetEase(Ease.Linear).SetDelay(delay);
            transform.DOScale(Vector3.one, 0.25f).From(Vector3.one * 1.2f).SetEase(Ease.OutBack).SetDelay(delay);
        }


        public void Initialize()
        {
            btn.onProductFetched.AddListener(OnPackFetch);
            btn.onPurchaseCompleteLegacy.AddListener(OnPackBoughtSuccess);
            btn.onPurchaseFailed.AddListener(OnPurchaseFail);
        }

        private void OnPackFetch(Product product)
        {
            priceText.text = product.metadata.localizedPriceString;
            //    var (number, symbol) = Help.ParseCurrency(product.metadata.localizedPriceString);
            //    realPriceText.text = symbol + (number * 10).ToString();
        }

        private void OnPackBoughtSuccess(Product product)
        {
            Debug.Log($"[IAP] Mua OK: {product.definition.id}");
            OnIAPPurchase e = new OnIAPPurchase();
            e.Product = product;
           // e.Level = GameData.LevelCompleted + 1;
            e.Pack_id = product.definition.id;
            e.Pack_name = product.definition.id;
            e.Placement = "shop";
            EventManager.Trigger(e);

            if (GameManager.Ins.GameState == GameState.Play)
            {
               // UIManager.Ins.GetUI<GameplayUI>().UpdateBoosterDisplay();
               // HighCanvas.Ins.SetActive(false);
            }
        }

        private void OnPurchaseFail(FailedOrder failedOrder)
        {
            Debug.Log($"[IAP] Mua Thất Bại: ");
            OnNotice e = new OnNotice();
            e.Content = "Purchase Failed";
            EventManager.Trigger(e);
        }

        private void TryOpenInfo()
        {
            infoOpenTween?.Kill();

            infoTab.transform.localScale = Vector3.zero;
            infoTab.gameObject.SetActive(true);

            infoOpenTween = infoTab.transform.DOScale(1f, 0.25f)
                                                 .OnComplete(() =>
                                                 {
                                                     infoOpenTween = DOVirtual.DelayedCall(1.5f, () =>
                                                     {
                                                         infoOpenTween = infoTab.transform.DOScale(0f, 0.25f)
                                                             .OnComplete(() =>
                                                             {
                                                                 infoTab.gameObject.SetActive(false);
                                                                 infoOpenTween = null;
                                                             });
                                                     });
                                                 });
        }

        public void Off()
        {
            gameObject.SetActive(false);
        }
    }

