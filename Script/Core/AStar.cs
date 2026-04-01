//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

///// <summary>
///// A* 寻路算法
///// 
///// ★ 修复内容：
///// 1. 原版没有检查 neighbor.walkable → 路径穿墙
///// 2. 原版没有重置 Node 的 gCost/parent → 第二次寻路结果错误/L字形
///// 3. 新增协程版本 FindPathVisual() → 可以逐帧看到搜索扩散过程
///// </summary>
//public static class AStar {
//    // ============================================================
//    //  同步版 —— 立即返回结果
//    // ============================================================
//    public static List<Node> FindPath(Node start, Node target, Node[,] grid) {
//        // ★ 关键修复：每次寻路前重置所有节点
//        ResetGrid(grid);

//        var openSet = new List<Node> { start };
//        var closedSet = new HashSet<Node>();

//        while (openSet.Count > 0) {
//            Node current = GetLowestFCost(openSet);
//            openSet.Remove(current);
//            closedSet.Add(current);

//            if (current == target)
//                return Retrace(start, target);

//            foreach (var neighbor in GetNeighbors(current, grid)) {
//                // ★ 关键修复：检查 walkable
//                if (!neighbor.walkable || closedSet.Contains(neighbor))
//                    continue;

//                int newGCost = current.gCost + GetDistance(current, neighbor);
//                if (newGCost < neighbor.gCost || !openSet.Contains(neighbor)) {
//                    neighbor.gCost = newGCost;
//                    neighbor.hCost = GetDistance(neighbor, target);
//                    neighbor.parent = current;

//                    if (!openSet.Contains(neighbor))
//                        openSet.Add(neighbor);
//                }
//            }
//        }

//        return null; // 找不到路径
//    }

//    // ============================================================
//    //  协程版 —— 逐步可视化搜索扩散过程
//    //  callback: 每一步通知外部 (node, isOpen)
//    //    isOpen=true  → 加入开放列表（青色扩散）
//    //    isOpen=false → 加入关闭列表（深蓝已探索）
//    // ============================================================
//    public static IEnumerator FindPathVisual(
//        Node start, Node target, Node[,] grid,
//        System.Action<Node, bool> onVisit,            // (node, isOpenSet)
//        System.Action<List<Node>> onComplete,          // 寻路完成回调
//        float stepDelay = 0.02f) {
//        ResetGrid(grid);

//        var openSet = new List<Node> { start };
//        var closedSet = new HashSet<Node>();

//        while (openSet.Count > 0) {
//            Node current = GetLowestFCost(openSet);
//            openSet.Remove(current);
//            closedSet.Add(current);

//            // 通知：这个节点进入关闭列表（已探索）
//            onVisit?.Invoke(current, false);

//            if (current == target) {
//                var path = Retrace(start, target);
//                onComplete?.Invoke(path);
//                yield break;
//            }

//            foreach (var neighbor in GetNeighbors(current, grid)) {
//                if (!neighbor.walkable || closedSet.Contains(neighbor))
//                    continue;

//                int newGCost = current.gCost + GetDistance(current, neighbor);
//                if (newGCost < neighbor.gCost || !openSet.Contains(neighbor)) {
//                    neighbor.gCost = newGCost;
//                    neighbor.hCost = GetDistance(neighbor, target);
//                    neighbor.parent = current;

//                    if (!openSet.Contains(neighbor)) {
//                        openSet.Add(neighbor);
//                        // 通知：这个节点进入开放列表（正在探索）
//                        onVisit?.Invoke(neighbor, true);
//                    }
//                }
//            }

//            // 每步暂停一帧或指定时间，让玩家看到扩散效果
//            if (stepDelay > 0)
//                yield return new WaitForSeconds(stepDelay);
//            else
//                yield return null;
//        }

//        // 找不到路径
//        onComplete?.Invoke(null);
//    }

//    // ============================================================
//    //  工具方法
//    // ============================================================

//    static void ResetGrid(Node[,] grid) {
//        int w = grid.GetLength(0);
//        int h = grid.GetLength(1);
//        for (int x = 0; x < w; x++)
//            for (int y = 0; y < h; y++)
//                grid[x, y].Reset();
//    }

//    static Node GetLowestFCost(List<Node> list) {
//        Node best = list[0];
//        for (int i = 1; i < list.Count; i++) {
//            if (list[i].fCost < best.fCost ||
//               (list[i].fCost == best.fCost && list[i].hCost < best.hCost)) {
//                best = list[i];
//            }
//        }
//        return best;
//    }

//    /// <summary>
//    /// 曼哈顿距离（4方向移动）
//    /// </summary>
//    static int GetDistance(Node a, Node b) {
//        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
//    }

//    static List<Node> Retrace(Node start, Node end) {
//        var path = new List<Node>();
//        Node cur = end;
//        while (cur != start) {
//            path.Add(cur);
//            cur = cur.parent;
//        }
//        path.Add(start); // 包含起点
//        path.Reverse();
//        return path;
//    }

//    static List<Node> GetNeighbors(Node node, Node[,] grid) {
//        var list = new List<Node>(4);
//        int w = grid.GetLength(0);
//        int h = grid.GetLength(1);

//        // 上下左右 4 方向
//        int[,] dirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
//        for (int i = 0; i < 4; i++) {
//            int nx = node.x + dirs[i, 0];
//            int ny = node.y + dirs[i, 1];
//            if (nx >= 0 && ny >= 0 && nx < w && ny < h)
//                list.Add(grid[nx, ny]);
//        }
//        return list;
//    }
//}