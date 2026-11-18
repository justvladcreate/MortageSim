using UnityEngine;

namespace MortgageSim.Interaction
{
    /// <summary>Базовый интерактивный объект: на него можно навести прицел и нажать ЛКМ/E.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public virtual bool CanInteract => true;
        public abstract string GetPrompt();
        public abstract void Interact();
        public virtual void OnHoverEnter() { }
        public virtual void OnHoverExit() { }
    }
}
