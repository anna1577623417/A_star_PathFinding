using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网格管理器 —— v2.0 升级版
/// 
/// 【v1.0 → v2.0 变化】
///   + 多地形生成（Wall, Water, Mud, SpeedBoost, Portal, Exit）
///   + 传送门自动配对（portalID 相同的两个节点互相链接）
///   + 出口系统（Exit 节点）
///   + 地形编辑（右键循环切换地形类型）
/// </summary>
public class GridManager : MonoSingleton<GridManager> {
    [Header("═══ 网格尺寸 ═══")]
    [Range(5, 50)]
    [SerializeField] public int width = 20;
    [Range(5, 50)]
    [SerializeField] public int height = 20;

    [Header("═══ 地形生成 ═══")]
    [Tooltip("随机种子（0=完全随机）")]
    [SerializeField] private int seed = 42;

    [Range(0f, 0.25f)]
    [SerializeField] private float wallDensity = 0.15f;
    [Range(0f, 0.1f)]
    [SerializeField] private float waterDensity = 0.05f;
    [Range(0f, 0.1f)]
    [SerializeField] private float mudDensity = 0.05f;
    [Range(0f, 0.05f)]
    [SerializeField] private float speedBoostDensity = 0.02f;

    [Header("═══ 特殊地形 ═══")]
    [Tooltip("传送门对数（每对 2 个节点）")]
    [Range(0, 5)]
    [SerializeField] private int portalPairs = 2;

    public Node[,] grid;
    public NodeView[,] views;

    // 传送门列表（供 A* 查询）
    public List<Node> portalNodes = new();

    // 出口位置
    public Node exitNode;

    /// <summary>由 GameInitializer 调用</summary>
    public void Init(NodeView[,] nodeViews) {
        views = nodeViews;
        width = views.GetLength(0);
        height = views.GetLength(1);

        // 创建数据层
        grid = new Node[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = new Node(x, y);

        // 生成地形
        GenerateTerrain();

        // 同步视觉
        SyncAllVisuals();
    }

    // ═══════════════════════════════════════════
    //  地形生成
    // ═══════════════════════════════════════════
    void GenerateTerrain() {
        Random.State oldState = Random.state;
        if (seed != 0) Random.InitState(seed);
        else Random.InitState(System.DateTime.Now.Millisecond);

        // ---- 第一遍：随机分配基础地形 ----
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                // 起点附近 3×3 保持空旷
                if (x <= 2 && y <= 2) continue;
                // 出口区域保留
                if (x >= width - 3 && y >= height - 3) continue;

                float r = Random.value;

                if (r < wallDensity)
                    grid[x, y].SetTerrain(TerrainType.Wall);
                else if (r < wallDensity + waterDensity)
                    grid[x, y].SetTerrain(TerrainType.Water);
                else if (r < wallDensity + waterDensity + mudDensity)
                    grid[x, y].SetTerrain(TerrainType.Mud);
                else if (r < wallDensity + waterDensity + mudDensity + speedBoostDensity)
                    grid[x, y].SetTerrain(TerrainType.SpeedBoost);
            }
        }

        // ---- 在水面上随机放桥 ----
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                if (grid[x, y].terrainType == TerrainType.Water && Random.value < 0.3f)
                    grid[x, y].SetTerrain(TerrainType.Bridge);
            }
        }

        // ---- 放置传送门 ----
        PlacePortals();

        // ---- 放置出口 ----
        PlaceExit();

        Random.state = oldState;
    }

    void PlacePortals() {
        portalNodes.Clear();

        for (int i = 0; i < portalPairs; i++) {
            // 找两个空地
            Node a = FindRandomEmptyNode();
            Node b = FindRandomEmptyNode();
            if (a == null || b == null) continue;
            if (a == b) continue;

            a.SetTerrain(TerrainType.Portal);
            a.portalID = i;
            b.SetTerrain(TerrainType.Portal);
            b.portalID = i;

            // 互相链接
            a.portalTarget = b;
            b.portalTarget = a;

            portalNodes.Add(a);
            portalNodes.Add(b);
        }
    }

    void PlaceExit() {
        // 出口放在右上角区域
        for (int x = width - 1; x >= width - 3; x--) {
            for (int y = height - 1; y >= height - 3; y--) {
                if (grid[x, y].terrainType == TerrainType.Normal) {
                    grid[x, y].SetTerrain(TerrainType.Exit);
                    exitNode = grid[x, y];
                    return;
                }
            }
        }
        // 兜底：强制放在右上角
        grid[width - 1, height - 1].SetTerrain(TerrainType.Exit);
        exitNode = grid[width - 1, height - 1];
    }

    Node FindRandomEmptyNode() {
        // 最多尝试 100 次
        for (int i = 0; i < 100; i++) {
            int x = Random.Range(3, width - 3);
            int y = Random.Range(3, height - 3);
            if (grid[x, y].terrainType == TerrainType.Normal)
                return grid[x, y];
        }
        return null;
    }

    // ═══════════════════════════════════════════
    //  同步视觉
    // ═══════════════════════════════════════════
    void SyncAllVisuals() {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                views[x, y].SetTerrain(grid[x, y].terrainType, grid[x, y].portalID);
    }

    // ═══════════════════════════════════════════
    //  公共查询
    // ═══════════════════════════════════════════
    public Node GetNode(int x, int y) {
        if (!InBounds(x, y)) return null;
        return grid[x, y];
    }

    public NodeView GetView(int x, int y) {
        if (!InBounds(x, y)) return null;
        return views[x, y];
    }

    public bool InBounds(int x, int y) {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    public void ClearAllPathVisuals() {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                views[x, y].ClearPathVisuals();
    }

    /// <summary>
    /// 右键编辑地形：循环切换
    /// Normal → Wall → Water → Mud → SpeedBoost → Normal
    /// </summary>
    public void CycleTerrainAt(int x, int y) {
        if (!InBounds(x, y)) return;

        var node = grid[x, y];
        // 不允许编辑传送门和出口
        if (node.terrainType == TerrainType.Portal || node.terrainType == TerrainType.Exit)
            return;

        TerrainType next = node.terrainType switch {
            TerrainType.Normal => TerrainType.Wall,
            TerrainType.Wall => TerrainType.Water,
            TerrainType.Water => TerrainType.Mud,
            TerrainType.Mud => TerrainType.SpeedBoost,
            TerrainType.SpeedBoost => TerrainType.Normal,
            _ => TerrainType.Normal
        };

        node.SetTerrain(next);
        views[x, y].SetTerrain(next);

        EventBus.Publish(new TerrainEditedEvent { x = x, y = y, newType = next });
    }
}