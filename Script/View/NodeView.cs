using UnityEngine;

/// <summary>
/// 格子的视觉控制器 —— 挂在每个 Cube Prefab 上
/// 
/// ★ 核心修复：你原版的 SetPath() 没有调用 UpdateVisual()，
///   导致路径颜色设了但状态没同步，旧路径清除时 isPath=false 
///   却不会触发重绘 → 旧路径永远残留。
///   
/// Shader _State 值对照表:
///   0 = Default(可行走)  1 = Hover  2 = Start/End标记
///   3 = Path(最终路径)   4 = Exploring(开放列表-正在搜索)
///   5 = Explored(关闭列表-已搜索)  6 = Wall(障碍物)
///   7 = Player(玩家位置)
/// </summary>
public class NodeView : MonoBehaviour {
    public int X { get; private set; }
    public int Y { get; private set; }

    [SerializeField] private Renderer meshRenderer;

    private Material mat;

    // ---- 状态标记 ----
    private bool isWall;
    private bool isHover;
    private bool isStart;
    private bool isEnd;
    private bool isPath;
    private bool isExploring;  // A*搜索中-开放列表
    private bool isExplored;   // A*搜索中-关闭列表
    private bool isPlayer;

    public void Init(int x, int y) {
        X = x;
        Y = y;
        // 每个格子独立材质实例，不用 sharedMaterial
        mat = meshRenderer.material;
        SetState(0);
    }

    // ============================================================
    //  统一的状态优先级判定（从高到低）
    //  优先级: Player > Wall > Start/End > Path > Exploring > Explored > Hover > Default
    // ============================================================
    private void UpdateVisual() {
        float state;

        if (isPlayer) state = 7;
        else if (isWall) state = 6;
        else if (isStart || isEnd) state = 2;
        else if (isPath) state = 3;
        else if (isExploring) state = 4;
        else if (isExplored) state = 5;
        else if (isHover) state = 1;
        else state = 0;

        SetState(state);
    }

    private void SetState(float state) {
        if (mat != null)
            mat.SetFloat("_State", state);
    }

    // ============================================================
    //  公共接口 —— 每个都正确调用 UpdateVisual()
    // ============================================================

    public void SetHover(bool on) {
        isHover = on;
        UpdateVisual();
    }

    public void SetStart(bool on) {
        isStart = on;
        UpdateVisual();
    }

    public void SetEnd(bool on) {
        isEnd = on;
        UpdateVisual();
    }

    public void SetPath(bool on) {
        isPath = on;
        UpdateVisual();   // ★ 原版缺少这一行！
    }

    public void SetExploring(bool on) {
        isExploring = on;
        UpdateVisual();
    }

    public void SetExplored(bool on) {
        isExplored = on;
        UpdateVisual();
    }

    public void SetWall(bool on) {
        isWall = on;
        UpdateVisual();
    }

    public void SetPlayer(bool on) {
        isPlayer = on;
        UpdateVisual();
    }

    /// <summary>
    /// 一次性清除所有"寻路可视化"状态（保留 wall、player）
    /// </summary>
    public void ClearPathVisuals() {
        isPath = false;
        isExploring = false;
        isExplored = false;
        isStart = false;
        isEnd = false;
        UpdateVisual();
    }
}