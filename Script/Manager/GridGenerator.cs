using UnityEngine;

/// <summary>
/// 网格生成器 —— 负责实例化所有 Cube 格子
/// 挂在场景中的空物体上，Inspector 里拖入 NodePrefab
/// </summary>
public class GridGenerator : MonoBehaviour {
    [Header("Grid GameObject Parent")]
    [SerializeField] private GameObject gridRoot;
    [Header("Grid Dimensions")]
    public int width = 20;
    public int height = 20;

    [Header("References")]
    public GameObject nodePrefab;   // Cube Prefab（上面挂 NodeView + Collider）

    private NodeView[,] views;

    /// <summary>
    /// 由 GameInitializer 调用，不再自己 Start()
    /// </summary>
    public NodeView[,] Init() {
        views = new NodeView[width, height];

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                var go = Instantiate(nodePrefab, gridRoot.transform);
                go.name = $"Node_{x}_{y}";
                go.transform.position = new Vector3(x, 0, y);

                var view = go.GetComponent<NodeView>();
                view.Init(x, y);
                views[x, y] = view;
            }
        }

        return views;
    }
}