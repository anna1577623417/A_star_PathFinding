using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 纯逻辑玩家 —— 没有实体，没有 Transform 移动
/// 玩家 = 坐标(gridX, gridY) + 对应格子 shader State=7（橙色）
/// 
/// 挂载位置：和 GridManager、GridGenerator、GridInput 同一个根节点
/// </summary>
public class Player : MonoSingleton<Player> {
    [Header("起始位置")]
    public int gridX = 0;
    public int gridY = 0;

    [Header("寻路可视化速度")]
    public float searchStepDelay = 0.02f;
    public float pathWalkDelay = 0.08f;

    [Header("WASD")]
    public float wasdInterval = 0.15f;

    [HideInInspector] public int totalSteps;
    [HideInInspector] public int pathfindCount;

    // ---- HUD 显示用的本次寻路数据 ----
    [HideInInspector] public float lastSearchTime;   // A*算法耗时（秒）
    [HideInInspector] public float lastWalkTime;     // 行走总耗时（秒）
    [HideInInspector] public int lastPathLength;   // 本次路径格数

    private bool isBusy = false;
    private bool initialized = false;
    private float wasdTimer = 0;
    private Coroutine currentRoutine;
    private List<Node> currentPath = new List<Node>();

    /// <summary>
    /// 由 GameInitializer 调用，此时 GridManager 已经就绪
    /// </summary>
    public void Init() {
        MarkPlayerCell(true);
        initialized = true;
    }

    void Update() {
        if (!initialized) return;

        if (!isBusy) {
            HandleWASD();
            HandleClick();
        }
    }

    // ============================================================
    //  WASD 逐格移动
    // ============================================================
    void HandleWASD() {
        wasdTimer -= Time.deltaTime;
        if (wasdTimer > 0) return;

        int dx = 0, dy = 0;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dy = 1;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dy = -1;
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dx = -1;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dx = 1;
        else return;

        int nx = gridX + dx;
        int ny = gridY + dy;

        var gm = GridManager.Instance;
        if (!gm.InBounds(nx, ny)) return;
        if (!gm.grid[nx, ny].walkable) return;

        gm.ClearAllPathVisuals();
        currentPath.Clear();

        MoveToCell(nx, ny);
        wasdTimer = wasdInterval;
    }

    // ============================================================
    //  鼠标左键点击寻路
    // ============================================================
    void HandleClick() {
        if (!Input.GetMouseButtonDown(0)) return;
        if (Camera.main == null) return;

        // 用数学平面求交替代物理射线（和 GridInputController 同理）
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (!ground.Raycast(ray, out float dist)) return;

        Vector3 worldPoint = ray.GetPoint(dist);
        int tx = Mathf.RoundToInt(worldPoint.x);
        int tz = Mathf.RoundToInt(worldPoint.z);

        var gm = GridManager.Instance;
        if (!gm.InBounds(tx, tz)) return;

        Node targetNode = gm.GetNode(tx, tz);
        if (targetNode == null || !targetNode.walkable) return;
        if (tx == gridX && tz == gridY) return;

        var targetView = gm.GetView(tx, tz);

        if (currentRoutine != null) {
            StopCoroutine(currentRoutine);
            isBusy = false;
        }

        gm.ClearAllPathVisuals();
        currentPath.Clear();

        gm.GetView(gridX, gridY).SetStart(true);
        targetView.SetEnd(true);

        Node startNode = gm.GetNode(gridX, gridY);
        currentRoutine = StartCoroutine(FindAndWalk(startNode, targetNode));
        pathfindCount++;
    }

    // ============================================================
    //  协程：可视化搜索 → 显示路径 → 逐格行走
    // ============================================================
    IEnumerator FindAndWalk(Node start, Node target) {
        isBusy = true;
        var gm = GridManager.Instance;
        List<Node> foundPath = null;

        // ---- 阶段1: 搜索扩散可视化 ----
        float searchStart = Time.realtimeSinceStartup;

        yield return StartCoroutine(AStar.FindPathVisual(
            start, target, gm.grid,
            onVisit: (node, isOpen) => {
                if (node.x == gridX && node.y == gridY) return;
                if (node == target) return;

                var view = gm.GetView(node.x, node.y);
                if (isOpen)
                    view.SetExploring(true);
                else
                    view.SetExplored(true);
            },
            onComplete: (path) => { foundPath = path; },
            stepDelay: searchStepDelay
        ));

        lastSearchTime = Time.realtimeSinceStartup - searchStart;

        if (foundPath == null || foundPath.Count == 0) {
            Toast.Show("无法到达目标！", Toast.Level.Error, 2f);
            lastPathLength = 0;
            gm.ClearAllPathVisuals();
            isBusy = false;
            yield break;
        }

        currentPath = foundPath;
        lastPathLength = currentPath.Count;

        Toast.Show($"路径已找到：{lastPathLength} 步  耗时 {lastSearchTime:F3}s", Toast.Level.Success, 1.5f);

        // ---- 阶段2: 高亮最终路径 ----
        foreach (var node in currentPath) {
            var v = gm.GetView(node.x, node.y);
            v.ClearPathVisuals();
            v.SetPath(true);
        }
        MarkPlayerCell(true);

        yield return new WaitForSeconds(0.3f);

        // ---- 阶段3: 逐格行走 ----
        float walkStart = Time.realtimeSinceStartup;

        for (int i = 1; i < currentPath.Count; i++) {
            Node next = currentPath[i];

            var prevView = gm.GetView(currentPath[i - 1].x, currentPath[i - 1].y);
            prevView.SetPath(false);
            prevView.SetPlayer(false);

            MoveToCell(next.x, next.y);

            yield return new WaitForSeconds(pathWalkDelay);
        }

        gm.ClearAllPathVisuals();
        currentPath.Clear();
        MarkPlayerCell(true);

        lastWalkTime = Time.realtimeSinceStartup - walkStart;
        isBusy = false;
    }

    // ============================================================
    //  纯数据移动 + 改色
    // ============================================================
    void MoveToCell(int x, int y) {
        MarkPlayerCell(false);
        gridX = x;
        gridY = y;
        MarkPlayerCell(true);
        totalSteps++;
    }

    void MarkPlayerCell(bool on) {
        var view = GridManager.Instance.GetView(gridX, gridY);
        if (view != null)
            view.SetPlayer(on);
    }
}