using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill cột: illusion xóa sạch cột, logic chỉ xóa 3 block cùng type, recovery + settlement lấp chỗ trống.
/// </summary>
[CreateAssetMenu(fileName = "ColumnMatchClearEffect", menuName = "Grid Game/Skills/Column Match Clear Effect")]
public class ColumnMatchClearSkillEffect : SkillEffectAsset
{
    [SerializeField] private float sweepDuration = 0.28f;
    [SerializeField] private float duckDepthMultiplier = 0.85f;
    [SerializeField] private float rowStagger = 0.04f;
    [SerializeField] private float holdAfterVanishDuration = 0.06f;
    [SerializeField] private float riseDuration = 0.15f;
    [SerializeField] private float columnSpawnDepthMultiplier = 2.5f;

    public override bool TryBuildPayload(SkillContext ctx, SkillTarget target, SkillEffectPayload payload)
    {
        payload.Clear();
        if (ctx?.BoardManager == null || target.Mode != SkillTargetMode.PickColumn)
            return false;

        return ColumnMatchClearLogic.TryFindThreeSameTypeInColumn(ctx.BoardManager, target.Col, payload);
    }

    public override IEnumerator PlayPresentation(SkillContext ctx, SkillTarget target, SkillEffectPayload payload)
    {
        if (ctx.Layout == null || payload.CellsToClear.Count == 0)
            yield break;

        int col = target.Col;
        float cellHeight = ctx.Layout.CellHeight;
        float duckOffset = cellHeight * duckDepthMultiplier;
        int pending = 0;
        bool done = false;

        void OnOneDone()
        {
            pending--;
            if (pending <= 0)
                done = true;
        }

        for (int row = ctx.BoardManager.Rows - 1; row >= 0; row--)
        {
            Slot slot = ctx.BoardManager.GetSlot(row, col);
            if (slot == null || !slot.HasBlock)
                continue;

            Block block = slot.CurrentBlock;
            if (block == null || !block.TryGetComponent<BlockView>(out var view))
                continue;

            Vector3 basePos = ctx.Layout.GetWorldPosition(row, col);
            Vector3 duckPos = basePos + Vector3.down * duckOffset;
            float delay = (ctx.BoardManager.Rows - 1 - row) * rowStagger;

            pending++;
            view.PlaySkillVanish(duckPos, sweepDuration, delay, OnOneDone);
        }

        if (pending == 0)
            yield break;

        float maxWait = sweepDuration + rowStagger * ctx.BoardManager.Rows + 0.35f;
        float elapsed = 0f;
        while (!done && elapsed < maxWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (holdAfterVanishDuration > 0f)
            yield return new WaitForSeconds(holdAfterVanishDuration);
    }

    public override SkillResolveResult Resolve(SkillContext ctx, SkillTarget target, SkillEffectPayload payload)
    {
        if (ctx?.BoardManager == null || payload.CellsToClear.Count == 0)
            return SkillResolveResult.Failed;

        foreach (Vector2Int cell in payload.CellsToClear)
            ctx.BoardManager.RemoveBlock(cell.x, cell.y);

        return SkillResolveResult.Ok(needsSettlement: true);
    }

    public override IEnumerator PlayRecovery(SkillContext ctx, SkillTarget target, SkillEffectPayload payload)
    {
        if (ctx?.BoardManager == null || ctx.Layout == null || target.Mode != SkillTargetMode.PickColumn)
            yield break;

        int col = target.Col;
        float cellHeight = ctx.Layout.CellHeight;

        if (GravitySystem.Instance != null)
            GravitySystem.Instance.ApplyGravitySilentForColumn(col);

        Vector3 columnBase = ctx.Layout.GetWorldPosition(0, col);
        Vector3 spawnPos = columnBase + Vector3.down * cellHeight * columnSpawnDepthMultiplier;

        int pending = 0;
        bool done = false;

        void OnOneDone()
        {
            pending--;
            if (pending <= 0)
                done = true;
        }

        for (int row = 0; row < ctx.BoardManager.Rows; row++)
        {
            Slot slot = ctx.BoardManager.GetSlot(row, col);
            if (slot == null || !slot.HasBlock)
                continue;

            Block block = slot.CurrentBlock;
            if (block == null || !block.TryGetComponent<BlockView>(out var view))
                continue;

            Vector3 targetPos = ctx.Layout.GetWorldPosition(row, col);
            float delay = row * rowStagger;

            pending++;
            view.PlayRiseFromWorldPosition(spawnPos, targetPos, riseDuration, row, delay, OnOneDone);
        }

        if (pending == 0)
            yield break;

        float maxWait = riseDuration + rowStagger * ctx.BoardManager.Rows + 0.35f;
        float elapsed = 0f;
        while (!done && elapsed < maxWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}

/// <summary>Helper tìm 3 block cùng type trong một cột.</summary>
public static class ColumnMatchClearLogic
{
    public static bool TryFindThreeSameTypeInColumn(BoardManager board, int col, SkillEffectPayload payload)
    {
        payload.Clear();
        if (board == null || col < 0 || col >= board.Columns)
            return false;

        var byType = new Dictionary<int, List<(int row, Block block)>>();

        for (int row = 0; row < board.Rows; row++)
        {
            Slot slot = board.GetSlot(row, col);
            if (slot == null || !slot.HasBlock)
                continue;

            Block block = slot.CurrentBlock;
            if (block == null || block.IsPendingDestroy)
                continue;

            int typeId = block.TypeId;
            if (typeId <= 0)
                continue;

            if (!byType.TryGetValue(typeId, out var list))
            {
                list = new List<(int, Block)>();
                byType.Add(typeId, list);
            }

            list.Add((row, block));
        }

        foreach (var pair in byType)
        {
            if (pair.Value.Count < 3)
                continue;

            pair.Value.Sort((a, b) => a.row.CompareTo(b.row));

            for (int i = 0; i < 3; i++)
            {
                var entry = pair.Value[i];
                payload.CellsToClear.Add(new Vector2Int(entry.row, col));
                payload.BlocksToClear.Add(entry.block);
            }

            return true;
        }

        return false;
    }
}
