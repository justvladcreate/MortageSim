using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MortgageSim.World
{
    /// <summary>
    /// Чем меньше вещей осталось в квартире, тем тусклее и серее мир.
    /// Настроение меняется через URP Volume + освещение, всё анимируется DOTween.
    /// </summary>
    public sealed class AmbienceController : MonoBehaviour
    {
        private VolumeProfile _profile;
        private ColorAdjustments _color;
        private Vignette _vignette;
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<float> _baseIntensities = new List<float>();
        private Tween _saturationTween;
        private Tween _exposureTween;
        private Tween _contrastTween;
        private Tween _vignetteTween;
        private Tween _ambientTween;
        private readonly List<Tween> _lightTweens = new List<Tween>();

        public void Initialize()
        {
            var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude);
            foreach (var volume in volumes)
            {
                if (!volume.isGlobal) continue;
                _profile = volume.profile;
                break;
            }

            if (_profile != null)
            {
                if (!_profile.TryGet(out _color)) _color = _profile.Add<ColorAdjustments>();
                if (!_profile.TryGet(out _vignette)) _vignette = _profile.Add<Vignette>();
                if (_color != null)
                {
                    _color.saturation.overrideState = true;
                    _color.saturation.value = 0f;
                    _color.postExposure.overrideState = true;
                    _color.postExposure.value = 0f;
                    _color.contrast.overrideState = true;
                    _color.contrast.value = 0f;
                }
                if (_vignette != null)
                {
                    _vignette.intensity.overrideState = true;
                    _vignette.intensity.value = 0.2f;
                }
            }

            _lights.Clear();
            _baseIntensities.Clear();
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light.type == LightType.Directional) continue;
                _lights.Add(light);
                _baseIntensities.Add(light.intensity);
            }
        }

        public void Apply(int remaining, int initial, bool instant = false)
        {
            float sold = 1f - Mathf.Clamp01(initial <= 0 ? 0f : (float)remaining / initial);
            float saturation = Mathf.Lerp(0f, -100f, sold);
            float exposure = Mathf.Lerp(0f, -2.4f, sold);
            float contrast = Mathf.Lerp(0f, 32f, sold);
            float vignette = Mathf.Lerp(0.2f, 0.8f, sold);
            float ambient = Mathf.Lerp(1f, 0.14f, sold);

            if (GameManager.Instance != null && GameManager.Instance.Audio != null)
                GameManager.Instance.Audio.ApplyDecay(remaining, initial);

            float duration = instant ? 0f : 1.4f;

            if (_color != null)
            {
                _saturationTween?.Kill();
                _exposureTween?.Kill();
                _contrastTween?.Kill();
                _saturationTween = DOTween.To(() => _color.saturation.value, v => _color.saturation.value = v, saturation, duration)
                    .SetEase(Ease.InOutQuad).SetUpdate(true);
                _exposureTween = DOTween.To(() => _color.postExposure.value, v => _color.postExposure.value = v, exposure, duration)
                    .SetEase(Ease.InOutQuad).SetUpdate(true);
                _contrastTween = DOTween.To(() => _color.contrast.value, v => _color.contrast.value = v, contrast, duration)
                    .SetEase(Ease.InOutQuad).SetUpdate(true);
            }

            if (_vignette != null)
            {
                _vignetteTween?.Kill();
                _vignetteTween = DOTween.To(() => _vignette.intensity.value, v => _vignette.intensity.value = v, vignette, duration)
                    .SetEase(Ease.InOutQuad).SetUpdate(true);
            }

            _ambientTween?.Kill();
            _ambientTween = DOTween.To(() => RenderSettings.ambientIntensity, v => RenderSettings.ambientIntensity = v, ambient, duration)
                .SetEase(Ease.InOutQuad).SetUpdate(true);

            for (int i = 0; i < _lights.Count; i++)
            {
                if (i < _lightTweens.Count) _lightTweens[i]?.Kill();
                var light = _lights[i];
                float target = _baseIntensities[i] * Mathf.Lerp(1f, 0.22f, sold);
                var tween = DOTween.To(() => light.intensity, v => light.intensity = v, target, duration)
                    .SetEase(Ease.InOutQuad).SetUpdate(true);
                if (i < _lightTweens.Count) _lightTweens[i] = tween;
                else _lightTweens.Add(tween);
            }
        }
    }
}
