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
        public float MinPitch = 14f;
        public float MaxPitch = 86f;
        public float MinDistance = 6f;
        public float MaxDistance = 22f;
        public float OrbitSensitivity = 0.22f;
        public float Smoothing = 10f;
        public bool Parallax = true;

        /// <summary>Demi-largeur du plateau cadre compris, pour adapter la distance au format d'écran.</summary>
        public float BoardHalfExtent = 5.2f;

        Camera cam;
        float yaw, pitch, distance;
        float targetYaw, targetPitch, targetDistance;
        Vector2 tilt, tiltBaseline;
        bool sensorEnabled;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            yaw = targetYaw = 0f;
            pitch = targetPitch = DefaultPitch;
            distance = targetDistance = FitDistance();
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
            targetPitch = DefaultPitch;
            targetDistance = FitDistance();
        }

        public void SnapToView(PieceColor side)
        {
            ResetView(side);
            yaw = targetYaw;
            pitch = targetPitch;
            distance = targetDistance;
            Apply(0f);
        }

        /// <summary>Distance pour voir tout le plateau, quel que soit le format (portrait téléphone ou paysage Mac).</summary>
        float FitDistance()
        {
            if (cam == null) return 12f;
            // Portrait (téléphone) : champ plus large pour garder le plateau proche et la perspective marquée.
            cam.fieldOfView = cam.aspect < 1f ? 56f : 40f;
            float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * cam.aspect);
            float limiting = Mathf.Min(vHalf * 1.15f, hHalf);
            return Mathf.Clamp(BoardHalfExtent / Mathf.Tan(limiting) + 1.5f, MinDistance, MaxDistance);
        }

        int lastWidth, lastHeight;

        void LateUpdate()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
            {
                lastWidth = Screen.width;
                lastHeight = Screen.height;
                targetDistance = Mathf.Clamp(Mathf.Max(targetDistance, FitDistance() * 0.8f), MinDistance, MaxDistance);
            }

            float k = 1f - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            pitch = Mathf.Lerp(pitch, targetPitch, k);
            distance = Mathf.Lerp(distance, targetDistance, k);
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
            transform.position = Target + rot * new Vector3(0f, 0f, -distance);
            transform.rotation = rot;
        }
    }
}
