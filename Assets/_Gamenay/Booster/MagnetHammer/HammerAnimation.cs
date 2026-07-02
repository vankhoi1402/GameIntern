using System;
using DG.Tweening;
using DG.Tweening.Plugins.Options;
using Sirenix.OdinInspector;
using UnityEngine;

public class HammerAnimation : MonoBehaviour
{
    public SpriteRenderer hammerRender;

    private float hammerUpTime = 0.35f;   // Thời gian búa nâng lên theo arc
    private float hammerDownTime = 0.2f;   // Thời gian búa đập xuống theo arc

    // Trạng thái ban đầu của hammerRender (localPosition + rotation)
    private Vector3 m_InitLocalPosition;
    private Quaternion m_InitLocalRotation;

    private Sequence m_HammerSequence;

    // Thông số animation — chỉnh tại đây
    private const float k_SpawnScaleTime = 0.25f;    // Thời gian scale 0 → 1 khi xuất hiện
    private const float k_UpOffsetY = 2.5f;      // Độ cao búa nâng lên
    private const float k_UpOffsetX = 1.25f;   // Lùi sang phải khi nâng (lấy đà)
    private const float k_ArcMidOffsetY = 0.6f;    // Điểm giữa cong của arc nâng lên
    private const float k_UpRotation = -55f;     // Góc xoay khi nâng (nghiêng ra sau)
    private const float k_StrikeArcMidX = 0.3f;    // Điểm giữa cong của arc đập xuống (lao về trước)
    private const float k_StrikeArcMidY = 0.8f;    // Độ cao giữa arc đập xuống
    private const float k_DownRotation = 30f;     // Góc xoay khi đập (nghiêng ra trước)
    private const float k_BounceOffsetY = -0.12f;   // Lún nhẹ sau cú đập
    private const float k_BounceTime = 0.06f;   // Thời gian lún
    private const float k_RecoverTime = 0.2f;    // Thời gian hồi về vị trí ban đầu

    private void Awake()
    {
        if (hammerRender != null)
        {
            m_InitLocalPosition = hammerRender.transform.localPosition;
            m_InitLocalRotation = hammerRender.transform.localRotation;
        }

    }


    public void ResetHammerState()
    {
        m_HammerSequence?.Kill();

        if (hammerRender == null) return;

        hammerRender.transform.localScale = Vector3.one;
        hammerRender.transform.localPosition = m_InitLocalPosition;
        hammerRender.transform.localRotation = m_InitLocalRotation;

    }

    /// <summary>Giữ API cũ: callback chạy lúc búa đập xuống (onHit).</summary>
    public void Configure(Vector3 position, Action completedAction)
        => Configure(position, completedAction, null);

