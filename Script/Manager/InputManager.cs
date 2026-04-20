using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 统一输入管理器 —— 瘦身版 + SO 数据驱动
///
/// 【架构三层】
///   KeyBindDatabase (SO)         → 元数据层（显示名、分类、是否可绑）
///   GameInputActions (.inputactions) → 绑定层（哪个键触发哪个 Action）
///   InputManager (本文件)        → 执行层（回调 → 事件转发 + Rebind）
///
/// 【瘦身前 vs 瘦身后】
///   前：每个 Action 一个 event 字段（OnLeftClick, OnRightClick...）
///       加新 Action = 加字段 + 加绑定 + 加回调 = 改 3 处
///   后：统一 OnActionTriggered event + 少量专用 event（Move 需要 Vector2）
///       加新 Action = 只在 .inputactions 和 SO 各加一条 = 改 0 处代码
///
/// 【保留的专用事件（签名不同于 string）】
///   OnMove(Vector2Int)   — Move：2DVector 上离散化（WA/WD/SA/SD 斜向；对向两键与 3+ 键不移动）
///   OnMouseDelta(Vector2) — Pan 是连续值
///   OnScroll(float)      — Zoom 是 Axis
///   OnMiddleMouse(bool)  — 需要 started/canceled 状态
///
/// 【被统一的 Button 事件】
///   LeftClick, RightClick, ToggleCamera, SwitchAlgorithm,
///   SwitchPolicy, ToggleHUD, Pause, Restart
///   → 全部通过 OnActionTriggered(string actionName) 派发
///   → 订阅者用 actionName 过滤
///
/// 挂载：根节点
/// </summary>
public class InputManager : MonoSingleton<InputManager> {

    public event Action<string> OnActionTriggered;//字符串参数事件
    //特殊事件
    public event Action<Vector2Int> OnMove;
    public event Action<bool> OnMiddleMouse;
    public event Action<Vector2> OnMouseDelta;
    public event Action<float> OnScroll;
    //无参数事件
    public event Action OnLeftClick;
    public event Action OnRightClick;
    public event Action OnToggleCamera;
    public event Action OnSwitchAlgorithm;
    public event Action OnSwitchPolicy;
    public event Action OnToggleHUD;
    public event Action OnTogglePause;
    public event Action OnRestart;

    [Header("═══ 数据 ═══")]
    [Tooltip("键位元数据库（SO）")]
    [SerializeField] private KeyBindDatabase keyBindDatabase;

    [Header("═══ WASD ═══")]
    [Range(0.05f, 0.3f)]
    [SerializeField] private float moveInterval = 0.15f;

    private GameInputActions input;
    private bool isDragging;
    private Coroutine moveRepeatCoroutine;
    private InputActionRebindingExtensions.RebindingOperation currentRebindOp;

    public GameInputActions InputActions => input;
    public KeyBindDatabase Database => keyBindDatabase;

    /// <summary>与 .inputactions 结构强相关；Move 改为 2DVector 后需换新 key，避免旧 JSON 覆盖新默认绑定。</summary>
    private const string REBIND_SAVE_KEY = "InputRebinds_v2_WASDComposite";

    // ✅ 新增：初始化保护
    private bool initialized = false;

    // ✅ 新增：统一初始化入口（GameInitializer 调用）
    public void Init() {
        if (initialized) return;
        initialized = true;

        input = new GameInputActions();

        if (keyBindDatabase != null)
            keyBindDatabase.Init();

        input.Gameplay.Enable();
        input.Camera.Enable();
        input.Global.Enable();

        BindCallbacks();

        EventBus.Subscribe<RebindRequestEvent>(OnRebindRequested);
        EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

        LoadBindings();
    }

    void OnEnable() {
        // 初始化逻辑已移动到 Init()
    }

    void OnDisable() {
        if (moveRepeatCoroutine != null)
            StopCoroutine(moveRepeatCoroutine);

        EventBus.Unsubscribe<RebindRequestEvent>(OnRebindRequested);
        EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);

