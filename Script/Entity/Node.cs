/// <summary>
/// 寻路节点 —— v2.1 新增陷阱伤害
/// 
/// 【v2.0 → v2.1 变化】
///   + damage    — 走上这个格子会受到的伤害（陷阱用）
///   + isTrap    — 便捷属性，等价于 terrainType == Trap
/// </summary>
public class Node {
    public int x;
    public int y;

    // ---- 地形 ----
    public TerrainType terrainType;
    public bool walkable;
    public float moveCost;      // 地形基础代价（物理属性）
    public int damage;          // 经过时受到的伤害

    // ---- 传送门 ----
    public int portalID = -1;
    public Node portalTarget = null;

    // ---- A* 数据 ----
    public float gCost;
    public float hCost;
    public float fCost => gCost + hCost;
    public Node parent;

    // ---- 便捷属性 ----
    public bool isTrap => terrainType == TerrainType.Trap;
    public bool isPortal => terrainType == TerrainType.Portal && portalTarget != null;

    public Node(int x, int y, TerrainType terrain = TerrainType.Normal) {
        this.x = x;
        this.y = y;
        SetTerrain(terrain);
    }

    public void SetTerrain(TerrainType type) {
        terrainType = type;
        walkable = TerrainData.IsWalkable(type);
        moveCost = TerrainData.GetCost(type);
        damage = TerrainData.GetTrapDamage(type);
    }

    public void Reset() {
        gCost = float.MaxValue;
        hCost = 0;
        parent = null;
    }
}