using System;
using UnityEngine;

namespace KevChess.Game.Audio
{
    /// <summary>
    /// Musique d'ambiance générative synthétisée en continu (aucun fichier) : nappe d'accords lents
    /// qui se fondent les uns dans les autres, filtre doux qui respire, quelques notes de cristal
    /// éparses et un écho stéréo. Les enchaînements sont tirés au hasard : pas de boucle audible.
    /// Désactivée par défaut ; état et volume persistés dans PlayerPrefs.
    /// </summary>
    public sealed class AmbientMusic : MonoBehaviour
    {
        public const string EnabledKey = "kevchess.music";
        public const string VolumeKey = "kevchess.music.volume";
        const float DefaultVolume = 0.5f;

        AudioSource source;
        int rate;
        volatile float targetGain;  // lu par le fil audio
        volatile float currentGain;
        bool enabledCache;
        float volumeCache;

        /// <summary>Crée le lecteur (un seul suffit pour toute l'app).</summary>
        public static AmbientMusic Create(Transform parent = null)
        {
            var go = new GameObject("Ambient Music");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<AmbientMusic>();
        }

        /// <summary>Musique active (persisté sous « kevchess.music », faux par défaut). Fondu d'entrée et de sortie.</summary>
        public bool Enabled
        {
            get => enabledCache;
            set
            {
                enabledCache = value;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplyGain();
            }
        }

        /// <summary>Volume de 0 à 1 (persisté sous « kevchess.music.volume », 0,5 par défaut).</summary>
        public float Volume
        {
            get => volumeCache;
            set
            {
                volumeCache = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, volumeCache);
                PlayerPrefs.Save();
                ApplyGain();
            }
        }

