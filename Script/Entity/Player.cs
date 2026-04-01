using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 纯逻辑玩家 —— v2.0 升级版
/// 
/// 【v1.0 → v2.0 变化】
///   + 事件驱动（订阅 InputManager 事件，不再自己检测 Input）
///   + 传送门支持（走到 Portal 自动传送）
///   + 出口检测（走到 Exit 触发 GameOver）
///   + 算法切换（Q 键循环 A*/Dijkstra/Greedy）
///   + 探索节点数统计
///   + 响应 GameState（暂停时不处理输入）
/// </summary>
public class Player : MonoSingleton<Player> {
    [Header("═══ 起始位置 ═══")]
    [SerializeField] public int gridX = 0;
    [SerializeField] public int gridY = 0;

    [Header("═══ 寻路可视化 ═══")]
    [Range(0.005f, 0.1f)]
    [SerializeField] private float searchStepDelay = 0.02f;
    [Range(0.02f, 0.3f)]
    [SerializeField] private float pathWalkDelay = 0.08f;

    [Header("═══ 算法 ═══")]
    [SerializeField] private PathfindingAlgorithm currentAlgorithm = PathfindingAlgorithm.AStar;

    // ---- 统计 ----
    [HideInInspector] public int totalSteps;
    [HideInInspector] public int pathfindCount;
    [HideInInspector] public float lastSearchTime;
    [HideInInspector] public float lastWalkTime;
    [HideInInspector] public int lastPathLength;
    [HideInInspector] public int lastExploredCount;

    public PathfindingAlgorithm CurrentAlgorithm => currentAlgorithm;

    // ---- 内部状态 ----
    private bool isBusy;
    private Coroutine currentRoutine;
    private List<Node> currentPath = new();

    // ═══════════════════════════════════════════
    //  初始化 + 事件绑定
    // ═══════════════════════════════════════════

    public void Init() {
        MarkPlayerCell(true);

        // 订阅输入事件
        var input = InputManager.Instance;
        input.OnMove += HandleMove;
        input.OnLeftClick += HandleClick;
        input.OnSwitchAlgorithm += CycleAlgorithm;
    }

    void OnDestroy() {
        // 取消订阅（防止泄漏）
        if (InputManager.Instance != null) {
            InputManager.Instance.OnMove -= HandleMove;
            InputManager.Instance.OnLeftClick -= HandleClick;
            InputManager.Instance.OnSwitchAlgorithm -= CycleAlgorithm;
        }
    }

    // ═══════════════════════════════════════════
    //  WASD 逐格移动
    // ═══════════════════════════════════════════
    void HandleMove(Vector2Int dir) {
        if (isBusy) return;

        int nx = gridX + dir.x;
        int ny = gridY + dir.y;

        var gm = GridManager.Instance;
        if (!gm.InBounds(nx, ny)) return;
        if (!gm.grid[nx, ny].walkable) return;

        gm.ClearAllPathVisuals();
        currentPath.Clear();

        MoveToCell(nx, ny);
    }

    // ═══════════════════════════════════════════
    //  左键点击寻路
    // ═══════════════════════════════════════════
    void HandleClick() {
        if (isBusy) return;

        if (!InputManager.Instance.GetMouseWorldPosition(out int tx, out int tz))
            return;

        var gm = GridManager.Instance;
        if (!gm.InBounds(tx, tz)) return;

        Node targetNode = gm.GetNode(tx, tz);
        if (targetNode == null || !targetNode.walkable) return;
        if (tx == gridX && tz == gridY) return;

        // 停掉旧协程
        if (currentRoutine != null) {
            StopCoroutine(currentRoutine);
            isBusy = false;
        }

        gm.ClearAllPathVisuals();
        currentPath.Clear();

        gm.GetView(gridX, gridY).SetStart(true);
        gm.GetView(tx, tz).SetEnd(true);

        Node startNode = gm.GetNode(gridX, gridY);
        currentRoutine = StartCoroutine(FindAndWalk(startNode, targetNode));
        pathfindCount++;
    }

