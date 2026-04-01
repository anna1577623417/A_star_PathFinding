/// <summary>
/// 寻路节点数据 —— v2.0 升级版
/// 
/// 【v1.0 → v2.0 变化】
///   + terrainType  — 地形类型（决定是否可走 + 移动代价）
///   + moveCost     — 从 TerrainData 查询的移动代价
///   + portalID     — 传送门配对 ID（-1 = 非传送门）
///   + portalTarget — 传送目的地（A* 视为额外邻居）
///   * walkable     — 现在从 terrainType 自动推导
///   * gCost        — 从 int 改为 float（支持 √2 和权重）
/// </summary>
public class Node {
    public int x;
    public int y;

    // ---- 地形 ----
    public TerrainType terrainType;
    public bool walkable;
    public float moveCost;

    // ---- 传送门 ----
    public int portalID = -1;           // -1 = 不是传送门
    public Node portalTarget = null;    // 配对的另一个传送门

    // ---- A* 寻路数据 ----
    public float gCost;
    public float hCost;
    public float fCost => gCost + hCost;
    public Node parent;

    public Node(int x, int y, TerrainType terrain = TerrainType.Normal) {
        this.x = x;
        this.y = y;
        SetTerrain(terrain);
    }

    /// <summary>设置地形类型，自动更新 walkable 和 moveCost</summary>
    public void SetTerrain(TerrainType type) {
        terrainType = type;
        walkable = TerrainData.IsWalkable(type);
        moveCost = TerrainData.GetCost(type);
    }

    /// <summary>每次寻路前重置 A* 数据</summary>
    public void Reset() {
        gCost = float.MaxValue;   // v2.0: 初始为极大值而非 0
        hCost = 0;
        parent = null;
    }
}