        input?.Disable();
    }

    void OnDestroy() {
        input?.Dispose();
    }

    void BindCallbacks() {
        input.Gameplay.Move.started += OnMoveStarted;
        input.Gameplay.Move.canceled += OnMoveCanceled;

        input.Camera.MiddleMouse.started += _ => { isDragging = true; OnMiddleMouse?.Invoke(true); };
        input.Camera.MiddleMouse.canceled += _ => { isDragging = false; OnMiddleMouse?.Invoke(false); };
        input.Camera.Pan.performed += ctx => { if (isDragging) OnMouseDelta?.Invoke(ctx.ReadValue<Vector2>()); };
        input.Camera.Zoom.performed += ctx => OnScroll?.Invoke(ctx.ReadValue<float>() / 120f);

        BindButton(input.Gameplay.LeftClick, "LeftClick", () => OnLeftClick?.Invoke());
        BindButton(input.Gameplay.RightClick, "RightClick", () => OnRightClick?.Invoke());
        BindButton(input.Gameplay.ToggleCamera, "ToggleCamera", () => OnToggleCamera?.Invoke());
        BindButton(input.Gameplay.SwitchAlgorithm, "SwitchAlgorithm", () => OnSwitchAlgorithm?.Invoke());
        BindButton(input.Gameplay.SwitchPolicy, "SwitchPolicy", () => OnSwitchPolicy?.Invoke());
        BindButton(input.Gameplay.ToggleHUD, "ToggleHUD", () => OnToggleHUD?.Invoke());

        input.Global.Pause.performed += _ => { OnActionTriggered?.Invoke("Pause"); OnTogglePause?.Invoke(); };
        input.Global.Restart.performed += _ => { OnActionTriggered?.Invoke("Restart"); OnRestart?.Invoke(); };
    }

    void BindButton(InputAction action, string name, Action legacyEvent) {
        action.performed += _ => {
            OnActionTriggered?.Invoke(name);
            legacyEvent?.Invoke();
        };
    }

    void OnMoveStarted(InputAction.CallbackContext ctx) {
        if (moveRepeatCoroutine != null) StopCoroutine(moveRepeatCoroutine);
        moveRepeatCoroutine = StartCoroutine(MoveRepeatLoop());
    }

    void OnMoveCanceled(InputAction.CallbackContext ctx) {
        if (moveRepeatCoroutine != null) { StopCoroutine(moveRepeatCoroutine); moveRepeatCoroutine = null; }
    }

    IEnumerator MoveRepeatLoop() {
        var move = input.Gameplay.Move;
        while (true) {
            Vector2 raw = DiscreteGridMoveFrom2DVector.ResolveMoveVector(move);
            if (raw.sqrMagnitude > 0.01f)
                OnMove?.Invoke(new Vector2Int(Mathf.RoundToInt(raw.x), Mathf.RoundToInt(raw.y)));
            yield return new WaitForSeconds(moveInterval);
        }
    }

    void OnGameStateChanged(GameStateChangedEvent e) {
        bool playing = (e.newState == GameStateType.Playing);
        if (playing) { input.Gameplay.Enable(); input.Camera.Enable(); } else { input.Gameplay.Disable(); input.Camera.Disable(); }

        if (moveRepeatCoroutine != null && !playing) { StopCoroutine(moveRepeatCoroutine); moveRepeatCoroutine = null; }
    }

    void OnRebindRequested(RebindRequestEvent e) {
        CancelCurrentRebind();

        var action = input.asset.FindAction($"{e.mapName}/{e.actionName}");
        if (action == null) {
            Debug.LogWarning($"[InputManager] Action not found: {e.mapName}/{e.actionName}");
            return;
        }

        int bindingIndex = InputActionRebindHelper.GetRebindRootBindingIndex(action);
        if (bindingIndex < 0) return;

        EventBus.Publish(new RebindStartEvent { actionName = e.actionName });

        action.Disable();

        currentRebindOp = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op => {
                string display = action.GetBindingDisplayString(bindingIndex,
                    InputBinding.DisplayStringOptions.DontIncludeInteractions);

                op.Dispose();
                currentRebindOp = null;
                action.Enable();

                SaveBindings();

                EventBus.Publish(new RebindCompleteEvent {
                    actionName = e.actionName,
                    newBindingDisplay = display
                });

                CheckConflicts();
            })
            .OnCancel(op => {
                op.Dispose();
                currentRebindOp = null;
                action.Enable();

                EventBus.Publish(new RebindCancelEvent { actionName = e.actionName });
            })
            .Start();
    }

    void CancelCurrentRebind() {
        if (currentRebindOp != null) {
            currentRebindOp.Cancel();
        }
    }

    void CheckConflicts() {
        var pathMap = new System.Collections.Generic.Dictionary<string,
            System.Collections.Generic.List<string>>();

        foreach (var map in input.asset.actionMaps) {
            foreach (var action in map.actions) {
                foreach (var binding in action.bindings) {
                    if (binding.isComposite) continue;
                    string path = binding.effectivePath;
                    if (string.IsNullOrEmpty(path)) continue;

                    if (!pathMap.ContainsKey(path))
                        pathMap[path] = new System.Collections.Generic.List<string>();

                    string label = $"{map.name}/{action.name}";
                    if (!pathMap[path].Contains(label))
                        pathMap[path].Add(label);
                }
            }
        }

        foreach (var kv in pathMap) {
            if (kv.Value.Count > 1) {
                Toast.Show($"按键冲突: {string.Join(", ", kv.Value)}", Toast.Level.Warning, 3f);
                break;
            }
        }
    }

    public void SaveBindings() {
        string json = input.asset.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(REBIND_SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    public void LoadBindings() {
        if (PlayerPrefs.HasKey(REBIND_SAVE_KEY))
            input.asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(REBIND_SAVE_KEY));
    }

    public void ResetAllBindings() {
        input.asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(REBIND_SAVE_KEY);
    }

    public string GetBindingDisplay(string mapName, string actionName) {
        var action = input.asset.FindAction($"{mapName}/{actionName}");
        if (action == null) return "???";
        return InputActionRebindHelper.GetPrimaryBindingDisplayString(action);
    }

    public bool GetMouseWorldPosition(out int gridX, out int gridZ) {
        gridX = gridZ = 0;
        if (Camera.main == null) return false;
        if (Mouse.current == null) return false;

        Vector2 screenPos = Mouse.current.position.ReadValue();
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
}
