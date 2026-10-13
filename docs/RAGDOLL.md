# Player Ragdoll Architecture

## Overview
Echo Protocol uses a custom physics-driven ragdoll system to heavily penalize players for getting caught, adding to the comedy horror genre. When caught or trapped, the player loses control, physically tumbles through the scene, generates noise on impact, and takes several seconds to recover. 

## Core Principles
1. **Decoupled from AI**: The enemy `EnemyBrain` has no concept of ragdolling. It merely invokes an event when catching a player. An external script (`EnemyCatchReaction`) listens to this and pushes the victim into ragdoll mode.
2. **Kinematic to Dynamic Switch**: During normal gameplay, a Unity `CharacterController` drives the player. During ragdoll, the controller is disabled, and a dynamic `Rigidbody` takes over.
3. **Multiplayer Ready**: The server decides a catch and tells the victim through `PlayerController.RpcTriggerRagdoll` (an `ObserversRpc`); only the **owner** simulates the ragdoll and `NetworkTransform` replicates the flight to everyone else. Remote copies never become dynamic, so triggers (`RagdollTrap`) and the `R` debug key also act on the owner only.

## Scripts & Responsibilities

### `PlayerRagdoll.cs`
The core component attached to the Player.
*   **State Machine**: Toggles between kinematic/CharacterController (normal) and dynamic/Rigidbody (ragdolling).
*   **Camera Tumble**: Applies rotational damping to the first-person camera pivot so the view wildly tumbles as the body rolls.
*   **Recovery**: Wait until the body stops moving (or max duration is reached), stand the player up looking in the direction their camera ended up facing, and restore normal controls.

### `EnemyCatchReaction.cs`
Attached to the Enemy.
*   Listens to `EnemyBrain.OnPlayerCaught`.
*   Locates `PlayerRagdoll` on the victim.
*   Calls `TryRagdoll` with a knockback vector (pushing the player away and up).
*   Stuns the enemy (`EnemyBrain.Stun()`) so it "gloats" for a few seconds before hunting again.

### `RagdollTrap.cs`
Attached to triggers in the world.
*   Purely environmental hazard.
*   Launches any player entering the trigger into ragdoll mode.

### `NoiseBurstEmitter.cs`
Attached to the Player (and required by `PlayerRagdoll`).
*   Implements `INoiseSource` for short, one-off impact sounds.
*   When the player ragdoll is triggered, this emits a loud, decaying noise that attracts enemies, meaning getting ragdolled might attract *more* trouble.

## Flow of a "Catch"
1. `EnemyBrain` enters `Hunt` state.
2. `EnemyBrain` touches the player (horizontal distance < `catchDistance`, 1.6 m on the multiplayer prefab, 2.2 m by default).
3. `EnemyBrain` fires `OnPlayerCaught(victimTransform)`.
4. `EnemyCatchReaction` receives event, calculates knockback velocity.
5. `EnemyCatchReaction` calls `victimRagdoll.TryRagdoll(knockback)`.
6. `PlayerRagdoll`:
    * Disables `PlayerController` input.
    * Disables `CharacterController`.
    * Enables `CapsuleCollider`.
    * Sets `Rigidbody.isKinematic = false`.
    * Applies knockback velocity.
    * Triggers `NoiseBurstEmitter` to make a loud thud.
7. `EnemyCatchReaction` calls `enemyBrain.Stun(3f)`. Enemy stops moving.
8. Player rolls on the floor. Camera spins.
9. Player velocity settles (or timer expires).
10. `PlayerRagdoll` calls `Recover()`:
    * Resets velocity.
    * Sets `Rigidbody.isKinematic = true`.
    * Enables `CharacterController`.
    * Enables `PlayerController` input.
    * Orients player upright.
11. Enemy stun timer expires. Enemy resumes hunting.
