using UnityEngine;

public class BillboardToCamera : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private bool _freezeXAxis = false, _freezeYAxis = false, _freezeZAxis = false;

    private void Start()
    {
        // Auto-detect main camera if not assigned
        if (_camera == null)
            _camera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_camera == null) return;

        Vector3 targetDirection = _camera.transform.position - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

        Vector3 euler = targetRotation.eulerAngles;
        if (_freezeXAxis) euler.x = transform.rotation.eulerAngles.x;
        if (_freezeYAxis) euler.y = transform.rotation.eulerAngles.y;
        if (_freezeZAxis) euler.z = transform.rotation.eulerAngles.z;

        transform.rotation = Quaternion.Euler(euler);
    }
}
