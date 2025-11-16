using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _followTarget;
    [SerializeField] private Vector3 _localFollowOffset;
    [SerializeField] private float _zoomRelativeMoveSpeed = 1f;
    [SerializeField] private float _minZoom = 2;
    [SerializeField] private float _maxZoom = 100;

    private InputAction _moveAction;
    private InputAction _zoomAction;

    private void OnValidate()
    {
        _camera ??= GetComponent<Camera>();
    }

    private void Start()
    {
        _moveAction = InputSystem.actions.FindAction("Move");
        _zoomAction = InputSystem.actions.FindAction("Zoom");
    }

    private void Update()
    {
        if (_followTarget)
        {
            _camera.transform.position = new Vector3(
                _followTarget.position.x,
                _followTarget.position.y,
                _camera.transform.position.z
            ) + _followTarget.TransformVector(_localFollowOffset);
        }
        else
        {
            var move = _moveAction.ReadValue<Vector2>();

            var moveSpeed = _zoomRelativeMoveSpeed * _camera.orthographicSize;
            _camera.transform.position += new Vector3(move.x, move.y, 0) * (moveSpeed * Time.unscaledDeltaTime);
        }

        var zoom = _zoomAction.ReadValue<float>();
        _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize - zoom, _minZoom, _maxZoom);
    }
}