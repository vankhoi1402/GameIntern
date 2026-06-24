using System;
using UnityEngine;

/// <summary>Định nghĩa một skill — metadata + effect asset.</summary>
[CreateAssetMenu(fileName = "SkillDefinition", menuName = "Grid Game/Skills/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string skillId = "skill_id";
    [SerializeField] private string displayName = "Skill";
    [SerializeField] private Sprite icon;

    [Header("Gameplay")]
    [SerializeField] private SkillTargetMode targetMode = SkillTargetMode.PickColumn;
    [SerializeField] private int defaultChargesPerLevel = 1;
    [SerializeField] private SkillEffectAsset effect;

    public string SkillId => skillId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public SkillTargetMode TargetMode => targetMode;
    public int DefaultChargesPerLevel => defaultChargesPerLevel;
    public SkillEffectAsset Effect => effect;
}

[Serializable]
public struct SkillLoadoutEntry
{
    public SkillDefinition Skill;
    public int Charges;
}
