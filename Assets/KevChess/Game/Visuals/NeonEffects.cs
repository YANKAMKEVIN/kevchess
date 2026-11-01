using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Effets lumineux ponctuels sans asset : bris de cristal, flash, onde d'atterrissage,
    /// onde de choc et pluie d'étincelles (mat).
    /// Quatre systèmes de particules partagés (créés à la demande) sont réutilisés via
    /// <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/> : aucun objet n'est instancié par effet.
    /// </summary>
    public static class NeonEffects
    {
        static GameObject _root;
        static ParticleSystem _sparks, _shards, _rings;
        static NeonFxRunner _runner;
        static Light _flashLight;
        static Coroutine _pulse;

        /// <summary>La pièce se brise en éclats de lumière.</summary>
        public static void Shatter(Vector3 basePosition, float height, Color color)
        {
            Ensure();
            var ep = new ParticleSystem.EmitParams { startColor = Brighten(color), applyShapeToPosition = true };
            // Éclats répartis sur la hauteur de la pièce.
            for (int i = 0; i < 4; i++)
            {
                ep.position = basePosition + Vector3.up * (height * (0.15f + 0.22f * i));
                _shards.Emit(ep, 6);
            }
            var sp = new ParticleSystem.EmitParams { startColor = Brighten(color), applyShapeToPosition = true, position = basePosition + Vector3.up * height * 0.5f };
            _sparks.Emit(sp, 28);
            // Halo bref au cœur de l'explosion.
            var glow = new ParticleSystem.EmitParams
            {
                position = basePosition + Vector3.up * height * 0.45f,
                startColor = color,
                startSize = 1.6f,
                startLifetime = 0.35f,
                velocity = Vector3.zero,
            };
            _sparks.Emit(glow, 1);
            Ring(basePosition, color, 1.8f, 0.6f);
            Pulse(basePosition + Vector3.up * height * 0.5f, color, 5f, 0.35f);
        }

        /// <summary>Flash de lumière (promotion, mise en valeur).</summary>
        public static void Flash(Vector3 basePosition, Color color, float height = 1f)
        {
            Ensure();
            var glow = new ParticleSystem.EmitParams
            {
                position = basePosition + Vector3.up * height * 0.55f,
                startColor = color,
                startSize = 2.6f,
                startLifetime = 0.5f,
                velocity = Vector3.zero,
            };
            _sparks.Emit(glow, 1);
            var sp = new ParticleSystem.EmitParams { startColor = Brighten(color), applyShapeToPosition = true, position = basePosition + Vector3.up * height * 0.5f };
            _sparks.Emit(sp, 36);
            Ring(basePosition, color, 2.4f, 0.7f);
            Ring(basePosition, Color.white, 1.4f, 0.45f);
            Pulse(basePosition + Vector3.up * height * 0.6f, color, 9f, 0.5f);
        }

        /// <summary>Onde discrète quand une pièce se pose.</summary>
        public static void LandingPulse(Vector3 basePosition, Color color)
        {
            Ensure();
            Ring(basePosition, color, 1.1f, 0.4f);
        }

        /// <summary>Onde de choc lumineuse qui balaie le plateau depuis <paramref name="center"/>.</summary>
        public static void Shockwave(Vector3 center, Color color)
        {
            Ensure();
            // Anneaux maillés (les grandes particules d'anneau se remplissent à cause des mipmaps vues de biais).
            _runner.StartCoroutine(ExpandingRing(center, Color.Lerp(color, Color.white, 0.5f), 6.5f, 0.55f, 0.32f));
            _runner.StartCoroutine(ExpandingRing(center, color, 11f, 1.1f, 0.5f));
            Ring(center, Color.white, 2.2f, 0.4f);
            // Gerbe d'étincelles rasante, projetée vers l'extérieur.
            var ep = new ParticleSystem.EmitParams { startColor = Brighten(color), startLifetime = 0.9f };
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f + Random.Range(-0.05f, 0.05f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ep.position = center + Vector3.up * 0.08f + dir * 0.2f;
                ep.velocity = dir * Random.Range(3.5f, 5.5f) + Vector3.up * Random.Range(0.3f, 1.2f);
                ep.startSize = Random.Range(0.05f, 0.11f);
                _sparks.Emit(ep, 1);
            }
            Pulse(center + Vector3.up * 1.2f, color, 6f, 0.7f, 8f);
        }

        /// <summary>Anneau lumineux plat qui s'élargit en s'estompant (maillage refait à chaque image).</summary>
        static IEnumerator ExpandingRing(Vector3 center, Color color, float maxRadius, float duration, float band)
        {
            const int segments = 96;
            var go = new GameObject("Shockwave");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(center.x, 0.035f, center.z);
            var mesh = new Mesh { name = "KevChess_Shockwave" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(NeonMaterials.Trail) { name = "KevChess_Shockwave" };
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var verts = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                uvs[2 * i] = new Vector2(i / (float)segments, 0f);
                uvs[2 * i + 1] = new Vector2(i / (float)segments, 1f);
                if (i == segments) break;
                int b = 2 * i;
                tris[6 * i] = b; tris[6 * i + 1] = b + 1; tris[6 * i + 2] = b + 2;
                tris[6 * i + 3] = b + 1; tris[6 * i + 4] = b + 3; tris[6 * i + 5] = b + 2;
            }
            mesh.vertices = verts;
            mesh.uv = uvs;
            var colors = new Color[verts.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors;
            mesh.triangles = tris;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                float r = Mathf.Lerp(0.3f, maxRadius, e);
                float w = band * Mathf.Lerp(1f, 0.55f, k);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts[2 * i] = d * (r - w * 0.5f);
                    verts[2 * i + 1] = d * (r + w * 0.5f);
                }
                mesh.vertices = verts;
                mesh.RecalculateBounds();
                float fade = (1f - k) * (1f - k);
                mat.SetColor(NeonMaterials.BaseColorProperty, color * (3.2f * fade));
                yield return null;
            }
            Object.Destroy(mat);
            Object.Destroy(mesh);
            Object.Destroy(go);
        }

        /// <summary>Pluie d'étincelles sur tout le plateau pendant <paramref name="duration"/> secondes.</summary>
        public static void SparkRain(Color color, float duration, float halfExtent = 4.6f)
        {
            Ensure();
            _runner.StartCoroutine(Rain(color, duration, halfExtent));
        }

        static IEnumerator Rain(Color color, float duration, float halfExtent)
        {
            const float perSecond = 170f;
            float t = 0f, pending = 0f;
            var ep = new ParticleSystem.EmitParams();
            while (t < duration)
            {
                t += Time.deltaTime;
                pending += perSecond * Time.deltaTime * (1f - 0.5f * t / duration);
                for (; pending >= 1f; pending -= 1f)
                {
                    ep.position = new Vector3(Random.Range(-halfExtent, halfExtent), Random.Range(4.5f, 6.5f), Random.Range(-halfExtent, halfExtent));
                    ep.velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-3.2f, -1.8f), Random.Range(-0.3f, 0.3f));
                    ep.startColor = Brighten(Color.Lerp(color, Color.white, Random.Range(0f, 0.45f)));
                    ep.startSize = Random.Range(0.05f, 0.13f);
                    ep.startLifetime = Random.Range(1.5f, 2.2f);
                    _sparks.Emit(ep, 1);
                }
                yield return null;
            }
        }

        static void Ring(Vector3 position, Color color, float size, float lifetime)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = position + Vector3.up * 0.02f,
                startColor = color,
                startSize = size,
                startLifetime = lifetime,
                velocity = Vector3.zero,
                rotation3D = new Vector3(90f, 0f, 0f),
            };
            _rings.Emit(ep, 1);
        }

        static void Pulse(Vector3 position, Color color, float intensity, float duration, float range = 5f)
        {
            if (_runner == null) return;
            if (_pulse != null) _runner.StopCoroutine(_pulse);
            _pulse = _runner.StartCoroutine(PulseLight(position, color, intensity, duration, range));
        }

        static IEnumerator PulseLight(Vector3 position, Color color, float intensity, float duration, float range)
        {
            _flashLight.range = range;
            _flashLight.transform.position = position;
            _flashLight.color = color;
            _flashLight.enabled = true;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                _flashLight.intensity = intensity * k * k;
                yield return null;
            }
            _flashLight.enabled = false;
            _pulse = null;
        }

        static Color32 Brighten(Color c)
        {
            // Les couleurs de particules sont 8 bits : on éclaircit vers le blanc, l'intensité HDR vient du matériau.
            Color b = Color.Lerp(c, Color.white, 0.25f);
            b.a = 1f;
            return b;
        }

        // ------------------------------------------------------------------ création

        static void Ensure()
        {
            if (_root != null) return;
            _root = new GameObject("KevChess FX");
            _runner = _root.AddComponent<NeonFxRunner>();

            var lightGo = new GameObject("Flash Light");
            lightGo.transform.SetParent(_root.transform, false);
            _flashLight = lightGo.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.range = 5f;
            _flashLight.shadows = LightShadows.None;
            _flashLight.enabled = false;

            _sparks = CreateSystem("Sparks", NeonMaterials.Sparks);
            {
                var main = _sparks.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
                main.gravityModifier = 0.35f;
                main.maxParticles = 700;
                var shape = _sparks.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.18f;
                var limit = _sparks.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 6f;
                limit.dampen = 0.12f;
                var col = _sparks.colorOverLifetime;
                col.enabled = true;
                col.color = FadeGradient();
                var size = _sparks.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            }

            _shards = CreateSystem("Shards", NeonMaterials.Shards);
            {
                var main = _shards.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 1.1f;
                main.maxParticles = 200;
                var shape = _shards.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.12f;
                var rot = _shards.rotationOverLifetime;
                rot.enabled = true;
                rot.separateAxes = true;
                rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
                var col = _shards.colorOverLifetime;
                col.enabled = true;
                col.color = FadeGradient();
                var size = _shards.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                var pr = _shards.GetComponent<ParticleSystemRenderer>();
                pr.renderMode = ParticleSystemRenderMode.Mesh;
                pr.mesh = PieceMeshFactory.Shard();
                pr.alignment = ParticleSystemRenderSpace.Local;
            }

            _rings = CreateSystem("Rings", NeonMaterials.Rings);
            {
                var main = _rings.main;
                main.startLifetime = 0.5f;
                main.startSpeed = 0f;
                main.startSize = 1f;
                main.startRotation3D = true;
                main.maxParticles = 32;
                var shape = _rings.shape;
                shape.enabled = false;
                var col = _rings.colorOverLifetime;
                col.enabled = true;
                col.color = FadeGradient();
                var size = _rings.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.15f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f)));
                var pr = _rings.GetComponent<ParticleSystemRenderer>();
                pr.alignment = ParticleSystemRenderSpace.World; // anneau couché au sol (rotation3D X = 90°)
            }

            _sparks.Play();
            _shards.Play();
            _rings.Play();
        }

        static ParticleSystem CreateSystem(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true; // le système reste « en lecture » sans rien émettre : Emit() suffit
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.enabled = false; // émission uniquement via Emit()
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = material;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.enableGPUInstancing = false;
            pr.sortingFudge = -10f;
            return ps;
        }

        static ParticleSystem.MinMaxGradient FadeGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }
    }
}
