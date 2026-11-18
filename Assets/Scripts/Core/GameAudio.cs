using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace MortgageSim.Audio
{
    /// <summary>
    /// Музыка и процедурный саунд-дизайн: клики, шаги, продажа, платежи.
    /// Ambient играет по кругу; тема коллектора перебивает её и после
    /// окончания возвращает ambient с того же места.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const float AmbientVolume = 0.34f;
        private const int SampleRate = 44100;

        private AudioSource _ambient;
        private AudioSource _collector;
        private AudioSource _sfx;
        private AudioLowPassFilter _ambientLowPass;
        private AudioDistortionFilter _ambientDistortion;

        private Tween _ambientFade;
        private Tween _pitchTween;
        private Tween _cutoffTween;
        private Tween _distortionTween;
        private Coroutine _collectorRoutine;
        private bool _ambientWasPlaying;

        private AudioClip _click;
        private AudioClip _step;
        private AudioClip _sell;
        private AudioClip _pay;
        private AudioClip _knock;
        private AudioClip _door;
        private AudioClip _pass;
        private AudioClip _fail;
        private AudioClip _month;
        private AudioClip _deny;

        public void Initialize()
        {
            _ambient = CreateSource("AmbientMusic", true);
            _collector = CreateSource("CollectorMusic", false);
            _sfx = CreateSource("SFX", false);

            _ambientLowPass = _ambient.gameObject.AddComponent<AudioLowPassFilter>();
            _ambientLowPass.cutoffFrequency = 22000f;
            _ambientLowPass.lowpassResonanceQ = 1f;

            _ambientDistortion = _ambient.gameObject.AddComponent<AudioDistortionFilter>();
            _ambientDistortion.distortionLevel = 0f;

            var ambientClip = Resources.Load<AudioClip>("Music/ambient");
            if (ambientClip != null)
            {
                _ambient.clip = ambientClip;
                _ambient.volume = AmbientVolume;
                _ambient.Play();
            }

            var collectorClip = Resources.Load<AudioClip>("Music/collector");
            if (collectorClip != null) _collector.clip = collectorClip;

            _click = BuildTone("sfx_click", 0.05f, 1150f, 760f, 0.20f, 0.002f);
            _step = BuildNoise("sfx_step", 0.11f, 0.085f, 0.012f);
            _sell = BuildArpeggio("sfx_sell", new[] { 659f, 988f, 1319f }, 0.085f, 0.20f);
            _pay = BuildArpeggio("sfx_pay", new[] { 196f, 294f, 392f }, 0.11f, 0.24f);
            _knock = BuildKnocks();
            _door = BuildNoise("sfx_door", 0.55f, 0.14f, 0.2f, 140f, 70f);
            _pass = BuildArpeggio("sfx_tests_pass", new[] { 523f, 659f, 784f, 1047f }, 0.09f, 0.20f);
            _fail = BuildArpeggio("sfx_tests_fail", new[] { 330f, 262f, 196f }, 0.12f, 0.22f, true);
            _month = BuildNoise("sfx_month", 0.9f, 0.13f, 0.05f, 400f, 80f, true);
            _deny = BuildArpeggio("sfx_deny", new[] { 180f, 140f }, 0.12f, 0.2f, true);
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 1f;
            return source;
        }

        // ----------------------------------------------------------- music

        public void PlayCollectorTheme()
        {
            if (_collector == null || _collector.clip == null) return;

            _collectorRoutine ??= StartCoroutine(CollectorThemeRoutine());
        }

        private IEnumerator CollectorThemeRoutine()
        {
            _ambientWasPlaying = _ambient != null && _ambient.isPlaying && !IsAmbientPaused();

            if (_ambient != null && _ambient.isPlaying)
            {
                _ambientFade?.Kill();
                _ambientFade = DOTween.To(() => _ambient.volume, v => _ambient.volume = v, 0f, 0.55f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
                yield return new WaitForSecondsRealtime(0.55f);
                _ambient.Pause();
            }

            _collector.Stop();
            _collector.volume = 0.62f;
            _collector.Play();

            yield return new WaitForSecondsRealtime(_collector.clip.length);

            if (_ambient != null)
            {
                if (_ambientWasPlaying)
                {
                    _ambient.UnPause();
                    _ambient.volume = 0f;
                    _ambientFade?.Kill();
                    _ambientFade = DOTween.To(() => _ambient.volume, v => _ambient.volume = v, AmbientVolume, 1.4f)
                        .SetEase(Ease.InOutQuad)
                        .SetUpdate(true);
                }
            }

            _collectorRoutine = null;
        }

        private bool IsAmbientPaused()
        {
            return _ambient != null && _ambient.time > 0f && !_ambient.isPlaying;
        }

        /// <summary>Мгновенно убирает тему коллектора и возвращает ambient (dev-пропуск месяца).</summary>
        public void StopCollectorTheme()
        {
            if (_collectorRoutine != null)
            {
                StopCoroutine(_collectorRoutine);
                _collectorRoutine = null;
            }
            if (_collector != null) _collector.Stop();

            if (_ambient != null)
            {
                _ambientFade?.Kill();
                if (_ambientWasPlaying && !_ambient.isPlaying) _ambient.UnPause();
                _ambient.volume = AmbientVolume;
                _ambientWasPlaying = false;
            }
        }

        /// <summary>Чем меньше вещей, тем ниже и «грязнее» звучит ambient.</summary>
        public void ApplyDecay(int remaining, int initial)
        {
            if (_ambient == null) return;

            float sold = 1f - Mathf.Clamp01(initial <= 0 ? 0f : (float)remaining / initial);
            float pitch = Mathf.Lerp(1f, 0.82f, sold);
            float cutoff = Mathf.Lerp(22000f, 2400f, sold);
            float distortion = Mathf.Lerp(0f, 0.42f, sold);
            const float duration = 1.5f;

            _pitchTween?.Kill();
            _pitchTween = DOTween.To(() => _ambient.pitch, v => _ambient.pitch = v, pitch, duration)
                .SetEase(Ease.InOutQuad).SetUpdate(true);

            if (_ambientLowPass != null)
            {
                _cutoffTween?.Kill();
                _cutoffTween = DOTween.To(() => _ambientLowPass.cutoffFrequency,
                        v => _ambientLowPass.cutoffFrequency = v, cutoff, duration)
                    .SetEase(Ease.InOutQuad).SetUpdate(true);
            }

            if (_ambientDistortion != null)
            {
                _distortionTween?.Kill();
                _distortionTween = DOTween.To(() => _ambientDistortion.distortionLevel,
                        v => _ambientDistortion.distortionLevel = v, distortion, duration)
                    .SetEase(Ease.InOutQuad).SetUpdate(true);
            }
        }

        // ------------------------------------------------------------- sfx

        public void Click() => Play(_click, 0.75f);
        public void Footstep() => Play(_step, 0.55f);
        public void Sell() => Play(_sell, 0.8f);
        public void Pay() => Play(_pay, 0.9f);
        public void Knock() => Play(_knock, 1f);
        public void Door() => Play(_door, 0.8f);
        public void TestsPassed() => Play(_pass, 0.85f);
        public void TestsFailed() => Play(_fail, 0.7f);
        public void MonthTransition() => Play(_month, 0.9f);
        public void Deny() => Play(_deny, 0.75f);

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null || _sfx == null) return;
            _sfx.PlayOneShot(clip, volume);
        }

        // ---------------------------------------------------- clip builders

        private static AudioClip BuildTone(string clipName, float duration, float fromHz, float toHz,
            float volume, float attack)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
            var data = new float[count];
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;
                float frequency = Mathf.Lerp(fromHz, toHz, progress);
                phase += 2.0 * Mathf.PI * frequency / SampleRate;
                float envelope = Mathf.Min(1f, t / Mathf.Max(0.0005f, attack)) *
                                 Mathf.Pow(1f - progress, 2.2f);
                data[i] = Mathf.Sin((float)phase) * envelope * volume;
            }
            return FromData(clipName, data);
        }

        private static AudioClip BuildArpeggio(string clipName, float[] notes, float step,
            float volume, bool square = false)
        {
            float duration = notes.Length * step;
            int count = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                int noteIndex = Mathf.Clamp((int)(t / step), 0, notes.Length - 1);
                float local = t - noteIndex * step;
                float env = Mathf.Min(1f, local / 0.006f) * Mathf.Pow(1f - local / step, 1.8f);
                float phase = 2f * Mathf.PI * notes[noteIndex] * local;
                float sample = square ? Mathf.Sign(Mathf.Sin(phase)) * 0.7f : Mathf.Sin(phase);
                data[i] = sample * env * volume;
            }
            return FromData(clipName, data);
        }

        private static AudioClip BuildKnocks()
        {
            const float duration = 0.85f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            var random = new System.Random(1337);
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float start = k * 0.26f;
                    float local = t - start;
                    if (local < 0f || local > 0.16f) continue;
                    float env = Mathf.Min(1f, local / 0.004f) * Mathf.Pow(1f - local / 0.16f, 3f);
                    float knock = Mathf.Sin(2f * Mathf.PI * 110f * local) * 0.8f;
                    knock += ((float)random.NextDouble() * 2f - 1f) * 0.25f;
                    sample += knock * env;
                }
                data[i] = sample * 0.3f;
            }
            return FromData("sfx_knock", data);
        }

        private static AudioClip BuildNoise(string clipName, float duration, float volume,
            float attack, float fromHz = 1200f, float toHz = 120f, bool swell = false)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate));
            var data = new float[count];
            var random = new System.Random(clipName.GetHashCode());
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;
                float frequency = Mathf.Lerp(fromHz, toHz, progress);
                float alpha = Mathf.Clamp01(2f * Mathf.PI * frequency / SampleRate);
                float white = (float)random.NextDouble() * 2f - 1f;
                filtered += alpha * (white - filtered);

                float envelope;
                if (swell)
                {
                    envelope = Mathf.Sin(progress * Mathf.PI);
                }
                else
                {
                    envelope = Mathf.Min(1f, t / Mathf.Max(0.0005f, attack)) *
                               Mathf.Pow(1f - progress, 2.5f);
                }
                data[i] = filtered * envelope * volume;
            }
            return FromData(clipName, data);
        }

        private static AudioClip FromData(string clipName, float[] data)
        {
            var clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            _ambientFade?.Kill();
            _pitchTween?.Kill();
            _cutoffTween?.Kill();
            _distortionTween?.Kill();
        }
    }
}
