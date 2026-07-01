using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class ClockAnimation : Singleton<ClockAnimation>
{
    [Header("References")]
    public Transform clockTransform;
    public SpriteRenderer clockRender;
    public SpriteRenderer clockHandRender;
    public ParticleSystem appearEffect;
    public ParticleSystem shockEffect;
    public AudioClip timeActiveSFX;
    public AudioClip timeTouchSFX;

    [Header("Phase 1: Appear & Scale In")]
    [Tooltip("Thời gian scale clock từ 0 → 1 và di chuyển + xoay")]
    [SerializeField] private float phase1Duration = 1f;

    [Tooltip("Vị trí offset ban đầu (di chuyển từ dưới lên)")]
    [SerializeField] private Vector3 phase1StartOffset = new Vector3(0, -1f, 0);

    [Tooltip("Góc xoay ban đầu của clock (độ)")]
    [SerializeField] private float phase1StartRotationZ = 0f;

    [Tooltip("Góc xoay kết thúc của clock trong phase 1 (độ)")]
    [SerializeField] private float phase1EndRotationZ = -25f;

    [Header("Phase 2: Delay & Fade In Clock Face")]
    [Tooltip("Thời gian chờ trước khi fade in mặt đồng hồ")]
    [SerializeField] private float fadeInDelay = 0.5f;

    [Tooltip("Thời gian fade in mặt đồng hồ (clockRender)")]
    [SerializeField] private float clockFaceFadeDuration = 0.5f;

    [Header("Phase 3: Hand Spin & Shock")]
    [Tooltip("Thời gian xoay kim đồng hồ (độ âm = ngược chiều kim đồng hồ)")]
    [SerializeField] private float handSpinDuration = 1f;

    [Tooltip("Số độ xoay của kim đồng hồ (âm để ngược chiều)")]
    [SerializeField] private float handSpinDegrees = -720f;

    [Tooltip("Thời gian scale shock (phóng to rồi về)")]
    [SerializeField] private float shockScaleDuration = 0.15f;

    [Tooltip("Hệ số scale khi shock (1 → giá trị này → 1)")]
    [SerializeField] private float shockScaleMultiplier = 1.2f;

    [Tooltip("Số lần lặp Yoyo cho shock scale")]
    [SerializeField] private int shockScaleLoops = 2;

    [Header("Phase 4: Fly to Target")]
    [Tooltip("Thời gian bay từ vị trí hiện tại đến target")]
    [SerializeField] private float flyDuration = 0.5f;

    [Tooltip("Hệ số scale cuối khi bay đến target (nhỏ lại)")]
    [SerializeField] private float flyEndScale = 0.25f;

    [Tooltip("Khoảng lệch ngang tối đa cho điểm giữa (tạo đường cong ngẫu nhiên)")]
    [SerializeField] private float midPointHorizontalRandomRange = 2f;

    [Header("General")]
    [Tooltip("Thời gian chờ sau shock trước khi bay")]
    [SerializeField] private float postShockDelay = 0.5f;

    private Sequence _sequence;

    public void Configure(Vector3 targetPosition, Action completedAction)
    {
        // Reset và chuẩn bị
        gameObject.SetActive(true);
        ResetAnimation(); // đảm bảo sạch sẽ
       // AudioManager.Ins.PlaySFX(timeActiveSFX);

        transform.position = Vector3.zero;
        if (appearEffect != null) appearEffect.Play();

        _sequence = DOTween.Sequence();

        // ── Phase 1: Scale in + Move up + Rotate nhẹ ─────────────────────────
        Vector3 phase1StartPos = Vector3.zero + phase1StartOffset;

        _sequence.Append(clockTransform.DOScale(1f, phase1Duration).From(0f));
        _sequence.Join(clockTransform.DOLocalMove(Vector3.zero, phase1Duration).From(phase1StartPos));
        _sequence.Join(clockTransform.DOLocalRotate(new Vector3(0, 0, phase1EndRotationZ), phase1Duration).From(new Vector3(0, 0, phase1StartRotationZ)));
        if (clockRender != null)
        {
            clockRender.color = new Color(1, 1, 1, 0);
            _sequence.Join(clockRender.DOFade(1f, clockFaceFadeDuration));
        }

        // ── Phase 3: Kim xoay + Shock effect ─────────────────────────────────
        _sequence.Join(clockHandRender.transform.DOLocalRotate(new Vector3(0, 0, handSpinDegrees), handSpinDuration, RotateMode.LocalAxisAdd));

        _sequence.AppendCallback(() =>
        {
            if (shockEffect != null) shockEffect.Play();
        });

        _sequence.Join(clockTransform.DOScale(shockScaleMultiplier, shockScaleDuration)
            .SetLoops(shockScaleLoops, LoopType.Yoyo)
            .SetEase(Ease.InOutSine));

        // Chờ sau shock
        _sequence.AppendInterval(postShockDelay);

        // ── Phase 4: Bay theo đường cong đến target ──────────────────────────
        Vector3 currentPos = transform.position;
        Vector3 midPoint = (currentPos + targetPosition) / 2f;
        float randomOffset = UnityEngine.Random.value > 0.5f ? midPointHorizontalRandomRange : -midPointHorizontalRandomRange;
        midPoint.x += randomOffset;
        Vector3[] path = new Vector3[] { currentPos, midPoint, targetPosition };

        _sequence.Append(transform.DOPath(path, flyDuration, PathType.CatmullRom)
            .SetEase(Ease.InOutSine));

        _sequence.Join(clockTransform.DOScale(flyEndScale, flyDuration));

        // ── Complete ─────────────────────────────────────────────────────────
        _sequence.OnComplete(() =>
        {
            completedAction?.Invoke();
        
            gameObject.SetActive(false);
        });

        _sequence.SetUpdate(true); // chạy mượt kể cả pause
    }

    public void ResetAnimation()
    {
        if (_sequence != null)
        {
            _sequence.Kill();
            _sequence = null;
        }

        DOTween.Kill(transform);
        DOTween.Kill(clockTransform);
        DOTween.Kill(clockHandRender.transform);

        transform.position = Vector3.zero;
        transform.localScale = Vector3.one;

        clockTransform.localScale = Vector3.zero;
        clockTransform.localPosition = Vector3.zero + phase1StartOffset;
        clockTransform.localRotation = Quaternion.identity;

        clockHandRender.transform.localRotation = Quaternion.identity;

        if (clockRender != null)
            clockRender.color = new Color(1, 1, 1, 0);

        if (appearEffect != null) appearEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (shockEffect != null) shockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    [Button(ButtonSizes.Gigantic)]
    public void TestAnimation()
    {
        Configure(new Vector3(0, 5, 0), () => Debug.Log("Clock Animation Completed!"));
    }
}