using UnityEngine;

[CreateAssetMenu(menuName = "Runner/Obstacle Type", fileName = "ObstacleType")]
public class ObstacleTypeSO : ScriptableObject
{
    public string displayName = "Obstacle";
    public PrimitiveType primitive = PrimitiveType.Cube;
    public Vector3 localScale = Vector3.one;
    public Color color = Color.red;
    public float yOffset = 0f;
    [Min(0)] public int damage = 1;
    public bool destroyOnHit = true;
    public bool avoidableByJump = true;
    public bool avoidableBySlide = false;
}
