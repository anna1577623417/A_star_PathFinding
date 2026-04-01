using UnityEngine;

/// <summary>
/// Toast 通知系统 —— v2.0 事件驱动版
/// 
/// 【v1.0 → v2.0 变化】
///   + 自动订阅 EventBus 事件，不再需要手动调 Toast.Show()
///   + 寻路失败、成功、算法切换、相机切换、到达出口 自动弹通知
///   + 仍保留 Toast.Show() 静态方法供自定义调用
/// </summary>
public class Toast : MonoBehaviour {
    public static Toast Instance { get; private set; }
    public enum Level { Info, Warning, Error, Success }

    [Header("═══ 时间 ═══")]
    [Range(0.05f, 1f)][SerializeField] private float fadeInTime = 0.2f;
    [Range(0.5f, 5f)][SerializeField] private float defaultDuration = 2.0f;
    [Range(0.1f, 2f)][SerializeField] private float fadeOutTime = 0.5f;

    [Header("═══ 布局 ═══")]
    [Range(0f, 0.5f)][SerializeField] private float verticalPosition = 0.12f;
    [Range(28, 80)][SerializeField] private int barHeight = 44;
    [Range(8, 80)][SerializeField] private int horizontalPadding = 48;
    [Range(0, 30)][SerializeField] private int slideDistance = 10;

    [Header("═══ 字体 ═══")]
    [Range(12, 30)][SerializeField] private int fontSize = 18;
    [Range(0, 3)][SerializeField] private int outlineOffset = 1;

    [Header("═══ 配色 ═══")]
    [SerializeField] private Color infoText = new(0.6f, 0.85f, 1f);
    [SerializeField] private Color infoBg = new(0.02f, 0.05f, 0.12f, 0.85f);
    [SerializeField] private Color warnText = new(1f, 0.75f, 0.25f);
    [SerializeField] private Color warnBg = new(0.12f, 0.08f, 0.01f, 0.85f);
    [SerializeField] private Color errorText = new(1f, 0.35f, 0.3f);
    [SerializeField] private Color errorBg = new(0.15f, 0.02f, 0.02f, 0.85f);
    [SerializeField] private Color successText = new(0.4f, 1f, 0.5f);
    [SerializeField] private Color successBg = new(0.02f, 0.1f, 0.03f, 0.85f);
    [SerializeField] private Color shadowColor = new(0, 0, 0, 0.9f);

    private string message = "";
    private Level level;
    private float timer, duration;
    private bool active;
    private GUIStyle msgStyle, shadowStyle;

    void Awake() { Instance = this; }

    void Start() {
        // 订阅游戏事件
        EventBus.Subscribe<PathFailedEvent>(_ =>
            Show("无法到达目标！", Level.Error));

        EventBus.Subscribe<PathFoundEvent>(e =>
            Show($"路径已找到: {e.pathLength}步  探索{e.exploredCount}格  耗时{e.searchTime:F3}s", Level.Success, 2.5f));

        EventBus.Subscribe<AlgorithmChangedEvent>(e =>
            Show($"算法切换: {e.algorithmName}", Level.Info, 1.5f));

        EventBus.Subscribe<CameraModeChangedEvent>(e =>
            Show(e.modeName, Level.Info, 1f));

        EventBus.Subscribe<ExitReachedEvent>(_ =>
            Show("恭喜通关！ 按 R 重玩", Level.Success, 5f));
    }

    public static void Show(string msg, Level lvl = Level.Info, float dur = -1f) {
        if (Instance == null) return;
        Instance.message = msg;
        Instance.level = lvl;
        Instance.duration = dur > 0 ? dur : Instance.defaultDuration;
        Instance.timer = 0;
        Instance.active = true;
    }

    void Update() {
        if (!active) return;
        timer += Time.unscaledDeltaTime; // 暂停时也要淡出
        if (timer >= fadeInTime + duration + fadeOutTime)
            active = false;
    }

    void OnGUI() {
        if (!active) return;

        float alpha;
        if (timer < fadeInTime) alpha = timer / fadeInTime;
        else if (timer < fadeInTime + duration) alpha = 1;
        else alpha = 1 - (timer - fadeInTime - duration) / fadeOutTime;
        alpha = Mathf.Clamp01(alpha);

        Color tc, bc;
        string icon;
        switch (level) {
            case Level.Error: tc = errorText; bc = errorBg; icon = "✖ "; break;
            case Level.Warning: tc = warnText; bc = warnBg; icon = "⚠ "; break;
            case Level.Success: tc = successText; bc = successBg; icon = "✓ "; break;
            default: tc = infoText; bc = infoBg; icon = "● "; break;
        }
        tc.a *= alpha;
        bc.a *= alpha;

        if (msgStyle == null)
            msgStyle = new GUIStyle(GUI.skin.label) {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        msgStyle.fontSize = fontSize;
        msgStyle.normal.textColor = tc;

        if (shadowStyle == null)
            shadowStyle = new GUIStyle(msgStyle);
        shadowStyle.fontSize = fontSize;
        shadowStyle.alignment = TextAnchor.MiddleCenter;
        shadowStyle.normal.textColor = new Color(shadowColor.r, shadowColor.g, shadowColor.b, alpha * shadowColor.a);

        string text = icon + message;
        float w = msgStyle.CalcSize(new GUIContent(text)).x + horizontalPadding;
        float h = barHeight;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * verticalPosition + (1 - Mathf.Clamp01(timer / fadeInTime * 2)) * -slideDistance;

        GUI.DrawTexture(new Rect(x, y, w, h), MakeTex(bc));

        int o = outlineOffset;
        if (o > 0) {
            var r = new Rect(x, y, w, h);
            GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), text, shadowStyle);
            GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), text, shadowStyle);
            GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), text, shadowStyle);
            GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), text, shadowStyle);
        }
        GUI.Label(new Rect(x, y, w, h), text, msgStyle);
    }

    static Texture2D MakeTex(Color c) {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}