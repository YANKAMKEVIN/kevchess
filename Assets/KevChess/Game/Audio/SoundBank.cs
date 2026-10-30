using System;
using UnityEngine;

namespace KevChess.Game.Audio
{
    public enum Sfx { Select, Move, Capture, Castle, Check, Promote, Victory, Defeat, Draw, Click, Illegal }

    /// <summary>
    /// Sons synthétisés au démarrage (timbres de verre/cristal) : aucun fichier audio.
    /// </summary>
    public sealed class SoundBank : MonoBehaviour
    {
        const int Rate = 44100;
        const string MuteKey = "kevchess.muted";

        AudioClip[] clips;
        AudioSource[] voices;
        int nextVoice;
        System.Random rng = new System.Random(7);

        public bool Muted
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set { PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void Awake()
        {
            voices = new AudioSource[6];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }

            var values = (Sfx[])Enum.GetValues(typeof(Sfx));
            clips = new AudioClip[values.Length];
            foreach (var s in values) clips[(int)s] = Build(s);
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            if (Muted || clips == null) return;
            var v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.06f;
            v.PlayOneShot(clips[(int)sfx], volume);
        }

        AudioClip Build(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Select: return Make("select", 0.12f, t => Bell(t, 1760f, 0.05f, 0.35f));
                case Sfx.Click: return Make("click", 0.06f, t => Bell(t, 2400f, 0.015f, 0.3f));
                case Sfx.Move: return Make("move", 0.45f, t => Thud(t, 150f, 0.06f, 0.5f) + Glass(t, 1320f, 0.18f, 0.45f));
                case Sfx.Castle:
                    return Make("castle", 0.6f, t => Thud(t, 140f, 0.06f, 0.45f) + Glass(t, 1180f, 0.16f, 0.35f)
                                                     + Glass(t - 0.14f, 1480f, 0.16f, 0.35f));
                case Sfx.Capture:
                    return Make("capture", 0.8f, t => Thud(t, 110f, 0.08f, 0.6f) + Shatter(t, 0.22f, 0.5f)
                                                      + Glass(t, 2100f, 0.3f, 0.3f));
                case Sfx.Check:
                    return Make("check", 0.9f, t => Bell(t, 1318.5f, 0.45f, 0.4f) + Bell(t - 0.09f, 1975.5f, 0.45f, 0.35f));
                case Sfx.Promote:
                    return Make("promote", 1.1f, t => Sweep(t, 600f, 2400f, 0.5f, 0.3f) + Bell(t - 0.45f, 2093f, 0.5f, 0.35f));
                case Sfx.Victory:
                    return Make("victory", 1.8f, t => Bell(t, 523.25f, 1f, 0.3f) + Bell(t - 0.15f, 659.25f, 1f, 0.3f)
                                                      + Bell(t - 0.3f, 783.99f, 1f, 0.3f) + Bell(t - 0.45f, 1046.5f, 1.1f, 0.35f));
                case Sfx.Defeat:
                    return Make("defeat", 1.8f, t => Bell(t, 659.25f, 1f, 0.3f) + Bell(t - 0.2f, 587.33f, 1f, 0.3f)
                                                     + Bell(t - 0.4f, 466.16f, 1.2f, 0.35f));
                case Sfx.Draw:
                    return Make("draw", 1.4f, t => Bell(t, 587.33f, 0.9f, 0.3f) + Bell(t - 0.2f, 880f, 0.9f, 0.3f));
                case Sfx.Illegal:
                    return Make("illegal", 0.2f, t => Thud(t, 95f, 0.07f, 0.5f) + Thud(t - 0.08f, 85f, 0.07f, 0.4f));
                default: return Make("silence", 0.05f, t => 0f);
            }
        }

        static AudioClip Make(string name, float seconds, Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                data[i] = wave(i / (float)Rate);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            // Normalisation douce + fondu de sortie pour éviter les clics.
            float gain = peak > 0.9f ? 0.9f / peak : 1f;
            int fade = Mathf.Min(n, Rate / 100);
            for (int i = 0; i < n; i++)
            {
                data[i] *= gain;
                if (i > n - fade) data[i] *= (n - i) / (float)fade;
            }
            var clip = AudioClip.Create("kevchess_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Cloche : partiels légèrement inharmoniques.
        static float Bell(float t, float f, float decay, float amp)
        {
            if (t < 0f) return 0f;
            float env = Mathf.Exp(-t / decay) * Mathf.Min(1f, t * 400f);
            return amp * env * (Mathf.Sin(2f * Mathf.PI * f * t)
                                + 0.45f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) * Mathf.Exp(-t / (decay * 0.5f))
                                + 0.2f * Mathf.Sin(2f * Mathf.PI * f * 3.02f * t) * Mathf.Exp(-t / (decay * 0.3f)));
        }

        // Verre : partiels très inharmoniques, attaque nette.
        static float Glass(float t, float f, float decay, float amp)
        {
            if (t < 0f) return 0f;
            float env = Mathf.Exp(-t / decay) * Mathf.Min(1f, t * 900f);
            return amp * env * (Mathf.Sin(2f * Mathf.PI * f * t)
                                + 0.6f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t / (decay * 0.4f))
                                + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 5.40f * t) * Mathf.Exp(-t / (decay * 0.2f)));
        }

        // Pose sur le plateau : sinus grave qui descend vite.
        static float Thud(float t, float f, float decay, float amp)
        {
            if (t < 0f) return 0f;
            float freq = f * (1f + 0.6f * Mathf.Exp(-t / 0.015f));
            return amp * Mathf.Exp(-t / decay) * Mathf.Sin(2f * Mathf.PI * freq * t);
        }

        // Éclats : bruit filtré grossièrement par une sinusoïde aiguë modulée.
        static float Shatter(float t, float decay, float amp)
        {
            if (t < 0f) return 0f;
            float noise = Mathf.PerlinNoise(t * 9000f, 0.37f) * 2f - 1f;
            float sparkle = Mathf.Sin(2f * Mathf.PI * 3400f * t) * (Mathf.PerlinNoise(t * 300f, 4.2f) > 0.55f ? 1f : 0.2f);
            return amp * Mathf.Exp(-t / decay) * (noise * 0.7f + sparkle * 0.3f);
        }

        static float Sweep(float t, float f0, float f1, float duration, float amp)
        {
            if (t < 0f || t > duration) return 0f;
            float u = t / duration;
            float phase = 2f * Mathf.PI * (f0 * t + (f1 - f0) * t * u * 0.5f);
            return amp * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(phase);
        }
    }
}
