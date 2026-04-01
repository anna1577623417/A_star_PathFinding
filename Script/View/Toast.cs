using UnityEngine;

/// <summary>
/// 屏幕通知系统 —— 在屏幕中上方弹出短暂的提示消息
/// 
/// 【效果】
///   消息从透明淡入 → 停留 → 淡出消失
///   支持不同级别的配色：Info（蓝）、Warning（橙）、Error（红）、Success（绿）
///
/// 【使用方式】
///   从任何地方调用静态方法：
///   Toast.Show("无法到达目标！", Toast.Level.Error);
///   Toast.Show("路径已找到", Toast.Level.Success);
///
/// 挂载：根节点（或任意常驻物体）
/// </summary>
public class Toast : MonoBehaviour {
    public static Toast Instance { get; private set; }

    public enum Level { Info, Warning, Error, Success }

    // ═══════════════════════════════════════════
    //  Inspector 可调参数
    // ═══════════════════════════════════════════

    [Header("═══ 时间 ═══")]
    [Tooltip("淡入时长（秒）")]
    [Range(0.05f, 1f)]
    [SerializeField] private float fadeInTime = 0.2f;

    [Tooltip("停留时长（秒），代码调用时可覆盖")]
    [Range(0.5f, 5f)]
    [SerializeField] private float defaultDuration = 2.0f;

    [Tooltip("淡出时长（秒）")]
    [Range(0.1f, 2f)]
    [SerializeField] private float fadeOutTime = 0.5f;

    [Header("═══ 布局 ═══")]
    [Tooltip("消息条距屏幕顶部的比例（0=顶部，0.5=正中）")]
    [Range(0f, 0.5f)]
    [SerializeField] private float verticalPosition = 0.12f;

    [Tooltip("消息条高度（像素）")]
    [Range(28, 80)]
    [SerializeField] private int barHeight = 44;

    [Tooltip("文字两侧额外留白（像素）")]
    [Range(8, 80)]
    [SerializeField] private int horizontalPadding = 48;

    [Tooltip("淡入时从上方滑入的距离（像素），0=无滑动")]
    [Range(0, 30)]
    [SerializeField] private int slideDistance = 10;

    [Header("═══ 字体 ═══")]
    [Range(12, 30)]
    [SerializeField] private int fontSize = 18;

