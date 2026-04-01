using UnityEngine;

/// <summary>
/// 网格输入控制器 —— v2.0 事件驱动版
/// 
/// 【v1.0 → v2.0 变化】
///   + 订阅 InputManager 事件，不再自己检测 Input
///   + 右键改为循环切换地形（Normal→Wall→Water→Mud→Speed→Normal）
///   + 悬停也改为事件检测（但 hover 需要每帧检测鼠标位置，
///     所以用 LateUpdate 做位置查询，但不直接检测按键）
/// </summary>
public class GridInputController : MonoSingleton<GridInputController> {
    private NodeView currentHover;

    public void Init() {
        // 订阅右键事件
        InputManager.Instance.OnRightClick += HandleRightClick;
    }

    void OnDestroy() {
        if (InputManager.Instance != null)
            InputManager.Instance.OnRightClick -= HandleRightClick;
    }

    // 悬停仍需每帧检测（鼠标位置持续变化）
    void LateUpdate() {
        if (!GameStateManager.Instance.IsPlaying) return;
        HandleHover();
    }

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

    void HandleRightClick() {
        NodeView target = GetNodeUnderMouse();
        if (target == null) return;

        // 循环切换地形类型
        GridManager.Instance.CycleTerrainAt(target.X, target.Y);
    }

    NodeView GetNodeUnderMouse() {
        if (!InputManager.Instance.GetMouseWorldPosition(out int gx, out int gz))
            return null;

        var gm = GridManager.Instance;
        if (gm != null && gm.InBounds(gx, gz))
            return gm.GetView(gx, gz);

        return null;
    }
}