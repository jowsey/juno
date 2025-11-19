using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Vector3 _localFollowOffset;
    [SerializeField] private float _zoomRelativeMoveSpeed = 2f;
    [SerializeField] private float _minZoom = 5;
    [SerializeField] private float _maxZoom = 2000;
    [SerializeField] private float _zoomSpeedLog = 1.2f;
    [SerializeField] private float _baseZoomSpeed = 0.5f;

    public Transform FollowTarget;

    private InputAction _moveAction;
    private InputAction _zoomAction;

    private float _targetZoom;

    private void OnValidate()
    {
        if (!_camera) _camera = GetComponent<Camera>();
    }

    private void Start()
    {
        _moveAction = InputSystem.actions.FindAction("Move");
        _zoomAction = InputSystem.actions.FindAction("Zoom");

        _targetZoom = _camera.orthographicSize;
    }

    private void LateUpdate()
    {
        if (FollowTarget)
        {
            _camera.transform.position = new Vector3(
                FollowTarget.position.x,
                FollowTarget.position.y,
                _camera.transform.position.z
            ) + FollowTarget.TransformVector(_localFollowOffset);
        }
        else
        {
            var move = _moveAction.ReadValue<Vector2>();

            var moveSpeed = _zoomRelativeMoveSpeed * _camera.orthographicSize;
            _camera.transform.position += new Vector3(move.x, move.y, 0) * (moveSpeed * Time.unscaledDeltaTime);
        }

        var logMultiplier = Mathf.Log(_camera.orthographicSize, _zoomSpeedLog);
        var zoom = _zoomAction.ReadValue<float>() * _baseZoomSpeed * logMultiplier;
        _targetZoom = Mathf.Clamp(_targetZoom - zoom, _minZoom, _maxZoom);

        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _targetZoom, 10f * Time.unscaledDeltaTime);
    }
}