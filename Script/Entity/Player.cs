using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家 —— v2.1 策略+生存系统
/// 
/// 【v2.0 → v2.1 变化】
///   + 血量系统（HP, TakeDamage, 死亡事件）
///   + 陷阱伤害（MoveToCell 时检测并扣血）
///   + 策略切换（E 键循环 5 种 PathPolicy）
///   + PathContext 构建（每次寻路时传入当前 HP 和策略）
///   + 预测伤害（PathResult.predictedDamage 发布到事件）
///   + Portal 传送视觉反馈
/// </summary>
public class Player : MonoSingleton<Player> {
    [Header("═══ 起始位置 ═══")]
    [SerializeField] public int gridX = 0;
    [SerializeField] public int gridY = 0;

    [Header("═══ 血量 ═══")]
    [Range(10, 500)]
    [SerializeField] private int maxHP = 100;
    [HideInInspector] public int currentHP;

    [Header("═══ 寻路可视化 ═══")]
    [Range(0.005f, 0.1f)]
    [SerializeField] private float searchStepDelay = 0.02f;
    [Range(0.02f, 0.3f)]
    [SerializeField] private float pathWalkDelay = 0.08f;

    [Header("═══ 算法 ═══")]
    [SerializeField] private PathfindingAlgorithm currentAlgorithm = PathfindingAlgorithm.AStar;

    [Header("═══ 策略 ═══")]
    [SerializeField] private PathPolicy currentPolicy = PathPolicy.Fastest;

    // ---- 公开属性 ----
    public int MaxHP => maxHP;
    public PathfindingAlgorithm CurrentAlgorithm => currentAlgorithm;
    public PathPolicy CurrentPolicy => currentPolicy;

    // ---- 统计 ----
    [HideInInspector] public int totalSteps;
    [HideInInspector] public int pathfindCount;
    [HideInInspector] public float lastSearchTime;
    [HideInInspector] public float lastWalkTime;
    [HideInInspector] public int lastPathLength;
    [HideInInspector] public int lastExploredCount;
    [HideInInspector] public int lastPredictedDamage;
    [HideInInspector] public float lastTotalCost;
    [HideInInspector] public bool lastUsedPortal;

    // ---- 内部 ----
    private bool isBusy;
    private Coroutine currentRoutine;
    private List<Node> currentPath = new();

    // ═══════════════════════════════════════════
    //  初始化
    // ═══════════════════════════════════════════

    public void Init() {
        currentHP = maxHP;
        MarkPlayerCell(true);

        EventBus.Publish(new HPChangedEvent { currentHP = currentHP, maxHP = maxHP });

        // 订阅输入事件
        var input = InputManager.Instance;
        input.OnMove += HandleMove;
        input.OnLeftClick += HandleClick;
        input.OnSwitchAlgorithm += CycleAlgorithm;
        input.OnSwitchPolicy += CyclePolicy;
    }

    void OnDestroy() {
        if (InputManager.Instance == null) return;
        InputManager.Instance.OnMove -= HandleMove;
        InputManager.Instance.OnLeftClick -= HandleClick;
        InputManager.Instance.OnSwitchAlgorithm -= CycleAlgorithm;
        InputManager.Instance.OnSwitchPolicy -= CyclePolicy;
    }

    // ═══════════════════════════════════════════
    //  血量系统
    // ═══════════════════════════════════════════

    /// <summary>受到伤害</summary>
    public void TakeDamage(int dmg, TerrainType source) {
        if (dmg <= 0) return;

        currentHP = Mathf.Max(0, currentHP - dmg);

        EventBus.Publish(new PlayerDamagedEvent {
            damage = dmg,
            currentHP = currentHP,
            maxHP = maxHP,
            source = source
        });

        EventBus.Publish(new HPChangedEvent {
            currentHP = currentHP,
            maxHP = maxHP
        });

        if (currentHP <= 0) {
            EventBus.Publish(new PlayerDeadEvent());
            // GameInitializer 会订阅 PlayerDeadEvent → GameState.GameOver
        }
    }

    // ═══════════════════════════════════════════
    //  构建寻路上下文
    // ═══════════════════════════════════════════

    /// <summary>
    /// 每次寻路时构建上下文
    /// 把当前玩家状态（HP、策略偏好）传给 Pathfinder
    /// </summary>
    PathContext BuildContext() {
        return new PathContext {
            policy = currentPolicy,
            allowDiagonal = true,
            allowPortal = true,
            currentHP = currentHP,
            maxHP = maxHP
        };
    }

