using System;
using UnityEngine;

/// <summary>
/// 统一输入管理器
/// 
/// 【v1.0 的问题】
///   Player、GridInputController、CameraController 各自在 Update 里
///   检测 Input.GetXXX，互相不知道对方的存在。
///   要加"暂停时禁用输入"需要改三个文件。
///
/// 【v2.0 方案】
///   所有输入集中在这里检测，通过 C# event 派发。
///   各系统只订阅事件，不直接碰 Input 类。
///   暂停时这里统一屏蔽，各系统零改动。
///
/// 【未来升级路径】
///   把 Update 里的 Input.GetXXX 替换为 Unity Input System 的回调，
///   事件签名不变，所有订阅者零改动。
///
/// 挂载：根节点
/// </summary>
public class InputManager : MonoSingleton<InputManager> {

    // ═══════════════════════════════════════════
    //  事件定义 —— 各系统订阅这些事件
    // ═══════════════════════════════════════════

    /// <summary>WASD/方向键移动（每次触发一个方向）</summary>
    public event Action<Vector2Int> OnMove;

    /// <summary>鼠标左键按下（世界坐标由订阅者自算）</summary>
    public event Action OnLeftClick;

    /// <summary>鼠标右键按下</summary>
    public event Action OnRightClick;

    /// <summary>鼠标中键按下/松开</summary>
    public event Action<bool> OnMiddleMouse; // true=按下, false=松开

    /// <summary>鼠标移动增量（中键拖拽用）</summary>
    public event Action<Vector2> OnMouseDelta;

    /// <summary>滚轮滚动</summary>
    public event Action<float> OnScroll;

    /// <summary>切换相机模式</summary>
    public event Action OnToggleCamera;

    /// <summary>切换暂停</summary>
    public event Action OnTogglePause;

    /// <summary>切换 HUD</summary>
    public event Action OnToggleHUD;

    /// <summary>切换算法</summary>
    public event Action OnSwitchAlgorithm;

    /// <summary>重新开始</summary>
    public event Action OnRestart;

    // ═══════════════════════════════════════════
    //  按键绑定（可在 Inspector 中重绑定）
    // ═══════════════════════════════════════════

    [Header("═══ 按键绑定 ═══")]
    [SerializeField] public KeyCode toggleCameraKey = KeyCode.Tab;
    [SerializeField] public KeyCode togglePauseKey = KeyCode.Escape;
    [SerializeField] public KeyCode toggleHUDKey = KeyCode.H;
    [SerializeField] public KeyCode switchAlgorithmKey = KeyCode.Q;
    [SerializeField] public KeyCode restartKey = KeyCode.R;

    [Header("═══ WASD ═══")]
    [Tooltip("按住移动的间隔（秒）")]
    [Range(0.05f, 0.3f)]
    [SerializeField] private float moveInterval = 0.15f;

    private float moveTimer;
    private Vector3 lastMousePos;

    public void Init() {

    }

    void Update() {
        // ---- 暂停键始终响应（即使在暂停中）----
        if (Input.GetKeyDown(togglePauseKey))
            OnTogglePause?.Invoke();

        // ---- 重启键在 GameOver 时响应 ----
        if (Input.GetKeyDown(restartKey))
            OnRestart?.Invoke();

        // ---- 以下输入只在 Playing 状态下响应 ----
        if (!GameStateManager.Instance.IsPlaying) return;

        // WASD / 方向键
        HandleMovement();

        // 鼠标按键
        if (Input.GetMouseButtonDown(0)) OnLeftClick?.Invoke();
        if (Input.GetMouseButtonDown(1)) OnRightClick?.Invoke();

        // 中键
        if (Input.GetMouseButtonDown(2)) {
            OnMiddleMouse?.Invoke(true);
            lastMousePos = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(2)) OnMiddleMouse?.Invoke(false);

        if (Input.GetMouseButton(2)) {
            Vector2 delta = (Vector2)(Input.mousePosition - lastMousePos);
            lastMousePos = Input.mousePosition;
            if (delta.sqrMagnitude > 0.01f)
                OnMouseDelta?.Invoke(delta);
        }

        // 滚轮
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
            OnScroll?.Invoke(scroll);

        // 功能键
        if (Input.GetKeyDown(toggleCameraKey)) OnToggleCamera?.Invoke();
        if (Input.GetKeyDown(toggleHUDKey)) OnToggleHUD?.Invoke();
        if (Input.GetKeyDown(switchAlgorithmKey)) OnSwitchAlgorithm?.Invoke();
    }

    void HandleMovement() {
        moveTimer -= Time.deltaTime;
        if (moveTimer > 0) return;

        Vector2Int dir = Vector2Int.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dir.y = 1;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dir.y = -1;
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dir.x = -1;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dir.x = 1;
        else return;

        OnMove?.Invoke(dir);
        moveTimer = moveInterval;
    }

    // ═══════════════════════════════════════════
    //  工具方法（供其他系统调用）
    // ═══════════════════════════════════════════

    /// <summary>获取鼠标在 Y=0 平面上的世界坐标</summary>
    public bool GetMouseWorldPosition(out int gridX, out int gridZ) {
        gridX = gridZ = 0;
        if (Camera.main == null) return false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, Vector3.zero);

        if (ground.Raycast(ray, out float dist)) {
            Vector3 wp = ray.GetPoint(dist);
            gridX = Mathf.RoundToInt(wp.x);
            gridZ = Mathf.RoundToInt(wp.z);
            return true;
        }
        return false;
    }
}