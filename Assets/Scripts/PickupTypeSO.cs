using UnityEngine;

public enum PickupKind
{
    Shield = 0,
    WallBreak = 1,
    JumpBoost = 2,
}

[CreateAssetMenu(menuName = "Runner/Pickup Type", fileName = "PickupType")]
public class PickupTypeSO : ScriptableObject
{
    public string displayName = "Pickup";
    public PrimitiveType primitive = PrimitiveType.Sphere;
    public Vector3 localScale = new Vector3(0.9f, 0.9f, 0.9f);
    public Color color = Color.white;
    public float yOffset = 0.8f;
    [Min(0.01f)] public float spawnWeight = 1f;
    public PickupKind kind = PickupKind.Shield;
}
