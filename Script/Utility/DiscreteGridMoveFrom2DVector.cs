using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 将 <see cref="InputAction"/> 上 <c>2DVector</c> 合成（up/down/left/right 四向）的按下状态，
/// 转为离散网格移动向量：单键为轴移；WA/WD/SA/SD 为斜移；W+S、A+D 对向抵消为不移动；三键及以上不移动。
/// </summary>
public static class DiscreteGridMoveFrom2DVector {
    const float AxisPressedThreshold = 0.5f;

    public static bool Is2DVectorCompositeMove(InputAction action) {
        if (action == null) return false;
        for (int i = 0; i < action.bindings.Count; i++) {
            var b = action.bindings[i];
            if (b.isComposite && b.path == "2DVector")
                return true;
        }
        return false;
    }

    /// <summary>由 Move 的 2DVector 部件读取四向是否按下（改键后仍按 binding 的 part name 识别）。</summary>
    public static void ReadFourWayPressed(InputAction move, out bool up, out bool down, out bool left, out bool right) {
        up = down = left = right = false;
        if (move == null) return;

        foreach (var control in move.controls) {
            int bi = move.GetBindingIndexForControl(control);
            if (bi < 0) continue;
            var binding = move.bindings[bi];
            if (!binding.isPartOfComposite) continue;

            if (!IsControlPressed(control)) continue;

            switch (binding.name) {
                case "up": up = true; break;
                case "down": down = true; break;
                case "left": left = true; break;
                case "right": right = true; break;
            }
        }
    }

    static bool IsControlPressed(InputControl control) {
        switch (control) {
            case ButtonControl b:
                return b.isPressed;
            case AxisControl a:
                return Mathf.Abs(a.ReadValue()) > AxisPressedThreshold;
            default:
                return control.EvaluateMagnitude() > AxisPressedThreshold;
        }
    }

    /// <summary>单键轴移、合法双键斜移、对向双键与 3+ 键不移动。</summary>
    public static Vector2 ComputeDiscreteVector(bool up, bool down, bool left, bool right) {
        int count = (up ? 1 : 0) + (down ? 1 : 0) + (left ? 1 : 0) + (right ? 1 : 0);
        if (count == 0 || count >= 3)
            return Vector2.zero;

        if (count == 1) {
            if (up) return new Vector2(0f, 1f);
            if (down) return new Vector2(0f, -1f);
            if (left) return new Vector2(-1f, 0f);
            return new Vector2(1f, 0f);
        }

        if (up && down) return Vector2.zero;
        if (left && right) return Vector2.zero;

        float x = (left ? -1f : 0f) + (right ? 1f : 0f);
        float y = (up ? 1f : 0f) + (down ? -1f : 0f);
        return new Vector2(x, y);
    }

    public static Vector2 ResolveMoveVector(InputAction move) {
        if (move == null) return Vector2.zero;
        if (!Is2DVectorCompositeMove(move))
            return move.ReadValue<Vector2>();

        ReadFourWayPressed(move, out bool u, out bool d, out bool l, out bool r);
        return ComputeDiscreteVector(u, d, l, r);
    }
}