    [Header("═══ 描边 ═══")]
    [Tooltip("文字描边偏移（像素），0=关闭")]
    [Range(0, 3)]
    [SerializeField] private int outlineOffset = 1;

    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.9f);

    [Header("═══ 级别配色 ═══")]
    [SerializeField] private Color infoTextColor = new Color(0.6f, 0.85f, 1f, 1f);
    [SerializeField] private Color infoBgColor = new Color(0.02f, 0.05f, 0.12f, 0.85f);
    [SerializeField] private Color warningTextColor = new Color(1f, 0.75f, 0.25f, 1f);
    [SerializeField] private Color warningBgColor = new Color(0.12f, 0.08f, 0.01f, 0.85f);
    [SerializeField] private Color errorTextColor = new Color(1f, 0.35f, 0.3f, 1f);
    [SerializeField] private Color errorBgColor = new Color(0.15f, 0.02f, 0.02f, 0.85f);
    [SerializeField] private Color successTextColor = new Color(0.4f, 1f, 0.5f, 1f);
    [SerializeField] private Color successBgColor = new Color(0.02f, 0.1f, 0.03f, 0.85f);

    // ---- 运行时状态（不暴露） ----
    private string message = "";
    private Level level = Level.Info;
    private float timer = 0f;
    private float duration = 2.0f;
    private bool active = false;

    // ---- 样式缓存 ----
    private GUIStyle msgStyle;
    private GUIStyle shadowStyle;
    private Texture2D bgTex;

    void Awake() {
        Instance = this;
    }

    // ============================================================
    //  静态调用入口
    // ============================================================

    /// <summary>
    /// 弹出一条通知
    /// </summary>
    /// <param name="msg">消息内容</param>
    /// <param name="lvl">级别（决定颜色）</param>
    /// <param name="dur">停留时长（秒）</param>
    public static void Show(string msg, Level lvl = Level.Info, float dur = -1f) {
        if (Instance == null) return;
        Instance.message = msg;
        Instance.level = lvl;
        Instance.duration = dur > 0 ? dur : Instance.defaultDuration;
        Instance.timer = 0f;
        Instance.active = true;
    }

    void Update() {
        if (!active) return;
        timer += Time.deltaTime;

        float totalTime = fadeInTime + duration + fadeOutTime;
        if (timer >= totalTime)
            active = false;
    }

    // ============================================================
    //  绘制
    // ============================================================
    void OnGUI() {
        if (!active) return;

        // ---- 计算透明度 ----
        float alpha;
        float totalTime = fadeInTime + duration + fadeOutTime;

        if (timer < fadeInTime) {
            // 淡入阶段
            alpha = timer / fadeInTime;
        } else if (timer < fadeInTime + duration) {
            // 停留阶段
            alpha = 1f;
        } else {
            // 淡出阶段
            float fadeProgress = (timer - fadeInTime - duration) / fadeOutTime;
            alpha = 1f - fadeProgress;
        }
        alpha = Mathf.Clamp01(alpha);

        // ---- 级别对应颜色（从 Inspector 读取，乘以 alpha）----
        Color textColor;
        Color bgColor;
        string icon;

        switch (level) {
            case Level.Error:
                textColor = errorTextColor;
                bgColor = errorBgColor;
                icon = "✖ ";
                break;
            case Level.Warning:
                textColor = warningTextColor;
                bgColor = warningBgColor;
                icon = "⚠ ";
                break;
            case Level.Success:
                textColor = successTextColor;
                bgColor = successBgColor;
                icon = "✓ ";
                break;
            default: // Info
                textColor = infoTextColor;
                bgColor = infoBgColor;
                icon = "● ";
                break;
        }

        // 把淡入淡出的 alpha 叠加到颜色上
        textColor.a *= alpha;
        bgColor.a *= alpha;

        // ---- 构建样式（每帧刷新以响应 Inspector 调参）----
        if (msgStyle == null) {
            msgStyle = new GUIStyle(GUI.skin.label) {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
        }
        msgStyle.fontSize = fontSize;
        msgStyle.normal.textColor = textColor;

        // 描边样式
        if (shadowStyle == null) {
            shadowStyle = new GUIStyle(msgStyle);
        }
        shadowStyle.fontSize = fontSize;
        shadowStyle.fontStyle = msgStyle.fontStyle;
        shadowStyle.alignment = msgStyle.alignment;
        shadowStyle.normal.textColor = new Color(shadowColor.r, shadowColor.g, shadowColor.b, alpha * shadowColor.a);

        // 背景纹理
        bgTex = MakeTex(4, 4, bgColor);

        // ---- 计算位置 ----
        string fullText = icon + message;
        float msgW = msgStyle.CalcSize(new GUIContent(fullText)).x + horizontalPadding;
        float msgH = barHeight;
        float posX = (Screen.width - msgW) * 0.5f;
        float posY = Screen.height * verticalPosition;

        // 淡入时轻微下滑效果
        float slideOffset = (1f - Mathf.Clamp01(timer / fadeInTime * 2f)) * -slideDistance;
        posY += slideOffset;

        Rect bgRect = new Rect(posX, posY, msgW, msgH);
        Rect textRect = new Rect(posX, posY, msgW, msgH);

        // ---- 绘制背景 ----
        GUI.DrawTexture(bgRect, bgTex);

        // ---- 描边 + 正文 ----
        int o = outlineOffset;
        if (o > 0) {
            GUI.Label(new Rect(textRect.x - o, textRect.y, textRect.width, textRect.height), fullText, shadowStyle);
            GUI.Label(new Rect(textRect.x + o, textRect.y, textRect.width, textRect.height), fullText, shadowStyle);
            GUI.Label(new Rect(textRect.x, textRect.y - o, textRect.width, textRect.height), fullText, shadowStyle);
            GUI.Label(new Rect(textRect.x, textRect.y + o, textRect.width, textRect.height), fullText, shadowStyle);
        }
        GUI.Label(textRect, fullText, msgStyle);
    }

    // ============================================================
    //  工具
    // ============================================================
    private static Texture2D MakeTex(int w, int h, Color col) {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }
}