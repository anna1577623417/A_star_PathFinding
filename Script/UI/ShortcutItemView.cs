using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 快捷键列表项 —— UGUI 版
/// 
/// 【职责】
///   1. 显示功能名 + 当前按键
///   2. 悬停时高亮背景
///   3. 点击按键按钮时发布 RebindRequestEvent（不直接调 InputManager）
///   4. 响应 Panel 转发的状态变更（Normal / Rebinding / Confirm）
///
/// 【预制体结构（你已创建的）】
///   ShortcutItem (本脚本 + CanvasGroup)
///     ├── Background (Image, 默认背景色)
///     ├── Highlight (Image, 默认隐藏, 悬停时显示)
///     ├── ActionNameText (TMP_Text 或 Text)
///     ├── KeyButton (Button)
///     │     └── KeyText (TMP_Text 或 Text)
///
/// 【三种状态】
///   Normal    → KeyText 显示当前绑定（如 "Tab"），按钮可点击
///   Rebinding → KeyText 显示 "请按下新按键..."，按钮不可点击，文字闪烁
///   Confirm   → KeyText 显示新按键名（绿色），1秒后自动回到 Normal
///
/// 挂载：列表项预制体根物体
/// </summary>
public class ShortcutItemView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler{
    // ═══════════════════════════════════════════
    //  状态枚举
    // ═══════════════════════════════════════════

    public enum ItemState {
        Normal,
        Rebinding,
        Confirm
    }

    // ═══════════════════════════════════════════
    //  Inspector 引用
    // ═══════════════════════════════════════════

    [Header("═══ UI 引用（拖入预制体内的子物体）═══")]
    [Tooltip("悬停高亮背景（默认隐藏）")]
    [SerializeField] private GameObject highlight;

    [Tooltip("功能名文本")]
    [SerializeField] private TMP_Text actionNameText;

    [Tooltip("按键显示文本（在 KeyButton 内部）")]
    [SerializeField] private TMP_Text keyText;

    [Tooltip("可交互区域")]
    [SerializeField] private Image keyArea;
    [Tooltip("是否可交互")]
    [SerializeField] private bool interactable = true;

    //[Tooltip("按键按钮")]
    //[SerializeField] private Button keyButton;

    [Header("═══ 配色 ═══")]
    [SerializeField] private Color normalKeyColor = new(0.7f, 0.9f, 1f);
    [SerializeField] private Color rebindingKeyColor = new(1f, 0.7f, 0.2f);
    [SerializeField] private Color confirmKeyColor = new(0.4f, 1f, 0.6f);
    [SerializeField] private Color disabledColor = new(0.5f, 0.5f, 0.5f);

    [Header("═══ 时间 ═══")]
    [Tooltip("确认状态显示时长（秒）")]
    [Range(0.5f, 3f)]
    [SerializeField] private float confirmDisplayTime = 1.0f;

    // ═══════════════════════════════════════════
    //  内部数据
    // ═══════════════════════════════════════════

    private KeyBindConfig config;
    private string currentBindingDisplay;
    private ItemState currentState = ItemState.Normal;
    private Coroutine confirmCoroutine;

    /// <summary>外部读取配置（Panel 用来查找对应 Item）</summary>
    public KeyBindConfig Config => config;

    // ═══════════════════════════════════════════
    //  初始化（由 ShortcutSettingsPanel.BuildList 调用）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 初始化列表项
    /// 
    /// 【参数】
    ///   config: 从 KeyBindDatabase 读取的 SO 元数据
    ///   bindingDisplay: 从 InputManager.GetBindingDisplay() 读取的当前按键名
    /// </summary>
    public void Init(KeyBindConfig config, string bindingDisplay) {
        this.config = config;
        this.currentBindingDisplay = bindingDisplay;

        // 设置功能名
        if (actionNameText != null)
            actionNameText.text = config.displayName;

        // 设置按键显示
        if (keyText != null) {
            keyText.text = bindingDisplay;
            keyText.color = normalKeyColor;
        }

        // 隐藏高亮
        if (highlight != null)
            highlight.SetActive(false);

        // 绑定按钮点击事件
        if (keyArea != null) {
            //keyButton.onClick.RemoveAllListeners();
            interactable = false;

            if (config.rebindable) {
                interactable = true;
            } else {
                // 不可重绑 → 禁用按钮，文字灰色
                //keyButton.interactable = false;
                interactable = false;
                if (keyText != null)
                    keyText.color = disabledColor;
            }
        }

        SetState(ItemState.Normal);
    }

    // ═══════════════════════════════════════════
    //  刷新按键显示（重置绑定后调用）
    // ═══════════════════════════════════════════

