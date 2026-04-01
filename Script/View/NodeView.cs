using UnityEngine;

/// <summary>
/// 格子视觉控制器 —— v2.0 升级版
/// 
/// 【v1.0 → v2.0 变化】
///   + _TerrainColor — 地形基础颜色（Material 属性）
///   + SetTerrain()  — 设置地形类型和颜色
///   + portalID 颜色区分
///   * Shader 新增 _TerrainColor 属性，State=0 时显示地形色而非固定灰色
///
/// Shader _State 值（交互状态，优先级从高到低）：
///   7 = Player    6 = Wall(已废弃,改用 TerrainColor)
///   5 = Explored  4 = Exploring
///   3 = Path      2 = Start/End
///   1 = Hover     0 = 显示 TerrainColor
/// </summary>
public class NodeView : MonoBehaviour {
    public int X { get; private set; }
    public int Y { get; private set; }

    [SerializeField] private Renderer meshRenderer;

    private Material mat;

    // ---- 交互状态标记 ----
    private bool isHover;
    private bool isStart;
    private bool isEnd;
    private bool isPath;
    private bool isExploring;
    private bool isExplored;
    private bool isPlayer;

    public void Init(int x, int y) {
        X = x;
        Y = y;
        mat = meshRenderer.material;  // 独立实例
        SetState(0);
    }

    /// <summary>设置地形颜色（一次性，地形变化时调用）</summary>
    public void SetTerrain(TerrainType type, int portalID = -1) {
        if (mat == null) return;

        Color c = (type == TerrainType.Portal && portalID >= 0)
            ? TerrainData.GetPortalColor(portalID)
            : TerrainData.GetColor(type);

        mat.SetColor("_TerrainColor", c);

        // Exit 地形额外设置发光
        float glow = (type == TerrainType.Exit) ? 1f : 0f;
        mat.SetFloat("_TerrainGlow", glow);

        UpdateVisual();
    }

    // ═══════════════════════════════════════════
    //  统一的状态优先级判定
    // ═══════════════════════════════════════════
    private void UpdateVisual() {
        float state;

        if (isPlayer) state = 7;
        else if (isStart || isEnd) state = 2;
        else if (isPath) state = 3;
        else if (isExploring) state = 4;
        else if (isExplored) state = 5;
        else if (isHover) state = 1;
        else state = 0; // 显示 _TerrainColor

        SetState(state);
    }

    private void SetState(float state) {
        if (mat != null)
            mat.SetFloat("_State", state);
    }

    // ═══════════════════════════════════════════
    //  公共接口
    // ═══════════════════════════════════════════
    public void SetHover(bool on) { isHover = on; UpdateVisual(); }
    public void SetStart(bool on) { isStart = on; UpdateVisual(); }
    public void SetEnd(bool on) { isEnd = on; UpdateVisual(); }
    public void SetPath(bool on) { isPath = on; UpdateVisual(); }
    public void SetExploring(bool on) { isExploring = on; UpdateVisual(); }
    public void SetExplored(bool on) { isExplored = on; UpdateVisual(); }
    public void SetPlayer(bool on) { isPlayer = on; UpdateVisual(); }

    /// <summary>清除所有寻路可视化状态（保留地形颜色和玩家）</summary>
    public void ClearPathVisuals() {
        isPath = false;
        isExploring = false;
        isExplored = false;
        isStart = false;
        isEnd = false;
        UpdateVisual();
    }
}