    // ═══════════════════════════════════════════
    //  WASD
    // ═══════════════════════════════════════════

    void HandleMove(Vector2Int dir) {
        if (isBusy || currentHP <= 0) return;

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
    //  左键寻路
    // ═══════════════════════════════════════════

    void HandleClick() {
        if (isBusy || currentHP <= 0) return;

        if (!InputManager.Instance.GetMouseWorldPosition(out int tx, out int tz))
            return;

        var gm = GridManager.Instance;
        if (!gm.InBounds(tx, tz)) return;

        Node targetNode = gm.GetNode(tx, tz);
        if (targetNode == null || !targetNode.walkable) return;
        if (tx == gridX && tz == gridY) return;

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
    //  寻路 + 行走协程
    // ═══════════════════════════════════════════

    IEnumerator FindAndWalk(Node start, Node target) {
        isBusy = true;
        var gm = GridManager.Instance;
        PathResult result = null;

        // ★ 构建上下文（传入当前 HP 和策略）
        PathContext ctx = BuildContext();

        // ---- 阶段1：可视化搜索 ----
        yield return StartCoroutine(Pathfinder.FindPathVisual(
            start, target, gm.grid, currentAlgorithm, ctx,
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

        // ---- 失败 ----
        if (!result.success) {
            lastPathLength = 0;
            gm.ClearAllPathVisuals();
            EventBus.Publish(new PathFailedEvent());
            isBusy = false;
            yield break;
        }

        currentPath = result.path;
        lastPathLength = currentPath.Count;
        lastTotalCost = result.totalCost;
        lastPredictedDamage = result.predictedDamage;
        lastUsedPortal = result.usedPortal;

        // 发布成功事件
        EventBus.Publish(new PathFoundEvent {
            pathLength = lastPathLength,
            exploredCount = lastExploredCount,
            searchTime = lastSearchTime,
            totalCost = lastTotalCost,
            predictedDamage = lastPredictedDamage,
            usedPortal = lastUsedPortal,
            policyName = currentPolicy.ToString()
        });

        // ---- 阶段2：高亮路径 ----
        foreach (var node in currentPath) {
            gm.GetView(node.x, node.y).ClearPathVisuals();
            gm.GetView(node.x, node.y).SetPath(true);
        }
        MarkPlayerCell(true);

        yield return new WaitForSeconds(0.3f);

        // ---- 阶段3：逐格行走 ----
        float walkStart = Time.realtimeSinceStartup;

        for (int i = 1; i < currentPath.Count; i++) {
            // 死了就停
            if (currentHP <= 0) break;

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
    //  移动 + 地形效果
    // ═══════════════════════════════════════════

    void MoveToCell(int x, int y) {
        MarkPlayerCell(false);
        gridX = x;
        gridY = y;
        MarkPlayerCell(true);
        totalSteps++;

        var node = GridManager.Instance.GetNode(x, y);

        EventBus.Publish(new PlayerMovedEvent {
            x = x, y = y,
            terrain = node.terrainType
        });

        // ★ 陷阱伤害
        if (node.isTrap && node.damage > 0) {
            TakeDamage(node.damage, node.terrainType);
        }

        // ★ 传送门跳跃
        if (node.isPortal) {
            MarkPlayerCell(false);
            gridX = node.portalTarget.x;
            gridY = node.portalTarget.y;
            MarkPlayerCell(true);
        }

        // ★ 出口
        if (node.terrainType == TerrainType.Exit) {
            EventBus.Publish(new ExitReachedEvent());
        }
    }

    void MarkPlayerCell(bool on) {
        var view = GridManager.Instance.GetView(gridX, gridY);
        if (view != null) view.SetPlayer(on);
    }

    // ═══════════════════════════════════════════
    //  算法 / 策略切换
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

    void CyclePolicy() {
        currentPolicy = currentPolicy switch {
            PathPolicy.Fastest => PathPolicy.Shortest,
            PathPolicy.Shortest => PathPolicy.AvoidMud,
            PathPolicy.AvoidMud => PathPolicy.PreferPortal,
            PathPolicy.PreferPortal => PathPolicy.Cautious,
            PathPolicy.Cautious => PathPolicy.Fastest,
            _ => PathPolicy.Fastest
        };
        EventBus.Publish(new PolicyChangedEvent { policyName = currentPolicy.ToString() });
    }
}