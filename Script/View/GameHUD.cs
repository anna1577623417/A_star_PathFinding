using UnityEngine;

/// <summary>
/// 屏幕 HUD —— 操作说明 + 颜色图例 + 寻路数据
/// 支持可调字体大小、行间距、边距、半透明背景面板 + 文字描边
/// 
/// 新增：
///   - 单次步数（本次寻路路径的格数）
///   - H 键 或 GUI 按钮切换 HUD 显示/隐藏
///
/// 挂在任意根节点即可
/// </summary>
public class GameHUD : MonoBehaviour {
    [Header("═══ 布局 ═══")]
    [Tooltip("面板左上角 X 偏移")]
    [Range(0, 100)]
    [SerializeField] private float marginX = 12f;

    [Tooltip("面板左上角 Y 偏移")]
    [Range(0, 100)]
    [SerializeField] private float marginY = 12f;

    [Tooltip("面板内边距")]
    [Range(4, 40)]
    [SerializeField] private float padding = 14f;

    [Header("═══ 字体 ═══")]
    [Range(10, 36)]
    [SerializeField] private int titleFontSize = 20;

    [Range(8, 28)]
    [SerializeField] private int bodyFontSize = 14;

    [Tooltip("每行额外间距(像素)")]
    [Range(0, 20)]
    [SerializeField] private float lineSpacing = 4f;

    [Header("═══ 配色 ═══")]
    [SerializeField] private Color titleColor = new Color(1f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color bodyColor = new Color(0.9f, 0.92f, 0.95f, 1f);
    [SerializeField] private Color accentColor = new Color(0.55f, 0.85f, 1f, 1f);
    [SerializeField] private Color dataColor = new Color(0.6f, 1f, 0.7f, 1f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.12f, 0.75f);

    [Header("═══ 描边 ═══")]
    [Tooltip("文字描边偏移(像素), 0=关闭")]
    [Range(0, 3)]
    [SerializeField] private int outlineOffset = 1;

    [Header("═══ 开关 ═══")]
    [Tooltip("HUD 初始是否显示")]
    [SerializeField] private bool showHUD = true;

    [Tooltip("切换 HUD 的快捷键")]
    [SerializeField] private KeyCode toggleKey = KeyCode.H;

    // ── 缓存 ──
    private GUIStyle _titleStyle;
    private GUIStyle _bodyStyle;
    private GUIStyle _accentStyle;
    private GUIStyle _dataStyle;
    private GUIStyle _btnStyle;
    private Texture2D _panelTex;
    private Texture2D _separatorTex;
    private Texture2D _btnTex;

    void Update() {
        // H 键切换 HUD 显隐
        if (Input.GetKeyDown(toggleKey))
            showHUD = !showHUD;
    }

    // ══════════════════════════════════════════
    //  样式构建
    // ══════════════════════════════════════════
    private void RebuildStyles() {
        _panelTex = MakeTex(4, 4, panelColor);
        _separatorTex = MakeTex(4, 1, new Color(1f, 1f, 1f, 0.15f));
        _btnTex = MakeTex(4, 4, new Color(0.2f, 0.2f, 0.3f, 0.85f));

        _titleStyle = new GUIStyle(GUI.skin.label) {
            fontSize = titleFontSize,
            fontStyle = FontStyle.Bold,
            normal = { textColor = titleColor },
            wordWrap = false
        };

        _bodyStyle = new GUIStyle(GUI.skin.label) {
            fontSize = bodyFontSize,
            fontStyle = FontStyle.Normal,
            normal = { textColor = bodyColor },
            wordWrap = false
        };

        _accentStyle = new GUIStyle(_bodyStyle) {
            normal = { textColor = accentColor }
        };

        _dataStyle = new GUIStyle(_bodyStyle) {
            normal = { textColor = dataColor }
        };

        _btnStyle = new GUIStyle(GUI.skin.button) {
            fontSize = bodyFontSize,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = _btnTex },
            hover = { textColor = titleColor, background = _btnTex },
            active = { textColor = dataColor, background = _btnTex },
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(8, 8, 4, 4)
        };
    }

