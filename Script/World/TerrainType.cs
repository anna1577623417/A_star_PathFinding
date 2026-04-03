using UnityEngine;

/// <summary>
/// 地形类型枚举 —— v2.1 新增 Trap（陷阱）
/// </summary>
public enum TerrainType {
    Normal,     // 普通，cost 1
    Wall,       // 墙壁，不可通过
    Water,      // 水面，不可通过
    Bridge,     // 桥梁，覆盖水面，cost 1
    Mud,        // 泥地，减速，cost 3
    SpeedBoost, // 加速带，cost 0.5
    Portal,     // 传送门，cost ≈ 0（跳跃时几乎免费）
    Exit,       // 出口，到达即通关
    Trap        // 陷阱，可通过但造成伤害
}

/// <summary>
/// 地形属性查询 —— v2.1 升级
/// 
/// 【v2.0 → v2.1 变化】
///   + Trap 类型（可走，cost 1，但有伤害）
///   * Portal 的 GetCost 保持 1（常规移动代价），
///     传送跳跃的"几乎免费"由 Pathfinder.GetMoveCost 在跳跃时特殊处理
///     这样设计的原因：走到 Portal 格子上的代价 = 正常，
///     从 Portal 跳到另一个 Portal 的代价 ≈ 0
/// </summary>
public static class TerrainData {
    public static bool IsWalkable(TerrainType type) {
        return type switch {
            TerrainType.Wall => false,
            TerrainType.Water => false,
            _ => true
        };
    }

    /// <summary>
    /// 地形基础移动代价（不含策略修正）
    /// 这是"物理属性"——泥地就是慢，和玩家选择无关
    /// </summary>
    public static float GetCost(TerrainType type) {
        return type switch {
            TerrainType.Mud => 3f,
            TerrainType.SpeedBoost => 0.5f,
            TerrainType.Wall => float.MaxValue,
            TerrainType.Water => float.MaxValue,
            _ => 1f
        };
    }

    /// <summary>陷阱默认伤害值</summary>
    public static int GetTrapDamage(TerrainType type) {
        return type switch {
            TerrainType.Trap => 15,
            _ => 0
        };
    }

    /// <summary>地形颜色</summary>
    public static Color GetColor(TerrainType type) {
        return type switch {
            TerrainType.Normal => new Color(0.75f, 0.75f, 0.75f),
            TerrainType.Wall => new Color(0.22f, 0.16f, 0.12f),
            TerrainType.Water => new Color(0.15f, 0.35f, 0.65f),
            TerrainType.Bridge => new Color(0.55f, 0.40f, 0.25f),
            TerrainType.Mud => new Color(0.45f, 0.35f, 0.20f),
            TerrainType.SpeedBoost => new Color(0.2f, 0.85f, 0.85f),
            TerrainType.Portal => new Color(0.7f, 0.3f, 0.9f),
            TerrainType.Exit => new Color(1f, 0.85f, 0f),
            TerrainType.Trap => new Color(0.85f, 0.2f, 0.25f),  // 暗红
            _ => Color.white
        };
    }

    public static Color GetPortalColor(int portalID) {
        return portalID switch {
            0 => new Color(0.7f, 0.2f, 0.9f),
            1 => new Color(0.9f, 0.4f, 0.1f),
            2 => new Color(0.1f, 0.9f, 0.5f),
            3 => new Color(0.9f, 0.1f, 0.4f),
            _ => new Color(0.5f, 0.5f, 0.9f)
        };
    }
}