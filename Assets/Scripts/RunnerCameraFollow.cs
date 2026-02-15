using UnityEngine;

public class RunnerCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 6f, -10f);
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1f, 10f);
    [SerializeField] private float smoothTime = 0.1f;

    private Vector3 velocity;
    private RunnerCameraShake shake;

    public void Initialize(Transform targetTransform, Vector3 cameraOffset, Vector3 cameraLookAtOffset, float cameraSmoothTime)
    {
        target = targetTransform;
        offset = cameraOffset;
        lookAtOffset = cameraLookAtOffset;
        smoothTime = Mathf.Max(0f, cameraSmoothTime);
        shake = GetComponent<RunnerCameraShake>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        var desired = target.position + offset;
        transform.position = smoothTime <= 0f
            ? desired
            : Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        transform.LookAt(target.position + lookAtOffset);

        if (shake != null)
        {
            shake.Evaluate(out var posOffset, out var rotEuler);
            transform.position += posOffset;
            transform.rotation = transform.rotation * Quaternion.Euler(rotEuler);
        }
    }
}
