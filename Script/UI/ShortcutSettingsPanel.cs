using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 快捷键设置面板 —— UGUI 版
/// 
/// 【职责】
///   1. 管理面板的显示/隐藏（CanvasGroup）
///   2. 从 KeyBindDatabase 读取配置，动态实例化 ShortcutItemView 列表
///   3. 订阅 EventBus 的 Rebind 事件，转发给对应的 Item
///   4. 提供"全部重置"和"保存"按钮逻辑
///
/// 【不做的事】
///   × 不直接调 InputManager 的 Rebind 方法
///   × 不检测输入（由 InputManager 和 EventBus 驱动）
///   × 不用 OnGUI
///
/// 【Hierarchy 结构（你已有的预制体）】
///   ShortcutSettingsPanel (本脚本)
///     ├── CanvasGroup（控制显隐）
///     ├── Header（标题文本）
///     ├── ScrollView
///     │     └── Content (VerticalLayoutGroup)
///     │           └── [动态生成的 ShortcutItemView × N]
///     ├── ButtonResetAll
///     ├── ButtonSave
///     └── ButtonClose
///
/// 挂载：面板根物体（带 CanvasGroup）
/// </summary>
public class ShortcutSettingsPanel : MonoSingleton<ShortcutSettingsPanel> {
    [Header("═══ 引用（拖入）═══")]
    [Tooltip("面板自身的 CanvasGroup（控制透明度和交互）")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("ScrollView 的 Content 容器（带 VerticalLayoutGroup）")]
    [SerializeField] private Transform contentParent;

    [Tooltip("列表项预制体")]
    [SerializeField] private GameObject itemPrefab;

    [Header("═══ 动画 ═══")]
    [Tooltip("显隐渐变时长（秒），0 = 立即")]
    [Range(0f, 0.5f)]
    [SerializeField] private float fadeDuration = 0.15f;

    // ---- 内部 ----
    private List<ShortcutItemView> itemViews = new List<ShortcutItemView>();
    private bool isOpen;
    private Coroutine fadeCoroutine;

    // ═══════════════════════════════════════════
    //  初始化
    // ═══════════════════════════════════════════

    void Awake() {
        // 启动时隐藏面板（不走动画）
        SetCanvasGroupImmediate(false);
        isOpen = false;
    }

    void OnEnable() {
        // 订阅 Rebind 事件 → 转发给对应 Item
        EventBus.Subscribe<RebindStartEvent>(OnRebindStart);
        EventBus.Subscribe<RebindCompleteEvent>(OnRebindComplete);
        EventBus.Subscribe<RebindCancelEvent>(OnRebindCancel);
    }

    void OnDisable() {
        EventBus.Unsubscribe<RebindStartEvent>(OnRebindStart);
        EventBus.Unsubscribe<RebindCompleteEvent>(OnRebindComplete);
        EventBus.Unsubscribe<RebindCancelEvent>(OnRebindCancel);
    }

    // ═══════════════════════════════════════════
    //  显示 / 隐藏（CanvasGroup 方案）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 打开面板
    /// 
    /// 【CanvasGroup 三属性】
    ///   alpha = 1          → 可见
    ///   interactable = true → 按钮可点击
    ///   blocksRaycasts = true → 阻挡底下的点击（防穿透）
    /// </summary>
    public void Show() {
        if (isOpen) return;
        isOpen = true;

        // 首次打开时生成列表（之后只刷新）
        if (itemViews.Count == 0)
            BuildList();
        else
            RefreshAllItems();

        // 渐显
        FadeTo(true);

        // 暂停游戏
        GameStateManager.Instance?.SetState(GameStateType.Paused);
    }

    /// <summary>
    /// 关闭面板
    /// 
    /// 由 Close 按钮调用（在 Inspector 的 Button.OnClick 中拖入此方法）
    /// 也可以被 Esc 键通过 EventBus 触发
    /// </summary>
    public void Hide() {
        if (!isOpen) return;
        isOpen = false;

        // 取消正在进行的 Rebind
        CancelAllRebindStates();

        // 渐隐
        FadeTo(false);

        // 恢复游戏
        var gs = GameStateManager.Instance;
        if (gs != null && gs.CurrentState == GameStateType.Paused)
            gs.SetState(GameStateType.Playing);
    }

    /// <summary>切换显隐（Menu 按钮用）</summary>
    public void Toggle() {
        if (isOpen) Hide();
        else Show();
    }

