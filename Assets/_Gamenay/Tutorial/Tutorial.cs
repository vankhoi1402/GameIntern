using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class Tutorial : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kéo UI Image hình bàn tay của bạn vào đây")]
    public RectTransform handIcon;

    public Image Background;

    [Header("Animation Settings")]
    public float moveDuration = 1f;
    public float clickScale = 0.8f;
    public float delayBetweenLoops = 0.3f;

    private Sequence handSequence;

    private void Awake()
    {
        ConfigureHandRaycasts();
        HideHand();
    }

    /// <summary>
    /// Gọi hàm này để bắt đầu hiệu ứng bàn tay.
    /// Truyền thẳng Transform của Item A (bắt đầu) và Item B (đích đến) vào.
    /// </summary>
    public void ShowHand(Transform startItem, Transform endItem)
    {
        if (handIcon == null || startItem == null || endItem == null)
            return;

        Background.gameObject.SetActive(true);
        ConfigureHandRaycasts();
        handIcon.gameObject.SetActive(true);

        handIcon.localScale = Vector3.one;
        handSequence?.Kill();
        handSequence = DOTween.Sequence();

        handIcon.position = startItem.position;

        handSequence.Append(handIcon.DOScale(clickScale, 0.2f))
                    .Append(handIcon.DOMove(endItem.position, moveDuration).SetEase(Ease.InOutSine))
                    .Append(handIcon.DOScale(1f, 0.2f))
                    .AppendInterval(delayBetweenLoops)
                    .Append(handIcon.DOMove(startItem.position, 0f))
                    .SetLoops(-1);
    }

    /// <summary>
    /// Gọi hàm này khi người chơi đã làm xong để tắt bàn tay đi.
    /// </summary>
    public void HideHand(bool keepBackground = false)
    {
        handSequence?.Kill();
        handSequence = null;

        if (handIcon != null)
            handIcon.gameObject.SetActive(false);

        if (Background != null && !keepBackground)
            Background.gameObject.SetActive(false);
    }

    private void ConfigureHandRaycasts()
    {
        if (handIcon == null)
            return;

        Graphic[] graphics = handIcon.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }
    }

    private void OnDestroy()
    {
        handSequence?.Kill();
    }
}
