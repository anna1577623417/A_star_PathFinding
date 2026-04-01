using UnityEngine;

/// <summary>
/// 双模式相机 —— v2.0 升级版
/// 
/// 【v1.0 → v2.0 变化】
///   + 事件驱动（订阅 InputManager）
///   + 滚轮改为 FOV 缩放（不再改高度）
///   + 屏幕抖动（寻路失败时触发）
///   + SmoothDamp 替代直接赋值（更丝滑）
/// 
/// 【FOV 缩放 vs 高度缩放】
///   高度缩放：改变相机 Y 坐标 → 改变透视关系 → 近处物体形变
///   FOV 缩放：改变视野角度 → 类似镜头变焦 → 更自然的缩放体验
///   正交相机用 orthographicSize，透视相机用 fieldOfView
/// </summary>
public class CameraController : MonoBehaviour {
    public enum CameraMode { Follow, Overview }

    [Header("═══ 模式 ═══")]
    [SerializeField] private CameraMode mode = CameraMode.Follow;

    // ═══ Follow 模式 ═══
    [Header("═══ Follow - 死区 ═══")]
    [Range(1f, 10f)][SerializeField] private float deadZoneX = 4f;
    [Range(1f, 10f)][SerializeField] private float deadZoneZ = 3f;

    [Header("═══ Follow - 平滑 ═══")]
    [Range(4f, 30f)][SerializeField] private float moveSpeed = 12f;

    [Header("═══ Follow - 偏移 ═══")]
    [SerializeField] private Vector3 followOffset = new(0, 18, -8);

    // ═══ FOV 缩放 ═══
    [Header("═══ 缩放 (FOV) ═══")]
    [Range(1f, 20f)][SerializeField] private float zoomSpeed = 5f;
    [Range(20f, 60f)][SerializeField] private float minFOV = 30f;
    [Range(60f, 120f)][SerializeField] private float maxFOV = 90f;
    [Tooltip("FOV 平滑速度")]
    [Range(0.01f, 0.3f)]
    [SerializeField] private float zoomSmoothTime = 0.1f;

    // ═══ 中键拖拽 ═══
    [Header("═══ 拖拽 ═══")]
    [Range(0.01f, 0.2f)][SerializeField] private float dragSensitivity = 0.05f;

    // ═══ Overview ═══
    [Header("═══ Overview ═══")]
    [Range(0f, 0.5f)][SerializeField] private float overviewPadding = 0.15f;
    [Range(45f, 90f)][SerializeField] private float overviewAngle = 75f;
    [Range(2f, 20f)][SerializeField] private float transitionSpeed = 6f;

    // ═══ 屏幕抖动 ═══
    [Header("═══ 屏幕抖动 ═══")]
    [Range(0.1f, 1f)][SerializeField] private float shakeIntensity = 0.3f;
    [Range(0.1f, 1f)][SerializeField] private float shakeDuration = 0.3f;

    // ---- 内部状态 ----
    private float targetFocusX, targetFocusZ;
    private float currentFocusX, currentFocusZ;
    private bool isDragging;

    private float targetFOV;
    private float fovVelocity;  // SmoothDamp 用

    private Vector3 overviewPosition;
    private Quaternion overviewRotation;
    private Vector3 desiredPosition;
    private Quaternion desiredRotation;

    // 屏幕抖动
    private float shakeTimer;
    private Vector3 shakeOffset;

    private Camera cam;

    // ═══════════════════════════════════════════
    //  初始化
    // ═══════════════════════════════════════════
    public void Init() {
        cam = GetComponent<Camera>();
        targetFOV = cam.fieldOfView;

        float sx = Player.Instance != null ? Player.Instance.gridX : GridManager.Instance.width * 0.5f;
        float sz = Player.Instance != null ? Player.Instance.gridY : GridManager.Instance.height * 0.5f;

        targetFocusX = currentFocusX = sx;
        targetFocusZ = currentFocusZ = sz;

        CalculateOverviewTransform();

        // 订阅事件
        var input = InputManager.Instance;
        input.OnToggleCamera += SwitchMode;
        input.OnScroll += HandleScroll;
        input.OnMiddleMouse += (down) => isDragging = down;
        input.OnMouseDelta += HandleDrag;

        // 屏幕抖动事件
        EventBus.Subscribe<PathFailedEvent>(OnPathFailed);

        ApplyFollowPosition();
        desiredPosition = transform.position;
        desiredRotation = transform.rotation;
    }

    void OnDestroy() {
        EventBus.Unsubscribe<PathFailedEvent>(OnPathFailed);
    }

    // ═══════════════════════════════════════════
    //  每帧更新
    // ═══════════════════════════════════════════
    void LateUpdate() {
        if (mode == CameraMode.Follow)
            UpdateFollow();
        else
            UpdateOverview();

        // FOV 平滑
        if (cam != null)
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFOV, ref fovVelocity, zoomSmoothTime);

