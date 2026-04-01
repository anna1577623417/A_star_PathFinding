using UnityEngine;

/// <summary>
/// 玩家数据（ScriptableObject，可在 Inspector 调参）
/// 创建方法：Assets → Create → AStarProject → PlayerData
/// </summary>
[CreateAssetMenu(fileName = "PlayerData", menuName = "AStarProject/PlayerData")]
public class PlayerData : ScriptableObject {
    [Header("Movement")]
    public float moveSpeed = 5f;         // 格/秒，沿路径移动的速度
    public float wasdMoveInterval = 0.15f; // WASD 按键移动间隔（秒）

    [Header("Pathfinding Visualization")]
    public float searchStepDelay = 0.02f;  // A*搜索每步延迟（秒）
    public float pathWalkDelay = 0.1f;     // 沿路径行走时每格延迟

    [Header("Stats")]
    public int totalSteps;                  // 累计行走步数
    public int pathfindCount;               // 累计寻路次数
}