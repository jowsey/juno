using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SeparateStageButton : MonoBehaviour
{
    [SerializeField] private SpaceshipController _ship;
    private Button _button;

    private void OnValidate()
    {
        _ship ??= FindAnyObjectByType<SpaceshipController>();
    }

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        _ship?.SeparateStage();
    }
}