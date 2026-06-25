using UnityEngine;

/// <summary>HUD tạm — nút skill + hiển thị charge (thay bằng UI Canvas sau).</summary>
public class SkillHUD : MonoBehaviour
{
    [SerializeField] private SkillManager skillManager;
    [SerializeField] private KeyCode hotkey = KeyCode.Alpha1;

    private void Awake()
    {
        if (skillManager == null)
            skillManager = FindObjectOfType<SkillManager>();
    }

    private void OnGUI()
    {
        if (skillManager == null)
            return;

        if (InputManager.Instance != null && InputManager.Instance.IsInputLocked)
            return;

        var skills = skillManager.GetEquippedSkills();
        if (skills.Count == 0)
            return;

        GUILayout.BeginArea(new Rect(12f, Screen.height - 80f, Screen.width - 24f, 68f));
        GUILayout.BeginHorizontal();

        for (int i = 0; i < skills.Count; i++)
        {
            SkillDefinition skill = skills[i];
            if (skill == null)
                continue;

            int charges = skillManager.GetCharges(skill);
            string label = $"{skill.DisplayName} ({charges})";
            GUI.enabled = charges > 0 && !skillManager.IsExecuting && !skillManager.IsTargeting;

            if (GUILayout.Button(label, GUILayout.Height(44f), GUILayout.MinWidth(140f)))
                skillManager.TryActivateSkill(skill);

            GUI.enabled = true;
        }

        if (skillManager.IsTargeting)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("Chọn cột (Esc hủy)", GUILayout.Height(44f));
        }

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void Update()
    {
        if (skillManager == null)
            return;

        if (InputManager.Instance != null && InputManager.Instance.IsInputLocked)
            return;

        var skills = skillManager.GetEquippedSkills();
        if (skills.Count == 0)
            return;

        if (Input.GetKeyDown(hotkey))
            skillManager.TryActivateSkill(skills[0]);
    }
}
