using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 寻路算法枚举 —— 支持切换不同策略
/// </summary>
public enum PathfindingAlgorithm {
    AStar,          // f = g + h（最优路径）
    Dijkstra,       // f = g    （无启发，保证最短，但慢）
    GreedyBestFirst // f = h    （只看启发，快但不保证最短）
}

/// <summary>
/// 寻路结果 —— 封装所有统计数据
/// </summary>
public class PathResult {
    public List<Node> path;         // 最终路径（null = 失败）
    public int exploredCount;       // 探索的节点数（closedSet.Count）
    public float searchTime;        // 算法耗时（秒）
    public bool success;

    public static PathResult Fail(int explored, float time) => new() {
        path = null, exploredCount = explored, searchTime = time, success = false
    };
}

/// <summary>
/// 寻路系统 —— v2.0 完全重写
/// 
/// 【v1.0 → v2.0 变化】
///   + 8方向移动（对角代价 √2）
///   + 地形权重（gCost += terrainCost）
///   + 传送门支持（Portal 节点视为额外邻居）
///   + 多算法切换（A* / Dijkstra / Greedy）
///   + 探索节点数统计
///   + PathResult 封装返回值
///   * gCost 初始值改为 float.MaxValue（v1.0 是 0，导致首次比较错误）
/// </summary>
public static class Pathfinder {
    // 8 方向偏移量
    private static readonly Vector2Int[] dirs8 = {
        new( 1,  0), new(-1,  0), new( 0,  1), new( 0, -1), // 上下左右
        new( 1,  1), new( 1, -1), new(-1,  1), new(-1, -1)  // 四个对角
    };

    // 直线和对角的代价
    private const float STRAIGHT_COST = 1f;
    private const float DIAGONAL_COST = 1.414f; // √2

