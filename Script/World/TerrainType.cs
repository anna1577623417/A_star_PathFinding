using UnityEngine;

/// <summary>
/// 地形类型枚举 —— 所有可能的格子地形
/// 
/// 每种地形有三个属性：
///   1. 是否可行走
///   2. 移动代价（用于 A* 的 gCost 权重）
///   3. 特殊行为（传送、胜利等）
/// </summary>
public enum TerrainType {
    Normal,     // 普通地面，代价 1
    Wall,       // 墙壁，不可通过
    Water,      // 水面，不可通过（除非有桥）
    Bridge,     // 桥梁，覆盖水面，代价 1
    Mud,        // 泥地，减速，代价 3
    SpeedBoost, // 加速带，代价 0.5（路径优先走这里）
    Portal,     // 传送门，代价 1，A* 视为额外邻居
    Exit        // 出口，到达即通关
}

/// <summary>
/// 地形数据工具类 —— 查询地形属性
/// 
/// 【为什么不用 ScriptableObject】
///   地形属性是固定的枚举映射，不需要运行时修改。
///   用静态方法查询比创建 8 个 SO 资产更简洁。
///   如果未来要加"地形编辑器"（玩家自定义代价），再迁移到 SO。
/// </summary>
public static class TerrainData {
    /// <summary>是否可行走</summary>
    public static bool IsWalkable(TerrainType type) {
        return type switch {
            TerrainType.Wall => false,
            TerrainType.Water => false,
            _ => true
        };
    }

    /// <summary>移动代价（A* 的 gCost 权重）</summary>
    public static float GetCost(TerrainType type) {
        return type switch {
            TerrainType.Mud => 3f,
            TerrainType.SpeedBoost => 0.5f,
            TerrainType.Wall => float.MaxValue,
            TerrainType.Water => float.MaxValue,
            _ => 1f   // Normal, Bridge, Portal, Exit
        };
    }

    /// <summary>地形基础颜色（Shader 用）</summary>
    public static Color GetColor(TerrainType type) {
        return type switch {
            TerrainType.Normal => new Color(0.75f, 0.75f, 0.75f),    // 浅灰
            TerrainType.Wall => new Color(0.22f, 0.16f, 0.12f),    // 深棕
            TerrainType.Water => new Color(0.15f, 0.35f, 0.65f),    // 深蓝
            TerrainType.Bridge => new Color(0.55f, 0.40f, 0.25f),    // 木棕
            TerrainType.Mud => new Color(0.45f, 0.35f, 0.20f),    // 泥黄
            TerrainType.SpeedBoost => new Color(0.2f, 0.85f, 0.85f),     // 亮青
            TerrainType.Portal => new Color(0.7f, 0.3f, 0.9f),      // 紫色（默认）
            TerrainType.Exit => new Color(1f, 0.85f, 0f),          // 金黄
            _ => Color.white
        };
    }

    /// <summary>传送门配对颜色（按 portalID 区分）</summary>
    public static Color GetPortalColor(int portalID) {
        // 不同 ID 的传送门用不同颜色
        return portalID switch {
            0 => new Color(0.7f, 0.2f, 0.9f),   // 紫色
            1 => new Color(0.9f, 0.4f, 0.1f),   // 橙色
            2 => new Color(0.1f, 0.9f, 0.5f),   // 翠绿
            3 => new Color(0.9f, 0.1f, 0.4f),   // 玫红
            _ => new Color(0.5f, 0.5f, 0.9f)    // 淡蓝
        };
    }
}