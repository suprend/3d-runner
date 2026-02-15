using UnityEngine;

public class RunnerHUD : MonoBehaviour
{
    [SerializeField] private RunnerHealth health;
    [SerializeField] private RunnerDifficulty difficulty;

    public void Initialize(RunnerHealth playerHealth, RunnerDifficulty runnerDifficulty)
    {
        health = playerHealth;
        difficulty = runnerDifficulty;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        int hp = health != null ? health.CurrentHealth : 0;
        int maxHp = health != null ? health.MaxHealth : 0;
        float t = difficulty != null ? difficulty.Elapsed : 0f;
        float speed = difficulty != null ? difficulty.CurrentSpeed : 0f;

        GUI.Label(new Rect(10, 10, 460, 22), $"HP: {hp}/{maxHp}");
        GUI.Label(new Rect(10, 32, 460, 22), $"Time: {t:0.0}s   Speed: {speed:0.0}");
    }
#endif
}
