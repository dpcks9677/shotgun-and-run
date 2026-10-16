using UnityEngine;
using PlayerComponent = ShotgunAndRun.Player.Player;

namespace ShotgunAndRun.Core
{
    // 플레이어를 다른 위치로 바로 옮깁니다. (스폰 · 순간이동)
    // 카메라는 플레이어의 자식이므로 함께 이동합니다.
    internal static class PlayerPlacement
    {
        public static void MoveTo(PlayerComponent player, Vector2 position)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
            }

            Transform playerTransform = player.transform;
            playerTransform.position = new Vector3(position.x, position.y, playerTransform.position.z);
        }
    }
}