    // ══════════════════════════════════════════
    //  绘制
    // ══════════════════════════════════════════
    private void OnGUI() {
        RebuildStyles();

        // ──── 右上角始终显示开关按钮 ────
        float btnW = 80f, btnH = 28f;
        Rect btnRect = new Rect(Screen.width - btnW - 12f, 12f, btnW, btnH);
        string btnLabel = showHUD ? "隐藏 [H]" : "显示 [H]";
        if (GUI.Button(btnRect, btnLabel, _btnStyle))
            showHUD = !showHUD;

        if (!showHUD) return;

        // ──── 计算尺寸 ────
        float lineH = bodyFontSize + lineSpacing;
        float titleH = titleFontSize + lineSpacing + 4f;
        float sepH = 6f;

        // 面板高度预算
        int dataLines = 0;
        var p = Player.Instance;
        if (p != null) {
            dataLines = 1;                          // 位置 + 总步数行
            if (p.pathfindCount > 0) dataLines += 2; // 单次步数 + 耗时
        }

        float contentH = titleH
                       + sepH
                       + lineH * 6         // 操作说明 6 行
                       + sepH
                       + lineH * 3         // 颜色图例
                       + sepH
                       + lineH * dataLines // 玩家数据（动态行数）
                       + padding * 2;

        float panelW = 480f;

        // ──── 底板 ────
        Rect panelRect = new Rect(marginX, marginY, panelW, contentH);
        GUI.DrawTexture(panelRect, _panelTex);

        float x = marginX + padding;
        float y = marginY + padding;
        float w = panelW - padding * 2;

        // ──── 标题 ────
        DrawOutlinedLabel(new Rect(x, y, w, titleH), "★ A* Pathfinding Lab v1.0", _titleStyle);
        y += titleH;
        DrawSeparator(x, y, w); y += sepH;

        // ──── 操作说明 ────
        DrawOutlinedLabel(new Rect(x, y, w, lineH), "WASD / 方向键    逐格移动", _bodyStyle); y += lineH;
        DrawOutlinedLabel(new Rect(x, y, w, lineH), "鼠标左键          A*寻路 + 自动行走", _bodyStyle); y += lineH;
        DrawOutlinedLabel(new Rect(x, y, w, lineH), "鼠标右键          放置 / 移除墙壁", _bodyStyle); y += lineH;
        DrawOutlinedLabel(new Rect(x, y, w, lineH), "鼠标中键拖拽    平移视野", _bodyStyle); y += lineH;
        DrawOutlinedLabel(new Rect(x, y, w, lineH), "鼠标滚轮          缩放", _bodyStyle); y += lineH;
        DrawOutlinedLabel(new Rect(x, y, w, lineH), "Tab                    切换 跟随 / 全局 视角", _bodyStyle); y += lineH;
        DrawSeparator(x, y, w); y += sepH;

        // ──── 颜色图例 ────
        DrawOutlinedLabel(new Rect(x, y, w, lineH), "■ 灰=空地    ■ 深棕=墙壁", _accentStyle); y += lineH;
        DrawOutlinedLabel(new Rect(x, y, w, lineH), "■ 深蓝=已探索   ■ 绿=最终路径", _accentStyle); y += lineH;
        DrawOutlinedLabel(new Rect(x, y, w, lineH), "■ 橙黄=玩家    ■ 金黄=起/终点", _accentStyle); y += lineH;
        DrawSeparator(x, y, w); y += sepH;

        // ──── 玩家实时数据 ────
        if (p != null) {
            DrawOutlinedLabel(new Rect(x, y, w, lineH),
                $"位置: ({p.gridX}, {p.gridY})   总步数: {p.totalSteps}   寻路次数: {p.pathfindCount}",
                _dataStyle);
            y += lineH;

            if (p.pathfindCount > 0) {
                // 单次步数（本次寻路走了几格）
                DrawOutlinedLabel(new Rect(x, y, w, lineH),
                    $"单次步数: {p.lastPathLength} 格",
                    _dataStyle);
                y += lineH;

                // 耗时数据
                DrawOutlinedLabel(new Rect(x, y, w, lineH),
                    $"算法耗时: {p.lastSearchTime:F3}s   行走耗时: {p.lastWalkTime:F2}s",
                    _dataStyle);
            }
        }
    }

    // ══════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════

    /// <summary>带描边的 Label（四方向偏移模拟 Outline）</summary>
    private void DrawOutlinedLabel(Rect rect, string text, GUIStyle style) {
        if (outlineOffset <= 0) {
            GUI.Label(rect, text, style);
            return;
        }
        Color original = style.normal.textColor;
        style.normal.textColor = shadowColor;
        int o = outlineOffset;
        GUI.Label(new Rect(rect.x - o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y - o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y + o, rect.width, rect.height), text, style);
        style.normal.textColor = original;
        GUI.Label(rect, text, style);
    }

    /// <summary>水平分割线</summary>
    private void DrawSeparator(float x, float y, float width) {
        if (_separatorTex != null)
            GUI.DrawTexture(new Rect(x, y + 2f, width, 1f), _separatorTex);
    }

    /// <summary>纯色纹理</summary>
    private static Texture2D MakeTex(int w, int h, Color col) {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}