        // 位置/旋转过渡
        float t = transitionSpeed * Time.unscaledDeltaTime;
        transform.position = Vector3.MoveTowards(transform.position, desiredPosition + shakeOffset, t * 5f);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);

        // 屏幕抖动衰减
        UpdateShake();
    }

    // ═══════════════════════════════════════════
    //  Follow 模式
    // ═══════════════════════════════════════════
    void UpdateFollow() {
        if (isDragging) return; // 拖拽时跳过死区

        if (Player.Instance == null) return;

        float px = Player.Instance.gridX;
        float pz = Player.Instance.gridY;

        if (px > targetFocusX + deadZoneX) targetFocusX = px - deadZoneX;
        else if (px < targetFocusX - deadZoneX) targetFocusX = px + deadZoneX;

        if (pz > targetFocusZ + deadZoneZ) targetFocusZ = pz - deadZoneZ;
        else if (pz < targetFocusZ - deadZoneZ) targetFocusZ = pz + deadZoneZ;

        float step = moveSpeed * Time.deltaTime;
        currentFocusX = Mathf.MoveTowards(currentFocusX, targetFocusX, step);
        currentFocusZ = Mathf.MoveTowards(currentFocusZ, targetFocusZ, step);

        ApplyFollowPosition();
    }

    void ApplyFollowPosition() {
        Vector3 focus = new(currentFocusX, 0, currentFocusZ);
        desiredPosition = focus + followOffset;
        desiredRotation = Quaternion.LookRotation(focus - desiredPosition, Vector3.up);
    }

    // ═══════════════════════════════════════════
    //  Overview 模式
    // ═══════════════════════════════════════════
    void UpdateOverview() {
        desiredPosition = overviewPosition;
        desiredRotation = overviewRotation;
    }

    void CalculateOverviewTransform() {
        var gm = GridManager.Instance;
        if (gm == null) return;

        float cx = (gm.width - 1f) * 0.5f;
        float cz = (gm.height - 1f) * 0.5f;
        Vector3 center = new(cx, 0, cz);

        float halfW = gm.width * 0.5f * (1f + overviewPadding);
        float halfH = gm.height * 0.5f * (1f + overviewPadding);

        float vFov = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
        float aspect = (float)Screen.width / Screen.height;
        float hFov = Mathf.Atan(Mathf.Tan(vFov) * aspect);

        float distV = halfH / Mathf.Tan(vFov);
        float distH = halfW / Mathf.Tan(hFov);
        float distance = Mathf.Max(distV, distH);

        float angleRad = overviewAngle * Mathf.Deg2Rad;
        overviewPosition = center + new Vector3(0, distance * Mathf.Sin(angleRad), -distance * Mathf.Cos(angleRad));
        overviewRotation = Quaternion.LookRotation(center - overviewPosition, Vector3.up);
    }

    // ═══════════════════════════════════════════
    //  输入处理（通过事件接收）
    // ═══════════════════════════════════════════
    void HandleScroll(float scroll) {
        Debug.Log("已滑动滚轮HandleScroll");
        // FOV 缩放：滚轮向上 → FOV 减小 → 放大效果
        targetFOV = Mathf.Clamp(targetFOV - scroll * zoomSpeed * 10f, minFOV, maxFOV);
    }

    void HandleDrag(Vector2 delta) {
        if (!isDragging || mode != CameraMode.Follow) return;

        float heightScale = followOffset.y * dragSensitivity;
        targetFocusX -= delta.x * heightScale * Time.deltaTime;
        targetFocusZ -= delta.y * heightScale * Time.deltaTime;
        currentFocusX = targetFocusX;
        currentFocusZ = targetFocusZ;
        ApplyFollowPosition();
    }

    // ═══════════════════════════════════════════
    //  屏幕抖动
    // ═══════════════════════════════════════════
    void OnPathFailed(PathFailedEvent evt) {
        shakeTimer = shakeDuration;
    }

    void UpdateShake() {
        if (shakeTimer > 0) {
            shakeTimer -= Time.unscaledDeltaTime;
            float intensity = shakeIntensity * (shakeTimer / shakeDuration);
            shakeOffset = new Vector3(
                Random.Range(-intensity, intensity),
                Random.Range(-intensity, intensity),
                0
            );
        } else {
            shakeOffset = Vector3.zero;
        }
    }

    // ═══════════════════════════════════════════
    //  模式切换
    // ═══════════════════════════════════════════
    public void SwitchMode() {
        if (mode == CameraMode.Follow) {
            mode = CameraMode.Overview;
            CalculateOverviewTransform();
        } else {
            mode = CameraMode.Follow;
            if (Player.Instance != null) {
                targetFocusX = currentFocusX = Player.Instance.gridX;
                targetFocusZ = currentFocusZ = Player.Instance.gridY;
            }
        }

        EventBus.Publish(new CameraModeChangedEvent {
            modeName = mode == CameraMode.Follow ? "跟随视角" : "全局视角"
        });
    }
}