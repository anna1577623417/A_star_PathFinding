using UnityEngine;

/// <summary>
/// 背景遮挡面 —— 在棋盘下方铺一个巨大 Plane 遮住天空盒
/// 
/// 【替代方案】
///   如果不想多一个 GO，直接：
///   Main Camera → Clear Flags → Solid Color → 选深灰色
///   效果一样，不需要这个脚本
///
/// 【本脚本做法】
///   在棋盘正下方铺一个 N 倍大的 Plane（深色材质）
///   大到中键拖拽到极限也看不到边缘
///
/// 挂载：单独的 Plane 物体（不是 GridShadow 那个）
/// 初始化：GameInitializer 调用 Init()
/// </summary>
public class GridBackground : MonoBehaviour {
    [Header("深度")]
    [Tooltip("在格子下方多少。要低于 GridShadow(-0.08)")]
    public float floorDepth = -0.5f;

    [Header("尺寸")]
    [Tooltip("= 棋盘最大边 × 此倍数")]
    public float sizeMultiplier = 5f;

    /// <summary>
    /// 由 GameInitializer 调用
    /// </summary>
    public void Init() {
        var gm = GridManager.Instance;
        if (gm == null) return;

        float w = gm.width;
        float h = gm.height;

        // ---- 棋盘中心 ----
        float cx = (w - 1f) * 0.5f;
        float cz = (h - 1f) * 0.5f;

        // ---- 尺寸：棋盘的 N 倍 ----
        float size = Mathf.Max(w, h) * sizeMultiplier;  // 20 × 5 = 100

        // ---- 应用 ----
        transform.position = new Vector3(cx, floorDepth, cz);
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(size / 10f, 1f, size / 10f); // 100/10 = 10
    }
}