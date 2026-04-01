using UnityEngine;

/// <summary>游戏全局状态枚举</summary>
public enum GameStateType {
    Loading,    // 初始化中
    Playing,    // 游戏进行
    Paused,     // 暂停
    GameOver    // 结束（通关或失败）
}

/// <summary>
/// 游戏状态管理器
/// 
/// 【职责】
///   管理全局状态（Playing/Paused/GameOver），
///   状态变更时通过 EventBus 通知所有系统。
///   各系统自己决定如何响应状态变化。
///
/// 【状态流转】
///   Loading → Playing → Paused ↔ Playing
///                     → GameOver → Loading（重玩）
///
/// 挂载：根节点
/// </summary>
public class GameStateManager : MonoBehaviour {
    public static GameStateManager Instance { get; private set; }

    [Header("═══ 状态 ═══")]
    [SerializeField] private GameStateType currentState = GameStateType.Loading;

    public GameStateType CurrentState => currentState;
    public bool IsPlaying => currentState == GameStateType.Playing;

    void Awake() {
        Instance = this;
    }

    /// <summary>切换状态并广播事件</summary>
    public void SetState(GameStateType newState) {
        if (newState == currentState) return;

        var old = currentState;
        currentState = newState;

        // 暂停/恢复时间
        Time.timeScale = (newState == GameStateType.Paused) ? 0f : 1f;

        // 广播状态变更
        EventBus.Publish(new GameStateChangedEvent {
            oldState = old,
            newState = newState
        });

        Debug.Log($"[GameState] {old} → {newState}");
    }

    /// <summary>开始游戏（初始化完成后调用）</summary>
    public void StartGame() => SetState(GameStateType.Playing);

    /// <summary>切换暂停</summary>
    public void TogglePause() {
        if (currentState == GameStateType.Playing)
            SetState(GameStateType.Paused);
        else if (currentState == GameStateType.Paused)
            SetState(GameStateType.Playing);
    }

    /// <summary>游戏结束（通关）</summary>
    public void Win() => SetState(GameStateType.GameOver);

    /// <summary>重新开始</summary>
    public void Restart() {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }
}