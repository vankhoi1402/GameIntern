using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Click chọn cột (Idle) hoặc xác nhận cột khi đang targeting skill.</summary>
public class SkillInputController : MonoBehaviour
{
    private static readonly List<RaycastResult> s_RaycastResults = new List<RaycastResult>();
    [SerializeField] private Camera targetCamera;
    [SerializeField] private BoardManager boardManager;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
    }

    private void Update()
    {
        if (SkillManager.Instance == null)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (SkillManager.Instance.IsTargeting)
                SkillManager.Instance.CancelTargeting();
            else
                SkillManager.Instance.ClearSelectedColumn();
            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        if (IsPointerOverInteractiveUI())
            return;

        if (targetCamera == null || boardManager?.Layout == null)
            return;

        Vector3 world = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;
        Vector2Int grid = boardManager.Layout.GetGridPosition(world);
        int col = grid.y;

        if (SkillManager.Instance.IsTargeting)
        {
            if (BoardStateManager.Instance == null ||
                BoardStateManager.Instance.CurrentState != BoardState.SkillTargeting)
                return;

            SkillTarget target = BuildTargetFromGrid(grid);
            if (!SkillManager.Instance.TryConfirmTarget(target))
                Debug.Log($"[SkillInput] Cột {col + 1} không hợp lệ — cần ≥3 block cùng loại trong cột.");
            return;
        }

        if (!CanSelectColumnOnBoard())
            return;

        SkillManager.Instance.TrySelectColumn(col);
    }

    private bool CanSelectColumnOnBoard()
    {
        if (SkillManager.Instance.IsExecuting)
            return false;

        if (BoardStateManager.Instance == null)
            return true;

        return BoardStateManager.Instance.CanAcceptInput();
    }

    /// <summary>Chỉ chặn khi trỏ vào UI tương tác (Button…), không chặn panel nền full màn.</summary>
    private static bool IsPointerOverInteractiveUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointerData = new PointerEventData(eventSystem) { position = Input.mousePosition };
        s_RaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, s_RaycastResults);

        foreach (RaycastResult result in s_RaycastResults)
        {
            if (result.gameObject.GetComponentInParent<Selectable>() != null)
                return true;
        }

        return false;
    }

    private SkillTarget BuildTargetFromGrid(Vector2Int grid)
    {
        SkillDefinition pending = SkillManager.Instance.PendingSkill;
        if (pending == null)
            return SkillTarget.None;

        switch (pending.TargetMode)
        {
            case SkillTargetMode.PickColumn:
                return SkillTarget.ForColumn(grid.y);

            case SkillTargetMode.PickRow:
                return new SkillTarget
                {
                    Mode = SkillTargetMode.PickRow,
                    Row = grid.x
                };

            case SkillTargetMode.PickBlock:
            {
                Slot slot = boardManager.GetSlot(grid.x, grid.y);
                return new SkillTarget
                {
                    Mode = SkillTargetMode.PickBlock,
                    Row = grid.x,
                    Col = grid.y,
                    Block = slot != null ? slot.CurrentBlock : null
                };
            }

            default:
                return SkillTarget.None;
        }
    }
}
