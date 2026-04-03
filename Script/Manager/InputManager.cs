using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 统一输入管理器 —— 官方 Input System 资产驱动版
/// 
/// 【架构三层】
///   InputActionAsset (.inputactions)  = 配置层（数据，Unity 编辑器里改绑定）
///   InputManager (本文件)             = 转发层（事件，把 Action 回调转为 C# event）
///   Player / Camera / GridInput       = 逻辑层（只订阅 event，不碰输入 API）
///
/// 【Action Map 分层】
///   Gameplay — 游戏内操作（Move, Click, 功能键），Playing 时启用
///   Camera   — 相机操作（Pan, Zoom, MiddleMouse），Playing 时启用
///   Global   — 全局操作（Pause, Restart），始终启用
///
/// 【状态驱动切换】
///   Playing  → Gameplay + Camera 启用
///   Paused   → Gameplay + Camera 禁用，Global 仍启用
///   GameOver → 只有 Global 启用（R 重启）
///
/// 【.inputactions 资产设置】
///   1. 把 GameInputActions.inputactions 放入 Assets/
///   2. 选中它 → Inspector → 勾选 "Generate C# Class" → Apply
///   3. Unity 自动生成 GameInputActions.cs（和 .inputactions 同目录）
///   4. 把 .inputactions 文件拖入本脚本的 Inspector 的 inputAsset 字段
///      或者不拖——代码会自动 new GameInputActions() 生成实例
///
/// 挂载：根节点
/// </summary>
public class InputManager : MonoSingleton<InputManager> {

    // ═══════════════════════════════════════════
    //  对外事件（签名不变，所有订阅者零改动）
    // ═══════════════════════════════════════════

    // -- Gameplay --
    public event Action<Vector2Int> OnMove;
    public event Action OnLeftClick;
    public event Action OnRightClick;
    public event Action OnToggleCamera;
    public event Action OnSwitchAlgorithm;
    public event Action OnSwitchPolicy;
    public event Action OnToggleHUD;

    // -- Camera --
    public event Action<bool> OnMiddleMouse;
    public event Action<Vector2> OnMouseDelta;
    public event Action<float> OnScroll;

    // -- Global --
    public event Action OnTogglePause;
    public event Action OnRestart;

    // ═══════════════════════════════════════════
    //  Inspector
    // ═══════════════════════════════════════════

    [Header("═══ WASD 重复移动 ═══")]
    [Range(0.05f, 0.3f)]
    [SerializeField] private float moveInterval = 0.15f;

    // ═══════════════════════════════════════════
    //  内部：官方 Input System 资产
    // ═══════════════════════════════════════════

    /// <summary>
    /// GameInputActions 是 .inputactions 文件自动生成的 C# 类。
    /// 勾选 "Generate C# Class" 后 Unity 会创建它。
    /// 它包含所有 Action Map 和 Action 的强类型属性。
    /// </summary>
    private GameInputActions input;

    // 中键拖拽状态
    private bool isDragging;
    private Coroutine moveRepeatCoroutine;

    /// <summary>暴露给 SystemSettingsUI 做 Rebind 和 保存/加载</summary>
    public GameInputActions InputActions => input;

    // ═══════════════════════════════════════════
    //  生命周期
    // ═══════════════════════════════════════════

     public void Init() {
        input = new GameInputActions();

        // 默认启用所有 Map（GameInitializer 会在适当时机调 SetGameplayActive）
        input.Gameplay.Enable();
        input.Camera.Enable();
        input.Global.Enable();

        BindCallbacks();
        LoadBindings();
    } 

    void OnEnable() {

    }

    void OnDisable() {
        if (moveRepeatCoroutine != null)
            StopCoroutine(moveRepeatCoroutine);

        input.Disable();
    }

    void OnDestroy() {
        input?.Dispose();
    }

    // 注意：没有 Update()

    // ═══════════════════════════════════════════
    //  绑定回调（一次性，Init 时调用）
    // ═══════════════════════════════════════════

