using UnityEngine;

/// <summary>
/// 棋盘阴影面 —— 蓝色 Plane，偏移到棋盘左上方，营造立体投影感
/// 
/// 【为什么之前不生效】
///   GridShadow.Start() 比 GameInitializer.Start() 先执行，
///   此时 GridManager.views == null，直接 return 了，Plane 大小没变。
///   现在删掉 Start()，只通过 GameInitializer 调用 Init()。
///
/// 【坐标系统备忘】
///   - Node(0,0) 在世界坐标 (0, 0, 0)
///   - 每个 Cube Scale = (1, 0.02, 1)，中心在整数坐标，占 1×1 世界单位
///   - 20×20 网格：Cube 中心 X ∈ [0, 19], Z ∈ [0, 19]
///   - Cube 边缘实际覆盖 X ∈ [-0.5, 19.5], Z ∈ [-0.5, 19.5]
///   - 棋盘中心 = ((20-1)/2, 0, (20-1)/2) = (9.5, 0, 9.5)
///
/// 【Unity Plane 尺寸规则】
///   Plane 在 Scale(1,1,1) = 10×10 世界单位
///   要覆盖 20×20 → Scale = (2, 1, 2)
///
/// 【相机视角下的方向】
///   相机 offset = (0, 18, -8)，即在玩家后上方俯视
///   屏幕左 = -X 方向，屏幕上 = +Z 方向
///   "左上方偏移" = offset 朝 (-X, +Z)
///
/// 挂载：蓝色 Plane 物体
/// 初始化：GameInitializer 调用 Init()（在 GridManager.Init 之后）
/// </summary>
public class GridShadow : MonoBehaviour {
    [Header("偏移方向（世界单位）")]
    [Tooltip("负值 = 向屏幕左偏（-X 方向）")]
    public float offsetX = -1.0f;

    [Tooltip("正值 = 向屏幕上偏（+Z 方向）")]
    public float offsetZ = 1.0f;

    [Header("高度")]
    [Tooltip("Y 坐标，必须 < 0 才在 Cube 下方。Cube 底面在 Y=-0.01")]
    public float yOffset = -0.08f;

    [Header("留边")]
    [Tooltip("阴影比棋盘每边多出多少单位")]
    public float padding = 0.3f;

    /// <summary>
    /// 由 GameInitializer 调用
    /// </summary>
    public void Init() {
        var gm = GridManager.Instance;
        if (gm == null || gm.views == null) {
            Debug.LogWarning("[GridShadow] GridManager 未就绪");
            return;
        }

        float w = gm.width;    // 20
        float h = gm.height;   // 20

        // ---- 棋盘中心 ----
        // Node 坐标 [0, w-1]，中心 = (w-1)/2
        float cx = (w - 1f) * 0.5f;    // 9.5
        float cz = (h - 1f) * 0.5f;    // 9.5

        // ---- Position: 中心 + 偏移 ----
        transform.position = new Vector3(
            cx + offsetX,       // 9.5 + (-1) = 8.5  → 向左
            yOffset,            // -0.08              → 在格子下方
            cz + offsetZ        // 9.5 + 1  = 10.5   → 向上
        );

        // ---- Scale: 覆盖整个棋盘 + padding ----
        // 棋盘面积 = w × h = 20 × 20 世界单位
        // 加 padding → (20 + 0.6) × (20 + 0.6)
        // Plane Scale(1) = 10 单位 → Scale = total / 10
        float totalW = w + padding * 2f;    // 20.6
        float totalH = h + padding * 2f;    // 20.6

        transform.localScale = new Vector3(
            totalW / 10f,       // 2.06
            1f,
            totalH / 10f        // 2.06
        );
    }
}