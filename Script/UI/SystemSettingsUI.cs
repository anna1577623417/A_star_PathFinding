using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 系统设置 UI —— 使用官方 Input System Rebind API
/// 
/// 【和 v2.0 的根本区别】
///   v2.0: 自造 KeyBindAction SO + KeyBindManager + 手动 Key 捕获
///   v2.1: 直接读 InputActionAsset，用 PerformInteractiveRebinding() 官方 API
///
/// 【官方 Rebind API 工作流程】
///   1. action.Disable() — 必须先禁用才能重绑
///   2. action.PerformInteractiveRebinding(bindingIndex) — 启动交互式重绑
///   3. .WithControlsExcluding("Mouse") — 排除鼠标（防止误绑鼠标移动）
///   4. .OnComplete(op => { op.Dispose(); action.Enable(); }) — 完成后清理
///   5. .Start() — 开始监听
///   6. action.GetBindingDisplayString(bindingIndex) — 获取人类可读的按键名
///   7. asset.SaveBindingOverridesAsJson() — 保存所有覆盖
///
/// 挂载：根节点
/// </summary>
public class SystemSettingsUI : MonoBehaviour {
    private enum PanelState { Closed, MainMenu, KeyBindList }

    [Header("═══ Menu 按钮 ═══")]
    [SerializeField] private string menuButtonText = "⚙";
    [Range(28, 60)][SerializeField] private int menuButtonSize = 40;

    [Header("═══ 面板 ═══")]
    [Range(300, 700)][SerializeField] private int panelWidth = 480;

    [Header("═══ 字体 ═══")]
    [Range(12, 24)][SerializeField] private int titleFontSize = 18;
    [Range(10, 20)][SerializeField] private int bodyFontSize = 14;

    [Header("═══ 配色 ═══")]
    [SerializeField] private Color panelBg = new(0.08f, 0.08f, 0.15f, 0.92f);
    [SerializeField] private Color headerColor = new(1f, 0.85f, 0.35f);
    [SerializeField] private Color bodyColor = new(0.9f, 0.92f, 0.95f);
    [SerializeField] private Color buttonBg = new(0.2f, 0.2f, 0.32f, 0.9f);
    [SerializeField] private Color keyBtnBg = new(0.15f, 0.25f, 0.4f, 0.9f);
    [SerializeField] private Color popupBg = new(0.05f, 0.05f, 0.1f, 0.95f);
    [SerializeField] private Color rebindColor = new(0.4f, 1f, 0.6f);
    [SerializeField] private Color warningColor = new(1f, 0.8f, 0.2f);

    [Header("═══ Rebind ═══")]
    [Range(0.5f, 3f)][SerializeField] private float confirmDelay = 1.0f;

    // ---- 状态 ----
    private PanelState state = PanelState.Closed;
    private bool isRebinding;
    private string rebindActionName = "";
    private string rebindResult = "";
    private InputActionRebindingExtensions.RebindingOperation currentRebindOp;
    private Coroutine confirmCoroutine;
    private Vector2 scrollPos;

    // ---- 样式缓存 ----
    private GUIStyle _title, _body, _btn, _keyBtn, _popupTitle, _popupHint;
    private Texture2D _panelTex, _btnTex, _keyBtnTex, _popupTex, _overlayTex;
    private bool stylesBuilt;

