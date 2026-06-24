using UnityEngine;

/// <summary>Tham chiếu runtime dùng chung cho mọi skill effect.</summary>
public class SkillContext
{
    public BoardManager BoardManager { get; set; }
    public LevelManager LevelManager { get; set; }
    public BoardLayout Layout => BoardManager != null ? BoardManager.Layout : null;
}
