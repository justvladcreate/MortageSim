using MortgageSim.Core;
using UnityEngine;

namespace MortgageSim.Interaction
{
    public sealed class CollectorInteractable : Interactable
    {
        public override bool CanInteract
        {
            get
            {
                var game = GameManager.Instance;
                return game != null && game.Collector != null && game.Collector.IsInside;
            }
        }

        public override string GetPrompt()
        {
            var game = GameManager.Instance;
            if (game == null) return "Поговорить с коллектором";
            if (game.Money < game.CurrentPayment)
                return "Поговорить с коллектором — не хватает " + GameBalance.Money(game.CurrentPayment - game.Money) + "   [ЛКМ]";
            return "Поговорить с коллектором и оплатить   [ЛКМ]";
        }

        public override void Interact()
        {
            var game = GameManager.Instance;
            if (game != null) game.TalkToCollector();
        }
    }
}
