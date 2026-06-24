/// <summary>Kết quả pha logic của skill.</summary>
public struct SkillResolveResult
{
    public bool Success;
    public bool NeedsSettlement;

    public static SkillResolveResult Failed => new SkillResolveResult { Success = false, NeedsSettlement = false };

    public static SkillResolveResult Ok(bool needsSettlement = true) => new SkillResolveResult
    {
        Success = true,
        NeedsSettlement = needsSettlement
    };
}
