using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BaseButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public Button button;
    public Image image;
    public AudioClip clickSFX;

    public CanvasGroup canvasGroup;

    [field: SerializeField] public RectTransform Rect { get; private set; }

    private bool isInteractable = true;

    public Action OnPress;
    public Action OnRelease;

    private bool isPressed = false;
    private float initialHoldDelay = 0f;
    public float clickScaleBack = 0.9f;

    private void OnEnable()
    {
        StopAllCoroutines();
        isPressed = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isPressed = false;
    }

    public void AddListener(UnityAction action)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void AddListenerHold(Action action)
    {
        OnPress = action;
    }

    public void AddListenerUp(Action action)
    {
        OnRelease = action;
    }

    public void RemoveListener() => button.onClick.RemoveAllListeners();

    public void SetInteractable(bool isInteractable, bool needEffect = true)
    {
        this.isInteractable = isInteractable;
        button.interactable = this.isInteractable;

        if (needEffect)
        {
            if (isInteractable)
            {
                image.material = null;
            }
            else
            {
               // image.material = GameSetting.Ins.GrayMaterial;
            }
        }
    }

    public void SetSprite(Sprite sprite) => image.sprite = sprite;

    public void OnPointerClick(PointerEventData eventData) // Animation and sound support when click
    {
        if (!isInteractable) return;
        if (image != null)
        {
           // MMFFeedbackManager.Ins.PlayHaptic(HapticType.Light);
            image.transform.DOKill();
            image.transform.DOScale(clickScaleBack, 0.1f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                image.transform.DOScale(1f, 0.1f).SetEase(Ease.InQuad);
            });
           // AudioManager.Ins.PlaySFX(clickSFX);
        }
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isInteractable) return;

        isPressed = true;
        StartCoroutine(HoldPressCoroutine()); // Bắt đầu lặp OnPress khi giữ
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isInteractable) return;

        isPressed = false;
        OnRelease?.Invoke(); // Gọi OnRelease khi thả
        StopAllCoroutines(); // Dừng coroutine nhấn giữ
    }

    private IEnumerator HoldPressCoroutine()
    {
        // Đợi độ trễ ban đầu trước khi lặp
        yield return new WaitForSeconds(initialHoldDelay);

        // Lặp gọi OnPress khi vẫn giữ nút
        while (isPressed && isInteractable)
        {
            OnPress?.Invoke();
            yield return null;
        }
    }

}
