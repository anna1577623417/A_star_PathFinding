using UnityEngine;

/// <summary>
/// 键位配置元数据（ScriptableObject）
/// 
/// 【职责分工】
///   InputActionAsset (.inputactions) → 管"按哪个键触发什么"（Unity 官方管，运行时真值）
///   KeyBindConfig（KeyDefinition 角色）→ 管"列表项显示名、分类、是否可改、对应哪条 Action"（仅 UI 元数据）
///
/// 【为什么需要这个 SO，InputActionAsset 不够吗？】
///   InputActionAsset 没有以下信息：
///     - 显示名称（中文/本地化）
///     - 功能描述
///     - 分类（Gameplay / Camera / Global）
///     - 是否允许重绑定
///   这些是"业务层元数据"，不属于输入绑定，应该独立管理。
///
/// 【使用方式】
///   Assets → Create → AStarProject → Input → KeyBindConfig
///   每个 Action 创建一个 SO，填写元数据
///   全部拖入 KeyBindDatabase 的列表
///
/// 【扩展新 Action 的流程】
///   1. 在 .inputactions 编辑器中添加新 Action + Binding
///   2. 创建一个新的 KeyBindConfig SO
///   3. 拖入 KeyBindDatabase
///   → UI 自动出现新行，零代码改动
/// </summary>
[CreateAssetMenu(fileName = "NewKeyBind", menuName = "AStarProject/Input/KeyBindConfig")]
public class KeyBindConfig : ScriptableObject {
    [Header("═══ 映射 ═══")]
    [Tooltip("必须和 .inputactions 中的 Action Name 完全一致")]
    public string actionName;

    [Tooltip("Action 所在的 Map 名（Gameplay / Camera / Global）")]
    public string mapName = "Gameplay";

    [Header("═══ 显示 ═══")]
    [Tooltip("UI 中显示的名称（支持中文，可接本地化）")]
    public string displayName;

    [Tooltip("功能描述（Popup 中显示）")]
    [TextArea(1, 3)]
    public string description;

    [Header("═══ 行为 ═══")]
    [Tooltip("是否允许玩家重绑定此按键")]
    public bool rebindable = true;

    [Tooltip("分类标签（UI 中按此分组显示）")]
    public string category = "Gameplay";

    [Tooltip("在 UI 列表中的排序权重（越小越靠前）")]
    [Range(0, 100)]
    public int sortOrder = 50;
}