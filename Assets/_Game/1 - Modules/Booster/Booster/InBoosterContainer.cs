using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InBoosterContainer : MonoBehaviour
{
    [SerializeField] private BaseButton btn;
    [SerializeField] private GameObject amountSection;
    [SerializeField] private GameObject addSection;
    [SerializeField] private CanvasGroup freeSection;
    [SerializeField] private CanvasGroup levelSection;
    [SerializeField] private GameObject lockSection;
    [SerializeField] private CanvasGroup tutorialSection;
    [SerializeField] private CanvasGroup levelUnlockSection;
    [SerializeField] private TextMeshProUGUI levelUnlockText;
    [SerializeField] private Image icon;
    [SerializeField] private Image lockBG;
    [SerializeField] private Image lockImage;
    [SerializeField] private Image fakeIcon; // dùng cho lúc mở khoá booster
    [SerializeField] private Sprite lockOpenSprite;
    [SerializeField] private ParticleSystem vfx;
    [SerializeField] private TextMeshProUGUI amountDisplay;

    public RectTransform Rect;
    private Tween tweenIconBoosterSuggest;

    public Booster Booster { get; private set; }

    private void Awake()
    {
      //  btn.AddListener(BoosterClick);
    }

    public void ShowAnimSuggest(bool isSuggest)
    {
        if (isSuggest)
        {
            tweenIconBoosterSuggest =
                icon.transform.DOScale(1.2f, 0.8f).SetEase(Ease.Linear).SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            icon.transform.localScale = Vector3.one;
            tweenIconBoosterSuggest?.Kill();
        }
    }

    public void ShowTutorialHighlight(bool isShow)
    {
        tutorialSection.gameObject.SetActive(isShow);
    }

    public void LockState()
    {
        levelUnlockSection.gameObject.SetActive(true);
        freeSection.gameObject.SetActive(false);
        addSection.gameObject.SetActive(false);
        amountSection.gameObject.SetActive(false);
        levelSection.gameObject.SetActive(true);
        lockSection.SetActive(true);
    }

    // public void UnlockState()
    // {
    //     Configure(Booster);
    // }

    // public void Configure(Booster booster, bool active = false)
    // {
    //     Booster = booster;
    //     amountDisplay.text = booster.Amount.ToString();

    //     if (!booster.Unlock)
    //     {
    //         levelUnlockSection.gameObject.SetActive(true);
    //         levelUnlockText.text = "Level" + " " + (booster.BoosterLevelUnlock).ToString();
    //         icon.sprite = booster.BoosterIcon;
    //         fakeIcon.sprite = booster.BoosterIcon;
    //         freeSection.gameObject.SetActive(false);
    //         addSection.gameObject.SetActive(false);
    //         amountSection.gameObject.SetActive(false);
    //         levelSection.gameObject.SetActive(true);
    //         lockSection.SetActive(true);
    //     }
    //     else
    //     {
    //         levelSection.gameObject.SetActive(false);
    //         lockSection.SetActive(false);
    //         icon.sprite = booster.BoosterIcon;
    //         fakeIcon.sprite = booster.BoosterIcon;
    //         levelUnlockSection.gameObject.SetActive(false);

    //         if (booster.FirstUse)
    //         {
    //             freeSection.gameObject.SetActive(true);
    //             addSection.gameObject.SetActive(false);
    //             amountSection.gameObject.SetActive(false);
    //         }
    //         else
    //         {
    //             if (booster.Amount == 0)
    //             {
    //                 freeSection.gameObject.SetActive(false);
    //                 addSection.gameObject.SetActive(true);
    //                 amountSection.gameObject.SetActive(false);
    //             }
    //             else
    //             {
    //                 freeSection.gameObject.SetActive(false);
    //                 addSection.gameObject.SetActive(false);
    //                 amountSection.gameObject.SetActive(true);
    //             }
    //         }
    //     }
    // }

    // private void BoosterClick()
    // {

    //     if (!Booster.Unlock)
    //     {
    //         OnNotice e = new OnNotice();
    //         e.Content = "Unlock at level " + (Booster.BoosterLevelUnlock).ToString();
    //         EventManager.Trigger(e);

    //         return;
    //     }

    //     if (Booster.FirstUse)
    //     {
    //         float delayToCallGiftUI = 1f;

    //         switch (Booster.BoosterType)
    //         {
    //             case BoosterType.Hint:
    //                 PlayManager.Ins.OnUseBoosterHint();
    //                 UIManager.Ins.GetUI<GameplayUI>().ActiveBoosterTutorialBG(false, Booster.BoosterType);
    //                 DOVirtual.DelayedCall(delayToCallGiftUI, () =>
    //                 {
    //                     OnNotice e = new OnNotice();
    //                     e.Content = "You get it now!";
    //                     EventManager.Trigger(e);

    //                     // UIManager.Ins.OpenUI<BoosterGiftUI>();
    //                     // UIManager.Ins.GetUI<BoosterGiftUI>().Configure(Booster);
    //                 });
    //                 break;
    //             case BoosterType.Reveal:
    //                 PlayManager.Ins.OnUseBoosterReveal();
    //                 UIManager.Ins.GetUI<GameplayUI>().ActiveBoosterTutorialBG(false, Booster.BoosterType);
    //                 DOVirtual.DelayedCall(delayToCallGiftUI, () =>
    //                 {
    //                     OnNotice e = new OnNotice();
    //                     e.Content = "You get it now!";
    //                     EventManager.Trigger(e);

    //                     // UIManager.Ins.OpenUI<BoosterGiftUI>();
    //                     // UIManager.Ins.GetUI<BoosterGiftUI>().Configure(Booster);
    //                 });

    //                 break;
    //             case BoosterType.FrostTime:
    //                 if (PlayManager.Ins.IsFreezeTimeActive)
    //                 {
    //                     OnNotice e = new OnNotice();
    //                     e.Content = "Frost Time is still active!";
    //                     EventManager.Trigger(e);
    //                     return;
    //                 }
    //                 PlayManager.Ins.OnUseBoosterFrostTime();
    //                 UIManager.Ins.GetUI<GameplayUI>().ActiveBoosterTutorialBG(false, Booster.BoosterType);
    //                 DOVirtual.DelayedCall(delayToCallGiftUI * 2.5f, () =>
    //                 {
    //                     OnNotice e = new OnNotice();
    //                     e.Content = "You get it now!";
    //                     EventManager.Trigger(e);
                        
    //                     // UIManager.Ins.OpenUI<BoosterGiftUI>();
    //                     // UIManager.Ins.GetUI<BoosterGiftUI>().Configure(Booster);
    //                 });
    //                 break;
    //             default:
    //                 break;
    //         }
    //     }
    //     else
    //     {
    //         if (Booster.Amount == 0)
    //         {
    //             UIManager.Ins.OpenUI<BuyBoosterUI>();
    //             UIManager.Ins.GetUI<BuyBoosterUI>().Configure(Booster);
    //         }
    //         else
    //         {
    //             switch (Booster.BoosterType)
    //             {
    //                 case BoosterType.Hint:
    //                     PlayManager.Ins.OnUseBoosterHint();
    //                     break;
    //                 case BoosterType.Reveal:
    //                     PlayManager.Ins.OnUseBoosterReveal();
    //                     break;
    //                 case BoosterType.FrostTime:
    //                     if (PlayManager.Ins.IsFreezeTimeActive)
    //                     {
    //                         OnNotice e = new OnNotice();
    //                         e.Content = "Frost Time is still active!";
    //                         EventManager.Trigger(e);
    //                         return;
    //                     }
    //                     PlayManager.Ins.OnUseBoosterFrostTime();
    //                     break;
    //                 default:
    //                     break;
    //             }
    //         }
    //     }
    // }

    // public Vector3 GetBoosterWorldPosition()
    // {
    //     if (Rect == null) return Vector3.zero;
    //     Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, Rect.position);
    //     Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
    //     worldPos.z = 0f; // game 2D thường để z = 0

    //     return worldPos;
    // }


    // [Button(ButtonSizes.Gigantic)]
    // public void UnlockBoosterContainer()
    // {
    //     GameManager.Ins.Block(true); // Block tương tác khi đang chạy animation mở khoá
    //     icon.gameObject.SetActive(false);
    //     fakeIcon.gameObject.SetActive(true);
    //     lockImage.sprite = lockOpenSprite;

    //     if (Booster != null)
    //         icon.sprite = Booster.BoosterIcon;

    //     Sequence seq = DOTween.Sequence();

    //     // 👉 Setup parent đúng cách (GIỮ world position)
    //     Transform canvas = GameManager.Ins.MainCanvasRect.transform;
    //     fakeIcon.transform.SetParent(canvas, true);
    //     fakeIcon.transform.localScale = Vector3.one;

    //     Vector3 startPos = new Vector3(0, 69, 0); // Vị trí bắt đầu (trên canvas)
    //     Vector3 targetPos = transform.position;
    //     fakeIcon.rectTransform.anchoredPosition = startPos;

    //     // ─────────────────────────────
    //     // 🟡 FAKE ICON JUMP VỀ SLOT
    //     // ─────────────────────────────
    //     seq.Append(
    //         fakeIcon.transform
    //             .DOJump(targetPos, 5f, 1, 1f)
    //             .SetEase(Ease.OutCubic).OnComplete(() =>
    //             {
    //                 lockBG.gameObject.SetActive(false);
    //                 icon.gameObject.SetActive(true);
    //             }
    //     ));

    //     // Scale nhỏ dần khi gần tới
    //     seq.Insert(0.2f,
    //         fakeIcon.transform
    //             .DOScale(0.3f, 0.4f)
    //             .SetEase(Ease.InBack)
    //     );

    //     // ─────────────────────────────
    //     // ✨ VFX
    //     // ─────────────────────────────
    //     seq.AppendCallback(() =>
    //     {
    //         vfx.Play();
    //     });
    //     // ─────────────────────────────
    //     // 🔴 LOCK BAY LÊN + XOAY + FADE
    //     // ─────────────────────────────
    //     Vector3 lockStart = lockImage.transform.position;

    //     float xLockPosition = 0;
    //     if (Rect.anchoredPosition.x > 0) // lock bên phải
    //     {
    //         xLockPosition = 2f;
    //     }
    //     else // lock bên trái
    //     {
    //         xLockPosition = -2f;
    //     }

    //     Vector3 lockTarget = lockStart + new Vector3(xLockPosition, 2f, 0);

    //     seq.Insert(0f,
    //         lockImage.transform
    //             .DOMove(lockTarget, 0.5f)
    //             .SetEase(Ease.OutQuad)
    //     );

    //     seq.Insert(0f,
    //         lockImage.transform
    //             .DORotate(new Vector3(0, 0, 25f), 0.5f)
    //             .SetEase(Ease.OutQuad)
    //     );

    //     seq.Insert(0.1f,
    //         lockImage
    //             .DOFade(0, 0.4f)
    //     );

    //     // ─────────────────────────────
    //     // 🟢 SWITCH ICON
    //     // ─────────────────────────────
    //     seq.AppendCallback(() =>
    //     {
    //         fakeIcon.gameObject.SetActive(false);
    //         lockImage.gameObject.SetActive(false);
    //         icon.gameObject.SetActive(true);
    //     });

    //     // Icon pop-in đẹp hơn
    //     seq.Append(
    //         icon.transform
    //             .DOScale(1f, 0.35f)
    //             .From(1.2f)
    //             .SetEase(Ease.OutBack)
    //     );

    //     // ─────────────────────────────
    //     // 🔵 LEVEL UNLOCK SECTION FADE + MOVE
    //     // ─────────────────────────────

    //     seq.Insert(seq.Duration() - 0.5f,
    //         levelUnlockSection.transform
    //             .DOLocalMoveY(levelUnlockSection.transform.localPosition.y - 30, 0.4f)
    //             .SetEase(Ease.InQuad)
    //     );

    //     seq.Insert(seq.Duration() - 0.5f,
    //         levelUnlockSection
    //             .DOFade(0, 0.4f)
    //     );

    //     seq.Insert(seq.Duration() - 0.2f,
    //        freeSection.transform
    //            .DOLocalMoveY(freeSection.transform.localPosition.y, 0.4f)
    //            .From(freeSection.transform.localPosition.y - 30)
    //            .SetEase(Ease.InQuad)
    //    );

    //     seq.Insert(seq.Duration() - 0.2f,
    //         freeSection
    //             .DOFade(1, 0.4f)
    //     );



    //     // ─────────────────────────────
    //     // 🔚 COMPLETE
    //     // ─────────────────────────────
    //     seq.OnComplete(() =>
    //     {
    //         GameManager.Ins.Block(false); // Unblock tương tác khi animation kết thúc
    //         levelUnlockSection.gameObject.SetActive(false);
    //         lockSection.SetActive(false);
    //         Configure(Booster);
    //         switch (Booster.BoosterType)
    //         {
    //             case BoosterType.Hint:
    //                 UIManager.Ins.GetUI<GameplayUI>().ActiveBoosterTutorialBG(true, Booster.BoosterType);
    //                 break;
    //             case BoosterType.Reveal:
    //                 UIManager.Ins.GetUI<GameplayUI>().ActiveBoosterTutorialBG(true, Booster.BoosterType);
    //                 break;
    //             case BoosterType.FrostTime:
    //                 UIManager.Ins.GetUI<GameplayUI>().ActiveBoosterTutorialBG(true, Booster.BoosterType);
    //                 break;
    //             default:
    //                 break;
    //         }

    //         lockImage.transform.localScale = Vector3.one;
    //         lockImage.transform.localRotation = Quaternion.identity;
    //         lockImage.color = Color.white;
    //         lockImage.gameObject.SetActive(true);

    //         var parameters = new AnalyticParameter[]
    //            {
    //                         new AnalyticParameter("booster_name", Booster.BoosterType.ToString()),
    //                         new AnalyticParameter("value",  FirebaseManager.Ins.BoosterFree),
    //                         new AnalyticParameter("placement", "free"),
    //            };
    //         AnalyticManager.Ins.LogEvent("booster_earn", parameters);
    //     });
    // }
}