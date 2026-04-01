using UnityEngine;

/// <summary>
/// HUD v2.0 —— 新增算法显示、探索统计、地形图例、游戏状态
/// </summary>
public class GameHUD : MonoBehaviour {
    [Header("═══ 布局 ═══")]
    [Range(0, 100)][SerializeField] private float marginX = 12f;
    [Range(0, 100)][SerializeField] private float marginY = 12f;
    [Range(4, 40)][SerializeField] private float padding = 14f;

    [Header("═══ 字体 ═══")]
    [Range(10, 36)][SerializeField] private int titleFontSize = 20;
    [Range(8, 28)][SerializeField] private int bodyFontSize = 14;
    [Range(0, 20)][SerializeField] private float lineSpacing = 4f;

    [Header("═══ 配色 ═══")]
    [SerializeField] private Color titleColor = new(1f, 0.85f, 0.35f);
    [SerializeField] private Color bodyColor = new(0.9f, 0.92f, 0.95f);
    [SerializeField] private Color accentColor = new(0.55f, 0.85f, 1f);
    [SerializeField] private Color dataColor = new(0.6f, 1f, 0.7f);
    [SerializeField] private Color shadowColor = new(0, 0, 0, 0.8f);
    [SerializeField] private Color panelColor = new(0.05f, 0.05f, 0.12f, 0.75f);

    [Header("═══ 开关 ═══")]
    [SerializeField] private bool showHUD = true;
    [Range(0, 3)][SerializeField] private int outlineOffset = 1;

    private GUIStyle _title, _body, _accent, _data, _btn;
    private Texture2D _panelTex, _sepTex, _btnTex;

    void Start() {
        InputManager.Instance.OnToggleHUD += () => showHUD = !showHUD;
    }

    void RebuildStyles() {
        _panelTex = Tex(panelColor);
        _sepTex = Tex(new Color(1, 1, 1, 0.15f));
        _btnTex = Tex(new Color(0.2f, 0.2f, 0.3f, 0.85f));

        _title = S(titleFontSize, FontStyle.Bold, titleColor);
        _body = S(bodyFontSize, FontStyle.Normal, bodyColor);
        _accent = S(bodyFontSize, FontStyle.Normal, accentColor);
        _data = S(bodyFontSize, FontStyle.Normal, dataColor);
        _btn = new GUIStyle(GUI.skin.button) {
            fontSize = bodyFontSize, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = _btnTex },
            alignment = TextAnchor.MiddleCenter
        };
    }

    GUIStyle S(int size, FontStyle fs, Color c) => new(GUI.skin.label) {
        fontSize = size, fontStyle = fs, normal = { textColor = c }, wordWrap = false
    };

    void OnGUI() {
        RebuildStyles();

        // 右上角开关按钮
        float btnW = 80, btnH = 28;
        if (GUI.Button(new Rect(Screen.width - btnW - 12, 12, btnW, btnH),
            showHUD ? "隐藏 [H]" : "显示 [H]", _btn))
            showHUD = !showHUD;

        // 游戏状态覆盖层
        var gs = GameStateManager.Instance;
        if (gs != null && gs.CurrentState == GameStateType.Paused)
            DrawCentered("已暂停 — Esc 恢复", _title);
        if (gs != null && gs.CurrentState == GameStateType.GameOver)
            DrawCentered("通关！ — R 重玩", _title);

        if (!showHUD) return;

        float lineH = bodyFontSize + lineSpacing;
        float titleH = titleFontSize + lineSpacing + 4;
        float sepH = 6;

        var p = Player.Instance;
        int dataLines = 0;
        if (p != null) {
            dataLines = 2; // 算法+位置
            if (p.pathfindCount > 0) dataLines += 2;
        }

        float contentH = titleH + sepH + lineH * 6 + sepH + lineH * 4 + sepH + lineH * dataLines + padding * 2;
        float panelW = 500;
        Rect panel = new(marginX, marginY, panelW, contentH);
        GUI.DrawTexture(panel, _panelTex);

        float x = marginX + padding, y = marginY + padding, w = panelW - padding * 2;

        // 标题
        OL(new Rect(x, y, w, titleH), "★ A* Pathfinding Lab v2.0", _title); y += titleH;
        Sep(x, y, w); y += sepH;

        // 操作
        OL(new Rect(x, y, w, lineH), "WASD           逐格移动", _body); y += lineH;
        OL(new Rect(x, y, w, lineH), "鼠标左键    A*寻路 + 自动行走", _body); y += lineH;
        OL(new Rect(x, y, w, lineH), "鼠标右键    循环切换地形", _body); y += lineH;
        OL(new Rect(x, y, w, lineH), "中键拖拽    平移视野", _body); y += lineH;
        OL(new Rect(x, y, w, lineH), "滚轮           缩放(FOV)", _body); y += lineH;
        OL(new Rect(x, y, w, lineH), "Tab 视角   Q 算法   Esc 暂停   R 重玩", _body); y += lineH;
        Sep(x, y, w); y += sepH;

        // 地形图例
        OL(new Rect(x, y, w, lineH), "■ 灰=空地  ■ 深棕=墙壁  ■ 蓝=水面  ■ 棕=桥", _accent); y += lineH;
        OL(new Rect(x, y, w, lineH), "■ 泥黄=泥地(慢)  ■ 青=加速带  ■ 紫=传送门", _accent); y += lineH;
        OL(new Rect(x, y, w, lineH), "■ 金黄=出口  ■ 橙=玩家  ■ 绿=路径", _accent); y += lineH;
        OL(new Rect(x, y, w, lineH), "■ 深蓝=已探索  ■ 淡蓝=悬停", _accent); y += lineH;
        Sep(x, y, w); y += sepH;

        // 数据
        if (p != null) {
            OL(new Rect(x, y, w, lineH), $"算法: {p.CurrentAlgorithm}   位置: ({p.gridX},{p.gridY})", _data); y += lineH;
            OL(new Rect(x, y, w, lineH), $"总步数: {p.totalSteps}   寻路次数: {p.pathfindCount}", _data); y += lineH;

            if (p.pathfindCount > 0) {
                OL(new Rect(x, y, w, lineH), $"路径: {p.lastPathLength}步   探索: {p.lastExploredCount}格", _data); y += lineH;
                OL(new Rect(x, y, w, lineH), $"算法耗时: {p.lastSearchTime:F3}s   行走耗时: {p.lastWalkTime:F2}s", _data);
            }
        }
    }

    void DrawCentered(string text, GUIStyle style) {
        float w = 400, h = 60;
        Rect r = new((Screen.width - w) / 2f, Screen.height * 0.4f, w, h);
        GUI.DrawTexture(r, _panelTex);
        GUI.Label(r, text, new GUIStyle(style) { alignment = TextAnchor.MiddleCenter });
    }

    void OL(Rect r, string t, GUIStyle s) {
        if (outlineOffset <= 0) { GUI.Label(r, t, s); return; }
        var c = s.normal.textColor;
        s.normal.textColor = shadowColor;
        int o = outlineOffset;
        GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), t, s);
        GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), t, s);
        GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), t, s);
        GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), t, s);
        s.normal.textColor = c;
        GUI.Label(r, t, s);
    }

    void Sep(float x, float y, float w) { if (_sepTex != null) GUI.DrawTexture(new Rect(x, y + 2, w, 1), _sepTex); }

    static Texture2D Tex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
}