    void BindCallbacks() {
        // ---- Gameplay Map ----
        input.Gameplay.Move.started += OnMoveStarted;
        input.Gameplay.Move.canceled += OnMoveCanceled;

        input.Gameplay.LeftClick.performed += _ => OnLeftClick?.Invoke();
        input.Gameplay.RightClick.performed += _ => OnRightClick?.Invoke();

        input.Gameplay.ToggleCamera.performed += _ => OnToggleCamera?.Invoke();
        input.Gameplay.SwitchAlgorithm.performed += _ => OnSwitchAlgorithm?.Invoke();
        input.Gameplay.SwitchPolicy.performed += _ => OnSwitchPolicy?.Invoke();
        input.Gameplay.ToggleHUD.performed += _ => OnToggleHUD?.Invoke();

        // ---- Camera Map ----
        input.Camera.MiddleMouse.started += _ => {
            isDragging = true;
            OnMiddleMouse?.Invoke(true);
        };
        input.Camera.MiddleMouse.canceled += _ => {
            isDragging = false;
            OnMiddleMouse?.Invoke(false);
        };

        input.Camera.Pan.performed += ctx => {
            if (isDragging)
                OnMouseDelta?.Invoke(ctx.ReadValue<Vector2>());
        };

        input.Camera.Zoom.performed += ctx => {
            float raw = ctx.ReadValue<float>();
            OnScroll?.Invoke(raw / 120f);
        };

        // ---- Global Map（始终响应）----
        input.Global.Pause.performed += _ => OnTogglePause?.Invoke();
        input.Global.Restart.performed += _ => OnRestart?.Invoke();

        // ---- GameState 变化 → 切换 Action Map ----
        EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
    }

    // ═══════════════════════════════════════════
    //  Action Map 切换（状态驱动）
    // ═══════════════════════════════════════════

    /// <summary>
    /// 根据游戏状态启用/禁用 Action Map
    /// 
    /// Playing  → Gameplay + Camera 启用（玩家可操作）
    /// Paused   → Gameplay + Camera 禁用（只有 Esc 恢复和 R 重启）
    /// GameOver → Gameplay + Camera 禁用（只有 R 重启）
    /// Global 始终启用（Pause/Restart 永远可用）
    /// </summary>
    void OnGameStateChanged(GameStateChangedEvent e) {
        bool playing = (e.newState == GameStateType.Playing);
        SetGameplayActive(playing);
    }

    /// <summary>启用/禁用游戏操作（不影响 Global）</summary>
    public void SetGameplayActive(bool active) {
        if (active) {
            input.Gameplay.Enable();
            input.Camera.Enable();
        } else {
            input.Gameplay.Disable();
            input.Camera.Disable();

            // 停止移动协程
            if (moveRepeatCoroutine != null) {
                StopCoroutine(moveRepeatCoroutine);
                moveRepeatCoroutine = null;
            }
        }
    }

    // ═══════════════════════════════════════════
    //  WASD 重复移动（协程，非 Update）
    // ═══════════════════════════════════════════

    void OnMoveStarted(InputAction.CallbackContext ctx) {
        if (moveRepeatCoroutine != null)
            StopCoroutine(moveRepeatCoroutine);
        moveRepeatCoroutine = StartCoroutine(MoveRepeatLoop());
    }

    void OnMoveCanceled(InputAction.CallbackContext ctx) {
        if (moveRepeatCoroutine != null) {
            StopCoroutine(moveRepeatCoroutine);
            moveRepeatCoroutine = null;
        }
    }

    IEnumerator MoveRepeatLoop() {
        while (true) {
            Vector2 raw = input.Gameplay.Move.ReadValue<Vector2>();
            if (raw.sqrMagnitude > 0.1f) {
                Vector2Int dir = new(
                    Mathf.RoundToInt(raw.x),
                    Mathf.RoundToInt(raw.y)
                );
                OnMove?.Invoke(dir);
            }
            yield return new WaitForSeconds(moveInterval);
        }
    }

    // ═══════════════════════════════════════════
    //  公共工具
    // ═══════════════════════════════════════════

    /// <summary>鼠标在 Y=0 平面上的世界坐标</summary>
    public bool GetMouseWorldPosition(out int gridX, out int gridZ) {
        gridX = gridZ = 0;
        if (Camera.main == null) return false;

        Vector2 screenPos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : Vector2.zero;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        Plane ground = new Plane(Vector3.up, Vector3.zero);

        if (ground.Raycast(ray, out float dist)) {
            Vector3 wp = ray.GetPoint(dist);
            gridX = Mathf.RoundToInt(wp.x);
            gridZ = Mathf.RoundToInt(wp.z);
            return true;
        }
        return false;
    }

    // ═══════════════════════════════════════════
    //  Rebind 持久化（供 SystemSettingsUI 调用）
    // ═══════════════════════════════════════════

    private const string REBIND_SAVE_KEY = "InputRebinds";

    /// <summary>保存当前绑定覆盖到 PlayerPrefs</summary>
    public void SaveBindings() {
        string json = input.asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(REBIND_SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    /// <summary>从 PlayerPrefs 加载绑定覆盖</summary>
    public void LoadBindings() {
        if (PlayerPrefs.HasKey(REBIND_SAVE_KEY)) {
            string json = PlayerPrefs.GetString(REBIND_SAVE_KEY);
            input.asset.LoadBindingOverridesFromJson(json);
        }
    }

    /// <summary>重置所有绑定为默认值</summary>
    public void ResetAllBindings() {
        input.asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(REBIND_SAVE_KEY);
    }
}