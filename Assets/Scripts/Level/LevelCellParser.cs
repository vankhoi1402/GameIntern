using UnityEngine;

/// <summary>Parse ô CSV: "1" hoặc "1:2" (type:stack).</summary>
public static class LevelCellParser
{
    public static bool TryParseCell(string cell, out string typeKey, out int stack)
    {
        typeKey = null;
        stack = 1;

        if (string.IsNullOrWhiteSpace(cell))
            return false;

        string trimmed = cell.Trim();
        if (trimmed == "0")
            return false;

        int colon = trimmed.IndexOf(':');
        if (colon < 0)
        {
            typeKey = trimmed;
            return true;
        }

        typeKey = trimmed.Substring(0, colon).Trim();
        if (string.IsNullOrEmpty(typeKey) || typeKey == "0")
            return false;

        if (!int.TryParse(trimmed.Substring(colon + 1).Trim(), out stack))
            stack = 1;

        stack = Mathf.Clamp(stack, 1, 2);
        return true;
    }
}
