using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [ExecuteInEditMode]
    public class SliderInput : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private TextMeshProUGUI _valueLabel;
        [SerializeField] private string _suffix;
        [SerializeField] private int _decimalPlaces = 1;

        private void OnValidate()
        {
            if (_slider) _slider = GetComponentInChildren<Slider>();
            if (_valueLabel) _valueLabel = GetComponentInChildren<TextMeshProUGUI>();

            OnSliderValueChanged(_slider.value);
        }

        private void OnEnable()
        {
            _slider.onValueChanged.AddListener(OnSliderValueChanged);
            OnSliderValueChanged(_slider.value);
        }

        private void OnDisable()
        {
            _slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        private void OnSliderValueChanged(float value)
        {
            if (_valueLabel == null) return;
            _valueLabel.text = value.ToString("F" + _decimalPlaces) + _suffix;
        }
    }
}