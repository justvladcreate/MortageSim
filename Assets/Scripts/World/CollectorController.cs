using System;
using DG.Tweening;
using MortgageSim.Core;
using MortgageSim.Interaction;
using UnityEngine;

namespace MortgageSim.World
{
    /// <summary>
    /// Коллектор: приходит из тамбура за картиной-дверью, стучит,
    /// входит в квартиру, разговаривает и уходит после оплаты.
    /// Вся анимация — DOTween.
    /// </summary>
    public sealed class CollectorController : MonoBehaviour
    {
        public bool IsAtDoor { get; private set; }
        public bool IsInside { get; private set; }
        public bool IsLeaving { get; private set; }
        public bool IsActive { get; private set; }

        private GameManager _game;
        private Bounds _doorBounds;

        private Transform _visual;
        private Transform _leftLeg;
        private Transform _rightLeg;
        private Transform _leftArm;
        private Transform _rightArm;

        private Vector3 _outsideStart;
        private Vector3 _doorSpot;
        private Vector3 _insideSpot;

        private Sequence _moveSequence;
        private Sequence _knockSequence;
        private Tween _walkTween;
        private Tween _idleTween;
        private Material _suitMaterial;
        private Material _skinMaterial;

        public static CollectorController Create(GameManager game, Bounds doorBounds)
        {
            var root = new GameObject("Collector");
            var collector = root.AddComponent<CollectorController>();
            collector._game = game;
            collector._doorBounds = doorBounds;
            collector.Build();
            collector.HideImmediate();
            return collector;
        }