    public void RefreshBindingDisplay(string newDisplay) {
        currentBindingDisplay = newDisplay;

        if (currentState == ItemState.Normal && keyText != null) {
            keyText.text = newDisplay;
            keyText.color = normalKeyColor;
        }
    }

    // ═══════════════════════════════════════════
    //  状态切换
    // ═══════════════════════════════════════════

    /// <summary>
    /// 设置 Item 的视觉状态
    /// 由 ShortcutSettingsPanel 根据 EventBus 事件调用
    /// </summary>
    public void SetState(ItemState newState) {
        currentState = newState;

        if (confirmCoroutine != null) {
            StopCoroutine(confirmCoroutine);
            confirmCoroutine = null;
        }

        switch (newState) {
            case ItemState.Normal:
                if (keyText != null) {
                    keyText.text = currentBindingDisplay;
                    keyText.color = normalKeyColor;
                }
                if (keyArea != null && config != null)
                    interactable = config.rebindable;
                break;

            case ItemState.Rebinding:
                if (keyText != null) {
                    keyText.text = "请按下新按键...";
                    keyText.color = rebindingKeyColor;
                }
                if (keyArea != null)
                    interactable = false; // 等待中不可再点
                break;

            case ItemState.Confirm:
                // Confirm 状态由 OnRebindComplete 设置，带新显示文本
                if (keyArea != null && config != null)
                    interactable = config.rebindable;
                break;
        }
    }

    /// <summary>
    /// Rebind 完成 → 显示新按键名（绿色）→ 延迟后回到 Normal
    /// 由 Panel 的 OnRebindComplete 调用
    /// </summary>
    public void OnRebindComplete(string newBindingDisplay) {
        currentBindingDisplay = newBindingDisplay;
        currentState = ItemState.Confirm;

        if (keyText != null) {
            keyText.text = newBindingDisplay;
            keyText.color = confirmKeyColor;
        }

        if (keyArea != null && config != null)
            interactable = config.rebindable;

        // 延迟后恢复 Normal（WaitForSecondsRealtime → 暂停时仍计时）
        confirmCoroutine = StartCoroutine(ConfirmToNormal());
    }

    IEnumerator ConfirmToNormal() {
        yield return new WaitForSecondsRealtime(confirmDisplayTime);
        SetState(ItemState.Normal);
    }

    // ═══════════════════════════════════════════
    //  悬停高亮（UGUI EventSystem 接口）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 鼠标进入 → 显示高亮背景
    /// 
    /// 【实现方式】
    ///   实现 IPointerEnterHandler 接口，
    ///   Unity EventSystem 会在鼠标进入 RectTransform 区域时自动调用。
    ///   不需要 Raycast 或 Update 轮询。
    ///   前提：物体上有 Graphic 组件（Image/Text）+ Raycast Target = true
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData) {
        if (highlight != null)
            highlight.SetActive(true);
    }

    /// <summary>鼠标离开 → 隐藏高亮</summary>
    public void OnPointerExit(PointerEventData eventData) {
        if (highlight != null)
            highlight.SetActive(false);
    }

    // ═══════════════════════════════════════════
    //  按钮点击（发布 EventBus 事件，不直接调 InputManager）
    // ═══════════════════════════════════════════


    //void OnClickKeyButton() {
    //    if (config == null || !config.rebindable) return;
    //    if (currentState == ItemState.Rebinding) return; // 防止重复点击

    //    EventBus.Publish(new RebindRequestEvent {
    //        actionName = config.actionName,
    //        mapName = config.mapName
    //    });
    //}

    /// <summary>
    /// 点击按键按钮 → 发布 RebindRequestEvent
    /// 
    /// 【安全机制】
    ///   如果此时另一个 Item 正在 Rebinding：
    ///   → InputManager 收到新的 RebindRequest
    ///   → 自动 Cancel 旧的（CancelCurrentRebind）
    ///   → 旧 Item 收到 RebindCancelEvent → 恢复 Normal
    ///   → 本 Item 收到 RebindStartEvent → 进入 Rebinding
    ///   整个安全逻辑在 InputManager 内完成，UI 不需要额外处理
    /// </summary>
    public void OnPointerClick(PointerEventData eventData) {
        // 1. 模拟 button.interactable = false 的阻断逻辑
        if (!interactable) return;

        // 2. 双重保险：检查配置与当前状态
        if (config == null || !config.rebindable) return;
        if (currentState == ItemState.Rebinding) return;// 防止重复点击

        // 3. 发布事件
        EventBus.Publish(new RebindRequestEvent {
            actionName = config.actionName,
            mapName = config.mapName
        });
    }
}