using UnityEngine;
using UnityEngine.UI;

public class SettingUI : UICanvas
{
    [Header("Buttons")]
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnHome;
    [SerializeField] private Button btnResume;

    [Header("Audio")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider soundSlider;

    private void Awake()
    {
        btnClose.onClick.AddListener(CloseSetting);
        btnResume.onClick.AddListener(Resume);
        btnHome.onClick.AddListener(Home);

        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        soundSlider.onValueChanged.AddListener(OnSoundChanged);
    }

    private void Start()
    {
        musicSlider.value = SaveManager.MusicVolume;
        soundSlider.value = SaveManager.SfxVolume;
    }

    private void OnMusicChanged(float value)
    {
        AudioManager.Ins.SetMusicVolume(value);
    }

    private void OnSoundChanged(float value)
    {
        AudioManager.Ins.SetSfxVolume(value);
    }

    private void OnDestroy()
    {
        btnClose.onClick.RemoveAllListeners();
        btnResume.onClick.RemoveAllListeners();
        btnHome.onClick.RemoveAllListeners();

        musicSlider.onValueChanged.RemoveAllListeners();
        soundSlider.onValueChanged.RemoveAllListeners();
    }

    private void CloseSetting()
    {
        
        PlayManager.Ins.OnCloseSetting();
    }

    private void Home()
    {
       PlayManager.Ins.OnHome();
    }

    private void Resume()
    {
        CloseSetting();
    }
}