        private void Build()
        {
            _suitMaterial = RuntimeAssets.Suit;
            _skinMaterial = RuntimeAssets.Lit("CollectorSkin", new Color(0.72f, 0.54f, 0.43f), 0.42f);
            Material shirt = RuntimeAssets.Lit("CollectorShirt", new Color(0.82f, 0.82f, 0.78f), 0.22f);
            Material tie = RuntimeAssets.Lit("CollectorTie", new Color(0.52f, 0.06f, 0.07f), 0.35f, 0f,
                new Color(0.16f, 0.01f, 0.01f));
            Material eye = RuntimeAssets.Lit("CollectorEye", new Color(0.04f, 0.04f, 0.05f), 0.75f);
            Material leather = RuntimeAssets.Lit("CollectorCase", new Color(0.22f, 0.13f, 0.08f), 0.3f);

            _visual = new GameObject("Collector_Visual").transform;
            _visual.SetParent(transform, false);
            _visual.localPosition = Vector3.zero;

            // Ноги на шарнирах, чтобы шагать.
            _leftLeg = CreateLimb("Leg_L", new Vector3(-0.14f, 0.80f, 0f), new Vector3(0.16f, 0.78f, 0.20f));
            _rightLeg = CreateLimb("Leg_R", new Vector3(0.14f, 0.80f, 0f), new Vector3(0.16f, 0.78f, 0.20f));

            // Корпус и плечи.
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Torso", _visual,
                new Vector3(0f, 1.10f, 0f), new Vector3(0.62f, 0.74f, 0.36f), _suitMaterial);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Shirt", _visual,
                new Vector3(0f, 1.24f, 0.186f), new Vector3(0.22f, 0.40f, 0.012f), shirt);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Tie", _visual,
                new Vector3(0f, 1.21f, 0.198f), new Vector3(0.075f, 0.34f, 0.014f), tie);

            // Руки.
            _leftArm = CreateLimb("Arm_L", new Vector3(-0.37f, 1.41f, 0f), new Vector3(0.13f, 0.68f, 0.16f));
            _rightArm = CreateLimb("Arm_R", new Vector3(0.37f, 1.41f, 0f), new Vector3(0.13f, 0.68f, 0.16f));

            // Голова, шляпа и глаза.
            RuntimeAssets.Primitive(PrimitiveType.Sphere, "Head", _visual,
                new Vector3(0f, 1.62f, 0f), Vector3.one * 0.27f, _skinMaterial);
            RuntimeAssets.Primitive(PrimitiveType.Cylinder, "Hat_Brim", _visual,
                new Vector3(0f, 1.755f, 0f), new Vector3(0.78f, 0.012f, 0.78f), _suitMaterial);
            RuntimeAssets.Primitive(PrimitiveType.Cylinder, "Hat_Top", _visual,
                new Vector3(0f, 1.815f, 0f), new Vector3(0.52f, 0.075f, 0.52f), _suitMaterial);
            RuntimeAssets.Primitive(PrimitiveType.Sphere, "Eye_L", _visual,
                new Vector3(-0.068f, 1.64f, 0.125f), Vector3.one * 0.045f, eye);
            RuntimeAssets.Primitive(PrimitiveType.Sphere, "Eye_R", _visual,
                new Vector3(0.068f, 1.64f, 0.125f), Vector3.one * 0.045f, eye);

            // Портфель в правой руке.
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Briefcase", _visual,
                new Vector3(0.47f, 0.66f, 0.03f), new Vector3(0.34f, 0.24f, 0.13f), leather);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Briefcase_Handle", _visual,
                new Vector3(0.47f, 0.795f, 0.03f), new Vector3(0.13f, 0.045f, 0.03f), RuntimeAssets.Metal);

            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.3f;

            gameObject.AddComponent<CollectorInteractable>();

            _doorSpot = new Vector3(_doorBounds.center.x, 0f, _doorBounds.center.z - 0.36f);
            _outsideStart = new Vector3(_doorBounds.center.x, 0f, _doorBounds.center.z - 1.45f);
            _insideSpot = new Vector3(_doorBounds.center.x, 0f, _doorBounds.center.z + 1.5f);

            transform.position = _outsideStart;
        }

        private Transform CreateLimb(string name, Vector3 pivot, Vector3 size)
        {
            var pivotGo = new GameObject(name);
            pivotGo.transform.SetParent(_visual, false);
            pivotGo.transform.localPosition = pivot;

            RuntimeAssets.Primitive(PrimitiveType.Cube, name + "_Mesh", pivotGo.transform,
                new Vector3(0f, -size.y * 0.5f, 0f), size, _suitMaterial);

            return pivotGo.transform;
        }

        public void BeginArrival(Action onAtDoor)
        {
            KillTweens();
            IsActive = true;
            IsAtDoor = false;
            IsInside = false;
            IsLeaving = false;
            gameObject.SetActive(true);
            transform.position = _outsideStart;
            transform.rotation = Quaternion.identity;

            StartWalking();

            _moveSequence = DOTween.Sequence();
            _moveSequence.Append(transform.DOMove(_doorSpot, 2.05f).SetEase(Ease.Linear));
            _moveSequence.AppendCallback(() =>
            {
                StopWalking();
                IsAtDoor = true;
                Knock();
                onAtDoor?.Invoke();
            });
        }

        public void Enter(Action onEntered)
        {
            if (!IsAtDoor || IsInside) return;
            IsAtDoor = false;

            _moveSequence?.Kill();
            _knockSequence?.Kill();
            SetLimbRotation(_rightArm, 0f);
            StartWalking();

            _moveSequence = DOTween.Sequence();
            _moveSequence.Append(transform.DOMove(_insideSpot, 1.15f).SetEase(Ease.OutQuad));
            _moveSequence.AppendCallback(() =>
            {
                StopWalking();
                IsInside = true;
                FacePlayer();
                StartIdle();
                onEntered?.Invoke();
            });
        }

        public void Leave(Action onLeft)
        {
            if (IsLeaving) return;
            if (!IsInside)
            {
                HideImmediate();
                onLeft?.Invoke();
                return;
            }

            IsInside = false;
            IsLeaving = true;
            _idleTween?.Kill();
            StartWalking();

            _moveSequence?.Kill();
            _moveSequence = DOTween.Sequence();
            _moveSequence.Append(transform.DOMove(_doorSpot, 0.95f).SetEase(Ease.InOutQuad));
            _moveSequence.Append(transform.DOMove(_outsideStart, 1.25f).SetEase(Ease.InQuad));
            _moveSequence.AppendCallback(() =>
            {
                StopWalking();
                IsLeaving = false;
                IsActive = false;
                gameObject.SetActive(false);
                onLeft?.Invoke();
            });
        }

        public void HideImmediate()
        {
            KillTweens();
            IsActive = false;
            IsAtDoor = false;
            IsInside = false;
            IsLeaving = false;
            gameObject.SetActive(false);
        }

        private void FacePlayer()
        {
            if (_game == null || _game.Player == null) return;
            Vector3 toPlayer = _game.Player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f) return;
            transform.DORotateQuaternion(Quaternion.LookRotation(toPlayer.normalized), 0.45f)
                .SetEase(Ease.OutQuad);
        }

        private void StartWalking()
        {
            _walkTween?.Kill();
            _walkTween = DOTween.To(() => 0f, t =>
                {
                    float angle = Mathf.Sin(t * Mathf.PI * 2f) * 27f;
                    SetLimbRotation(_leftLeg, angle);
                    SetLimbRotation(_rightLeg, -angle);
                    SetLimbRotation(_leftArm, -angle * 0.55f);
                    SetLimbRotation(_rightArm, angle * 0.55f);
                    if (_visual != null)
                        _visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * 0.025f, 0f);
                }, 1f, 0.92f)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        }

        private void StopWalking()
        {
            _walkTween?.Kill();
            _walkTween = null;
            SetLimbRotation(_leftLeg, 0f);
            SetLimbRotation(_rightLeg, 0f);
            SetLimbRotation(_leftArm, 0f);
            SetLimbRotation(_rightArm, 0f);
            if (_visual != null) _visual.localPosition = Vector3.zero;
        }

        private void StartIdle()
        {
            _idleTween?.Kill();
            _idleTween = DOTween.To(() => 0f, t =>
                {
                    if (_visual == null) return;
                    _visual.localPosition = new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.008f, 0f);
                    _visual.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * Mathf.PI * 2f) * 2.5f, 0f);
                }, 1f, 3.2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Restart);
        }

        private static void SetLimbRotation(Transform limb, float xAngle)
        {
            if (limb != null) limb.localRotation = Quaternion.Euler(xAngle, 0f, 0f);
        }

        private void Knock()
        {
            _knockSequence?.Kill();
            _idleTween?.Kill();
            _knockSequence = DOTween.Sequence();
            for (int i = 0; i < 3; i++)
            {
                _knockSequence.Append(_rightArm.DORotate(new Vector3(-72f, 0f, 0f), 0.13f).SetEase(Ease.OutQuad));
                _knockSequence.Append(_rightArm.DORotate(new Vector3(-18f, 0f, 0f), 0.11f).SetEase(Ease.InQuad));
                _knockSequence.AppendInterval(0.16f);
            }
            _knockSequence.OnComplete(() => SetLimbRotation(_rightArm, 0f));
        }

        private void KillTweens()
        {
            _moveSequence?.Kill();
            _knockSequence?.Kill();
            _walkTween?.Kill();
            _idleTween?.Kill();
            _moveSequence = null;
            _knockSequence = null;
            _walkTween = null;
            _idleTween = null;
            if (_visual != null)
            {
                _visual.localPosition = Vector3.zero;
                _visual.localRotation = Quaternion.identity;
            }
        }

        private void OnDestroy()
        {
            KillTweens();
        }
    }
}
