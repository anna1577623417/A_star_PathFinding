using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>寻路算法枚举</summary>
public enum PathfindingAlgorithm {
    AStar,
    Dijkstra,
    GreedyBestFirst,
    JumpPointSearch
}

/// <summary>
/// 寻路结果 —— v2.1 升级
/// 新增：totalCost, usedPortal, predictedDamage
/// </summary>
public class PathResult {
    public List<Node> path;
    public int exploredCount;
    public float searchTime;
    public float totalCost;         // 路径总代价
    public bool usedPortal;         // 是否经过了传送门
    public int predictedDamage;     // 预测总伤害（陷阱）
    public bool success;

    public static PathResult Fail(int explored, float time) => new() {
        path = null, exploredCount = explored, searchTime = time,
        totalCost = 0, usedPortal = false, predictedDamage = 0, success = false
    };
}

/// <summary>
/// 策略驱动寻路系统 —— v2.1 完全重写
/// 
/// 【v2.0 → v2.1 核心变化】
///   1. GetMoveCost() 从"查表"变为"策略函数"
///      同一条边，不同策略 → 不同代价 → 不同路径
///   2. Portal 跳跃代价 ≈ 0.01（而非 1.0）
///      解决"Portal 不会被主动选择"的问题
///   3. 陷阱伤害计入代价，且随血量放大
///      低血时自动绕开陷阱
///   4. PathResult 包含预测伤害，UI 可以在寻路前告诉玩家"这条路会掉多少血"
///
/// 【为什么 Portal 在 v2.0 不被选择】
///   A* 的 f = g + h。Portal 是"跳到远处"，但 h 不知道可以跳，
///   所以 h(Portal目标) 很可能比 h(直接走) 大 → A* 不会展开 Portal 方向。
///   解决：Portal 跳跃 cost 设为 0.01，几乎免费 → g 极小 → f 小 → 被选中。
///
/// 【策略如何改变路径（同一张图）】
///   Fastest:      泥地 cost=3, 陷阱 cost=1     → 可能穿泥/踩阱
///   AvoidMud:     泥地 cost=9, 陷阱 cost=1     → 绕开泥地
///   PreferPortal: Portal跳跃 cost=0.001        → 绕路去传送门
///   Cautious:     陷阱 cost=1+damage×riskFactor → 低血时大幅绕开
/// </summary>
public static class Pathfinder {
    // 8方向偏移
    private static readonly Vector2Int[] dirs8 = {
        new( 1,  0), new(-1,  0), new( 0,  1), new( 0, -1),
        new( 1,  1), new( 1, -1), new(-1,  1), new(-1, -1)
    };

    private const float STRAIGHT = 1f;
    private const float DIAGONAL = 1.414f;
    private const float PORTAL_JUMP_DISTANCE = 1f; // A1 -> A2 视作 1 步距离

