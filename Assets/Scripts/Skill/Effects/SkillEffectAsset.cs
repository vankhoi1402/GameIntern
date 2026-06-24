using System.Collections;
using UnityEngine;

/// <summary>Logic + presentation của một loại skill — mỗi skill = 1 ScriptableObject.</summary>
public abstract class SkillEffectAsset : ScriptableObject
{
    /// <summary>Validate và điền payload (ô/block sẽ bị ảnh hưởng).</summary>
    public abstract bool TryBuildPayload(SkillContext ctx, SkillTarget target, SkillEffectPayload payload);

    /// <summary>Pha illusion — chỉ visual, không sửa board data.</summary>
    public abstract IEnumerator PlayPresentation(SkillContext ctx, SkillTarget target, SkillEffectPayload payload);

    /// <summary>Pha logic — sửa board data.</summary>
    public abstract SkillResolveResult Resolve(SkillContext ctx, SkillTarget target, SkillEffectPayload payload);

    /// <summary>Pha recovery sau resolve — block còn lại bật lại trước gravity/refill.</summary>
    public virtual IEnumerator PlayRecovery(SkillContext ctx, SkillTarget target, SkillEffectPayload payload)
    {
        yield break;
    }
}
