using System;
using MFrameork.Event;
public class EventBus : MonoBehaviour
{
   private static readonly Dictionary<Type,List<object>> EventDict = new Dictionary<Type,List<object>>();

    public static void Subscribe<TEvent>(IEventReceiver<TEvent> receiver) where TEvent : IEvent
    {
        Type eventType = typeof(TEvent);
        if (!EventDict.TryGetValue(eventType, out var receivers))
        {
            receivers = new List<object>();
            EventDict[eventType] = receivers;
        }
        if (!receivers.Contains(receiver))//contains:判断字典内某元素是否已经存在，如果是，返回ture
        {
            receivers.Add(receiver);
        }
    }

    public static void Unsubscribe<TEvent>(IEventReceiver<TEvent> receiver) where TEvent : IEvent
    {
        Type eventType =typeof(TEvent);

        if (EventDict.TryGetValue(eventType, out var receivers))
        {
            receivers.Remove(receiver);
        }

        if (receivers.Count == 0)
        {
            EventDict.Remove(eventType);
        }
    }

    public static void Publish<TEvent>(TEvent evt) where TEvent : IEvent
    {
        Type eventType = typeof(TEvent);

        if (EventDict.TryGetValue(eventType, out var receivers))
        {
            ;

            for (int i = 0; i < receivers.Count; i++)
            {
                var obj = receivers[i];
                if (obj is UnityEngine.Object unityObj && unityObj == null)
                {
                    receivers.Remove(i);
                    continue;
                }

                ((IEventReceiver<TEvent>)receivers[i]).OnEvent(evt);//强制转类型，让其可以使用OnEvent方法
                
            }
        }

        if (receivers != null&&receivers.Count == 0)  
        {
            EventDict.Remove(eventType);
        }
    }

}
