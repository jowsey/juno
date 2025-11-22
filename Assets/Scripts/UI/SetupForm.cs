using System;
using System.Globalization;
using ML;
using Ship;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class SetupForm : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _maxGenerationsInput;
        [SerializeField] private TMP_InputField _generationDurationInput;
        [SerializeField] private TMP_InputField _populationSizeInput;
        [SerializeField] private TMP_InputField _eliteCountInput;
        [SerializeField] private Slider _mutationRateInput;
        [SerializeField] private Slider _mutationStrengthInput;
        [SerializeField] private TMP_InputField _hiddenLayersInput;
        [SerializeField] private Toggle _passOutputsToInputsToggle;
        [SerializeField] private TextMeshProUGUI _inputOutputCountLabel;

        [SerializeField] private Button _submitButton;

        [SerializeField] private RectTransform _simUI;

        private void Start()
        {
            var currentOptions = SimulationManager.Instance.Options;

            _maxGenerationsInput.text = currentOptions.MaxGenerations.ToString();
            _generationDurationInput.text = currentOptions.GenerationDuration.ToString(CultureInfo.InvariantCulture);
            _populationSizeInput.text = currentOptions.PopulationSize.ToString();
            _eliteCountInput.text = currentOptions.EliteCount.ToString();
            _mutationRateInput.value = currentOptions.MutationRate * 100f;
            _mutationStrengthInput.value = currentOptions.MutationStrength * 100f;
            _hiddenLayersInput.text = string.Join("-", currentOptions.HiddenLayers);
            _passOutputsToInputsToggle.isOn = currentOptions.PassOutputsToInputs;

            UpdatePassOutputsToInputs(currentOptions.PassOutputsToInputs);

            _maxGenerationsInput.onEndEdit.AddListener(_ => ValidateIntField(_maxGenerationsInput, 1));
            _generationDurationInput.onEndEdit.AddListener(_ => ValidateFloatField(_generationDurationInput, 1));
            _populationSizeInput.onEndEdit.AddListener(_ => ValidateIntField(_populationSizeInput, 2));
            _eliteCountInput.onEndEdit.AddListener(_ => ValidateIntField(_eliteCountInput, 0));
            _hiddenLayersInput.onEndEdit.AddListener(_ => ValidateLayersField(_hiddenLayersInput));
            _passOutputsToInputsToggle.onValueChanged.AddListener(UpdatePassOutputsToInputs);

            _submitButton.onClick.AddListener(OnSubmit);
        }

        private void UpdatePassOutputsToInputs(bool isOn)
        {
            var inputCount = SpaceshipController.InputCount + (isOn ? SpaceshipController.OutputCount : 0);
            _inputOutputCountLabel.text = $"{inputCount} inputs, {SpaceshipController.OutputCount} outputs";
        }

        private static void ValidateIntField(TMP_InputField inputField, int minValue, int maxValue = int.MaxValue)
        {
            inputField.text = int.TryParse(inputField.text, out var value)
                ? Mathf.Clamp(value, minValue, maxValue).ToString()
                : minValue.ToString();
        }

        private void ValidateFloatField(TMP_InputField inputField, float minValue, float maxValue = float.MaxValue)
        {
            inputField.text = float.TryParse(inputField.text, out var value)
                ? Mathf.Clamp(value, minValue, maxValue).ToString(CultureInfo.InvariantCulture)
                : minValue.ToString(CultureInfo.InvariantCulture);
        }

        private int[] ReadLayersField(TMP_InputField inputField)
        {
            var parts = inputField.text.Split('-', StringSplitOptions.RemoveEmptyEntries);
            var layers = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                layers[i] = int.TryParse(parts[i], out var layerSize) ? Mathf.Max(1, layerSize) : 1;
            }

            return layers;
        }

        private void ValidateLayersField(TMP_InputField inputField)
        {
            var layers = ReadLayersField(inputField);
            inputField.text = string.Join("-", layers);
        }

        private void OnSubmit()
        {
            var maxGenerations = int.Parse(_maxGenerationsInput.text);
            var generationDuration = int.Parse(_generationDurationInput.text);
            var populationSize = int.Parse(_populationSizeInput.text);
            var eliteCount = int.Parse(_eliteCountInput.text);
            var mutationRate = _mutationRateInput.value / 100f;
            var mutationStrength = _mutationStrengthInput.value / 100f;
            var hiddenLayers = ReadLayersField(_hiddenLayersInput);
            var passOutputsToInputs = _passOutputsToInputsToggle.isOn;

            var options = new SimulationManager.SimulationOptions
            {
                MaxGenerations = maxGenerations,
                GenerationDuration = generationDuration,
                PopulationSize = populationSize,
                EliteCount = eliteCount,
                MutationRate = mutationRate,
                MutationStrength = mutationStrength,
                HiddenLayers = hiddenLayers,
                PassOutputsToInputs = passOutputsToInputs
            };

            SimulationManager.Instance.LaunchWithOptions(options);

            gameObject.SetActive(false);
            _simUI.gameObject.SetActive(true);
        }
    }
}