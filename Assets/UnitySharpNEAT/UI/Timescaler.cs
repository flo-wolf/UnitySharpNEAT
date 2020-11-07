/*
------------------------------------------------------------------
  This file is part of UnitySharpNEAT 
  Copyright 2020, Florian Wolf
  https://github.com/flo-wolf/UnitySharpNEAT
------------------------------------------------------------------
*/
using UnityEngine;
using UnityEngine.UI;

namespace UnitySharpNEAT
{
    /// <summary>
    /// This class does something for some reason.
    /// </summary>
    public class Timescaler : MonoBehaviour
    {
        #region FIELDS
        [Header("References")]
        [SerializeField] private Slider _slider;
        [SerializeField] private Text _text;

        [Header("Settings")]
        [SerializeField] private float _initialTimeScale = 5f;
        [SerializeField] private bool _autoLowerTimeScale = true;
        [SerializeField] private float _fpsCheckIntervall = 12;
        [SerializeField] private int _lowerTimeScaleBelowFps = 10;

        private float _timeUntilNextFpsCheck = 0;
        private float _accumulatedFrametimes = 0;
        private int _framesPassed = 0;
        #endregion

        #region UNITY FUNCTIONS
        private void Start()
        {
            _slider.value = _initialTimeScale * 10; // when the slider vlaue is 10, the timescale should be 1.0. The slider is only multiplied by 10 to get the 0.1 steps.
            SetTimescale(_initialTimeScale);
        }

        private void Update()
        {
            if (_autoLowerTimeScale)
                FitTimescaleToFps();
        }
        #endregion

        #region FUNCTIONS
        /// <summary>
        /// Change Timescale and update UI
        /// </summary>
        private void SetTimescale(float newTimeScale)
        {
            Time.timeScale = newTimeScale;
            _text.text = "Timescale: x" + Mathf.Round(newTimeScale * 100) / 100;
        }

        /// <summary>
        /// Lower the TimeScale in case the frames drop too low.
        /// </summary>
        private void FitTimescaleToFps()
        {
            _timeUntilNextFpsCheck -= Time.deltaTime;
            _accumulatedFrametimes += Time.timeScale / Time.deltaTime;
            ++_framesPassed;

            if (_timeUntilNextFpsCheck <= 0.0)
            {
                var fps = _accumulatedFrametimes / _framesPassed;
                _timeUntilNextFpsCheck = _fpsCheckIntervall;
                _accumulatedFrametimes = 0.0f;
                _framesPassed = 0;
                //   print("FPS: " + fps);
                if (fps < _lowerTimeScaleBelowFps)
                {
                    float loweredTimeScale = Time.timeScale - 1;

                    if (loweredTimeScale > 0)
                    {
                        SetTimescale(loweredTimeScale);
                        _slider.SetValueWithoutNotify(loweredTimeScale * 10);
                        Debug.Log("Lowering time scale to: " + loweredTimeScale);
                    }
                }
            }
        }
        #endregion

        #region EVENT HANDLER
        public void HandleSliderChanged()
        {
            SetTimescale(_slider.value / 10); // when the slider vlaue is 10, the timescale should be 1.0. The slider is only multiplied by 10 to get the 0.1 steps.
        }
        #endregion
    }
}
