using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Điều phối kích hoạt skill — validate, presentation, resolve, settlement.</summary>
public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private BoardSettlementService settlementService;

    [Header("Default loadout (khi level không gán SkillLoadout)")]
    [SerializeField] private SkillDefinition[] defaultSkills;

    [Header("Test")]
    [SerializeField] private bool unlimitedChargesForTest = true;

    private readonly SkillContext m_Context = new SkillContext();
    private readonly SkillEffectPayload m_Payload = new SkillEffectPayload();
    private readonly Dictionary<SkillDefinition, int> m_Charges = new Dictionary<SkillDefinition, int>();

    private SkillDefinition m_PendingSkill;
    private bool m_IsExecuting;
    private int m_SelectedColumn = -1;

    public bool UnlimitedChargesForTest => unlimitedChargesForTest;
    public bool IsTargeting => m_PendingSkill != null;
    public bool IsExecuting => m_IsExecuting;
    public SkillDefinition PendingSkill => m_PendingSkill;
    public bool HasSelectedColumn => m_SelectedColumn >= 0;
    public int SelectedColumn => m_SelectedColumn;

    public event Action<SkillDefinition> OnTargetingStarted;
    public event Action OnTargetingCancelled;
    public event Action<SkillDefinition, int> OnChargesChanged;
    public event Action<int> OnColumnSelected;
    public event Action OnColumnSelectionCleared;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
        if (levelManager == null)
            levelManager = FindObjectOfType<LevelManager>();
        if (settlementService == null)
            settlementService = FindObjectOfType<BoardSettlementService>();

        m_Context.BoardManager = boardManager;
        m_Context.LevelManager = levelManager;
    }

    private void Start()
    {
        if (m_Charges.Count == 0)
            ApplyLoadout(null);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ApplyLoadout(SkillLoadoutEntry[] loadout)
    {
        m_Charges.Clear();

        if (loadout != null && loadout.Length > 0)
        {
            foreach (SkillLoadoutEntry entry in loadout)
            {
                if (entry.Skill == null)
                    continue;

                int charges = entry.Charges > 0 ? entry.Charges : entry.Skill.DefaultChargesPerLevel;
                m_Charges[entry.Skill] = charges;
            }
        }
        else if (defaultSkills != null)
        {
            foreach (SkillDefinition skill in defaultSkills)
            {
                if (skill == null)
                    continue;
                m_Charges[skill] = skill.DefaultChargesPerLevel;
            }
        }

        foreach (var pair in m_Charges)
            OnChargesChanged?.Invoke(pair.Key, pair.Value);
    }

    public int GetCharges(SkillDefinition skill)
    {
        if (skill == null)
            return 0;
        if (unlimitedChargesForTest)
            return int.MaxValue;
        return m_Charges.TryGetValue(skill, out int count) ? count : 0;
    }

    public bool CanUseSkill(SkillDefinition skill)
    {
        if (skill == null || m_IsExecuting)
            return false;
        if (unlimitedChargesForTest)
            return true;
        return GetCharges(skill) > 0;
    }

    public IReadOnlyList<SkillDefinition> GetEquippedSkills()
    {
        var list = new List<SkillDefinition>();
        foreach (var pair in m_Charges)
            list.Add(pair.Key);
        return list;
    }

    public bool TrySelectColumn(int col)
    {
        if (boardManager == null || col < 0 || col >= boardManager.Columns)
            return false;

        if (m_IsExecuting || IsTargeting)
            return false;

        m_SelectedColumn = col;
        OnColumnSelected?.Invoke(col);
        return true;
    }

    public void ClearSelectedColumn()
    {
        if (m_SelectedColumn < 0)
            return;

        m_SelectedColumn = -1;
        OnColumnSelectionCleared?.Invoke();
    }

    public bool TryActivateSkill(SkillDefinition skill)
    {
        if (skill == null || skill.Effect == null)
            return false;

        if (m_IsExecuting || m_PendingSkill != null)
            return false;

        if (BoardStateManager.Instance != null && BoardStateManager.Instance.CurrentState != BoardState.Idle)
            return false;

        if (settlementService != null && settlementService.IsRunning)
            return false;

        if (!unlimitedChargesForTest && GetCharges(skill) <= 0)
        {
            Debug.Log($"[SkillManager] Hết lượt skill: {skill.DisplayName}");
            return false;
        }

        if (skill.TargetMode == SkillTargetMode.None)
            return TryExecuteWithTarget(skill, SkillTarget.None);

        if (skill.TargetMode == SkillTargetMode.PickColumn && HasSelectedColumn)
            return TryExecuteWithTarget(skill, SkillTarget.ForColumn(m_SelectedColumn));

        m_PendingSkill = skill;
        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.SkillTargeting);

        OnTargetingStarted?.Invoke(skill);
        return true;
    }

    public void CancelTargeting()
    {
        if (m_PendingSkill == null)
            return;

        m_PendingSkill = null;
        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.Idle);

        OnTargetingCancelled?.Invoke();
    }

    public bool TryConfirmTarget(SkillTarget target)
    {
        if (m_PendingSkill == null)
            return false;

        SkillDefinition skill = m_PendingSkill;

        if (skill.Effect == null || !skill.Effect.TryBuildPayload(m_Context, target, m_Payload))
        {
            Debug.Log($"[SkillManager] Cột {target.Col + 1} không hợp lệ — cần ≥3 block cùng loại trong cột.");
            return false;
        }

        m_PendingSkill = null;

        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.Idle);

        if (target.Mode == SkillTargetMode.PickColumn)
            m_SelectedColumn = target.Col;

        return TryExecuteWithTarget(skill, target);
    }

    private bool TryExecuteWithTarget(SkillDefinition skill, SkillTarget target)
    {
        if (skill.Effect == null)
            return false;

        if (m_Payload.CellsToClear.Count == 0 &&
            !skill.Effect.TryBuildPayload(m_Context, target, m_Payload))
        {
            Debug.Log($"[SkillManager] Cột {target.Col + 1} không hợp lệ — cần ≥3 block cùng loại trong cột.");
            return false;
        }

        ClearSelectedColumn();
        StartCoroutine(ExecuteSkillRoutine(skill, target));
        return true;
    }

    private IEnumerator ExecuteSkillRoutine(SkillDefinition skill, SkillTarget target)
    {
        m_IsExecuting = true;

        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.UsingSkill);

        SkillEffectAsset effect = skill.Effect;

        yield return effect.PlayPresentation(m_Context, target, m_Payload);

        SkillResolveResult result = effect.Resolve(m_Context, target, m_Payload);

        if (result.Success)
            yield return effect.PlayRecovery(m_Context, target, m_Payload);

        if (result.Success && result.NeedsSettlement && settlementService != null)
        {
            bool settlementDone = false;
            settlementService.RunGravityThenRefill(() => settlementDone = true);

            float timeout = 10f;
            float elapsed = 0f;
            while (!settlementDone && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else if (BoardStateManager.Instance != null)
        {
            BoardStateManager.Instance.ChangeState(BoardState.Idle);
        }

        if (result.Success)
            ConsumeCharge(skill);
        else if (BoardStateManager.Instance != null &&
                 BoardStateManager.Instance.CurrentState != BoardState.Idle)
            BoardStateManager.Instance.ForceIdle("skill resolve failed");

        m_IsExecuting = false;
        m_Payload.Clear();
    }

    private void ConsumeCharge(SkillDefinition skill)
    {
        if (unlimitedChargesForTest)
            return;

        if (!m_Charges.ContainsKey(skill))
            return;

        m_Charges[skill] = Mathf.Max(0, m_Charges[skill] - 1);
        OnChargesChanged?.Invoke(skill, m_Charges[skill]);
    }
}