    // ═══════════════════════════════════════════
    //  同步版 —— 立即返回 PathResult
    // ═══════════════════════════════════════════
    public static PathResult FindPath(
        Node start, Node target, Node[,] grid,
        PathfindingAlgorithm algorithm = PathfindingAlgorithm.AStar) {
        float startTime = Time.realtimeSinceStartup;
        ResetGrid(grid);

        var openSet = new List<Node> { start };
        var closedSet = new HashSet<Node>();

        start.gCost = 0;
        start.hCost = Heuristic(start, target);

        while (openSet.Count > 0) {
            Node current = GetBestNode(openSet, algorithm);
            openSet.Remove(current);
            closedSet.Add(current);

            // 到达目标
            if (current == target) {
                float elapsed = Time.realtimeSinceStartup - startTime;
                return new PathResult {
                    path = Retrace(start, target),
                    exploredCount = closedSet.Count,
                    searchTime = elapsed,
                    success = true
                };
            }

            // 展开邻居
            foreach (var neighbor in GetNeighbors(current, grid)) {
                if (!neighbor.walkable || closedSet.Contains(neighbor))
                    continue;

                float stepCost = IsAdjacent(current, neighbor)
                    ? STRAIGHT_COST
                    : DIAGONAL_COST;

                // ★ 地形权重：步进代价 × 目标格的移动代价
                float newGCost = current.gCost + stepCost * neighbor.moveCost;

                if (newGCost < neighbor.gCost) {
                    neighbor.gCost = newGCost;
                    neighbor.hCost = Heuristic(neighbor, target);
                    neighbor.parent = current;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        float failTime = Time.realtimeSinceStartup - startTime;
        return PathResult.Fail(closedSet.Count, failTime);
    }

    // ═══════════════════════════════════════════
    //  协程版 —— 逐步可视化
    // ═══════════════════════════════════════════
    public static IEnumerator FindPathVisual(
        Node start, Node target, Node[,] grid,
        PathfindingAlgorithm algorithm,
        Action<Node, bool> onVisit,
        Action<PathResult> onComplete,
        float stepDelay = 0.02f) {
        float startTime = Time.realtimeSinceStartup;
        ResetGrid(grid);

        var openSet = new List<Node> { start };
        var closedSet = new HashSet<Node>();

        start.gCost = 0;
        start.hCost = Heuristic(start, target);

        while (openSet.Count > 0) {
            Node current = GetBestNode(openSet, algorithm);
            openSet.Remove(current);
            closedSet.Add(current);

            onVisit?.Invoke(current, false); // 进入 closedSet

            if (current == target) {
                float elapsed = Time.realtimeSinceStartup - startTime;
                onComplete?.Invoke(new PathResult {
                    path = Retrace(start, target),
                    exploredCount = closedSet.Count,
                    searchTime = elapsed,
                    success = true
                });
                yield break;
            }

            foreach (var neighbor in GetNeighbors(current, grid)) {
                if (!neighbor.walkable || closedSet.Contains(neighbor))
                    continue;

                float stepCost = IsAdjacent(current, neighbor) ? STRAIGHT_COST : DIAGONAL_COST;
                float newGCost = current.gCost + stepCost * neighbor.moveCost;

                if (newGCost < neighbor.gCost) {
                    neighbor.gCost = newGCost;
                    neighbor.hCost = Heuristic(neighbor, target);
                    neighbor.parent = current;

                    if (!openSet.Contains(neighbor)) {
                        openSet.Add(neighbor);
                        onVisit?.Invoke(neighbor, true); // 进入 openSet
                    }
                }
            }

            if (stepDelay > 0) yield return new WaitForSeconds(stepDelay);
            else yield return null;
        }

        float failTime = Time.realtimeSinceStartup - startTime;
        onComplete?.Invoke(PathResult.Fail(closedSet.Count, failTime));
    }

    // ═══════════════════════════════════════════
    //  核心工具方法
    // ═══════════════════════════════════════════

    /// <summary>
    /// 根据算法类型选择 openSet 中的最优节点
    /// A*:      f = g + h
    /// Dijkstra: f = g（h 被忽略）
    /// Greedy:   f = h（g 被忽略）
    /// </summary>
    static Node GetBestNode(List<Node> openSet, PathfindingAlgorithm algo) {
        Node best = openSet[0];
        for (int i = 1; i < openSet.Count; i++) {
            float bestF = GetF(best, algo);
            float curF = GetF(openSet[i], algo);
            if (curF < bestF || (Mathf.Approximately(curF, bestF) && openSet[i].hCost < best.hCost))
                best = openSet[i];
        }
        return best;
    }

    static float GetF(Node n, PathfindingAlgorithm algo) {
        return algo switch {
            PathfindingAlgorithm.Dijkstra => n.gCost,
            PathfindingAlgorithm.GreedyBestFirst => n.hCost,
            _ => n.fCost  // A*
        };
    }

    /// <summary>
    /// 启发函数：八方向欧几里得距离
    /// （v1.0 用曼哈顿距离，但 8 方向移动应该用欧几里得或 Chebyshev）
    /// </summary>
    static float Heuristic(Node a, Node b) {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        // Octile distance：比欧几里得更适合 8 方向网格
        return STRAIGHT_COST * (dx + dy) + (DIAGONAL_COST - 2 * STRAIGHT_COST) * Mathf.Min(dx, dy);
    }

    /// <summary>判断两个节点是否直线相邻（非对角）</summary>
    static bool IsAdjacent(Node a, Node b) {
        return (a.x == b.x || a.y == b.y);
    }

    /// <summary>
    /// 获取邻居 —— 8 方向 + 传送门
    /// 
    /// 【对角移动的墙角检查】
    ///   从 (0,0) 走到 (1,1) 时，如果 (1,0) 或 (0,1) 是墙，
    ///   不允许对角穿过（防止"擦墙而过"的不自然路径）
    /// </summary>
    static List<Node> GetNeighbors(Node node, Node[,] grid) {
        var list = new List<Node>(10); // 最多 8 + 1 传送门
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        for (int i = 0; i < dirs8.Length; i++) {
            int nx = node.x + dirs8[i].x;
            int ny = node.y + dirs8[i].y;

            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;

            // 对角移动的墙角检查（i >= 4 是对角方向）
            if (i >= 4) {
                int cx = node.x + dirs8[i].x;
                int cy = node.y;
                int dx = node.x;
                int dy = node.y + dirs8[i].y;

                // 如果两个相邻的直线格有任一不可走，禁止对角
                if (!InBounds(cx, cy, w, h) || !grid[cx, cy].walkable) continue;
                if (!InBounds(dx, dy, w, h) || !grid[dx, dy].walkable) continue;
            }

            list.Add(grid[nx, ny]);
        }

        // ★ 传送门：如果当前节点是传送门，把配对节点也加入邻居
        if (node.terrainType == TerrainType.Portal && node.portalTarget != null) {
            list.Add(node.portalTarget);
        }

        return list;
    }

    static bool InBounds(int x, int y, int w, int h) {
        return x >= 0 && y >= 0 && x < w && y < h;
    }

    static void ResetGrid(Node[,] grid) {
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                grid[x, y].Reset();
    }

    static List<Node> Retrace(Node start, Node end) {
        var path = new List<Node>();
        Node cur = end;
        while (cur != start) {
            path.Add(cur);
            cur = cur.parent;
        }
        path.Add(start);
        path.Reverse();
        return path;
    }
}