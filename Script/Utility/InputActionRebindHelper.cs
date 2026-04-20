using UnityEngine.InputSystem;

/// <summary>
/// 为 InputAction 选择「主绑定」索引：优先 Composite（如 2DVector / WASD），否则取第一个非 Composite 部件的绑定。
/// 用于 GetBindingDisplayString 与 PerformInteractiveRebinding 的根索引一致。
/// </summary>
public static class InputActionRebindHelper {
    const InputBinding.DisplayStringOptions DisplayOpts =
        InputBinding.DisplayStringOptions.DontIncludeInteractions;

    /// <summary>用于显示与交互式重绑的起始 binding 索引（Composite 为根行，单键为第一个有效行）。</summary>
    public static int GetRebindRootBindingIndex(InputAction action) {
        if (action == null || action.bindings.Count == 0) return -1;

        for (int i = 0; i < action.bindings.Count; i++) {
            if (action.bindings[i].isComposite)
                return i;
        }

        for (int i = 0; i < action.bindings.Count; i++) {
            if (!action.bindings[i].isPartOfComposite)
                return i;
        }

        return 0;
    }

    public static string GetPrimaryBindingDisplayString(InputAction action) {
        int i = GetRebindRootBindingIndex(action);
        if (i < 0) return "???";
        return action.GetBindingDisplayString(i, DisplayOpts);
    }
}
