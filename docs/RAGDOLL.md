# Player ragdoll

## Overview
Echo Protocol uses a custom physics-driven ragdoll to punish players for getting caught, and it fits the comedy-horror tone. When a player is caught or trapped, they lose control, tumble through the scene, make noise when they hit the ground, and need a few seconds to recover.

## Principles
1. **Separate from the AI.** `EnemyBrain` knows nothing about ragdolls. It only raises an event when it catches a player. A separate script (`EnemyCatchReaction`) listens to that event and sends the victim into ragdoll mode.
2. **Kinematic to dynamic.** In normal play a Unity `CharacterController` drives the player. During a ragdoll the controller is disabled and a dynamic `Rigidbody` takes over.
3. **Built for multiplayer.** The server decides a catch and tells the victim through `PlayerController.RpcTriggerRagdoll` (an `ObserversRpc`). Only the **owner** simulates the ragdoll, and `NetworkTransform` replicates the flight to everyone else. Remote copies never become dynamic, so triggers (`RagdollTrap`) and the `R` debug key also act on the owner only.

## Scripts and what they do

### `PlayerRagdoll.cs`
The main component, attached to the player.
- **State machine:** switches between kinematic with the CharacterController (normal) and dynamic with the Rigidbody (ragdolling).
- **Camera tumble:** applies rotational damping to the first-person camera pivot, so the view spins as the body rolls.
- **Recovery:** it waits until the body stops moving (or the maximum time is reached), stands the player up facing the direction the camera ended up looking, and gives control back.

### `EnemyCatchReaction.cs`
Attached to the enemy.
- Listens to `EnemyBrain.OnPlayerCaught`.
- Finds the `PlayerRagdoll` on the victim.
- Calls `TryRagdoll` with a knockback vector that pushes the player away and up.
- Stuns the enemy (`EnemyBrain.Stun()`), so it gloats for a few seconds before it hunts again.

### `RagdollTrap.cs`
Attached to triggers in the world.
- It is a pure environmental hazard.
- It sends any player who enters the trigger into ragdoll mode.

### `NoiseBurstEmitter.cs`
Attached to the player, and required by `PlayerRagdoll`.
- Implements `INoiseSource` for short, one-off impact sounds.
- When the ragdoll starts, it emits a loud noise that fades out and attracts enemies. Getting ragdolled can bring *more* trouble.

## What happens during a catch
1. `EnemyBrain` enters the `Hunt` state.
2. `EnemyBrain` touches the player (horizontal distance below `catchDistance`: 1.6 m on the multiplayer prefab, 2.2 m by default).
3. `EnemyBrain` fires `OnPlayerCaught(victimTransform)`.
4. `EnemyCatchReaction` receives the event and calculates the knockback velocity.
5. `EnemyCatchReaction` calls `victimRagdoll.TryRagdoll(knockback)`.
6. `PlayerRagdoll`:
    - Disables the `PlayerController` input.
    - Disables the `CharacterController`.
    - Enables the `CapsuleCollider`.
    - Sets `Rigidbody.isKinematic = false`.
    - Applies the knockback velocity.
    - Triggers `NoiseBurstEmitter` for a loud thud.
7. `EnemyCatchReaction` calls `enemyBrain.Stun(3f)` and the enemy stops moving.
8. The player rolls on the floor and the camera spins.
9. The player's velocity settles, or the timer runs out.
10. `PlayerRagdoll` calls `Recover()`:
    - Resets the velocity.
    - Sets `Rigidbody.isKinematic = true`.
    - Enables the `CharacterController`.
    - Enables the `PlayerController` input.
    - Stands the player upright.
11. The enemy's stun timer ends and it starts hunting again.
