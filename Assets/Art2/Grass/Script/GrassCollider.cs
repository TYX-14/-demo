using System.Collections.Generic;
using UnityEngine;

namespace Grass.Script
{
    [ExecuteInEditMode]
    public class GrassCollider : MonoBehaviour
    {
        // 注册所有当前启用的草地碰撞体，包括运行时实例化的队伍跟随者。
        public static readonly List<GrassCollider> ActiveColliders = new List<GrassCollider>();

        public float radius = 1;
        public Vector3 centryOffcet = Vector3.zero;

        public Vector3 Position
        {
            get
            {
                return transform.position + centryOffcet;
            }
        }

        private void OnEnable()
        {
            if (!ActiveColliders.Contains(this))
            {
                ActiveColliders.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveColliders.Remove(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color((float)0xed / 0xff, (float)0x65 / 0xff, 1, 1);
            Gizmos.DrawWireSphere(Position, radius);
        }
    }
}
