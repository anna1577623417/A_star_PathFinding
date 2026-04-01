using UnityEngine;

/// <summary>
/// 双模式相机控制器
/// 
/// 【模式 A：跟随模式（Follow）】
///   死区 + MoveTowards 平滑，玩家碰边界才推动相机
///   支持中键拖拽、滚轮缩放
///   
/// 【模式 B：全局模式（Overview）】
///   相机锁定在棋盘正中央，自动计算高度使所有格子可见
///   不跟随玩家，不响应拖拽
///   仍然支持滚轮微调
///
/// 【切换方式】
///   按 Tab 键切换，或代码调用 SwitchMode()
///   切换时相机平滑过渡到新位置，不会跳切
///
/// 挂载：Main Camera
/// </summary>
public class CameraController : MonoBehaviour {
    // ═══════════════════════════════════════════
    //  模式
    // ═══════════════════════════════════════════

    public enum CameraMode { Follow, Overview }

    [Header("═══ 模式 ═══")]
    [Tooltip("当前相机模式")]
    [SerializeField] private CameraMode mode = CameraMode.Follow;

    [Tooltip("切换模式的按键")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    // ═══════════════════════════════════════════
    //  Follow 模式参数
    // ═══════════════════════════════════════════

    [Header("═══ Follow 模式 - 死区 ═══")]
    [Range(1f, 10f)]
    [SerializeField] private float deadZoneX = 4f;

    [Range(1f, 10f)]
    [SerializeField] private float deadZoneZ = 3f;

    [Header("═══ Follow 模式 - 平滑 ═══")]
    [Tooltip("相机平移速度（格/秒）")]
    [Range(4f, 30f)]
    [SerializeField] private float moveSpeed = 12f;

    [Header("═══ Follow 模式 - 偏移 ═══")]
    [SerializeField] private Vector3 followOffset = new Vector3(0, 18, -8);

    [Header("═══ Follow 模式 - 缩放 ═══")]
    [Range(1f, 10f)]
    [SerializeField] private float zoomSpeed = 3f;

    [Range(5f, 15f)]
    [SerializeField] private float minY = 8f;

    [Range(15f, 50f)]
    [SerializeField] private float maxY = 30f;

    [Header("═══ Follow 模式 - 中键拖拽 ═══")]
    [Range(0.01f, 0.2f)]
    [SerializeField] private float dragSensitivity = 0.05f;

    // ═══════════════════════════════════════════
    //  Overview 模式参数
    // ═══════════════════════════════════════════

    [Header("═══ Overview 模式 ═══")]
    [Tooltip("自动计算高度时额外留边比例（0.1 = 多留10%）")]
    [Range(0f, 0.5f)]
    [SerializeField] private float overviewPadding = 0.15f;

    [Tooltip("相机俯角（度），90=正俯视，60=斜俯视")]
    [Range(45f, 90f)]
    [SerializeField] private float overviewAngle = 75f;

    [Tooltip("切换模式时的过渡速度")]
    [Range(2f, 20f)]
    [SerializeField] private float transitionSpeed = 6f;

    // ═══════════════════════════════════════════
    //  内部状态
    // ═══════════════════════════════════════════

    // Follow 模式的两层焦点
    private float targetFocusX;
    private float targetFocusZ;
    private float currentFocusX;
    private float currentFocusZ;

    // 中键拖拽
    private bool isDragging;
    private Vector3 lastMousePos;

    // Overview 模式的目标位置/朝向（Init 时计算一次）
    private Vector3 overviewPosition;
    private Quaternion overviewRotation;

    // 通用：当前相机实际目标（用于平滑过渡）
    private Vector3 desiredPosition;
    private Quaternion desiredRotation;

    // ═══════════════════════════════════════════
    //  初始化
    // ═══════════════════════════════════════════

    public void Init() {
        // Follow 模式初始焦点
        float startX = 0, startZ = 0;
        if (Player.Instance != null) {
            startX = Player.Instance.gridX;
            startZ = Player.Instance.gridY;
        } else {
            var gm = GridManager.Instance;
            if (gm != null) {
                startX = gm.width * 0.5f;
                startZ = gm.height * 0.5f;
            }
        }
        targetFocusX = currentFocusX = startX;
        targetFocusZ = currentFocusZ = startZ;

        // 预计算 Overview 模式的位置
        CalculateOverviewTransform();

        // 根据初始模式设置相机
        if (mode == CameraMode.Overview) {
            desiredPosition = overviewPosition;
            desiredRotation = overviewRotation;
        } else {
            ApplyFollowPosition();
            desiredPosition = transform.position;
            desiredRotation = transform.rotation;
        }

        transform.position = desiredPosition;
        transform.rotation = desiredRotation;
    }

    void Start() {
        Init();
    }

    // ═══════════════════════════════════════════
    //  计算 Overview 模式的相机位置
    // ═══════════════════════════════════════════

    /// <summary>
    /// 【Overview 位置计算】
    /// 
    /// 目标：相机看向棋盘中心，高度刚好让所有格子都在画面内
    /// 
    /// 步骤：
    ///   1. 棋盘中心 = ((w-1)/2, 0, (h-1)/2)
    ///   2. 棋盘需要覆盖的最大半径 = max(w, h) / 2 + padding
    ///   3. 根据 Camera.main.fieldOfView 和屏幕宽高比
    ///      反算出需要的相机高度
    ///   4. 按 overviewAngle 决定相机俯角
    ///      90° = 正俯视，75° = 稍微倾斜（有立体感）
    /// </summary>
    void CalculateOverviewTransform() {
        var gm = GridManager.Instance;
        if (gm == null) return;

        float w = gm.width;
        float h = gm.height;

        // 棋盘中心
        float cx = (w - 1f) * 0.5f;
        float cz = (h - 1f) * 0.5f;
        Vector3 center = new Vector3(cx, 0, cz);

        // 需要可见的半尺寸（加 padding）
        float halfW = w * 0.5f * (1f + overviewPadding);
        float halfH = h * 0.5f * (1f + overviewPadding);

        // 根据 FOV 和宽高比计算需要的距离
        Camera cam = Camera.main;
        if (cam == null) cam = GetComponent<Camera>();

        float vFov = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
        float aspect = (float)Screen.width / Screen.height;
        float hFov = Mathf.Atan(Mathf.Tan(vFov) * aspect);

        // 垂直方向需要的距离（看到 halfH）
        // 水平方向需要的距离（看到 halfW）
        float distV = halfH / Mathf.Tan(vFov);
        float distH = halfW / Mathf.Tan(hFov);

        // 取较大值，确保两个方向都能完整显示
        float distance = Mathf.Max(distV, distH);

        // 按俯角计算相机偏移
        // overviewAngle=90 → 正上方，overviewAngle=75 → 稍偏后方
        float angleRad = overviewAngle * Mathf.Deg2Rad;
        float height = distance * Mathf.Sin(angleRad);
        float back = distance * Mathf.Cos(angleRad);

        overviewPosition = center + new Vector3(0, height, -back);
        overviewRotation = Quaternion.LookRotation(center - overviewPosition, Vector3.up);
    }

    // ═══════════════════════════════════════════
    //  每帧更新
    // ═══════════════════════════════════════════

    void LateUpdate() {
        // ---- 按键切换模式 ----
        if (Input.GetKeyDown(toggleKey))
            SwitchMode();

        if (mode == CameraMode.Follow)
            UpdateFollow();
        else
            UpdateOverview();

        // ---- 平滑过渡到目标位置/朝向 ----
        // 两种模式共用同一套过渡逻辑
        float t = transitionSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, desiredPosition, t * 5f);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
    }

