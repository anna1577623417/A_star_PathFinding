using UnityEngine;

/// <summary>
/// 统一初始化入口 —— v2.0
/// 
/// 【v1.0 → v2.0 变化】
///   + GameStateManager 初始化
///   + 事件总线清理
///   + Exit 到达 → GameOver 状态转换
///   + Restart 事件响应
/// 
/// 【初始化链】
///   ① GridGenerator.Init()      → 生成 Cube 网格
///   ② GridManager.Init()        → 数据层 + 多地形生成
///   ③ Player.Init()             → 放置玩家 + 绑定输入事件
///   ④ GridInputController.Init()→ 绑定右键事件
///   ⑤ GridShadow.Init()         → 阴影适配
///   ⑥ GridBackground.Init()     → 背景
///   ⑦ CameraController.Init()   → 相机焦点 + 绑定缩放/拖拽事件
///   ⑧ GameStateManager.StartGame() → 状态 → Playing
/// </summary>
public class GameInitializer : MonoBehaviour {
    [Header("═══ 同 GO 组件（自动获取）═══")]
    [SerializeField] private GridGenerator gridGenerator;
    [SerializeField] private Player player;
    [SerializeField] private GridInputController gridInput;

    [Header("═══ 独立 GO（需手动拖入）═══")]
    [SerializeField] private GridShadow gridShadow;
    [SerializeField] private GridBackground gridBackground;
    [SerializeField] private CameraController cameraController;

    void Start() {
        // 清理上一轮的事件订阅（重玩时）
        EventBus.Clear();

        // 自动获取
        if (gridGenerator == null) gridGenerator = GetComponent<GridGenerator>();
        if (player == null) player = Player.Instance;
        if (gridInput == null) gridInput = GridInputController.Instance;
        if (cameraController == null) cameraController = FindObjectOfType<CameraController>();

        // ==================== 启动顺序 ====================

        InputManager.Instance.Init();
        // ⓪ 按键绑定管理器（在所有输入系统之前初始化）
        //if (KeyBindManager.Instance != null)
        //    KeyBindManager.Instance.Init();

        // ① 生成网格
        NodeView[,] views = gridGenerator.Init();

        // ② 初始化数据层 + 地形
        GridManager.Instance.Init(views);

        // ③ 放置玩家 + 绑定输入
        player.Init();

        // ④ 网格输入控制器
        if (gridInput != null) gridInput.Init();

        // ⑤ 阴影
        if (gridShadow != null) gridShadow.Init();

        // ⑥ 背景
        if (gridBackground != null) gridBackground.Init();

        // ⑦ 相机
        if (cameraController != null) cameraController.Init();

        // ---- 游戏事件绑定 ----

        // 到达出口 → GameOver
        EventBus.Subscribe<ExitReachedEvent>(_ =>
            GameStateManager.Instance.Win());

        // 暂停切换
        InputManager.Instance.OnTogglePause += () =>
            GameStateManager.Instance.TogglePause();

        // 重启
        InputManager.Instance.OnRestart += () => {
            if (GameStateManager.Instance.CurrentState == GameStateType.GameOver
             || GameStateManager.Instance.CurrentState == GameStateType.Paused)
                GameStateManager.Instance.Restart();
        };

        // ⑧ 开始游戏
        GameStateManager.Instance.StartGame();

        Debug.Log($"[GameInitializer] v2.0 启动完成: {GridManager.Instance.width}x{GridManager.Instance.height}");
    }
}