    // ═══════════════════════════════════════════
    //  列表生成（数据驱动）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 从 KeyBindDatabase 读取配置，实例化列表项
    /// 
    /// 【流程】
    ///   1. 清空旧的列表项
    ///   2. 从 InputManager.Database 获取排序后的配置列表
    ///   3. 遍历每个 KeyBindConfig
    ///   4. Instantiate 预制体 → GetComponent<ShortcutItemView>
    ///   5. 调用 item.Init(config) → 文本更新 + 按钮绑定
    ///   6. 存入 itemViews 列表
    /// </summary>
    void BuildList() {
        // 清空旧的
        foreach (var item in itemViews) {
            if (item != null)
                Destroy(item.gameObject);
        }
        itemViews.Clear();

        var im = InputManager.Instance;
        if (im == null || im.Database == null) {
            Debug.LogWarning("[ShortcutSettingsPanel] InputManager 或 Database 未就绪");
            return;
        }

        var configs = im.Database.GetSorted();

        string lastCategory = "";

        foreach (var config in configs) {
            // TODO: 如果要做分类折叠标题，可以在这里
            // 检测 category 变化时插入一个 CategoryHeader 预制体
            // 目前先跳过，所有 Item 平铺

            // 实例化列表项
            GameObject go = Instantiate(itemPrefab, contentParent);
            go.name = $"Item_{config.actionName}";

            var itemView = go.GetComponent<ShortcutItemView>();
            if (itemView == null) {
                Debug.LogError($"[ShortcutSettingsPanel] 预制体缺少 ShortcutItemView 组件: {config.actionName}");
                Destroy(go);
                continue;
            }

            // 初始化：传入配置 + 当前绑定显示
            string bindingDisplay = im.GetBindingDisplay(config.mapName, config.actionName);
            itemView.Init(config, bindingDisplay);

            itemViews.Add(itemView);
        }
    }

    /// <summary>刷新所有 Item 的按键显示（重置绑定后调用）</summary>
    void RefreshAllItems() {
        var im = InputManager.Instance;
        if (im == null) return;

        foreach (var item in itemViews) {
            if (item == null || item.Config == null) continue;
            string display = im.GetBindingDisplay(item.Config.mapName, item.Config.actionName);
            item.RefreshBindingDisplay(display);
        }
    }

    // ═══════════════════════════════════════════
    //  EventBus 回调 → 转发给对应 Item
    // ═══════════════════════════════════════════

    void OnRebindStart(RebindStartEvent e) {
        // 先把所有 Item 退出 Rebind 状态（安全机制）
        foreach (var item in itemViews)
            item.SetState(ShortcutItemView.ItemState.Normal);

        // 找到目标 Item，设为 Rebinding
        var target = FindItem(e.actionName);
        if (target != null)
            target.SetState(ShortcutItemView.ItemState.Rebinding);
    }

    void OnRebindComplete(RebindCompleteEvent e) {
        var target = FindItem(e.actionName);
        if (target != null)
            target.OnRebindComplete(e.newBindingDisplay);
    }

    void OnRebindCancel(RebindCancelEvent e) {
        var target = FindItem(e.actionName);
        if (target != null)
            target.SetState(ShortcutItemView.ItemState.Normal);
    }

    ShortcutItemView FindItem(string actionName) {
        foreach (var item in itemViews) {
            if (item.Config != null && item.Config.actionName == actionName)
                return item;
        }
        return null;
    }

    void CancelAllRebindStates() {
        foreach (var item in itemViews)
            item.SetState(ShortcutItemView.ItemState.Normal);
    }

    // ═══════════════════════════════════════════
    //  按钮回调（Inspector OnClick 拖入）
    // ═══════════════════════════════════════════

    /// <summary>全部重置按钮</summary>
    public void OnClickResetAll() {
        InputManager.Instance?.ResetAllBindings();
        RefreshAllItems();
        Toast.Show("所有绑定已重置", Toast.Level.Info, 1.5f);
    }

    /// <summary>保存按钮</summary>
    public void OnClickSave() {
        InputManager.Instance?.SaveBindings();
        Toast.Show("绑定已保存", Toast.Level.Success, 1.5f);
    }

    /// <summary>关闭按钮</summary>
    public void OnClickClose() {
        Hide();
    }

    // ═══════════════════════════════════════════
    //  CanvasGroup 渐变动画
    // ═══════════════════════════════════════════

    void FadeTo(bool visible) {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        if (fadeDuration <= 0) {
            SetCanvasGroupImmediate(visible);
            return;
        }

        fadeCoroutine = StartCoroutine(FadeCoroutine(visible));
    }

    IEnumerator FadeCoroutine(bool visible) {
        float start = canvasGroup.alpha;
        float end = visible ? 1f : 0f;
        float elapsed = 0f;

        // 显示时先开启交互（否则渐显期间无法点击）
        if (visible) {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        while (elapsed < fadeDuration) {
            // 用 unscaledDeltaTime → 暂停时动画仍然播放
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, end, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = end;

        // 隐藏时关闭交互
        if (!visible) {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    void SetCanvasGroupImmediate(bool visible) {
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}