    // ═══════════════════════════════════════════
    //  Follow 模式逻辑（和之前完全一样）
    // ═══════════════════════════════════════════

    void UpdateFollow() {
        HandleMiddleClickDrag();

        if (isDragging) {
            float dragStep = moveSpeed * Time.deltaTime;
            currentFocusX = Mathf.MoveTowards(currentFocusX, targetFocusX, dragStep);
            currentFocusZ = Mathf.MoveTowards(currentFocusZ, targetFocusZ, dragStep);
            HandleZoom();
            ApplyFollowPosition();
            return;
        }

        if (Player.Instance == null) return;

        float px = Player.Instance.gridX;
        float pz = Player.Instance.gridY;

        // 死区
        if (px > targetFocusX + deadZoneX)
            targetFocusX = px - deadZoneX;
        else if (px < targetFocusX - deadZoneX)
            targetFocusX = px + deadZoneX;

        if (pz > targetFocusZ + deadZoneZ)
            targetFocusZ = pz - deadZoneZ;
        else if (pz < targetFocusZ - deadZoneZ)
            targetFocusZ = pz + deadZoneZ;

        float step = moveSpeed * Time.deltaTime;
        currentFocusX = Mathf.MoveTowards(currentFocusX, targetFocusX, step);
        currentFocusZ = Mathf.MoveTowards(currentFocusZ, targetFocusZ, step);

        HandleZoom();
        ApplyFollowPosition();
    }

