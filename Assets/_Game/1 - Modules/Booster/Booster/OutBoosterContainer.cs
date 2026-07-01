using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OutBoosterContainer : MonoBehaviour
{
    public enum State
    {
        On,
        Off
    }

    [SerializeField] private BaseButton btn;
    [SerializeField] private GameObject amountSection;
    [SerializeField] private GameObject confirmSection;
    [SerializeField] private GameObject addSection;
    [SerializeField] private GameObject freeSection;
    [SerializeField] private GameObject levelSection;
    [SerializeField] private GameObject lockSection;
    [SerializeField] private TextMeshProUGUI levelUnlockSection;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI amountDisplay;
    [SerializeField] private Sprite lockSprite;
    public RectTransform Rect;
    private State state;

    public Booster Booster { get; private set; }

    private void Awake()
    {
        btn.AddListener(BoosterClick);
    }

    public void Configure(Booster booster)
    {
        Booster = booster;
        amountDisplay.text = booster.Amount.ToString();
        state = State.Off;

        if (!booster.Unlock)
        {
            //icon.sprite = lockSprite;
            icon.sprite = booster.BoosterIcon;
            addSection.SetActive(false);
            confirmSection.SetActive(false);
            amountSection.SetActive(false);
            freeSection.gameObject.SetActive(false);

            btn.SetInteractable(false, false);
            levelUnlockSection.gameObject.SetActive(true);
            levelSection.SetActive(true);
            lockSection.SetActive(true);

            levelUnlockSection.text = "Level" + " " + booster.BoosterLevelUnlock.ToString();

        }
        else
        {
            levelSection.SetActive(false);
            lockSection.SetActive(false);
            icon.sprite = booster.BoosterIcon;
            btn.SetInteractable(true, true);
            levelUnlockSection.gameObject.SetActive(false);

            if (booster.FirstUse)
            {
                addSection.SetActive(false);
                confirmSection.SetActive(false);
                amountSection.SetActive(false);
                freeSection.gameObject.SetActive(true);
            }
            else
            {
                if (booster.Amount == 0)
                {
                    addSection.SetActive(true);
                    confirmSection.SetActive(false);
                    amountSection.SetActive(false);
                    freeSection.gameObject.SetActive(false);
                }
                else
                {
                    addSection.SetActive(false);
                    confirmSection.SetActive(false);
                    amountSection.SetActive(true);
                    freeSection.gameObject.SetActive(false);
                }
            }
        }
    }

    private void BoosterClick()
    {
        if (Booster.Amount == 0)
        {
            
        }
        else
        {
            if (state == State.Off)
            {
                BoosterManager.Ins.SetBoosterPay(Booster);
                //UIManager.Ins.GetUI<StartLevelUI>().EquipBooster(Booster, true);
                state = State.On;

                addSection.SetActive(false);
                confirmSection.SetActive(true);
                amountSection.SetActive(false);
                freeSection.SetActive(false);
            }
            else
            {
                BoosterManager.Ins.ResetBoosterPay(Booster);
                //UIManager.Ins.GetUI<StartLevelUI>().EquipBooster(Booster, false);
                state = State.Off;

                if (Booster.FirstUse)
                {
                    addSection.SetActive(false);
                    confirmSection.SetActive(false);
                    amountSection.SetActive(false);
                    freeSection.SetActive(true);
                }
                else
                {
                    addSection.SetActive(false);
                    confirmSection.SetActive(false);
                    amountSection.SetActive(true);
                    freeSection.SetActive(false);
                }
            }
        }
    }
}