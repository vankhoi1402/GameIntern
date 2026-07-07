using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : Singleton<AudioManager>
{
    #region SFX
    public static string Win = "Win";
    public static string Lose = "Lose";
    public static string Fail = "Fail";
    public static string BlockMerge = "BlockMerge";
    public static string BlockMergeFail = "BlockMergeFail";
    public static string BlockMergeSuccess = "BlockMergeSuccess";
    public static string Refill = "Refill";
    [Header("Sound speaking")]
    public static string SoundGood = "SoundGood";
    public static string SoundGreat = "SoundGreat";
    public static string SoundExcellent = "SoundExcellent";
    public static string SoundAmazing = "SoundAmazing";
    public static string SoundUnbelievable = "SoundUnbelievable";
    public static string Pop = "Pop";
    public static string Ting = "Ting";
    public static string CoinPay = "CoinPay";
    public static string CoinOut = "CoinOut";
    public static string CoinCollect = "CoinCollect";
    public static string TranOpen = "TranOpen";
    public static string TranClose = "TranClose";
    public static string TabChange = "TabChange";
    public static string NewFeatureUnlock = "NewFeatureUnlock";

    public static string BoosterHint = "BoosterHint";
    public static string BoosterReveal = "BoosterReveal";
    public static string BoosterFreeze = "BoosterFreeze";


    public static string CardMove = "CardMove";
    public static string CardPick = "CardPick";
    public static string Stamp = "Stamp";
    public static string TopicComplete = "TopicComplete";
    public static string TopicComplete2 = "TopicComplete2";
    public static string Reward = "Reward";
    public static string BoosterIntroduce = "BoosterIntroduce";
    public static string BoosterClaim = "BoosterClaim";
    public static string Bomb = "Bomb";
    public static string BombFail = "BombFail";

    #endregion
    #region Song   
    public static string MainSong = "MainSong";
    public static string HomeSong = "HomeSong";
    #endregion

    public AudioSource musicSource;
    public AudioSource sfxSource;
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;


    public List<AudioClip> sfxClips;
    public List<AudioClip> musicClips;

    [HideInInspector] public bool IsPlaySFX;
    [HideInInspector] public bool IsPlayMusic;
    [HideInInspector] public bool IsHaptic;

    public float MaxVolume = 1f;
    public float MinVolume = 0.1f;

    protected override void Awake()
    {
        base.Awake();
        Initialize();
    }


    public void Initialize()
    {
        IsPlaySFX = true;
        IsPlayMusic = true;
        // = ES3.Load<bool>(Constain.IsHaptic, defaultValue: true);
        Debug.Log("Initialize audio manager");
        ChangeMusicState();
        PlayMusic(MainSong);
    }

    public void PlaySFX(string sfxName)
    {
        if (!IsPlaySFX) return;
        foreach (AudioClip clip in sfxClips)
        {
            if (clip.name == sfxName)
            {
                sfxSource.PlayOneShot(clip);
            }
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (!IsPlaySFX || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    public void PlayRandomSFX(string[] sfxNames)
    {
        if (!IsPlaySFX || sfxNames == null || sfxNames.Length == 0) return;

        // 1. Chọn ngẫu nhiên một tên SFX từ danh sách
        int randomIndex = Random.Range(0, sfxNames.Length);
        string selectedSfxName = sfxNames[randomIndex];

        // 2. Tìm AudioClip tương ứng trong List
        foreach (AudioClip clip in sfxClips)
        {
            if (clip.name == selectedSfxName)
            {
                sfxSource.PlayOneShot(clip);
                break;
            }
        }
    }


    public void StopPlaySFX() => sfxSource.Stop();

    public void PlayMusic(string musicName)
    {
        AudioClip musicClip;
        foreach (AudioClip clip in musicClips)
        {
            if (clip.name == musicName)
            {
                musicClip = clip;
                if (musicSource.clip != musicClip)
                {
                    musicSource.clip = musicClip;
                    musicSource.Play();
                }
            }
        }
    }

    public void ActiveMusic(bool active)
    {
        if (IsPlayMusic)
        {
            float targetVolume = active ? MaxVolume : 0f;
            float fadeDuration = 1f;
            musicSource.DOFade(targetVolume, fadeDuration);
        }
    }

    public void ChangeMusicState() => musicSource.volume = IsPlayMusic ? SaveManager.MusicVolume : 0f;

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            // ES3.Save(Constain.IsPlayMusic, IsPlayMusic);
            // ES3.Save(Constain.IsPlaySFX, IsPlaySFX);
            // ES3.Save(Constain.IsHaptic, IsHaptic);
        }
    }

    private void OnApplicationQuit()
    {
        // ES3.Save(Constain.IsPlayMusic, IsPlayMusic);
        // ES3.Save(Constain.IsPlaySFX, IsPlaySFX);
        //ES3.Save(Constain.IsHaptic, IsHaptic);
    }
    #region Audio Mixer
    public void SetMusicVolume(float volume)
    {
        SaveManager.MusicVolume = volume;

        float db = volume > 0.001f
            ? Mathf.Log10(volume) * 20f
            : -80f;

        audioMixer.SetFloat("Music", db);
    }

    public void SetSfxVolume(float volume)
    {
        SaveManager.SfxVolume = volume;

        float db = volume > 0.001f
            ? Mathf.Log10(volume) * 20f
            : -80f;

        audioMixer.SetFloat("SFX", db);
    }
    #endregion


}
