using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace ShotgunAndRun.Player.Tests
{
    public sealed class PlayerPrefabIntegrationTests
    {
        [Test]
        public void InputAsset_ContainsMouseAimAndKeyboardDash()
        {
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Settings/InputSystem_Actions.inputactions");

            Assert.That(input, Is.Not.Null);
            InputActionMap playerMap = input.FindActionMap("Player", true);
            InputAction aim = playerMap.FindAction("AimPoint", true);
            InputAction dash = playerMap.FindAction("Dash", true);

            Assert.That(aim.bindings, Has.Some.Matches<InputBinding>(binding =>
                binding.path == "<Pointer>/position"));
            Assert.That(dash.bindings, Has.Some.Matches<InputBinding>(binding =>
                binding.path == "<Keyboard>/leftShift"));
        }
    }
}