    // ═══════════════════════════════════════════
    //  样式
    // ═══════════════════════════════════════════
    void BuildStyles() {
        _panelTex = Tex(panelBg);
        _btnTex = Tex(buttonBg);
        _keyBtnTex = Tex(keyBtnBg);
        _popupTex = Tex(popupBg);
        _overlayTex = Tex(new Color(0, 0, 0, 0.5f));

        _title = new GUIStyle(GUI.skin.label) {
            fontSize = titleFontSize, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = headerColor }
        };
        _body = new GUIStyle(GUI.skin.label) {
            fontSize = bodyFontSize,
            normal = { textColor = bodyColor }
        };
        _btn = new GUIStyle(GUI.skin.button) {
            fontSize = bodyFontSize, fontStyle = FontStyle.Bold,
            normal = { textColor = bodyColor, background = _btnTex },
            hover = { textColor = headerColor, background = _btnTex },
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(12, 12, 6, 6)
        };
        _keyBtn = new GUIStyle(_btn) {
            normal = { textColor = new Color(0.7f, 0.9f, 1f), background = _keyBtnTex },
            hover = { textColor = Color.white, background = _keyBtnTex }
        };
        _popupTitle = new GUIStyle(_title) { fontSize = titleFontSize + 2 };
        _popupHint = new GUIStyle(GUI.skin.label) {
            fontSize = bodyFontSize + 2, alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Italic,
            normal = { textColor = new Color(0.7f, 0.7f, 0.8f) }
        };
        stylesBuilt = true;
    }

    // ═══════════════════════════════════════════
    //  OnGUI 主入口
    // ═══════════════════════════════════════════
    void OnGUI() {
        if (!stylesBuilt) BuildStyles();

        // ---- Menu 按钮 ----
        Rect menuRect = new(Screen.width - menuButtonSize - 12, 50, menuButtonSize, menuButtonSize);
        var menuStyle = new GUIStyle(_btn) { fontSize = menuButtonSize / 2 };
        if (GUI.Button(menuRect, menuButtonText, menuStyle)) {
            if (state == PanelState.Closed) {
                state = PanelState.MainMenu;
                GameStateManager.Instance.SetState(GameStateType.Paused);
            } else CloseAll();
        }

        switch (state) {
            case PanelState.MainMenu: DrawMainMenu(); break;
            case PanelState.KeyBindList: DrawKeyBindList(); break;
        }

        if (isRebinding) DrawRebindPopup();
    }

    // ═══════════════════════════════════════════
    //  主菜单
    // ═══════════════════════════════════════════
    void DrawMainMenu() {
        float w = panelWidth, h = 240;
        Rect panel = CenterRect(w, h);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlayTex);
        GUI.DrawTexture(panel, _panelTex);

        float x = panel.x + 20, y = panel.y + 16, bw = w - 40, bh = 36, gap = 8;

        GUI.Label(new Rect(x, y, bw, 30), "系统设置", _title); y += 40;

        var gs = GameStateManager.Instance;
        string pauseLabel = (gs?.CurrentState == GameStateType.Paused) ? "恢复游戏" : "暂停游戏";
        if (GUI.Button(new Rect(x, y, bw, bh), pauseLabel, _btn)) { gs?.TogglePause(); CloseAll(); }
        y += bh + gap;

        if (GUI.Button(new Rect(x, y, bw, bh), "重新开始", _btn)) { CloseAll(); gs?.Restart(); }
        y += bh + gap;

        if (GUI.Button(new Rect(x, y, bw, bh), "按键管理", _btn)) state = PanelState.KeyBindList;
        y += bh + gap;

        if (GUI.Button(new Rect(x, y, bw, bh), "关闭", _btn)) CloseAll();
    }

    // ═══════════════════════════════════════════
    //  按键管理（直接读 InputActionAsset）
    // ═══════════════════════════════════════════
    void DrawKeyBindList() {
        var im = InputManager.Instance;
        if (im == null || im.InputActions == null) return;

        var asset = im.InputActions.asset;

        // 统计可重绑的 action 数量
        int actionCount = 0;
        foreach (var map in asset.actionMaps)
            foreach (var action in map.actions)
                actionCount++;

        float w = panelWidth + 60;
        float listH = actionCount * 42f;
        float h = Mathf.Min(120 + listH + 60, Screen.height * 0.85f);
        Rect panel = CenterRect(w, h);

        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlayTex);
        GUI.DrawTexture(panel, _panelTex);

        float x = panel.x + 20, y = panel.y + 16, iw = w - 40;

        GUI.Label(new Rect(x, y, iw, 30), "按键管理", _title); y += 40;

        // 表头
        GUI.Label(new Rect(x, y, iw * 0.15f, 24), "分组", _body);
        GUI.Label(new Rect(x + iw * 0.15f, y, iw * 0.35f, 24), "功能", _body);
        GUI.Label(new Rect(x + iw * 0.55f, y, iw * 0.45f, 24), "按键", _body);
        y += 28;

        // 可滚动列表
        float scrollH = h - 160;
        float contentH = actionCount * 42f;
        scrollPos = GUI.BeginScrollView(
            new Rect(x, y, iw, scrollH), scrollPos,
            new Rect(0, 0, iw - 20, contentH));

        float rowY = 0;
        foreach (var map in asset.actionMaps) {
            foreach (var action in map.actions) {
                DrawActionRow(map.name, action, iw - 20, rowY);
                rowY += 42;
            }
        }

        GUI.EndScrollView();
        y += scrollH + 10;

        // 底部按钮
        float btnW = (iw - 20) / 3f;
        if (GUI.Button(new Rect(x, y, btnW, 34), "全部重置", _btn)) {
            im.ResetAllBindings();
        }
        if (GUI.Button(new Rect(x + btnW + 10, y, btnW, 34), "保存", _btn)) {
            im.SaveBindings();
            Toast.Show("绑定已保存", Toast.Level.Success, 1.5f);
        }
        if (GUI.Button(new Rect(x + (btnW + 10) * 2, y, btnW, 34), "返回", _btn)) {
            state = PanelState.MainMenu;
        }
    }

    /// <summary>
    /// 绘制一行：[分组] [功能名] [按键按钮]
    /// 
    /// 【官方 API 用法】
    ///   action.GetBindingDisplayString(bindingIndex)
    ///   → 返回人类可读的按键名，如 "W" "Ctrl+S" "Left Stick"
    ///   自动处理 Composite binding 的显示
    /// </summary>
    void DrawActionRow(string mapName, InputAction action, float rowW, float rowY) {
        float col1 = rowW * 0.15f;
        float col2 = rowW * 0.35f;
        float col3 = rowW * 0.45f;
        float col3X = rowW - col3;

        // 分组名
        GUI.Label(new Rect(4, rowY + 4, col1, 34), mapName, _body);

        // 功能名
        GUI.Label(new Rect(col1 + 4, rowY + 4, col2, 34), action.name, _body);

        // 按键显示（取第一个非 Composite 绑定的显示名）
        string displayStr = GetMainBindingDisplay(action);

        // 按键按钮 → 点击启动 Rebind
        if (GUI.Button(new Rect(col3X, rowY + 4, col3, 34), displayStr, _keyBtn)) {
            if (!isRebinding)
                StartRebind(action);
        }
    }

    /// <summary>
    /// 获取 Action 的主要绑定显示字符串
    /// 
    /// Composite binding（如 WASD）显示为 "W/A/S/D"
    /// 普通 binding 显示为 "Tab" "Q" 等
    /// </summary>
    string GetMainBindingDisplay(InputAction action) {
        if (action.bindings.Count == 0) return "未绑定";

        // 对于 Composite（如 Move 的 WASD），显示组合名
        for (int i = 0; i < action.bindings.Count; i++) {
            if (action.bindings[i].isComposite) {
                // 返回整个 Composite 的显示字符串
                return action.GetBindingDisplayString(i,
                    InputBinding.DisplayStringOptions.DontIncludeInteractions);
            }
        }

        // 普通绑定：取第一个
        return action.GetBindingDisplayString(0,
            InputBinding.DisplayStringOptions.DontIncludeInteractions);
    }

    // ═══════════════════════════════════════════
    //  Rebind（官方 PerformInteractiveRebinding API）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 启动交互式重绑定
    /// 
    /// 【官方 API 工作流】
    ///   1. action.Disable() — 不禁用会冲突
    ///   2. PerformInteractiveRebinding(bindingIndex)
    ///      - 找到第一个非 Composite 的绑定索引
    ///   3. WithControlsExcluding("Mouse") — 防止鼠标移动被误绑
    ///   4. WithCancelingThrough("<Keyboard>/escape") — Esc 取消
    ///   5. OnComplete → 显示结果 → 延迟关闭 → 重新 Enable
    ///   6. OnCancel → 直接关闭
    ///   7. Start() — 开始监听下一次按键
    /// </summary>
    void StartRebind(InputAction action) {
        // 找到要重绑的 binding 索引（跳过 Composite 本身，找第一个实际绑定）
        int bindingIndex = -1;
        for (int i = 0; i < action.bindings.Count; i++) {
            if (!action.bindings[i].isComposite) {
                bindingIndex = i;
                break;
            }
        }
        if (bindingIndex < 0) return;

        isRebinding = true;
        rebindActionName = action.name;
        rebindResult = "";

        // ★ 必须先禁用 Action
        action.Disable();

        currentRebindOp = action.PerformInteractiveRebinding(bindingIndex)
            // 排除鼠标位置/移动（防止误绑）
            .WithControlsExcluding("Mouse")
            // Esc 取消
            .WithCancelingThrough("<Keyboard>/escape")
            // 完成回调
            .OnComplete(op => {
                // 读取新绑定的显示名
                rebindResult = action.GetBindingDisplayString(bindingIndex,
                    InputBinding.DisplayStringOptions.DontIncludeInteractions);

                op.Dispose();
                currentRebindOp = null;

                // 延迟关闭（WaitForSecondsRealtime → 暂停时仍计时）
                confirmCoroutine = StartCoroutine(ConfirmAndClose(action));
            })
            // 取消回调
            .OnCancel(op => {
                op.Dispose();
                currentRebindOp = null;
                action.Enable();
                isRebinding = false;
            })
            .Start();
    }

    /// <summary>
    /// 重绑完成后：显示结果 → 等 confirmDelay 秒 → 关闭 Popup → Enable Action
    /// </summary>
    IEnumerator ConfirmAndClose(InputAction action) {
        // 等待，让玩家看到新绑定的按键名
        yield return new WaitForSecondsRealtime(confirmDelay);

        // 重新启用 Action
        action.Enable();
        isRebinding = false;

        // 自动保存
        InputManager.Instance.SaveBindings();

        // 检测冲突（同一按键绑定了多个 Action）
        CheckConflicts();
    }

    /// <summary>
    /// 冲突检测：遍历所有 Action 的所有 Binding，
    /// 找到相同 effectivePath 的不同 Action → Toast 警告
    /// </summary>
    void CheckConflicts() {
        var im = InputManager.Instance;
        if (im == null) return;

        var asset = im.InputActions.asset;
        var pathToActions = new System.Collections.Generic.Dictionary<string,
            System.Collections.Generic.List<string>>();

        foreach (var map in asset.actionMaps) {
            foreach (var action in map.actions) {
                foreach (var binding in action.bindings) {
                    if (binding.isComposite) continue;
                    string path = binding.effectivePath;
                    if (string.IsNullOrEmpty(path)) continue;

                    if (!pathToActions.ContainsKey(path))
                        pathToActions[path] = new();

                    string label = $"{map.name}/{action.name}";
                    if (!pathToActions[path].Contains(label))
                        pathToActions[path].Add(label);
                }
            }
        }

        foreach (var kv in pathToActions) {
            if (kv.Value.Count > 2) // 超过 2 个绑定才警告
            {
                Toast.Show($"按键冲突: {string.Join(", ", kv.Value)}", Toast.Level.Warning, 3f);
                break;
            }
        }
    }

    // ═══════════════════════════════════════════
    //  Rebind Popup 绘制
    // ═══════════════════════════════════════════
    void DrawRebindPopup() {
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _overlayTex);

        float w = 320, h = 160;
        Rect popup = CenterRect(w, h);
        GUI.DrawTexture(popup, _popupTex);

        float px = popup.x + 20, py = popup.y + 20, iw = w - 40;

        // 标题
        GUI.Label(new Rect(px, py, iw, 30), rebindActionName, _popupTitle);
        py += 40;

        // 提示 / 结果
        if (string.IsNullOrEmpty(rebindResult)) {
            GUI.Label(new Rect(px, py, iw, 30), "请按下新按键...", _popupHint);
            py += 40;

            // 取消按钮
            if (GUI.Button(new Rect(px + iw * 0.3f, py, iw * 0.4f, 30), "取消", _btn)) {
                currentRebindOp?.Cancel();
            }
        } else {
            var resultStyle = new GUIStyle(_popupHint) {
                fontStyle = FontStyle.Bold,
                normal = { textColor = rebindColor }
            };
            GUI.Label(new Rect(px, py, iw, 30), rebindResult, resultStyle);
        }
    }

    // ═══════════════════════════════════════════
    //  工具
    // ═══════════════════════════════════════════
    void CloseAll() {
        state = PanelState.Closed;

        // 如果正在 Rebind，取消
        currentRebindOp?.Cancel();
        isRebinding = false;

        if (confirmCoroutine != null)
            StopCoroutine(confirmCoroutine);

        var gs = GameStateManager.Instance;
        if (gs != null && gs.CurrentState == GameStateType.Paused)
            gs.SetState(GameStateType.Playing);
    }

    Rect CenterRect(float w, float h)
        => new((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

    static Texture2D Tex(Color c) {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}