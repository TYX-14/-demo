using TMPro;
using UnityEngine;

/// <summary>
/// 挂在队友说话的头顶气泡预制体上。
/// 负责显示文本、控制透明度，并始终面向摄像机。
/// </summary>
public class BattleBarkBubble : MonoBehaviour
{
    [SerializeField, Tooltip("气泡文本。留空时自动从子物体查找。")]
    private TMP_Text text;

    [Header("气泡底图")]
    [SerializeField, Tooltip("气泡背景精灵（九宫格）。留空表示不使用底图。")]
    private SpriteRenderer background;

    [SerializeField, Min(0f), Tooltip("底图比文字每边多出的世界单位。")]
    private float backgroundPadding = 0.08f;

    [SerializeField, Tooltip("底图的最小尺寸，防止短句时气泡过小。")]
    private Vector2 backgroundMinSize = new(0.5f, 0.28f);

    [SerializeField, Tooltip("是否让气泡始终面向摄像机。")]
    private bool faceCamera = true;

    [Header("渲染顺序")]
    [SerializeField, Tooltip("强制使用下面的排序值，避免气泡被角色精灵盖住。")]
    private bool overrideSorting = true;

    [SerializeField, Tooltip("气泡的 Sorting Order。文字会使用 +1，保证压在底图之上。")]
    private int bubbleSortingOrder = 100;

    private Color textBaseColor = Color.white;
    private Color backgroundBaseColor = Color.white;
    private bool colorsCached;

    private void Awake()
    {
        EnsureReferences();
    }

    /// <summary>
    /// 解析文本、底图与透明度引用。
    /// 气泡在预制体里默认关闭时 Awake 不会执行，所以 Show 里也要调用一次。
    /// </summary>
    private void EnsureReferences()
    {
        if (text == null)
            text = GetComponentInChildren<TMP_Text>(true);

        // 底图也自动查找：只要 SpriteRenderer 是子物体就不用在 Inspector 里再拖一次。
        if (background == null)
            background = GetComponentInChildren<SpriteRenderer>(true);

        if (!colorsCached)
        {
            textBaseColor = text != null ? text.color : Color.white;
            backgroundBaseColor = background != null ? background.color : Color.white;
            colorsCached = true;
        }

        ApplySorting();
    }

    /// <summary>
    /// 3D 文字与精灵都在透明队列里，光靠距离排序容易和角色精灵打架，
    /// 这里直接指定 Sorting Order：底图用设定值，文字用 +1。
    /// </summary>
    private void ApplySorting()
    {
        if (!overrideSorting)
            return;

        if (background != null)
            background.sortingOrder = bubbleSortingOrder;

        // TMP_Text 本身没有 sortingOrder，3D 文本要通过它的 Renderer 设置。
        if (text != null)
        {
            Renderer textRenderer = text.GetComponent<Renderer>();
            if (textRenderer != null)
                textRenderer.sortingOrder = bubbleSortingOrder + 1;
        }
    }

    public void Show(string line)
    {
        gameObject.SetActive(true);
        EnsureReferences();

        if (text != null)
            text.text = line;

        FitBackground();
        SetAlpha(0f);
    }

    /// <summary>以指定文字颜色显示。用于按精神状态区分（美德金色、折磨红色）。</summary>
    public void Show(string line, Color textColor)
    {
        EnsureReferences();
        textBaseColor = textColor;
        Show(line);
    }

    /// <summary>按文字实际大小调整九宫格底图，短句时不低于最小尺寸。</summary>
    private void FitBackground()
    {
        if (background == null)
            return;

        // 九宫格拉伸需要 Sliced 模式；sprite 的 Mesh Type 必须是 Full Rect。
        if (background.drawMode != SpriteDrawMode.Sliced)
            background.drawMode = SpriteDrawMode.Sliced;

        if (text == null)
            return;

        text.ForceMeshUpdate();
        Vector2 textSize = text.textBounds.size;

        background.size = new Vector2(
            Mathf.Max(backgroundMinSize.x, textSize.x + backgroundPadding * 2f),
            Mathf.Max(backgroundMinSize.y, textSize.y + backgroundPadding * 2f));
    }

    /// <summary>隐藏并清空文本，供下次复用。</summary>
    public void Hide()
    {
        if (text != null)
            text.text = string.Empty;

        gameObject.SetActive(false);
    }

    public void SetAlpha(float alpha)
    {
        // 世界空间没有 CanvasGroup 可用，直接改渲染颜色的透明度。
        if (text != null)
        {
            Color color = textBaseColor;
            color.a = textBaseColor.a * alpha;
            text.color = color;
        }

        if (background != null)
        {
            Color color = backgroundBaseColor;
            color.a = backgroundBaseColor.a * alpha;
            background.color = color;
        }
    }

    private void LateUpdate()
    {
        if (!faceCamera)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        transform.rotation = mainCamera.transform.rotation;
    }
}
