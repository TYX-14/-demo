using System.Collections.Generic;
using UnityEngine;

namespace Grass.Script
{
    [ExecuteInEditMode]
    public class GetPlayerPos : MonoBehaviour
    {
        // Shader 中 _Players 固定为 100 个元素。
        // 每次都传入固定长度数组，避免 Unity Material 数组容量无法扩展。
        private const int MaxPlayers = 100;
        private readonly Vector4[] _playerPositions = new Vector4[MaxPlayers];

        public Material material;

        void Update()
        {
            List<GrassCollider> colliders = GrassCollider.ActiveColliders;
            int playerCount = 0;

            // 清空数组，防止上帧角色残留在数组中。
            for (int i = 0; i < MaxPlayers; i++)
            {
                _playerPositions[i] = Vector4.zero;
            }

            for (int i = 0; i < colliders.Count && playerCount < MaxPlayers; i++)
            {
                GrassCollider col = colliders[i];
                if (col == null || !col.isActiveAndEnabled)
                    continue;

                Vector3 pos = col.Position;
                _playerPositions[playerCount] = new Vector4(pos.x, pos.y, pos.z, col.radius);
                playerCount++;
            }

            if (material != null)
            {
                material.SetVectorArray("_Players", _playerPositions);
                material.SetInt("_PlayerCount", playerCount);
            }
        }
    }
}
