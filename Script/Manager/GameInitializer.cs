using UnityEngine;

/// <summary>
/// 唯一的初始化入口 —— 整个项目的启动顺序在这里一目了然
/// 
/// 【工程原则】
///   1. 所有子系统不写 Start()，只暴露 public Init()
///   2. 依赖关系用"调用顺序"表达，不靠 Unity 的随机执行顺序
///   3. 加新系统只需在这里加一行
///
/// 【当前初始化链】
///   ① GridGenerator.Init()   → 生成 Cube 网格，返回 NodeView[,]
///   ② GridManager.Init()     → 接收 views，创建 Node[,]，生成地形
///   ③ Player.Init()          → 标记玩家初始位置（需要 GridManager 就绪）
///   ④ GridShadow.Init()      → 计算阴影 Plane 的大小和位置（需要 GridManager.width/height）
///   ⑤ GridBackground.Init()  → 计算背景 Plane（需要 GridManager.width/height）
///   ⑥ CameraController.Init()→ 初始化焦点到玩家位置（需要 Player 就绪）
///   
///   GridInput / GameHUD 无需显式 Init，它们在 Update 中通过 Instance 读数据
///
/// 挂载：和所有子系统同一个根节点（GridShadow/GridBackground 除外，它们是独立的 Plane）
/// </summary>
public class GameInitializer : MonoBehaviour {
    [Header("同 GO 上的组件（自动获取）")]
    [SerializeField] private GridGenerator gridGenerator;
    [SerializeField] private Player player;

    [Header("独立 GO 上的组件（需要手动拖入）")]
    [SerializeField] private GridShadow gridShadow;
    [SerializeField] private GridBackground gridBackground;
    [SerializeField] private CameraController cameraController;

    void Start() {
        // 自动获取同物体上的组件
        if (gridGenerator == null) gridGenerator = GetComponent<GridGenerator>();
        if (player == null) player = Player.Instance;

        // Camera 在另一个 GO 上，通过 FindObjectOfType 兜底
        if (cameraController == null) cameraController = FindObjectOfType<CameraController>();

        // ==================== 启动顺序 ====================

        // ① 生成网格
        NodeView[,] views = gridGenerator.Init();

        // ② 初始化数据层 + 地形
        GridManager.Instance.Init(views);

        // ③ 放置玩家
        player.Init();

        // ④ 阴影面适配棋盘（可选，Inspector 没拖就跳过）
        if (gridShadow != null)
            gridShadow.Init();

        // ⑤ 背景面遮挡天空盒（可选）
        if (gridBackground != null)
            gridBackground.Init();

        // ⑥ 相机初始化焦点
        if (cameraController != null)
            cameraController.Init();

        Debug.Log($"[GameInitializer] 完成: {GridManager.Instance.width}x{GridManager.Instance.height} grid, player at ({player.gridX},{player.gridY})");
    }
}