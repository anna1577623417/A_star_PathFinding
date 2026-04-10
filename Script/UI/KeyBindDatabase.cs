using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 键位数据库（ScriptableObject）
/// 
/// 【职责】
///   持有所有 KeyBindConfig 的列表，提供按 actionName 的快速查找。
///   UI 遍历这个列表自动生成 Shortcut Item，不需要硬编码。
///
/// 【使用方式】
///   Assets → Create → AStarProject → Input → KeyBindDatabase
///   只需创建一个，把所有 KeyBindConfig SO 拖入 configs 列表
///   然后把这个 Database 拖入 InputManager 的 Inspector 字段
///
/// 挂载：不挂载，是资产文件。被 InputManager 引用。
/// </summary>
[CreateAssetMenu(fileName = "KeyBindDatabase", menuName = "AStarProject/Input/KeyBindDatabase")]
public class KeyBindDatabase : ScriptableObject {
    [Header("═══ 所有键位配置 ═══")]
    [Tooltip("把所有 KeyBindConfig SO 拖进来")]
    public List<KeyBindConfig> configs = new List<KeyBindConfig>();

    // 运行时查找表
    private Dictionary<string, KeyBindConfig> lookup;

    /// <summary>初始化查找表（运行时调用一次）</summary>
    public void Init() {
        lookup = new Dictionary<string, KeyBindConfig>();
        foreach (var c in configs) {
            if (c != null && !string.IsNullOrEmpty(c.actionName))
                lookup[c.actionName] = c;
        }
    }

    /// <summary>按 Action 名查找配置</summary>
    public KeyBindConfig Get(string actionName) {
        if (lookup == null) Init();
        return lookup.TryGetValue(actionName, out var c) ? c : null;
    }

    /// <summary>获取按分类分组、排序后的列表（UI 用）</summary>
    public List<KeyBindConfig> GetSorted() {
        return configs
            .Where(c => c != null)
            .OrderBy(c => c.category)
            .ThenBy(c => c.sortOrder)
            .ToList();
    }

    /// <summary>获取所有分类名（UI 分组标题用）</summary>
    public List<string> GetCategories() {
        return configs
            .Where(c => c != null)
            .Select(c => c.category)
            .Distinct()
            .ToList();
    }
}