    // ═══════════════════════════════════════════
    //  协程：可视化搜索 → 路径高亮 → 逐格行走
    // ═══════════════════════════════════════════
    IEnumerator FindAndWalk(Node start, Node target) {
        isBusy = true;
        var gm = GridManager.Instance;
        PathResult result = null;

        // ---- 阶段1：可视化搜索 ----
        yield return StartCoroutine(Pathfinder.FindPathVisual(
            start, target, gm.grid, currentAlgorithm,
            onVisit: (node, isOpen) => {
                if (node.x == gridX && node.y == gridY) return;
                if (node == target) return;

                var view = gm.GetView(node.x, node.y);
                if (isOpen) view.SetExploring(true);
                else view.SetExplored(true);
            },
            onComplete: (r) => { result = r; },
            stepDelay: searchStepDelay
        ));

        lastSearchTime = result.searchTime;
        lastExploredCount = result.exploredCount;

        // ---- 寻路失败 ----
        if (!result.success) {
            lastPathLength = 0;
            gm.ClearAllPathVisuals();

            EventBus.Publish(new PathFailedEvent());

            isBusy = false;
            yield break;
        }

        currentPath = result.path;
        lastPathLength = currentPath.Count;

        // 发布成功事件
        EventBus.Publish(new PathFoundEvent {
            pathLength = lastPathLength,
            exploredCount = lastExploredCount,
            searchTime = lastSearchTime
        });

        // ---- 阶段2：高亮路径 ----
        foreach (var node in currentPath) {
            var v = gm.GetView(node.x, node.y);
            v.ClearPathVisuals();
            v.SetPath(true);
        }
        MarkPlayerCell(true);

        yield return new WaitForSeconds(0.3f);

        // ---- 阶段3：逐格行走 ----
        float walkStart = Time.realtimeSinceStartup;

        for (int i = 1; i < currentPath.Count; i++) {
            Node next = currentPath[i];

            var prevView = gm.GetView(currentPath[i - 1].x, currentPath[i - 1].y);
            prevView.SetPath(false);
            prevView.SetPlayer(false);

            MoveToCell(next.x, next.y);

            yield return new WaitForSeconds(pathWalkDelay);
        }

        lastWalkTime = Time.realtimeSinceStartup - walkStart;

        gm.ClearAllPathVisuals();
        currentPath.Clear();
        MarkPlayerCell(true);

        isBusy = false;
    }

    // ═══════════════════════════════════════════
    //  移动 + 特殊地形处理
    // ═══════════════════════════════════════════
    void MoveToCell(int x, int y) {
        MarkPlayerCell(false);
        gridX = x;
        gridY = y;
        MarkPlayerCell(true);
        totalSteps++;

        var node = GridManager.Instance.GetNode(x, y);

        // 发布移动事件
        EventBus.Publish(new PlayerMovedEvent {
            x = x, y = y,
            terrain = node.terrainType
        });

        // ---- 传送门 ----
        if (node.terrainType == TerrainType.Portal && node.portalTarget != null) {
            MarkPlayerCell(false);
            gridX = node.portalTarget.x;
            gridY = node.portalTarget.y;
            MarkPlayerCell(true);
        }

        // ---- 出口 ----
        if (node.terrainType == TerrainType.Exit) {
            EventBus.Publish(new ExitReachedEvent());
        }
    }

    void MarkPlayerCell(bool on) {
        var view = GridManager.Instance.GetView(gridX, gridY);
        if (view != null) view.SetPlayer(on);
    }

    // ═══════════════════════════════════════════
    //  算法切换
    // ═══════════════════════════════════════════
    void CycleAlgorithm() {
        currentAlgorithm = currentAlgorithm switch {
            PathfindingAlgorithm.AStar => PathfindingAlgorithm.Dijkstra,
            PathfindingAlgorithm.Dijkstra => PathfindingAlgorithm.GreedyBestFirst,
            PathfindingAlgorithm.GreedyBestFirst => PathfindingAlgorithm.AStar,
            _ => PathfindingAlgorithm.AStar
        };

        EventBus.Publish(new AlgorithmChangedEvent { algorithmName = currentAlgorithm.ToString() });
    }
}