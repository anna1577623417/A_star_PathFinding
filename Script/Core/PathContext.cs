/// <summary>
/// 寻路策略枚举
/// 
/// 【设计思路】
///   不改算法本身，只改"代价计算函数"的权重。
///   同一张地图、同一个 A*，不同策略 → 不同路径 → 玩家有选择感。
///
/// 【策略对比（同一起终点）】
///   Fastest:      穿过泥地直达（快但脏）
///   AvoidMud:     绕开泥地走远路（慢但安全）
///   PreferPortal: 绕路去传送门再跳过去（可能更快）
///   Cautious:     低血时避开陷阱（活着比快重要）
/// </summary>
public enum PathPolicy {
    Fastest,        // 最快时间（默认，terrain cost 原值）
    Shortest,       // 最短距离（忽略 terrain cost，所有格子 cost=1）
    AvoidMud,       // 回避泥地（Mud cost ×3 惩罚）
    PreferPortal,   // 偏好传送门（Portal cost ×0.1 奖励）
    Cautious        // 谨慎模式（陷阱惩罚随血量放大）
}

/// <summary>
/// 寻路上下文 —— 传入 Pathfinder，影响代价计算
/// 
/// 【为什么不直接改 Node.moveCost】
///   Node.moveCost 是地形的"物理属性"（泥地就是慢），
///   PathContext 是"决策偏好"（我选择避开泥地）。
///   同一个 Node，不同 Context → 不同的实际代价。
///   数据和策略分离 → 可以同时算两条路径做对比。
/// </summary>
public class PathContext {
    public PathPolicy policy;
    public bool allowDiagonal;
    public bool allowPortal;
    public int currentHP;       // 玩家当前血量（影响陷阱惩罚）
    public int maxHP;

    /// <summary>默认上下文</summary>
    public static PathContext Default => new PathContext {
        policy = PathPolicy.Fastest,
        allowDiagonal = true,
        allowPortal = true,
        currentHP = 100,
        maxHP = 100
    };
}