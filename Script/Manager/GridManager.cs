using UnityEngine;

public class GridManager : MonoSingleton<GridManager> {
    [Header("Grid Size")]
    public int width = 20;
    public int height = 20;

    [Header("Terrain")]
    [Range(0f, 0.35f)]
    public float wallDensity = 0.2f;
    public int seed = 42;//0为完全随机

    public Node[,] grid;
    public NodeView[,] views;

    public void Init(NodeView[,] nodeViews) {
        views = nodeViews;
        width = views.GetLength(0);
        height = views.GetLength(1);

        grid = new Node[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = new Node(x, y);

        GenerateTerrain();
    }

    void GenerateTerrain() {
        Random.State oldState = Random.state;
        if (seed != 0) Random.InitState(seed);
        else Random.InitState(System.DateTime.Now.Millisecond);

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                if (x <= 2 && y <= 2) continue;
                if (Random.value < wallDensity) {
                    grid[x, y].walkable = false;
                    views[x, y].SetWall(true);
                }
            }
        }
        Random.state = oldState;
    }

    public Node GetNode(int x, int y) {
        if (x < 0 || y < 0 || x >= width || y >= height) return null;
        return grid[x, y];
    }

    public NodeView GetView(int x, int y) {
        if (x < 0 || y < 0 || x >= width || y >= height) return null;
        return views[x, y];
    }

    public bool InBounds(int x, int y) {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    public void ClearAllPathVisuals() {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                views[x, y].ClearPathVisuals();
    }

    public void ResetAllNodes() {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y].Reset();
    }

    public void ToggleWall(int x, int y) {
        if (!InBounds(x, y)) return;
        var node = grid[x, y];
        node.walkable = !node.walkable;
        views[x, y].SetWall(!node.walkable);
    }
}