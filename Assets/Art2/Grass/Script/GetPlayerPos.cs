using System.Collections.Generic;
using UnityEngine;

namespace Grass.Script
{
    [ExecuteInEditMode]
    public class GetPlayerPos : MonoBehaviour
    {
        private readonly List<Vector4> _poss = new List<Vector4>();

        public Material material;

        // 每帧读取当前启用的草地碰撞体，支持运行时生成/销毁的队伍跟随者。
        void Update()
        {
            _poss.Clear();

            List<GrassCollider> colliders = GrassCollider.ActiveColliders;
            for (int i = 0; i < colliders.Count; i++)
            {
                GrassCollider col = colliders[i];
                if (col == null || !col.isActiveAndEnabled)
                    continue;

                Vector3 pos = col.Position;
                _poss.Add(new Vector4(pos.x, pos.y, pos.z, col.radius));
            }

            if (material != null)
            {
                material.SetVectorArray("_Players", _poss.ToArray());
            }
        }
    }
}
