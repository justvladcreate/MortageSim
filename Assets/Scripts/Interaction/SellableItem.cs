using System.Collections.Generic;
using DG.Tweening;
using MortgageSim.Core;
using UnityEngine;

namespace MortgageSim.Interaction
{
    /// <summary>
    /// Любой предмет мебели, который можно продать. Цена зависит от объёма
    /// ограничивающего бокса: чем больше вещь — тем дороже.
    /// </summary>
    public sealed class SellableItem : Interactable
    {
        public string DisplayName;
        public int Price;
        public float Volume;
        public bool Sold;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Color> _baseEmission = new List<Color>();
        private Tween _hoverTween;

        public void Initialize(string displayName, int price, float volume)
        {
            DisplayName = displayName;
            Price = price;
            Volume = volume;

            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                _renderers.Add(r);
                var mat = r.material;
                mat.EnableKeyword("_EMISSION");
                _materials.Add(mat);
                _baseEmission.Add(mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black);
            }
        }

        public override string GetPrompt()
        {
            return "Продать «" + DisplayName + "» за " + GameBalance.Money(Price) + "   [ЛКМ]";
        }

        public override void Interact()
        {
            if (Sold) return;
            var game = GameManager.Instance;
            if (game == null) return;
            game.UI.ShowSellConfirm(this);
        }

        public override void OnHoverEnter()
        {
            if (Sold) return;
            _hoverTween?.Kill();
            _hoverTween = DOTween.To(() => 0f, t =>
            {
                for (int i = 0; i < _materials.Count; i++)
                {
                    if (_materials[i] == null) continue;
                    _materials[i].SetColor("_EmissionColor", _baseEmission[i] + new Color(0.20f, 0.30f, 0.06f) * t);
                }
            }, 1f, 0.18f).SetEase(Ease.OutQuad);
        }

        public override void OnHoverExit()
        {
            _hoverTween?.Kill();
            _hoverTween = DOTween.To(() => 1f, t =>
            {
                for (int i = 0; i < _materials.Count; i++)
                {
                    if (_materials[i] == null) continue;
                    _materials[i].SetColor("_EmissionColor", _baseEmission[i] + new Color(0.20f, 0.30f, 0.06f) * t);
                }
            }, 0f, 0.2f).SetEase(Ease.OutQuad);
        }

        public void Sell()
        {
            if (Sold) return;
            Sold = true;

            var game = GameManager.Instance;
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            if (game != null)
            {
                game.OnItemSold(this);
                game.Terminal.LogCommand("sell \"" + DisplayName + "\"  # +" + GameBalance.Money(Price));
            }

            _hoverTween?.Kill();
            var sequence = DOTween.Sequence();
            sequence.Join(transform.DOScale(Vector3.zero, 0.38f).SetEase(Ease.InBack));
            sequence.Join(transform.DORotate(new Vector3(0f, 180f, 0f), 0.38f, RotateMode.FastBeyond360));
            sequence.Join(transform.DOMove(transform.position + Vector3.down * 0.35f, 0.38f).SetEase(Ease.InQuad));
            sequence.OnComplete(() => Destroy(gameObject));
        }

        public string SizeLabel()
        {
            return Volume.ToString("0.00") + " м³";
        }
    }
}
