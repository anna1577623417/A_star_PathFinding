/// <summary>
/// A*寻路节点数据（纯数据，不挂MonoBehaviour）
/// </summary>
public class Node {
    public int x;
    public int y;
    public bool walkable;
    public int gCost;
    public int hCost;
    public int fCost => gCost + hCost;
    public Node parent;

    public Node(int x, int y, bool walkable = true) {
        this.x = x;
        this.y = y;
        this.walkable = walkable;
    }

    /// <summary>
    /// 每次寻路前必须重置，否则旧的 parent/gCost 会污染新路径
    /// 这是你原版"路径总是L形"的根本原因之一
    /// </summary>
    public void Reset() {
        gCost = 0;
        hCost = 0;
        parent = null;
    }
}