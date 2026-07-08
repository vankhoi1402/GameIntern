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
    [SerializeField] private BoosterType m_BoosterType = BoosterType.None;
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
    [SerializeField] private Image fakeIcon;
    [SerializeField] private Sprite lockOpenSprite;
    [SerializeField] private ParticleSystem vfx;
    [SerializeField] private TextMeshProUGUI amountDisplay;

    public RectTransform Rect;
    private Tween tweenIconBoosterSuggest;

    public Booster Booster { get; private set; }

    public BoosterType AssignedType => ResolveAssignedType();

    private void Awake()
    {
        m_BoosterType = ResolveAssignedType();

        if (btn != null)
            btn.AddListener(BoosterClick);
    }

    public void BindFromManager()
    {
        if (AssignedType == BoosterType.None)
            return;

        Booster booster = BoosterManager.Ins.GetBooster(AssignedType);
        if (booster != null)
            Configure(booster);
    }

    public void Configure(Booster booster)
    {
        Booster = booster;

        if (booster == null)
            return;

        if (amountDisplay != null)
            amountDisplay.text = booster.Amount.ToString();

        if (!booster.Unlock)
        {
            if (levelUnlockSection != null)
                levelUnlockSection.gameObject.SetActive(true);

            if (levelUnlockText != null)
                levelUnlockText.text = "Level " + booster.BoosterLevelUnlock;

            if (icon != null)
                icon.sprite = booster.BoosterIcon;

            if (fakeIcon != null)
                fakeIcon.sprite = booster.BoosterIcon;

            freeSection?.gameObject.SetActive(false);
            addSection?.SetActive(false);
            amountSection?.SetActive(false);
            levelSection?.gameObject.SetActive(true);
            lockSection?.SetActive(true);
            btn?.SetInteractable(false, false);
            return;
        }

        levelSection?.gameObject.SetActive(false);
        lockSection?.SetActive(false);

        if (icon != null)
            icon.sprite = booster.BoosterIcon;

        if (fakeIcon != null)
            fakeIcon.sprite = booster.BoosterIcon;

        levelUnlockSection?.gameObject.SetActive(false);
        btn?.SetInteractable(true, true);

        if (booster.FirstUse)
        {
            freeSection?.gameObject.SetActive(true);
            addSection?.SetActive(false);
            amountSection?.SetActive(false);
            return;
        }

        if (booster.Amount == 0)
        {
            freeSection?.gameObject.SetActive(false);
            addSection?.SetActive(true);
            amountSection?.SetActive(false);
            return;
        }

        freeSection?.gameObject.SetActive(false);
        addSection?.SetActive(false);
        amountSection?.SetActive(true);
    }

    private void BoosterClick()
    {
         
        if (Booster == null)
            return;

        if (!BoosterManager.Ins.CanUseBooster(Booster))
            return;

        bool used = BoosterManager.Ins.TryUseBooster(Booster.BoosterType, ExecuteBoosterEffect);
        if (used)
            Configure(BoosterManager.Ins.GetBooster(Booster.BoosterType));
    }

    private bool ExecuteBoosterEffect()
    {
        if (!PlayManager.Exists())
            return false;

        switch (Booster.BoosterType)
        {
            case BoosterType.Hint:
                return PlayManager.Ins.TryHintBooster();
            case BoosterType.Shuffle:
                return PlayManager.Ins.TryShuffleBoard();
            case BoosterType.FrostTime:
                return PlayManager.Ins.TryFreezeTimer();
            case BoosterType.Hammer:
                return PlayManager.Ins.TryMagnetBooster();
            default:
                return false;
        }
    }

    private static BoosterType ResolveTypeFromName(string objectName)
    {
        if (objectName.Contains("Hint"))
            return BoosterType.Hint;

        if (objectName.Contains("Shuffle"))
            return BoosterType.Shuffle;

        if (objectName.Contains("Hammer") || objectName.Contains("Magnet"))
            return BoosterType.Hammer;

        if (objectName.Contains("Freeze") || objectName.Contains("Frost"))
            return BoosterType.FrostTime;

        return BoosterType.None;
    }

    private BoosterType ResolveAssignedType()
    {
        BoosterType fromName = ResolveTypeFromName(gameObject.name);
        if (fromName != BoosterType.None)
            return fromName;

        return m_BoosterType;
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
}