    /// <summary>
    /// onHit: lúc búa chạm điểm đập.
    /// onFinished: sau khi búa hồi + fade xong và object bị tắt.
    /// </summary>
    public void Configure(Vector3 position, Action onHit, Action onFinished)
    {
        m_HammerSequence?.Kill();

        transform.position = position;
        
        // Reset về trạng thái ban đầu trước khi play
        hammerRender.transform.localScale = Vector3.zero;
        hammerRender.transform.localPosition = m_InitLocalPosition;
        hammerRender.transform.localRotation = m_InitLocalRotation;
        hammerRender.color = new Color(hammerRender.color.r, hammerRender.color.g, hammerRender.color.b, 1f);

        // ── Tính toán các điểm trên arc trong local space ────────────────

        Vector3 upPosition = m_InitLocalPosition
                           + Vector3.up * k_UpOffsetY
                           + Vector3.right * k_UpOffsetX;

        
        // Điểm giữa của arc nâng lên: đi lên trước, lùi sang ngang sau
        // → tạo đường cong tự nhiên thay vì đường thẳng chéo
        Vector3 arcUpMid = m_InitLocalPosition
                         + Vector3.up * k_ArcMidOffsetY
                         + Vector3.right * (k_UpOffsetX * 0.25f);

        // Điểm giữa của arc đập xuống: từ upPosition lao về phía trước và xuống
        // → cảm giác búa "vung" theo đường tròn, không rơi thẳng
        Vector3 arcDownMid = upPosition
                           + Vector3.right * (-k_StrikeArcMidX)
                           + Vector3.up * (-k_StrikeArcMidY);

        Vector3 bouncePosition = m_InitLocalPosition + Vector3.up * k_BounceOffsetY;

        m_HammerSequence = DOTween.Sequence();

        // ── Scale 0 → 1: búa "bật ra" tại vị trí ban đầu ───────────────
        // Chờ scale xong hoàn toàn trước khi bắt đầu vung lên
        m_HammerSequence.Append(
            hammerRender.transform.DOScale(Vector3.one, k_SpawnScaleTime)
                        .SetEase(Ease.OutBack)
        );

        // sau khi scale xong, chờ thêm 0.15s rồi mới bắt đầu vung để tạo cảm giác "động" hơn
        m_HammerSequence.AppendInterval(0.15f);

        // ── Pha 1: Nâng búa theo arc cong + xoay ra sau ─────────────────
        // DOPath với waypoint arcUpMid → tạo đường cong tự nhiên khi nâng
        m_HammerSequence.Append(
            hammerRender.transform.DOLocalPath(
                new[] { arcUpMid, upPosition },
                hammerUpTime,
                PathType.CatmullRom
            ).SetEase(Ease.OutSine)
        );
        m_HammerSequence.Join(
            hammerRender.transform.DOLocalRotate(new Vector3(0f, 0f, k_UpRotation), hammerUpTime)
                        .SetEase(Ease.OutSine)
        );

        // đoạn này thêm đồng thời scale búa lên 1.5 để tạo cảm giác "động" hơn khi vung lên, sau đó scale về 1 ở pha đập xuống
        m_HammerSequence.Join(
            hammerRender.transform.DOScale(Vector3.one * 1.75f, hammerUpTime)
                        .SetEase(Ease.OutSine)
        );

        // ── Pha 2: Đập xuống theo arc cong + xoay ra trước ──────────────
        // arcDownMid kéo búa lao chéo về phía trước → cảm giác vung búa tròn
        m_HammerSequence.Append(
            hammerRender.transform.DOLocalPath(
                new[] { arcDownMid, m_InitLocalPosition },
                hammerDownTime,
                PathType.CatmullRom
            ).SetEase(Ease.InQuart).OnComplete(() => onHit?.Invoke())
        );


        m_HammerSequence.Join(
            hammerRender.transform.DOLocalRotate(new Vector3(0f, 0f, k_DownRotation), hammerDownTime)
                        .SetEase(Ease.InQuart)
                        
        );

        // đoạn này có thể thêm đồng thời scale búa lên về 1 để tạo cảm giác "động" hơn khi đập
        m_HammerSequence.Join(
            hammerRender.transform.DOScale(Vector3.one, hammerDownTime)
                        .SetEase(Ease.InQuart)
        );


        // ── Pha 3: Lún nhẹ — cảm giác impact ───────────────────────────
        m_HammerSequence.Append(
            hammerRender.transform.DOLocalMove(bouncePosition, k_BounceTime)
                        .SetEase(Ease.OutQuad)
        );

        // ── Pha 4: Hồi về vị trí và góc ban đầu ─────────────────────────
        m_HammerSequence.Append(
            hammerRender.transform.DOLocalMove(m_InitLocalPosition, k_RecoverTime)
                        .SetEase(Ease.OutSine)
        );
        m_HammerSequence.Join(
            hammerRender.transform.DOLocalRotate(Vector3.zero, k_RecoverTime)
                        .SetEase(Ease.OutSine)
        );

        m_HammerSequence.OnComplete(() =>
        {
            ResetHammerState();
            // fade hammerRender về 0 trong 0.25fsau khi hoàn thành để tạo hiệu ứng "tan biến" nhẹ nhàng
            hammerRender.DOFade(0f, 0.25f).SetEase(Ease.OutSine).OnComplete(() =>
            {
                gameObject.SetActive(false);
                onFinished?.Invoke();
            });

        });
    }

    [Button(ButtonSizes.Gigantic)]
    public void TestAnimation()
    {
       // Vector3 random = new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f), 0f);
        Vector3 position = transform.position;
        Configure(position, null);
    }
}