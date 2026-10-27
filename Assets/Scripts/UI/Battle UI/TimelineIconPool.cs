using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CTB 时间轴头像对象池。
/// 只负责实例创建、回收与复用，不负责队列顺序或动画时机。
/// </summary>
public sealed class TimelineIconPool : MonoBehaviour
{
    private readonly Stack<TimelineIconController> pooledIcons = new();
    private GameObject iconPrefab;
    private Transform poolRoot;

    public void Initialize(GameObject prefab, Transform hiddenPoolRoot)
    {
        iconPrefab = prefab;
        poolRoot = hiddenPoolRoot != null ? hiddenPoolRoot : transform;
    }

    public TimelineIconController Get(Transform parent)
    {
        TimelineIconController icon = null;
        while (pooledIcons.Count > 0 && icon == null)
            icon = pooledIcons.Pop();

        if (icon == null)
        {
            if (iconPrefab == null)
            {
                Debug.LogError("TimelineIconPool：没有指定 Timeline Icon 预制体。", this);
                return null;
            }

            GameObject instance = Instantiate(iconPrefab, parent, false);
            icon = instance.GetComponent<TimelineIconController>();
            if (icon == null)
            {
                Debug.LogError("TimelineIconPool：Timeline Icon 预制体缺少 TimelineIconController 组件。", instance);
                Destroy(instance);
                return null;
            }
        }
        else
        {
            icon.PrepareForReuse(parent);
            icon.gameObject.SetActive(true);
        }

        return icon;
    }

    public void Release(TimelineIconController icon)
    {
        if (icon == null || pooledIcons.Contains(icon))
            return;

        icon.PrepareForReuse(poolRoot);
        icon.gameObject.SetActive(false);
        pooledIcons.Push(icon);
    }
}