        void Awake()
        {
            enabledCache = PlayerPrefs.GetInt(EnabledKey, 0) == 1;
            volumeCache = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));

            rate = AudioSettings.outputSampleRate;
            if (rate <= 0) rate = 48000;
            synth = new Synth(rate, Environment.TickCount);

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.volume = 1f;
            // Clip en flux : le contenu est produit à la demande par le synthétiseur, la « boucle » du clip n'existe pas.
            source.clip = AudioClip.Create("kevchess_ambient", rate * 8, 2, rate, true, OnRead);
            ApplyGain();
        }

        void ApplyGain()
        {
            targetGain = enabledCache ? volumeCache : 0f;
            if (source != null && enabledCache && !source.isPlaying) source.Play();
        }

        void Update()
        {
            // Une fois le fondu de sortie terminé, on met en pause : plus aucun calcul.
            if (!enabledCache && source.isPlaying && currentGain < 0.0005f) source.Pause();
        }

        void OnDestroy()
        {
            if (source != null && source.clip != null) Destroy(source.clip);
        }

        Synth synth;

        void OnRead(float[] data)
        {
            currentGain = synth.Render(data, currentGain, targetGain);
        }

        // ------------------------------------------------------------------ synthétiseur (fil audio)

        sealed class Synth
        {
            const int TableSize = 2048;
            const int Voices = 5;            // basse + 4 voix
            const float Attack = 3.0f;       // secondes
            const float Release = 4.5f;

            // Accords (notes MIDI) : basse puis voix ; voicings proches pour des transitions douces.
            static readonly int[][] Chords =
            {
                new[] { 38, 57, 60, 64, 65 }, // Rém9
                new[] { 34, 57, 60, 62, 65 }, // Sib maj9
                new[] { 41, 55, 60, 64, 69 }, // Fa maj9
                new[] { 43, 57, 58, 62, 65 }, // Solm9
                new[] { 36, 55, 60, 62, 64 }, // Do add9
                new[] { 45, 55, 60, 64, 67 }, // Lam7
            };

            readonly float[] table = new float[TableSize];
            readonly int rate;
            readonly System.Random rng;

            // Deux jeux de notes : l'un monte pendant que l'autre s'éteint (fondu enchaîné).
            readonly float[] freq = new float[Voices * 2];
            readonly float[] amp = new float[Voices * 2];
            readonly float[] target = new float[Voices * 2];
            readonly double[] phaseA = new double[Voices * 2];
            readonly double[] phaseB = new double[Voices * 2];
            readonly float[] pan = new float[Voices * 2];
            readonly float[] lfoRate = new float[Voices * 2];
            readonly float[] lfoPhase = new float[Voices * 2];
            readonly float[] lfoGain = new float[Voices * 2];
            int activeSet;
            int chord = -1;
            double samplesToNextChord;

            // Cloche de cristal occasionnelle.
            float bellFreq, bellAmp;
            double bellPhase, samplesToNextBell;

            // Filtres et écho.
            float lpL1, lpR1, lpL2, lpR2;
            float filterLfo;
            readonly float[] delayL, delayR;
            int delayPos;
            readonly int delayLenL, delayLenR;
            float dampL, dampR;

            public Synth(int sampleRate, int seed)
            {
                rate = sampleRate;
                rng = new System.Random(seed);
                for (int i = 0; i < TableSize; i++)
                {
                    double x = 2.0 * Math.PI * i / TableSize;
                    // Onde douce : quelques harmoniques décroissantes (timbre de nappe chaude).
                    table[i] = (float)((Math.Sin(x) + 0.32 * Math.Sin(2 * x) + 0.16 * Math.Sin(3 * x) + 0.06 * Math.Sin(5 * x)) / 1.4);
                }
                for (int v = 0; v < Voices * 2; v++)
                {
                    pan[v] = v % Voices == 0 ? 0f : (float)(rng.NextDouble() * 1.2 - 0.6);
                    lfoRate[v] = 0.04f + (float)rng.NextDouble() * 0.09f;
                    lfoPhase[v] = (float)(rng.NextDouble() * Math.PI * 2);
                }
                delayLenL = (int)(rate * 0.43);
                delayLenR = (int)(rate * 0.61);
                delayL = new float[delayLenL];
                delayR = new float[delayLenR];
                samplesToNextBell = rate * 4.0;
                NextChord();
            }

            static float MidiToHz(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

            void NextChord()
            {
                int next;
                do next = rng.Next(Chords.Length); while (next == chord);
                chord = next;
                // Éteint l'ancien jeu, prépare le nouveau.
                for (int v = 0; v < Voices; v++) target[activeSet * Voices + v] = 0f;
                activeSet = 1 - activeSet;
                for (int v = 0; v < Voices; v++)
                {
                    int i = activeSet * Voices + v;
                    freq[i] = MidiToHz(Chords[chord][v]);
                    target[i] = v == 0 ? 0.55f : 0.32f + (float)rng.NextDouble() * 0.12f;
                    phaseA[i] = rng.NextDouble();
                    phaseB[i] = rng.NextDouble();
                }
                samplesToNextChord = rate * (9.0 + rng.NextDouble() * 5.0);
            }

            void MaybeBell()
            {
                // Note aiguë tirée de l'accord, une ou deux octaves au-dessus.
                int note = Chords[chord][1 + rng.Next(Voices - 1)] + (rng.NextDouble() < 0.6 ? 12 : 24);
                bellFreq = MidiToHz(note);
                bellAmp = 0.10f + (float)rng.NextDouble() * 0.06f;
                bellPhase = 0;
                samplesToNextBell = rate * (3.0 + rng.NextDouble() * 6.0);
            }

            public float Render(float[] data, float gain, float targetGain)
            {
                int frames = data.Length / 2;
                float dt = 1f / rate;
                float attackK = 1f - Mathf.Exp(-dt / (Attack / 3f));
                float releaseK = 1f - Mathf.Exp(-dt / (Release / 3f));
                float gainK = 1f - Mathf.Exp(-dt / 0.6f);
                float bellDecay = Mathf.Exp(-dt / 1.6f);
                const float detune = 1.0035f; // ~6 cents entre les deux oscillateurs d'une voix

                // Filtre : coupure qui respire lentement entre ~600 et ~1500 Hz (calculée par bloc).
                filterLfo += frames * dt * 0.035f * 2f * Mathf.PI;
                if (filterLfo > Mathf.PI * 2f) filterLfo -= Mathf.PI * 2f;
                float cutoff = 1050f + 450f * Mathf.Sin(filterLfo);
                float lpK = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * dt);

                // Respiration lente de chaque voix (également calculée par bloc).
                for (int v = 0; v < Voices * 2; v++)
                {
                    lfoPhase[v] += lfoRate[v] * frames * dt * 2f * Mathf.PI;
                    if (lfoPhase[v] > Mathf.PI * 2f) lfoPhase[v] -= Mathf.PI * 2f;
                    lfoGain[v] = 0.75f + 0.25f * Mathf.Sin(lfoPhase[v]);
                }

                for (int n = 0; n < frames; n++)
                {
                    if (--samplesToNextChord <= 0) NextChord();
                    if (--samplesToNextBell <= 0) MaybeBell();

                    float l = 0f, r = 0f;
                    for (int v = 0; v < Voices * 2; v++)
                    {
                        float a = amp[v];
                        float tgt = target[v];
                        a += (tgt - a) * (tgt > a ? attackK : releaseK);
                        amp[v] = a;
                        if (a < 1e-5f) continue;

                        double inc = freq[v] * dt;
                        phaseA[v] += inc;
                        phaseB[v] += inc * detune;
                        if (phaseA[v] >= 1.0) phaseA[v] -= 1.0;
                        if (phaseB[v] >= 1.0) phaseB[v] -= 1.0;
                        float s = table[(int)(phaseA[v] * TableSize) & (TableSize - 1)]
                                + table[(int)(phaseB[v] * TableSize) & (TableSize - 1)];
                        s *= a * lfoGain[v];
                        float p = pan[v];
                        l += s * (1f - p) * 0.5f;
                        r += s * (1f + p) * 0.5f;
                    }

                    // Passe-bas doux à deux étages.
                    lpL1 += (l - lpL1) * lpK; lpL2 += (lpL1 - lpL2) * lpK;
                    lpR1 += (r - lpR1) * lpK; lpR2 += (lpR1 - lpR2) * lpK;
                    l = lpL2; r = lpR2;

                    // Cloche : sinus + partiel, déclin exponentiel, hors filtre.
                    if (bellAmp > 1e-4f)
                    {
                        bellPhase += bellFreq * dt;
                        if (bellPhase >= 1.0) bellPhase -= 1.0;
                        float ph = (float)bellPhase * 2f * Mathf.PI;
                        float b = bellAmp * (Mathf.Sin(ph) + 0.25f * Mathf.Sin(ph * 2.76f));
                        bellAmp *= bellDecay;
                        l += b * 0.8f;
                        r += b * 0.6f;
                    }

                    // Écho stéréo amorti (ping-pong léger) pour l'espace.
                    int iL = delayPos % delayLenL, iR = delayPos % delayLenR;
                    float dl = delayL[iL], dr = delayR[iR];
                    dampL += (dl - dampL) * 0.25f;
                    dampR += (dr - dampR) * 0.25f;
                    delayL[iL] = l + dampR * 0.42f;
                    delayR[iR] = r + dampL * 0.42f;
                    delayPos++;
                    if (delayPos >= delayLenL * delayLenR) delayPos = 0;
                    l += dampL * 0.35f;
                    r += dampR * 0.35f;

                    gain += (targetGain - gain) * gainK;
                    float g = gain * 0.22f;
                    l *= g; r *= g;
                    // Écrêtage doux par sécurité.
                    data[2 * n] = l / (1f + Mathf.Abs(l));
                    data[2 * n + 1] = r / (1f + Mathf.Abs(r));
                }
                return gain;
            }
        }
    }
}
