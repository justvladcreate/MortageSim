using DG.Tweening;
using MortgageSim.Player;
using MortgageSim.World;
using UnityEngine;

namespace MortgageSim.CameraTool
{
    /// <summary>Камера от первого лица + «магнит» к монитору компьютера.</summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public FirstPersonPlayer Player { get; private set; }

        private Vector3 _savedPosition;
        private Quaternion _savedRotation;
        private Vector3 _savedLocalPosition;
        private Quaternion _savedLocalRotation;
        private Sequence _sequence;
        private bool _zoomed;

        public bool IsZoomed => _zoomed;

        public void Initialize(Camera cam, FirstPersonPlayer player)
        {
            Camera = cam;
            Player = player;
            cam.fieldOfView = 60f;
        }

        public void MenuView()
        {
            if (Camera == null || Player == null) return;
            Player.SnapCameraToPlayer();
            Camera.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);
        }

        public void EnterComputer(MonitorTerminal monitor)
        {
            if (Camera == null || monitor == null || _zoomed) return;
            _zoomed = true;

            _savedPosition = Camera.transform.position;
            _savedRotation = Camera.transform.rotation;
            _savedLocalPosition = Camera.transform.localPosition;
            _savedLocalRotation = Camera.transform.localRotation;

            Camera.transform.SetParent(null, true);

            Vector3 center = monitor.ScreenTransform.position + monitor.ScreenTransform.forward * 0.03f;
            Vector3 normal = monitor.ScreenTransform.forward;
            float halfFov = Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distanceV = (monitor.ScreenHeight * 0.5f) / Mathf.Max(0.01f, halfFov);
            float distanceH = (monitor.ScreenWidth * 0.5f) / Mathf.Max(0.01f, halfFov * Camera.aspect);
            float distance = Mathf.Max(distanceV, distanceH) * 1.045f;

            Vector3 targetPosition = center + normal * distance;
            Quaternion targetRotation = Quaternion.LookRotation(-normal, Vector3.up);

            _sequence?.Kill();
            _sequence = DOTween.Sequence();
            _sequence.Join(Camera.transform.DOMove(targetPosition, 1.05f).SetEase(Ease.InOutCubic));
            _sequence.Join(Camera.transform.DORotateQuaternion(targetRotation, 1.05f).SetEase(Ease.InOutCubic));
            _sequence.Join(DOTween.To(() => Camera.fieldOfView, f => Camera.fieldOfView = f, 54f, 1.05f)
                .SetEase(Ease.InOutCubic));
        }

        public void ExitComputer()
        {
            if (Camera == null || !_zoomed) return;
            _zoomed = false;
            _sequence?.Kill();

            _sequence = DOTween.Sequence();
            _sequence.Join(Camera.transform.DOMove(_savedPosition, 0.85f).SetEase(Ease.InOutCubic));
            _sequence.Join(Camera.transform.DORotateQuaternion(_savedRotation, 0.85f).SetEase(Ease.InOutCubic));
            _sequence.Join(DOTween.To(() => Camera.fieldOfView, f => Camera.fieldOfView = f, 60f, 0.85f)
                .SetEase(Ease.InOutCubic));
            _sequence.OnComplete(ReturnToPlayer);
        }

        public void ReturnToPlayerImmediate()
        {
            _sequence?.Kill();
            _zoomed = false;
            if (Camera != null && Player != null)
            {
                Camera.transform.SetParent(Player.transform, false);
                Camera.transform.localPosition = new Vector3(0f, 1.66f, 0f);
                Camera.transform.localRotation = _savedLocalRotation;
                Camera.fieldOfView = 60f;
            }
        }

        private void ReturnToPlayer()
        {
            if (Camera == null || Player == null) return;
            Camera.transform.SetParent(Player.transform, false);
            Camera.transform.localPosition = new Vector3(0f, 1.66f, 0f);
            Camera.transform.localRotation = _savedLocalRotation;
            Camera.fieldOfView = 60f;
        }
    }
}
