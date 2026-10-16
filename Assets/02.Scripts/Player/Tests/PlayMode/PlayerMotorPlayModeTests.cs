using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShotgunAndRun.Player.Tests
{
    public sealed class PlayerMotorPlayModeTests
    {
        [UnityTest]
        public IEnumerator Motor_PrioritizesDashAndStopsWhenDisabled()
        {
            PlayerDataSO data = ScriptableObject.CreateInstance<PlayerDataSO>();
            PlayerRuntimeStats stats = new(data);
            GameObject gameObject = new("motor-test");
            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            PlayerMovement movement = gameObject.AddComponent<PlayerMovement>();
            PlayerDash dash = gameObject.AddComponent<PlayerDash>();
            PlayerMotor motor = gameObject.AddComponent<PlayerMotor>();

            movement.Initialize(stats);
            dash.Initialize(stats);
            motor.Initialize(movement, dash);
            movement.SetInput(Vector2.right);

            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.x, Is.EqualTo(stats.MoveSpeed).Within(0.001f));

            Assert.That(dash.TryStart(Vector2.up), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.up * stats.DashSpeed));

            motor.SetMotorEnabled(false);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));

            Object.Destroy(gameObject);
            Object.Destroy(data);
        }
    }
}