    void ApplyFollowPosition() {
        Vector3 focus = new Vector3(currentFocusX, 0, currentFocusZ);
        desiredPosition = focus + followOffset;
        desiredRotation = Quaternion.LookRotation(focus - desiredPosition, Vector3.up);
    }

    // ═══════════════════════════════════════════
    //  Overview 模式逻辑
    // ═══════════════════════════════════════════

    //滚轮缩放调整的是摄像机的Size（纵深，抬高视角）
    void UpdateOverview() {
        // Overview 模式下相机固定在预计算位置
        // 只允许滚轮微调高度
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f) {
            // 微调 overview 高度（±10% 范围）
            float adjust = scroll * zoomSpeed;
            overviewPosition.y = Mathf.Max(overviewPosition.y - adjust, 5f);

            // 重新计算朝向
            var gm = GridManager.Instance;
            if (gm != null) {
                Vector3 center = new Vector3((gm.width - 1f) * 0.5f, 0, (gm.height - 1f) * 0.5f);
                overviewRotation = Quaternion.LookRotation(center - overviewPosition, Vector3.up);
            }
        }

        desiredPosition = overviewPosition;
        desiredRotation = overviewRotation;
    }

    // ═══════════════════════════════════════════
    //  Follow 模式的输入处理
    // ═══════════════════════════════════════════

    void HandleMiddleClickDrag() {
        if (Input.GetMouseButtonDown(2)) {
            isDragging = true;
            lastMousePos = Input.mousePosition;
        } else if (Input.GetMouseButtonUp(2)) {
            isDragging = false;
        }

        if (isDragging) {
            Vector3 delta = Input.mousePosition - lastMousePos;
            lastMousePos = Input.mousePosition;

            float heightScale = followOffset.y * dragSensitivity;
            targetFocusX -= delta.x * heightScale * Time.deltaTime;
            targetFocusZ -= delta.y * heightScale * Time.deltaTime;

            currentFocusX = targetFocusX;
            currentFocusZ = targetFocusZ;
        }
    }

    void HandleZoom() {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
            followOffset.y = Mathf.Clamp(followOffset.y - scroll * zoomSpeed, minY, maxY);
    }

    // ═══════════════════════════════════════════
    //  模式切换
    // ═══════════════════════════════════════════

    /// <summary>
    /// 切换到另一个模式。
    /// Follow → Overview: 重新计算全局视角位置
    /// Overview → Follow: 焦点重置到玩家当前位置
    /// </summary>
    public void SwitchMode() {
        if (mode == CameraMode.Follow) {
            mode = CameraMode.Overview;

            // 重新计算（窗口大小可能变了）
            CalculateOverviewTransform();

            Toast.Show("全局视角", Toast.Level.Info, 1f);
        } else {
            mode = CameraMode.Follow;

            // 焦点重置到玩家当前位置
            if (Player.Instance != null) {
                targetFocusX = currentFocusX = Player.Instance.gridX;
                targetFocusZ = currentFocusZ = Player.Instance.gridY;
            }

            Toast.Show("跟随视角", Toast.Level.Info, 1f);
        }
    }
}