    // ═══════════════════════════════════════════
    //  同步版
    // ═══════════════════════════════════════════
    public static PathResult FindPath(
        Node start, Node target, Node[,] grid,
        PathfindingAlgorithm algorithm,
        PathContext ctx) {
        if (algorithm == PathfindingAlgorithm.JumpPointSearch) {
            return FindPathJPS(start, target, grid, ctx);
        }

        float startTime = Time.realtimeSinceStartup;
        ResetGrid(grid);

        var openSet = new List<Node> { start };
        var closedSet = new HashSet<Node>();

        start.gCost = 0;
        start.hCost = Heuristic(start, target, ctx);

        while (openSet.Count > 0) {
            Node current = GetBestNode(openSet, algorithm, ctx);
            openSet.Remove(current);
            closedSet.Add(current);

            if (current == target) {
                float elapsed = Time.realtimeSinceStartup - startTime;
                var path = Retrace(start, target);
                return BuildResult(path, closedSet.Count, elapsed);
            }

            foreach (var neighbor in GetNeighbors(current, grid, ctx)) {
                if (!neighbor.walkable || closedSet.Contains(neighbor))
                    continue;

                // ★ 策略驱动代价计算
                float edgeCost = GetMoveCost(current, neighbor, ctx);
                float newGCost = current.gCost + edgeCost;

                if (newGCost < neighbor.gCost) {
                    neighbor.gCost = newGCost;
                    neighbor.hCost = (algorithm == PathfindingAlgorithm.Dijkstra)
                        ? 0 : Heuristic(neighbor, target, ctx);
                    neighbor.parent = current;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return PathResult.Fail(closedSet.Count, Time.realtimeSinceStartup - startTime);
    }

    // ═══════════════════════════════════════════
    //  协程版（可视化）
    // ═══════════════════════════════════════════
    public static IEnumerator FindPathVisual(
        Node start, Node target, Node[,] grid,
        PathfindingAlgorithm algorithm,
        PathContext ctx,
        Action<Node, bool> onVisit,
        Action<PathResult> onComplete,
        float stepDelay = 0.02f) {
        if (algorithm == PathfindingAlgorithm.JumpPointSearch) {
            yield return FindPathVisualJPS(start, target, grid, ctx, onVisit, onComplete, stepDelay);
            yield break;
        }

        float startTime = Time.realtimeSinceStartup;
        ResetGrid(grid);

        var openSet = new List<Node> { start };
        var closedSet = new HashSet<Node>();

        start.gCost = 0;
        start.hCost = Heuristic(start, target, ctx);

        while (openSet.Count > 0) {
            Node current = GetBestNode(openSet, algorithm, ctx);
            openSet.Remove(current);
            closedSet.Add(current);

            onVisit?.Invoke(current, false);

            if (current == target) {
                float elapsed = Time.realtimeSinceStartup - startTime;
                var path = Retrace(start, target);
                onComplete?.Invoke(BuildResult(path, closedSet.Count, elapsed));
                yield break;
            }

            foreach (var neighbor in GetNeighbors(current, grid, ctx)) {
                if (!neighbor.walkable || closedSet.Contains(neighbor))
                    continue;

                float edgeCost = GetMoveCost(current, neighbor, ctx);
                float newGCost = current.gCost + edgeCost;

                if (newGCost < neighbor.gCost) {
                    neighbor.gCost = newGCost;
                    neighbor.hCost = (algorithm == PathfindingAlgorithm.Dijkstra)
                        ? 0 : Heuristic(neighbor, target, ctx);
                    neighbor.parent = current;

                    if (!openSet.Contains(neighbor)) {
                        openSet.Add(neighbor);
                        onVisit?.Invoke(neighbor, true);
                    }
                }
            }

            if (stepDelay > 0) yield return new WaitForSeconds(stepDelay);
            else yield return null;
        }

        onComplete?.Invoke(PathResult.Fail(
            closedSet.Count, Time.realtimeSinceStartup - startTime));
    }

    // ═══════════════════════════════════════════
    //  ★ 核心：策略驱动代价计算
    // ═══════════════════════════════════════════

    /// <summary>
    /// 计算从 from 到 to 的移动代价
    /// 
    /// 分三层叠加：
    ///   ① 基础代价 = 地形 moveCost × 直线/对角系数
    ///   ② 策略修正 = 根据 PathPolicy 放大或缩小特定地形的代价
    ///   ③ 陷阱惩罚 = damage × 血量风险系数
    ///   
    /// 特殊情况：
    ///   Portal 跳跃（from 和 to 是传送配对）→ 固定 0.01
    /// </summary>
    static float GetMoveCost(Node from, Node to, PathContext ctx) {
        // ---- 传送门跳跃：几乎免费 ----
        // 判断：from 是 Portal 且 to 是它的 portalTarget
        if (from.isPortal && from.portalTarget == to)
            return GetPortalJumpCost(ctx);

        // ---- ① 基础代价 ----
        float baseCost = to.moveCost;

        // Shortest 策略：忽略地形，所有格子 cost=1
        if (ctx.policy == PathPolicy.Shortest)
            baseCost = 1f;

        // 对角线系数
        bool isDiagonal = (from.x != to.x && from.y != to.y);
        float directionCost = isDiagonal ? DIAGONAL : STRAIGHT;

        float totalCost = baseCost * directionCost;

        // ---- ② 策略修正 ----
        switch (ctx.policy) {
            case PathPolicy.AvoidMud:
                // 泥地惩罚 ×3（叠加在原有 cost=3 上 → 实际 cost=9）
                if (to.terrainType == TerrainType.Mud)
                    totalCost *= 3f;
                break;

            case PathPolicy.PreferPortal:
                // 走到传送门格子本身也有奖励（引导路径向传送门靠拢）
                if (to.terrainType == TerrainType.Portal)
                    totalCost *= 0.25f;
                break;
        }

        // 全局轻微偏好 Portal（不只 PreferPortal 生效）
        if (to.terrainType == TerrainType.Portal && ctx.policy != PathPolicy.PreferPortal)
            totalCost *= 0.8f;

        // ---- ③ 陷阱惩罚（所有策略都生效）----
        if (to.isTrap && to.damage > 0) {
            // 基础：伤害值直接加到代价上（15 伤害 → +15 代价）
            float damageContribution = to.damage;

            // Cautious 策略 或 低血时：放大惩罚
            if (ctx.policy == PathPolicy.Cautious || ctx.currentHP < ctx.maxHP * 0.3f) {
                // 血越低风险系数越大：100% HP → ×1, 30% HP → ×3, 10% HP → ×10
                float hpRatio = Mathf.Max(0.1f, (float)ctx.currentHP / ctx.maxHP);
                float riskFactor = 1f / hpRatio;
                damageContribution *= riskFactor;
            }

            totalCost += damageContribution;
        }

        return totalCost;
    }

    // ═══════════════════════════════════════════
    //  邻居获取（含 Portal 跳跃 + 对角墙角检查）
    // ═══════════════════════════════════════════

    static List<Node> GetNeighbors(Node node, Node[,] grid, PathContext ctx) {
        var list = new List<Node>(10);
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        int dirCount = ctx.allowDiagonal ? 8 : 4;

        for (int i = 0; i < dirCount; i++) {
            int nx = node.x + dirs8[i].x;
            int ny = node.y + dirs8[i].y;
            if (!InBounds(nx, ny, w, h)) continue;

            // 对角墙角检查（i >= 4 是对角方向）
            if (i >= 4) {
                int sideX = node.x + dirs8[i].x;
                int sideY = node.y;
                int sideX2 = node.x;
                int sideY2 = node.y + dirs8[i].y;

                bool side1Blocked = !InBounds(sideX, sideY, w, h) || !grid[sideX, sideY].walkable;
                bool side2Blocked = !InBounds(sideX2, sideY2, w, h) || !grid[sideX2, sideY2].walkable;

                // 两侧都堵 → 禁止对角
                // 一侧堵 → 也禁止（防止擦墙而过）
                if (side1Blocked || side2Blocked) continue;
            }

            list.Add(grid[nx, ny]);
        }

        // ★ 传送门跳跃：当前节点是 Portal → 把配对节点加入邻居
        if (ctx.allowPortal && node.isPortal) {
            list.Add(node.portalTarget);
        }

        return list;
    }

    // ═══════════════════════════════════════════
    //  启发函数 + 工具方法
    // ═══════════════════════════════════════════

    /// <summary>
    /// Octile distance（8方向最优启发）
    /// 比欧几里得更准确，比曼哈顿不会高估
    /// </summary>
    static float Heuristic(Node a, Node b, PathContext ctx) {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        float direct = STRAIGHT * (dx + dy) + (DIAGONAL - 2 * STRAIGHT) * Mathf.Min(dx, dy);

        if (!ctx.allowPortal)
            return direct;

        // Portal 感知启发：起点到传送门入口 + 1 步跳跃 + 出口到目标
        // 避免 A* 因 h 不知道“可跳跃”而迟迟不走 Portal。
        float viaPortal = float.MaxValue;
        var gm = GridManager.Instance;
        if (gm != null && gm.portalNodes != null) {
            foreach (var portal in gm.portalNodes) {
                if (portal == null || portal.portalTarget == null) continue;
                viaPortal = Mathf.Min(viaPortal,
                    OctileDistance(a, portal) + PORTAL_JUMP_DISTANCE + OctileDistance(portal.portalTarget, b));
            }
        }

        float h = Mathf.Min(direct, viaPortal);
        if (ctx.policy == PathPolicy.PreferPortal && viaPortal < float.MaxValue)
            h = Mathf.Min(h, viaPortal * 0.9f);

        return h;
    }

    static Node GetBestNode(List<Node> openSet, PathfindingAlgorithm algo, PathContext ctx) {
        Node best = openSet[0];
        for (int i = 1; i < openSet.Count; i++) {
            float bestF = GetF(best, algo);
            float curF = GetF(openSet[i], algo);
            if (curF < bestF ||
                (Mathf.Approximately(curF, bestF) && openSet[i].hCost < best.hCost) ||
                (ctx.policy == PathPolicy.PreferPortal && Mathf.Approximately(curF, bestF) &&
                 openSet[i].isPortal && !best.isPortal))
                best = openSet[i];
        }
        return best;
    }

    static float GetF(Node n, PathfindingAlgorithm algo) {
        return algo switch {
            PathfindingAlgorithm.Dijkstra => n.gCost,
            PathfindingAlgorithm.GreedyBestFirst => n.hCost,
            _ => n.fCost
        };
    }

    static float GetPortalJumpCost(PathContext ctx) {
        return ctx.policy switch {
            PathPolicy.PreferPortal => 0.35f,
            PathPolicy.Shortest => PORTAL_JUMP_DISTANCE,
            _ => 0.6f
        };
    }

    static float OctileDistance(Node a, Node b) {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return STRAIGHT * (dx + dy) + (DIAGONAL - 2 * STRAIGHT) * Mathf.Min(dx, dy);
    }

    // ═══════════════════════════════════════════
    //  Jump Point Search
    // ═══════════════════════════════════════════
    static PathResult FindPathJPS(Node start, Node target, Node[,] grid, PathContext ctx) {
        float startTime = Time.realtimeSinceStartup;
        ResetGrid(grid);

        var openSet = new List<Node> { start };
        var closedSet = new HashSet<Node>();

        start.gCost = 0f;
        start.hCost = Heuristic(start, target, ctx);

        while (openSet.Count > 0) {
            Node current = GetBestNode(openSet, PathfindingAlgorithm.JumpPointSearch, ctx);
            openSet.Remove(current);
            closedSet.Add(current);

            if (current == target) {
                var compactPath = Retrace(start, target);
                var expandedPath = ExpandJpsPath(compactPath, grid);
                return BuildResult(expandedPath, closedSet.Count, Time.realtimeSinceStartup - startTime);
            }

            var successors = IdentifySuccessors(current, target, grid, ctx);
            foreach (var succ in successors) {
                if (!succ.node.walkable || closedSet.Contains(succ.node))
                    continue;

                float newG = current.gCost + succ.travelCost;
                if (newG < succ.node.gCost) {
                    succ.node.gCost = newG;
                    succ.node.hCost = Heuristic(succ.node, target, ctx);
                    succ.node.parent = current;
                    if (!openSet.Contains(succ.node))
                        openSet.Add(succ.node);
                }
            }
        }

        return PathResult.Fail(closedSet.Count, Time.realtimeSinceStartup - startTime);
    }

    static IEnumerator FindPathVisualJPS(
        Node start, Node target, Node[,] grid, PathContext ctx,
        Action<Node, bool> onVisit, Action<PathResult> onComplete, float stepDelay) {
        float startTime = Time.realtimeSinceStartup;
        ResetGrid(grid);

        var openSet = new List<Node> { start };
        var closedSet = new HashSet<Node>();

        start.gCost = 0f;
        start.hCost = Heuristic(start, target, ctx);

        while (openSet.Count > 0) {
            Node current = GetBestNode(openSet, PathfindingAlgorithm.JumpPointSearch, ctx);
            openSet.Remove(current);
            closedSet.Add(current);
            onVisit?.Invoke(current, false);

            if (current == target) {
                var compactPath = Retrace(start, target);
                var expandedPath = ExpandJpsPath(compactPath, grid);
                onComplete?.Invoke(BuildResult(expandedPath, closedSet.Count, Time.realtimeSinceStartup - startTime));
                yield break;
            }

            var successors = IdentifySuccessors(current, target, grid, ctx);
            foreach (var succ in successors) {
                if (!succ.node.walkable || closedSet.Contains(succ.node))
                    continue;

                float newG = current.gCost + succ.travelCost;
                if (newG < succ.node.gCost) {
                    succ.node.gCost = newG;
                    succ.node.hCost = Heuristic(succ.node, target, ctx);
                    succ.node.parent = current;

                    if (!openSet.Contains(succ.node)) {
                        openSet.Add(succ.node);
                        onVisit?.Invoke(succ.node, true);
                    }
                }
            }

            if (stepDelay > 0) yield return new WaitForSeconds(stepDelay);
            else yield return null;
        }

        onComplete?.Invoke(PathResult.Fail(closedSet.Count, Time.realtimeSinceStartup - startTime));
    }

    struct JumpSuccessor {
        public Node node;
        public float travelCost;
        public JumpSuccessor(Node node, float travelCost) {
            this.node = node;
            this.travelCost = travelCost;
        }
    }

    static List<JumpSuccessor> IdentifySuccessors(Node node, Node target, Node[,] grid, PathContext ctx) {
        var result = new List<JumpSuccessor>(8);
        var dirs = PrunedDirections(node, grid, ctx.allowDiagonal);

        for (int i = 0; i < dirs.Count; i++) {
            var dir = dirs[i];
            if (Jump(node, dir, target, grid, ctx, out Node jumpPoint, out float cost))
                result.Add(new JumpSuccessor(jumpPoint, cost));
        }

        if (ctx.allowPortal && node.isPortal && node.portalTarget != null) {
            result.Add(new JumpSuccessor(node.portalTarget, GetMoveCost(node, node.portalTarget, ctx)));
        }
        return result;
    }

    static bool Jump(Node from, Vector2Int dir, Node target, Node[,] grid, PathContext ctx, out Node jumpPoint, out float travelCost) {
        jumpPoint = null;
        travelCost = 0f;

        int nx = from.x + dir.x;
        int ny = from.y + dir.y;
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        if (!InBounds(nx, ny, w, h)) return false;
        Node next = grid[nx, ny];
        if (!next.walkable) return false;

        travelCost = GetMoveCost(from, next, ctx);

        if (next == target) {
            jumpPoint = next;
            return true;
        }

        if (HasForcedNeighbor(next, dir, grid, ctx.allowDiagonal)) {
            jumpPoint = next;
            return true;
        }

        if (ctx.allowDiagonal && dir.x != 0 && dir.y != 0) {
            if (Jump(next, new Vector2Int(dir.x, 0), target, grid, ctx, out _, out _) ||
                Jump(next, new Vector2Int(0, dir.y), target, grid, ctx, out _, out _)) {
                jumpPoint = next;
                return true;
            }
        }

        if (Jump(next, dir, target, grid, ctx, out Node jp, out float extraCost)) {
            jumpPoint = jp;
            travelCost += extraCost;
            return true;
        }

        return false;
    }

    static List<Vector2Int> PrunedDirections(Node node, Node[,] grid, bool allowDiagonal) {
        var dirs = new List<Vector2Int>(8);
        if (node.parent == null) {
            dirs.Add(new Vector2Int(1, 0));
            dirs.Add(new Vector2Int(-1, 0));
            dirs.Add(new Vector2Int(0, 1));
            dirs.Add(new Vector2Int(0, -1));
            if (allowDiagonal) {
                dirs.Add(new Vector2Int(1, 1));
                dirs.Add(new Vector2Int(1, -1));
                dirs.Add(new Vector2Int(-1, 1));
                dirs.Add(new Vector2Int(-1, -1));
            }
            return dirs;
        }

        int dx = Mathf.Clamp(node.x - node.parent.x, -1, 1);
        int dy = Mathf.Clamp(node.y - node.parent.y, -1, 1);
        dirs.Add(new Vector2Int(dx, dy));

        if (allowDiagonal && dx != 0 && dy != 0) {
            dirs.Add(new Vector2Int(dx, 0));
            dirs.Add(new Vector2Int(0, dy));
            dirs.Add(new Vector2Int(-dx, dy));
            dirs.Add(new Vector2Int(dx, -dy));
        } else if (dx != 0) {
            if (allowDiagonal) {
                dirs.Add(new Vector2Int(dx, 1));
                dirs.Add(new Vector2Int(dx, -1));
            }
        } else if (dy != 0) {
            if (allowDiagonal) {
                dirs.Add(new Vector2Int(1, dy));
                dirs.Add(new Vector2Int(-1, dy));
            }
        }

        // 去重 + 可走过滤
        var unique = new List<Vector2Int>(dirs.Count);
        int w = grid.GetLength(0), h = grid.GetLength(1);
        foreach (var d in dirs) {
            if (d == Vector2Int.zero || unique.Contains(d)) continue;
            int nx = node.x + d.x;
            int ny = node.y + d.y;
            if (!InBounds(nx, ny, w, h)) continue;
            if (!grid[nx, ny].walkable) continue;
            unique.Add(d);
        }
        return unique;
    }

    static bool HasForcedNeighbor(Node node, Vector2Int dir, Node[,] grid, bool allowDiagonal) {
        int w = grid.GetLength(0), h = grid.GetLength(1);
        int x = node.x, y = node.y;

        bool Blocked(int px, int py) => !InBounds(px, py, w, h) || !grid[px, py].walkable;
        bool Walkable(int px, int py) => InBounds(px, py, w, h) && grid[px, py].walkable;

        if (!allowDiagonal || dir.x == 0 || dir.y == 0) {
            if (dir.x != 0) {
                return (Blocked(x, y + 1) && Walkable(x + dir.x, y + 1)) ||
                       (Blocked(x, y - 1) && Walkable(x + dir.x, y - 1));
            }
            if (dir.y != 0) {
                return (Blocked(x + 1, y) && Walkable(x + 1, y + dir.y)) ||
                       (Blocked(x - 1, y) && Walkable(x - 1, y + dir.y));
            }
            return false;
        }

        return (Blocked(x - dir.x, y) && Walkable(x - dir.x, y + dir.y)) ||
               (Blocked(x, y - dir.y) && Walkable(x + dir.x, y - dir.y));
    }

    static List<Node> ExpandJpsPath(List<Node> compactPath, Node[,] grid) {
        if (compactPath == null || compactPath.Count <= 1) return compactPath;

        var expanded = new List<Node> { compactPath[0] };
        for (int i = 1; i < compactPath.Count; i++) {
            Node prev = compactPath[i - 1];
            Node cur = compactPath[i];

            // 传送跳跃直接连
            if (prev.isPortal && prev.portalTarget == cur) {
                expanded.Add(cur);
                continue;
            }

            int dx = Mathf.Clamp(cur.x - prev.x, -1, 1);
            int dy = Mathf.Clamp(cur.y - prev.y, -1, 1);

            int cx = prev.x + dx;
            int cy = prev.y + dy;
            while (cx != cur.x || cy != cur.y) {
                expanded.Add(grid[cx, cy]);
                cx += dx;
                cy += dy;
            }
            expanded.Add(cur);
        }
        return expanded;
    }

    /// <summary>构建 PathResult，统计预测伤害和是否使用传送门</summary>
    static PathResult BuildResult(List<Node> path, int explored, float time) {
        float totalCost = 0;
        int totalDamage = 0;
        bool usedPortal = false;

        for (int i = 1; i < path.Count; i++) {
            totalCost += path[i].moveCost;
            totalDamage += path[i].damage;

            // 检测是否有传送跳跃（相邻两个节点不相邻 = 传送）
            if (Mathf.Abs(path[i].x - path[i - 1].x) > 1 ||
                Mathf.Abs(path[i].y - path[i - 1].y) > 1)
                usedPortal = true;
        }

        return new PathResult {
            path = path,
            exploredCount = explored,
            searchTime = time,
            totalCost = totalCost,
            predictedDamage = totalDamage,
            usedPortal = usedPortal,
            success = true
        };
    }

    static void ResetGrid(Node[,] grid) {
        int w = grid.GetLength(0), h = grid.GetLength(1);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                grid[x, y].Reset();
    }

    static List<Node> Retrace(Node start, Node end) {
        var path = new List<Node>();
        Node cur = end;
        while (cur != start) { path.Add(cur); cur = cur.parent; }
        path.Add(start);
        path.Reverse();
        return path;
    }

    static bool InBounds(int x, int y, int w, int h)
        => x >= 0 && y >= 0 && x < w && y < h;
}