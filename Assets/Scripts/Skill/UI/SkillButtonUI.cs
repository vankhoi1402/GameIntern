using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Nút skill trên Canvas — gắn lên từng Button.</summary>
[RequireComponent(typeof(Button))]
public class SkillButtonUI : MonoBehaviour
{
    [SerializeField] private SkillManager skillManager;
    [SerializeField] private SkillDefinition skill;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private KeyCode hotkey = KeyCode.None;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (labelText == null)
            labelText = GetComponentInChildren<TMP_Text>(true);
        if (skillManager == null)
            skillManager = SkillManager.Instance != null
                ? SkillManager.Instance
                : FindObjectOfType<SkillManager>();

        if (button != null)
            button.onClick.AddListener(OnButtonClicked);
    }

    private void Start()
    {
        RefreshVisual();
    }

    private void OnEnable()
    {
        if (skillManager == null)
            return;

        skillManager.OnChargesChanged += HandleChargesChanged;
        skillManager.OnTargetingStarted += HandleTargetingStarted;
        skillManager.OnTargetingCancelled += HandleTargetingCancelled;
        skillManager.OnColumnSelected += HandleColumnSelected;
        skillManager.OnColumnSelectionCleared += HandleColumnSelectionCleared;
        RefreshVisual();
    }

    private void OnDisable()
    {
        if (skillManager == null)
            return;

        skillManager.OnChargesChanged -= HandleChargesChanged;
        skillManager.OnTargetingStarted -= HandleTargetingStarted;
        skillManager.OnTargetingCancelled -= HandleTargetingCancelled;
        skillManager.OnColumnSelected -= HandleColumnSelected;
        skillManager.OnColumnSelectionCleared -= HandleColumnSelectionCleared;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnButtonClicked);
    }

    private void Update()
    {
        if (hotkey == KeyCode.None || skillManager == null || skill == null)
            return;

        if (Input.GetKeyDown(hotkey))
            skillManager.TryActivateSkill(skill);
    }

    private void OnButtonClicked()
    {
        if (skillManager == null || skill == null)
            return;

        skillManager.TryActivateSkill(skill);
    }

    private void HandleChargesChanged(SkillDefinition changedSkill, int _) => RefreshIfMine(changedSkill);
    private void HandleTargetingStarted(SkillDefinition targetingSkill) => RefreshIfMine(targetingSkill);
    private void HandleTargetingCancelled() => RefreshVisual();
    private void HandleColumnSelected(int _) => RefreshVisual();
    private void HandleColumnSelectionCleared() => RefreshVisual();

    private void RefreshIfMine(SkillDefinition changedSkill)
    {
        if (changedSkill == skill)
            RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (skillManager == null || skill == null || button == null)
            return;

        bool isThisTargeting = skillManager.IsTargeting && skillManager.PendingSkill == skill;
        button.interactable = skillManager.CanUseSkill(skill) && !isThisTargeting;

        if (labelText == null)
            return;

        if (isThisTargeting)
        {
            labelText.text = "Chọn cột\n(Esc hủy)";
            return;
        }

        if (skillManager.HasSelectedColumn && skill.TargetMode == SkillTargetMode.PickColumn)
            labelText.text = $"{skill.DisplayName}\nCột {skillManager.SelectedColumn + 1}";
        else
            labelText.text = skill.DisplayName;
    }
}
