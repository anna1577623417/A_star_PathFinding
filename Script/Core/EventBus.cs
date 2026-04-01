using System;
using System.Collections.Generic;

/// <summary>
/// 轻量级事件总线 —— 系统间通信的唯一通道
/// 
/// 【设计目的】
///   替代系统间的直接调用。Player 不再直接调 Toast.Show()，
///   而是发布 EventBus.Publish(new PathFailedEvent())，
///   Toast 自己订阅这个事件并决定如何显示。
///   
/// 【使用方式】
///   订阅: EventBus.Subscribe<PathFoundEvent>(OnPathFound);
///   发布: EventBus.Publish(new PathFoundEvent { path = ... });
///   取消: EventBus.Unsubscribe<PathFoundEvent>(OnPathFound);
/// </summary>
public static class EventBus {
    // 按事件类型存储所有订阅者
    private static readonly Dictionary<Type, Delegate> listeners = new();

    /// <summary>订阅事件</summary>
    public static void Subscribe<T>(Action<T> callback) where T : struct {
        var type = typeof(T);
        if (listeners.ContainsKey(type))
            listeners[type] = Delegate.Combine(listeners[type], callback);
        else
            listeners[type] = callback;
    }

    /// <summary>取消订阅</summary>
    public static void Unsubscribe<T>(Action<T> callback) where T : struct {
        var type = typeof(T);
        if (listeners.ContainsKey(type)) {
            listeners[type] = Delegate.Remove(listeners[type], callback);
            if (listeners[type] == null)
                listeners.Remove(type);
        }
    }

    /// <summary>发布事件（所有订阅者同步收到）</summary>
    public static void Publish<T>(T evt) where T : struct {
        if (listeners.TryGetValue(typeof(T), out var d))
            ((Action<T>)d)?.Invoke(evt);
    }

    /// <summary>清除所有订阅（场景切换时调用）</summary>
    public static void Clear() => listeners.Clear();
}

// ═══════════════════════════════════════════
//  事件定义 —— 所有事件集中在这里，便于查阅
// ═══════════════════════════════════════════

/// <summary>玩家移动到新格子</summary>
public struct PlayerMovedEvent {
    public int x, y;
    public TerrainType terrain;
}

/// <summary>寻路完成（成功）</summary>
public struct PathFoundEvent {
    public int pathLength;
    public int exploredCount;
    public float searchTime;
}

/// <summary>寻路失败</summary>
public struct PathFailedEvent { }

/// <summary>游戏状态变更</summary>
public struct GameStateChangedEvent {
    public GameStateType oldState;
    public GameStateType newState;
}

/// <summary>相机模式变更</summary>
public struct CameraModeChangedEvent {
    public string modeName;
}

/// <summary>玩家到达出口</summary>
public struct ExitReachedEvent { }

/// <summary>算法切换</summary>
public struct AlgorithmChangedEvent {
    public string algorithmName;
}

/// <summary>地形被编辑</summary>
public struct TerrainEditedEvent {
    public int x, y;
    public TerrainType newType;
}