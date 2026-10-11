using UnityEngine;

namespace EchoProtocol.Interaction
{
    /// <summary>
    /// Generic interface for any interactive entity in the game (relics, altar sockets, doors, levers).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Prompt text displayed on the carrier's HUD (e.g. "Pick Up Relic", "Insert into Altar").</summary>
        string PromptText { get; }

        /// <summary>Returns true if the player can interact right now.</summary>
        bool CanInteract(PlayerController player);

        /// <summary>Executes the interaction on behalf of the player.</summary>
        void Interact(PlayerController player);
    }
}
