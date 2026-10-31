using KevChess.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KevChess.Game.CameraRig
{
    /// <summary>
    /// Caméra en orbite autour du plateau : glisser pour tourner, pincer/molette pour zoomer,
    /// recentrage animé sur la vue d'un camp. Léger mouvement de respiration et parallaxe
    /// à l'inclinaison du téléphone pour renforcer la sensation de 3D.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class OrbitCamera : MonoBehaviour
    {
        public Vector3 Target = new Vector3(0f, 0.2f, 0f);
        public float DefaultPitch = 52f;
        /// <summary>Portrait : vue un peu plus plongeante, le plateau occupe mieux la hauteur.</summary>
        public float PortraitPitch = 60f;
        public float MinPitch = 14f;
        public float MaxPitch = 86f;
        public float MinDistance = 5f;
        public float MaxDistance = 30f;

        /// <summary>Zones d'écran (viewport, origine en bas) laissées libres par l'interface.</summary>
        public Rect PortraitFrame = new Rect(0.02f, 0.10f, 0.96f, 0.71f);
        public Rect LandscapeFrame = new Rect(0.05f, 0.11f, 0.90f, 0.72f);
        public float OrbitSensitivity = 0.22f;
        public float Smoothing = 10f;
        public bool Parallax = true;

        // Points à garder à l'écran : coins du plateau (coordonnées comprises) et sommets des pièces des rangées du fond.
        static readonly Vector3[] FitPoints =
        {
            new Vector3(-4.35f, 0f, -4.35f), new Vector3(4.35f, 0f, -4.35f),
            new Vector3(-4.35f, 0f, 4.35f), new Vector3(4.35f, 0f, 4.35f),
            new Vector3(-3.5f, 1.15f, -3.5f), new Vector3(3.5f, 1.15f, -3.5f),
            new Vector3(-3.5f, 1.15f, 3.5f), new Vector3(3.5f, 1.15f, 3.5f),
        };

        Camera cam;
        float yaw, pitch, distance;
        float targetYaw, targetPitch, targetDistance;
        Vector3 focusOffset, targetFocusOffset;
        Vector2 tilt, tiltBaseline;
        bool sensorEnabled;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            yaw = targetYaw = 0f;
            pitch = targetPitch = CurrentDefaultPitch;
            distance = targetDistance = FitView(targetYaw, targetPitch, out targetFocusOffset);
            focusOffset = targetFocusOffset;
            Apply(0f);
        }

        void OnEnable()
        {
            if (Parallax && Accelerometer.current != null)
            {
                InputSystem.EnableDevice(Accelerometer.current);
                sensorEnabled = true;
            }
        }

        void OnDisable()
        {
            if (sensorEnabled && Accelerometer.current != null) InputSystem.DisableDevice(Accelerometer.current);
            sensorEnabled = false;
        }

        public void Orbit(Vector2 pixelDelta)
        {
            float scale = 1080f / Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
            targetYaw += pixelDelta.x * OrbitSensitivity * scale;
            targetPitch = Mathf.Clamp(targetPitch - pixelDelta.y * OrbitSensitivity * scale, MinPitch, MaxPitch);
        }

        public void Zoom(float factor)
        {
            if (factor <= 0f) return;
            targetDistance = Mathf.Clamp(targetDistance / factor, MinDistance, MaxDistance);
        }

        /// <summary>Recentre la caméra derrière le camp donné (animé).</summary>
        public void ResetView(PieceColor side)
        {
            float wanted = side == PieceColor.White ? 0f : 180f;
            // Tourner par le chemin le plus court.
            targetYaw = yaw + Mathf.DeltaAngle(yaw, wanted);
            targetPitch = CurrentDefaultPitch;
            targetDistance = FitView(targetYaw, targetPitch, out targetFocusOffset);
        }

        public void SnapToView(PieceColor side)
        {
            ResetView(side);
            yaw = targetYaw;
            pitch = targetPitch;
            distance = targetDistance;
            focusOffset = targetFocusOffset;
            Apply(0f);
        }

        bool IsPortrait => cam != null && cam.aspect < 1f;
        float CurrentDefaultPitch => IsPortrait ? PortraitPitch : DefaultPitch;

        /// <summary>
        /// Cadrage exact : distance la plus courte qui garde le plateau dans la zone libre de l'écran,
        /// et décalage du point visé pour l'y centrer. Valable en portrait (téléphone) comme en paysage (Mac).
        /// </summary>
        float FitView(float yawDeg, float pitchDeg, out Vector3 offset)
        {
            offset = Vector3.zero;
            if (cam == null) return 12f;
            // Portrait : champ plus large pour garder la perspective marquée de près.
            cam.fieldOfView = IsPortrait ? 52f : 40f;
            var frame = IsPortrait ? PortraitFrame : LandscapeFrame;
            var rot = Quaternion.Euler(pitchDeg, yawDeg, 0f);
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * cam.aspect;

            float d = 12f;
            for (int pass = 0; pass < 3; pass++)
            {
                // Recherche dichotomique de la distance minimale où tout tient dans le cadre.
                float lo = 2f, hi = 60f;
                for (int i = 0; i < 30; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Fits(rot, Target + offset, mid, tanH, tanV, frame, out _, out _)) hi = mid;
                    else lo = mid;
                }
                d = hi;

                // Recentrage vertical dans le cadre (le haut et le bas de l'écran ne sont pas symétriques).
                Fits(rot, Target + offset, d, tanH, tanV, frame, out var min, out var max);
                float shift = (min.y + max.y) * 0.5f - frame.center.y;
                float viewportHeightWorld = 2f * d * tanV;
                offset += rot * Vector3.up * (shift * viewportHeightWorld);
            }
            return Mathf.Clamp(d, MinDistance, MaxDistance);
        }

        static bool Fits(Quaternion rot, Vector3 focus, float distance, float tanH, float tanV, Rect frame,
                         out Vector2 min, out Vector2 max)
        {
            var camPos = focus + rot * new Vector3(0f, 0f, -distance);
            var inv = Quaternion.Inverse(rot);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in FitPoints)
            {
                var local = inv * (p - camPos);
                if (local.z <= 0.1f) return false;
                var v = new Vector2(0.5f + 0.5f * local.x / (local.z * tanH), 0.5f + 0.5f * local.y / (local.z * tanV));
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            return min.x >= frame.xMin && max.x <= frame.xMax && max.y - min.y <= frame.height;
        }

        int lastWidth, lastHeight;

        void LateUpdate()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
            {
                lastWidth = Screen.width;
                lastHeight = Screen.height;
                // Rotation de l'écran : nouveau cadrage complet.
                targetPitch = CurrentDefaultPitch;
                targetDistance = FitView(targetYaw, targetPitch, out targetFocusOffset);
            }

            float k = 1f - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            distance = Mathf.Lerp(distance, targetDistance, k);
            focusOffset = Vector3.Lerp(focusOffset, targetFocusOffset, k);
            UpdateTilt();
            Apply(Time.unscaledTime);
        }

        void UpdateTilt()
        {
            if (!sensorEnabled || Accelerometer.current == null) return;
            Vector3 g = Accelerometer.current.acceleration.ReadValue();
            var raw = new Vector2(g.x, g.y);
            // La référence suit lentement la tenue du téléphone : seuls les mouvements récents comptent.
            tiltBaseline = Vector2.Lerp(tiltBaseline, raw, Time.unscaledDeltaTime * 0.6f);
            tilt = Vector2.Lerp(tilt, Vector2.ClampMagnitude(raw - tiltBaseline, 0.35f), Time.unscaledDeltaTime * 6f);
        }

        void Apply(float time)
        {
            // Respiration très légère : la scène ne paraît jamais figée.
            float breatheYaw = Mathf.Sin(time * 0.23f) * 0.6f;
            float breathePitch = Mathf.Sin(time * 0.31f + 1.3f) * 0.4f;
            float y = yaw + breatheYaw + tilt.x * 10f;
            float p = Mathf.Clamp(pitch + breathePitch - tilt.y * 7f, MinPitch - 3f, MaxPitch);

            var rot = Quaternion.Euler(p, y, 0f);
            transform.position = Target + focusOffset + rot * new Vector3(0f, 0f, -distance);
            transform.rotation = rot;
        }
    }
}
