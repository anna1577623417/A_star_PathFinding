using UnityEngine;

/// <summary>
/// 网格输入控制器 —— 悬停高亮 + 右键编辑墙壁
/// 
/// 【修复策略】
///   右键不再依赖 currentHover，自己独立做一次平面求交。
///   这样即使 Hover 因为某种原因没有命中，右键依然能独立工作。
///   同时添加了详细的 Debug.Log 来定位问题。
///
/// 挂载：根节点
/// </summary>
public class GridInputController : MonoSingleton<GridInputController> {
    [Header("═══ 调试 ═══")]
    [Tooltip("开启后在 Console 打印每次右键的详细信息")]
    [SerializeField] private bool debugMode = true;

    private NodeView currentHover;
    private Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

    void Start() {
        Debug.Log($"[GridInputController] ★ 已启动，挂在 '{gameObject.name}' 上");
    }

    void Update() {
        HandleHover();
        HandleRightClick();
    }

    // ============================================================
    //  悬停高亮
    // ============================================================
    void HandleHover() {
        NodeView hit = GetNodeUnderMouse();

        if (hit != currentHover) {
            if (currentHover != null)
                currentHover.SetHover(false);

            currentHover = hit;

            if (currentHover != null)
                currentHover.SetHover(true);
        }
    }

    // ============================================================
    //  右键编辑墙壁 —— 完全独立于 Hover，自己做检测
    // ============================================================
    void HandleRightClick() {
        // 检测所有鼠标按键（调试用）
        if (debugMode) {
            if (Input.GetMouseButtonDown(0)) Debug.Log("[GridInput] 检测到 左键按下");
            if (Input.GetMouseButtonDown(1)) Debug.Log("[GridInput] 检测到 右键按下");
            if (Input.GetMouseButtonDown(2)) Debug.Log("[GridInput] 检测到 中键按下");
        }

        if (!Input.GetMouseButtonDown(1)) return;

        // 右键被按下了，独立做一次平面求交（不依赖 currentHover）
        NodeView target = GetNodeUnderMouse();

        if (debugMode) {
            if (target != null)
                Debug.Log($"[GridInput] 右键命中格子 ({target.X}, {target.Y})，当前 walkable={GridManager.Instance.GetNode(target.X, target.Y).walkable}");
            else
                Debug.Log("[GridInput] 右键未命中任何格子");
        }

        if (target == null) return;

        // 切换墙壁
        GridManager.Instance.ToggleWall(target.X, target.Y);

        if (debugMode) {
            bool newWalkable = GridManager.Instance.GetNode(target.X, target.Y).walkable;
            Debug.Log($"[GridInput] ✓ 格子 ({target.X}, {target.Y}) 已切换 → walkable={newWalkable}");
        }
    }

    // ============================================================
    //  平面求交 —— 鼠标位置 → 格子坐标
    // ============================================================
    NodeView GetNodeUnderMouse() {
        if (Camera.main == null) return null;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (groundPlane.Raycast(ray, out float distance)) {
            Vector3 worldPoint = ray.GetPoint(distance);
            int gx = Mathf.RoundToInt(worldPoint.x);
            int gz = Mathf.RoundToInt(worldPoint.z);

            var gm = GridManager.Instance;
            if (gm != null && gm.InBounds(gx, gz))
                return gm.GetView(gx, gz);
        }

        return null;
    }
}