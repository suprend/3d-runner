using UnityEngine;
using UnityEngine.InputSystem;

public static class RunnerInputFactory
{
    public static InputActionAsset CreateDefault()
    {
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();

        var map = new InputActionMap("Player");

        var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
        move.AddBinding("<Gamepad>/leftStick");

        var wasd = move.AddCompositeBinding("2DVector");
        wasd.With("Up", "<Keyboard>/w");
        wasd.With("Down", "<Keyboard>/s");
        wasd.With("Left", "<Keyboard>/a");
        wasd.With("Right", "<Keyboard>/d");

        var arrows = move.AddCompositeBinding("2DVector");
        arrows.With("Up", "<Keyboard>/upArrow");
        arrows.With("Down", "<Keyboard>/downArrow");
        arrows.With("Left", "<Keyboard>/leftArrow");
        arrows.With("Right", "<Keyboard>/rightArrow");

        var jump = map.AddAction("Jump", InputActionType.Button);
        jump.AddBinding("<Keyboard>/space");
        jump.AddBinding("<Gamepad>/buttonSouth");

        var ability = map.AddAction("Ability", InputActionType.Button);
        ability.AddBinding("<Keyboard>/e");
        ability.AddBinding("<Gamepad>/buttonEast");

        var restart = map.AddAction("Restart", InputActionType.Button);
        restart.AddBinding("<Keyboard>/r");
        restart.AddBinding("<Gamepad>/start");

        asset.AddActionMap(map);
        return asset;
    }
}
