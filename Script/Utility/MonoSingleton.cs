using UnityEngine;

public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour {
    //“T 必须是 MonoBehaviour 或其子类”
    //在这里，我们约定这个T是继承MonoSingleton<T>的子类，这个子类继承的同时也要作为T传入基类
    //而实际上，在编译器语法层面上，不要求子类一定是T
    //例如GridManager : MonoSingleton<NodeView>,NodeView是另一个继承了MonoBehaviour的类
    //但是这样就完全失去了我想要的语义，失去了单例的功能

    private static T instance;

    public static T Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<T>();

                if (instance == null) {
                    GameObject obj = new GameObject(typeof(T).Name);
                    instance = obj.AddComponent<T>();
                }
            }
            return instance;
        }
    }

    protected virtual void Awake() {
        if (instance == null) {
            instance = this as T;
            DontDestroyOnLoad(gameObject); // 跨场景
        } else if (instance != this) {
            Destroy(gameObject); // 保证唯一
        }
    }
}