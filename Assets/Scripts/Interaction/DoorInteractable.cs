using MortgageSim.Core;
using UnityEngine;

namespace MortgageSim.Interaction
{
    public sealed class DoorInteractable : Interactable
    {
        public override string GetPrompt()
        {
            var game = GameManager.Instance;
            if (game == null) return "Дверь";
            if (game.IsDoorOpen) return "Дверь открыта";
            if (game.Collector != null && game.Collector.IsAtDoor)
                return "Открыть дверь — коллектор стучит   [ЛКМ]";
            if (game.Collector != null && game.Collector.IsInside)
                return "Дверь в подъезд (закрыта)";
            return "Картина-дверь. Пока тихо.";
        }

        public override void Interact()
        {
            var game = GameManager.Instance;
            if (game != null) game.InteractDoor();
        }
    }
}
