using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals.Fantasy
{
    /// <summary>
    /// Petits effets propres aux personnages (filaments de téléportation, givre, gerbes d'étincelles,
    /// éclair arcanique). Deux systèmes de particules partagés, alimentés par Emit() comme
    /// <see cref="NeonEffects"/> : aucun objet instancié par effet. Le fichier FantasyEffects prévu par
    /// la conception (souffle, éclats vers la jauge…) pourra absorber ces fonctions.
    /// </summary>
    public static class FantasyFx
    {
        static GameObject _root;
        static ParticleSystem _streaks, _motes;
        static NeonFxRunner _runner;
        static LineRenderer _bolt;
        static Material _boltMaterial;

        /// <summary>Filaments de lumière : montants (la pièce se défait) ou descendants (elle se recompose).</summary>
        public static void Filaments(Vector3 basePosition, float height, Color color, bool rising, int count = 46)
        {
            Ensure();
            var ep = new ParticleSystem.EmitParams { startColor = Tint(color, 0.2f) };
            for (int i = 0; i < count; i++)
            {
                Vector2 disc = Random.insideUnitCircle * 0.22f;
                float y = Random.Range(0.05f, height);
                if (rising)
                {
                    ep.position = basePosition + new Vector3(disc.x, y, disc.y);
                    ep.velocity = new Vector3(disc.x * 0.6f, Random.Range(1.4f, 3.2f), disc.y * 0.6f);
                }
                else
                {
                    ep.position = basePosition + new Vector3(disc.x * 1.6f, y + Random.Range(0.6f, 1.4f), disc.y * 1.6f);
                    ep.velocity = new Vector3(-disc.x * 2.2f, -Random.Range(1.8f, 3.4f), -disc.y * 2.2f);
                }
                ep.startLifetime = Random.Range(0.25f, 0.5f);
                ep.startSize = Random.Range(0.025f, 0.05f);
                _streaks.Emit(ep, 1);
            }
        }

        /// <summary>Givre : poussière bleu pâle qui tombe doucement autour de la pièce.</summary>
        public static void Frost(Vector3 basePosition, float height, int count = 30)
        {
            Ensure();
            var ep = new ParticleSystem.EmitParams { startColor = new Color(0.75f, 0.92f, 1f, 1f) };
            for (int i = 0; i < count; i++)
            {
                Vector2 disc = Random.insideUnitCircle.normalized * Random.Range(0.25f, 0.45f);
                ep.position = basePosition + new Vector3(disc.x, Random.Range(0.1f, height * 1.1f), disc.y);
                ep.velocity = new Vector3(disc.x * 0.3f, Random.Range(-0.15f, 0.25f), disc.y * 0.3f);
                ep.startLifetime = Random.Range(0.6f, 1.3f);
                ep.startSize = Random.Range(0.03f, 0.07f);
                _motes.Emit(ep, 1);
            }
        }

        /// <summary>Gerbe d'étincelles (impact d'arme).</summary>
        public static void Burst(Vector3 position, Color color, int count = 24, float speed = 2.2f)
        {
            Ensure();
            var ep = new ParticleSystem.EmitParams { startColor = Tint(color, 0.3f) };
            for (int i = 0; i < count; i++)
            {
                ep.position = position;
                ep.velocity = Random.onUnitSphere * Random.Range(speed * 0.4f, speed);
                ep.startLifetime = Random.Range(0.15f, 0.4f);
                ep.startSize = Random.Range(0.02f, 0.045f);
                _streaks.Emit(ep, 1);
            }
        }

        /// <summary>Éclair arcanique en zigzag de <paramref name="from"/> à <paramref name="to"/>.</summary>
        public static void ArcaneBolt(Vector3 from, Vector3 to, Color color, float duration)
        {
            Ensure();
            _runner.StartCoroutine(BoltRoutine(from, to, color, duration));
        }

        static IEnumerator BoltRoutine(Vector3 from, Vector3 to, Color color, float duration)
        {
            const int points = 12;
            _bolt.positionCount = points;
            _bolt.enabled = true;
            Color c = Tint(color, 0.35f);
            float t = 0f, nextJitter = 0f;
            while (t < duration)
            {
                if (t >= nextJitter)
                {
                    nextJitter = t + 0.03f;
                    Vector3 d = to - from;
                    Vector3 side = Vector3.Cross(d, Vector3.up).normalized;
                    if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                    Vector3 up = Vector3.Cross(side, d).normalized;
                    for (int i = 0; i < points; i++)
                    {
                        float k = i / (float)(points - 1);
                        float amp = Mathf.Sin(k * Mathf.PI) * 0.09f;
                        _bolt.SetPosition(i, from + d * k + side * Random.Range(-amp, amp) + up * Random.Range(-amp, amp));
                    }
                }
                float fade = 1f - Mathf.Clamp01(t / duration);
                float w = 0.05f * (0.4f + 0.6f * fade);
                _bolt.startWidth = w;
                _bolt.endWidth = w * 0.6f;
                Color cc = c;
                cc.a = fade;
                _bolt.startColor = cc;
                _bolt.endColor = cc;
                t += Time.deltaTime;
                yield return null;
            }
            _bolt.enabled = false;
        }

        static Color32 Tint(Color c, float towardWhite)
        {
            Color b = Color.Lerp(c, Color.white, towardWhite);
            b.a = 1f;
            return b;
        }

        // ------------------------------------------------------------------ création

        static void Ensure()
        {
            if (_root != null) return;
            _root = new GameObject("KevChess Fantasy FX");
            _runner = _root.AddComponent<NeonFxRunner>();

            _streaks = CreateSystem("Streaks");
            {
                var main = _streaks.main;
                main.gravityModifier = 0f;
                main.maxParticles = 600;
                var pr = _streaks.GetComponent<ParticleSystemRenderer>();
                pr.renderMode = ParticleSystemRenderMode.Stretch;
                pr.velocityScale = 0.09f;
                pr.lengthScale = 1.5f;
            }
            _motes = CreateSystem("Frost");
            {
                var main = _motes.main;
                main.gravityModifier = 0.03f;
                main.maxParticles = 300;
            }

            var boltGo = new GameObject("Arcane Bolt");
            boltGo.transform.SetParent(_root.transform, false);
            _bolt = boltGo.AddComponent<LineRenderer>();
            _boltMaterial = NeonMaterials.CreateAdditive("KevChess_ArcaneBolt", null, Color.white * 4f, 41);
            _bolt.sharedMaterial = _boltMaterial;
            _bolt.useWorldSpace = true;
            _bolt.textureMode = LineTextureMode.Stretch;
            _bolt.shadowCastingMode = ShadowCastingMode.Off;
            _bolt.receiveShadows = false;
            _bolt.numCapVertices = 2;
            _bolt.enabled = false;
        }

        static ParticleSystem CreateSystem(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = NeonMaterials.Sparks;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
