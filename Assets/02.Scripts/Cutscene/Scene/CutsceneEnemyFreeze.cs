using System.Collections.Generic;
using UnityEngine;
using EnemyComponent = global::Enemy;

namespace ShotgunAndRun.Cutscene
{
    /// <summary>
    /// 게임 정지 컷씬 동안 적을 멈춥니다. 적의 프레임 단위 동작(FSM · 이동 · 공격 · 투사체 · 웨이브 스포너)을 끄고
    /// 속도를 0으로 만들며, 매 프레임 훑어 컷씬 중에 생긴 적도 멈춥니다. Animator는 그대로 돌아갑니다.
    /// 컷씬이 연출하는 적(MoveEnemies)은 위치를 직접 옮기므로 멈춰 있어도 연출에 지장이 없습니다. Enemy 코드는 건드리지 않습니다.
    /// </summary>
    internal sealed class CutsceneEnemyFreeze
    {
        private readonly List<Behaviour> _disabled = new List<Behaviour>();
        private bool _active;

        /// <summary>
        /// 멈추기 시작합니다. 이후 Sweep을 매 프레임 불러야 새로 생긴 적도 멈춥니다.
        /// </summary>
        internal void Begin()
        {
            _active = true;
            Sweep();
        }

        /// <summary>
        /// 아직 멈추지 않은 적을 멈춥니다. Begin 전이나 End 후에는 아무 일도 없습니다.
        /// </summary>
        internal void Sweep()
        {
            if (!_active) return;

            Disable<EnemyStateMachine>();
            Disable<EnemyMove>();
            Disable<EnemyCombat>();
            Disable<EnemyProjectile>();
            Disable<EnemyDestructionSpawner>();

            foreach (var enemy in Object.FindObjectsByType<EnemyComponent>(FindObjectsSortMode.None))
            {
                var body = enemy.GetComponent<Rigidbody2D>();
                if (body != null) body.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// 멈추기를 끝내고, 이 클래스가 끈 것만 다시 켭니다. 이미 없어진 오브젝트는 건너뜁니다.
        /// </summary>
        internal void End()
        {
            _active = false;
            foreach (var behaviour in _disabled)
            {
                if (behaviour != null) behaviour.enabled = true;
            }
            _disabled.Clear();
        }

        private void Disable<T>() where T : Behaviour
        {
            foreach (var behaviour in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (!behaviour.enabled) continue;
                behaviour.enabled = false;
                _disabled.Add(behaviour);
            }
        }
    }
}
