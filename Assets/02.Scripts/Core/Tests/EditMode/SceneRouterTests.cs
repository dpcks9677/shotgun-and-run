using NUnit.Framework;

namespace ShotgunAndRun.Core.Tests
{
    public sealed class SceneRouterTests
    {
        [Test]
        public void Resolve_DefaultRoutes()
        {
            SceneRouter router = new SceneRouter();

            Assert.That(router.Resolve("TitleScene"), Is.EqualTo(GameState.Title));
            Assert.That(router.Resolve("ShelterScene"), Is.EqualTo(GameState.Shelter));
            Assert.That(router.Resolve("DungeonStageScene"), Is.EqualTo(GameState.Run));
            Assert.That(router.Resolve("TutorialScene"), Is.EqualTo(GameState.Tutorial));
        }

        [Test]
        public void Resolve_UnknownScene_ReturnsNone()
        {
            SceneRouter router = new SceneRouter();

            Assert.That(router.Resolve("CombatTestScene"), Is.EqualTo(GameState.None));
        }

        [Test]
        public void TryGetScene_ReturnsFirstRouteForState()
        {
            SceneRouter router = new SceneRouter();

            Assert.That(router.TryGetScene(GameState.Run, out string sceneName), Is.True);
            Assert.That(sceneName, Is.EqualTo("DungeonStageScene"));
            Assert.That(router.TryGetScene(GameState.Result, out _), Is.False);
        }
    }
}
