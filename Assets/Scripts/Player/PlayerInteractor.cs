using System;
using MortgageSim.Core;
using MortgageSim.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MortgageSim.Player
{
    /// <summary>Прицел/курсор, наведение и клики по объектам.</summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        public FirstPersonPlayer Player;
        public Camera Cam;

        private Interactable _current;
        private Interactable _hover;

        private void Update()
        {
            var game = GameManager.Instance;
            if (game == null || game.UI == null || !game.CanPlayerInteract)
            {
                ClearHover();
                return;
            }

            Vector2 screenPoint = Cursor.lockState == CursorLockMode.Locked
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            if (game.UI.IsPointerOverUI(screenPoint))
            {
                ClearHover();
                return;
            }

            Interactable found = Raycast(screenPoint);
            if (found != _current)
            {
                if (_current != null) _current.OnHoverExit();
                _current = found;
                if (_current != null && _current.CanInteract) _current.OnHoverEnter();
            }

            if (_current != null && _current.CanInteract)
            {
                game.UI.ShowPrompt(_current.GetPrompt());
                game.UI.SetCrosshairActive(true);
            }
            else
            {
                game.UI.SetCrosshairActive(false);
                game.UI.HidePrompt();
            }

            bool usePressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                              (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);

            if (usePressed && _current != null && _current.CanInteract)
            {
                game.Audio?.Click();
                _current.Interact();
            }
        }

        private Interactable Raycast(Vector2 screenPoint)
        {
            if (Cam == null) return null;
            Ray ray = Cam.ScreenPointToRay(screenPoint);
            RaycastHit[] hits = Physics.RaycastAll(ray, GameBalance.InteractionRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return null;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;
                if (hit.collider.GetComponentInParent<FirstPersonPlayer>() != null) continue;

                var interactable = hit.collider.GetComponentInParent<Interactable>();
                if (interactable != null) return interactable;

                // Стена или другой объект перекрывает обзор.
                return null;
            }
            return null;
        }

        private void ClearHover()
        {
            if (_current != null)
            {
                _current.OnHoverExit();
                _current = null;
            }
            if (_hover != null) _hover = null;
            var game = GameManager.Instance;
            if (game != null && game.UI != null)
            {
                game.UI.HidePrompt();
                game.UI.SetCrosshairActive(false);
            }
        }
    }
}
