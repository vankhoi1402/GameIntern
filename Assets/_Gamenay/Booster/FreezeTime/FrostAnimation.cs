using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class FrostAnimation : MonoBehaviour
{
    public ParticleSystem rightVFX;
    public ParticleSystem leftVFX;
    public ParticleSystem explosionVFX;
    public ParticleSystem showVFX;
    public Vector3 startPos;
    public Vector3 endPos;
    public GameObject frostObjet;
    public float moveDuration = 0.75f;
    public CanvasGroup frostImageBG;
    private Sequence _sequence;

    [Button(ButtonSizes.Gigantic)]
    public void TestAniamtioN()
    {
        Vector3 randomPos1 = new Vector3(3f, -5f, 0);
        Vector3 randomPos2 = Vector3.zero;
        Configure(randomPos1, randomPos2);
    }

    public void Configure(Vector3 startPos, Vector3 endPos, Action onComplete = null)
    {
        ResetAnimation();
       // GameManager.Ins.Block(true);

        frostObjet.transform.position = startPos;
        frostObjet.SetActive(true);

        // Tính điểm vòng cung: midpoint nâng lên theo độ dài quỹ đạo
        Vector3 mid = (startPos + endPos) * 0.5f - Vector3.up * Vector3.Distance(startPos, endPos) * 0.75f;

        // Scale-in xuất hiện tại startPos
        frostObjet.transform.localScale = Vector3.zero;

        _sequence = DOTween.Sequence();
        
        // Xuất hiện
        _sequence.Append(frostObjet.transform.DOScale(1f, 0.15f).SetEase(Ease.OutBack));

        // Bay vòng cung qua mid đến endPos
        _sequence.Append(frostObjet.transform.DOPath(
            new Vector3[] { mid, endPos },
            moveDuration,
            PathType.CatmullRom
        ).SetEase(Ease.InQuad));

        _sequence.Append(
          frostObjet.transform.DOScale(2, 0.35f).SetEase(Ease.OutQuad)
        );
        _sequence.AppendCallback(() =>
        {
            showVFX.Play();
        });

        // Đến nơi: phát nổ + hide frostObject
        _sequence.AppendCallback(() =>
        {
            frostObjet.SetActive(false);
            explosionVFX.transform.position = endPos;
            float screenEdge = Camera.main.orthographicSize * Camera.main.aspect + 0.5f;
            rightVFX.transform.position = new Vector3(Camera.main.transform.position.x + screenEdge, endPos.y, 0);
            leftVFX.transform.position  = new Vector3(Camera.main.transform.position.x - screenEdge, endPos.y, 0);
            explosionVFX.Play();
            rightVFX.Play();
            leftVFX.Play();
            onComplete?.Invoke();

            frostImageBG.gameObject.SetActive(true);
            frostImageBG.DOKill();
            frostImageBG.DOFade(1f, 0.5f).SetEase(Ease.OutQuad);
            //GameManager.Ins.Block(false);
        });
    }

    public void OffFrostImageBG()
    {
        rightVFX.Stop();
        leftVFX.Stop();
        frostImageBG.DOKill();
        frostImageBG.DOFade(0f, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            frostImageBG.gameObject.SetActive(false);
        });
    }   


    public void ResetAnimation()
    {
        _sequence?.Kill();

        frostObjet.transform.position = startPos;
        frostObjet.transform.localScale = Vector3.one;
        frostObjet.SetActive(false);

        explosionVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        rightVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        leftVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        frostImageBG.DOFade(0f, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            frostImageBG.gameObject.SetActive(false);
        });

    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }
}