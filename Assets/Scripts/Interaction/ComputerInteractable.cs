using MortgageSim.Core;
using MortgageSim.World;
using UnityEngine;

namespace MortgageSim.Interaction
{
    public sealed class ComputerInteractable : Interactable
    {
        public MonitorTerminal Monitor;

        public override string GetPrompt()
        {
            var game = GameManager.Instance;
            if (game != null && game.TaskSolvedThisMonth)
                return "Открыть компьютер — задача уже сдана   [ЛКМ]";
            return "Сесть за компьютер и открыть IDE   [ЛКМ]";
        }

        public override void Interact()
        {
            var game = GameManager.Instance;
            if (game != null) game.OpenComputer();
        }

        public override void OnHoverEnter()
        {
            if (Monitor != null) Monitor.SetHover(true);
        }

        public override void OnHoverExit()
        {
            if (Monitor != null) Monitor.SetHover(false);
        }
    }
}
