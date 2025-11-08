# Locked With Me – Local Co-op Horror Game

A **local co-op horror game optimized for Android** built in **Unity (C#)**.  
Players must escape a locked house, avoid AI-controlled enemies and destroy the dam trapping them on an island.  
Playable in **singleplayer** or **local co-op** via LAN.

---

## Project Overview

I originally developed this as a university prototype, but decided to expand it into a **fully playable mobile game** (10–15 min).  
Focused on **AI behavior**, **local networking** and **Android performance optimization**.

| **Category** | **Description** |
|---------------|----------------|
| **Engine** | Unity (URP) |
| **Language** | C# |
| **Platform** | Android (optimized for mid-range devices) |
| **Networking** | Mirror (LAN Co-op) |
| **Development Time** | 6 months (solo project) |

---

## Technical Implementation

### AI & Gameplay Systems
- **AI Pathfinding**: Implemented using Unity’s NavMesh system. Enemies update their navigation targets in real time and adapt to obstacles.
- **Stealth gameplay**: Enemy AI with **sound and sight detection**.   
- **Killing enemies** and the **inventory system** are synchronized over network.
- **Finite State Machine (FSM)** architecture for AI behavior (`Rest`, `Walk`, `Chase`, `Investigate`, `Attack`).

**Key Scripts:**
- `NPCScript.cs`: Pathfinding, perception system, state transitions.  
- `PlayerMovement.cs`: Animation states, inventory, multiplayer sync.  
- `GameManager.cs`: Handles player tracking, objectives and victory conditions.
- `UIManager.cs`: Manages a servers list using Network discovery, progressively loads terrain, controls player HUD and menus.

---

### Networking (Mirror)
- **Local Co-op** using Mirror NetworkDiscovery.  
- Synchronized:
  - Player positions, animations, inventory.
  - Enemy AI state and world events.
  - Match state transitions (start, death, victory).

**Networking Architecture**

The game uses a host-authoritative hybrid model where the host validates all persistent gameplay state (AI, inventory, object destruction) while clients perform local prediction for movement and animation. Cooperative actions (e.g., doors, pickups) use `[Command(requiresAuthority=false)]` calls for client-initiated interactions on server-owned objects (objects they don't own).

Host-authoritative: All gameplay state is controlled by the server (`[Server]` attributes in `GameManager.cs` like `AddPlayer()`, AI updates in `NPCScript.cs` through `[Server]` blocks).

Hybrid: **Client-side prediction** for movement (clients move locally via FixedUpdate, then NetworkTransformReliable syncs) while the server validates persistent state. Another example would be grabbing the keys and checking if it's valid locally and then asking the server to validate it.

---

### Performance Optimization
- Designed specifically for **Android hardware**.  
- Techniques implemented:
  - **Level of Detail (LOD)** meshes on environment models.
  - **Occlusion Culling** for hiding objects outside the direct camera's vision.
  - **Progressive world loading** to avoid overloading the system.
  - **Light Probes** + **Baked Lightmaps** for realistic indoor lighting at minimal runtime cost.

Optimized to maintain 60+ FPS on mid-range Android devices

---

### Content Creation
- Modeled the **main house** in **Blender** (exported as FBX).  
- Designed the terrain and UI with free assets from various websites.  
- Implemented adaptive soundscapes:
  - Dynamic rain, waves, swamp wildlife.
  - Ambient layers reacting to player proximity.
  - Adaptive music based on player location.
- Implemented VFX for:
  - Missile explosion.
  - Dam destruction.
  - Smoke coming out of a boiler.

---

## Key Features
- **Dynamic AI Perception**: NPCs react to player visibility and sound triggers in real-time.
- **Progressive World Loading**: Terrain objects spawn progressively to avoid frame drops.
- **Robust Network State Management**: Automatic cleanup and reconnection handling
- **Adaptive Audio System**: Music and ambient sounds respond to player location.
- **Inventory Synchronization**: Multiplayer inventory system with stacking and usage tracking.

---

## Challenges & Solutions
- **Challenge:** Synchronizing AI state and perception in LAN co-op.  
  **Solution:** Implemented host-authoritative AI control and event replication through Mirror `[Command]` / `[SyncVar]` calls.

- **Challenge:** Maintaining stable performance on mid-range Android devices.  
  **Solution:** Used LOD groups, occlusion culling, baked lighting and progressive world loading to reduce draw calls and GPU load.

- **Challenge**: Players returning to menu could cause connection conflicts if network wasn't fully stopped.  
  **Solution:** Stop network components one at a time with small delays between each and disable menu buttons during the process to avoid connection conflicts.

---

## Code Architecture
```text
Scripts/
├── GameLogic/
│   ├── DamHealth.cs
│   ├── DoorAttachedToLockScript.cs
│   ├── DoorScript.cs
│   ├── LockScript.cs
│   └── PortalScript.cs
├── Manager/
│   ├── CustomNetworkManager.cs
│   ├── GameManager.cs
│   ├── NetworkSpawner.cs
│   └── UIManager.cs
├── NPC/
│   ├── NPCScript.cs
│   ├── EnemyHealth.cs
│   ├── FieldOfView.cs
│   └── Behaviours/
│       ├── Attack.cs
│       ├── Chase.cs
│       ├── Investigate.cs
│       ├── Rest.cs
│       └── Walk.cs
├── Player&Inventory/
│   ├── CameraScript.cs
│   ├── FirePositionScript.cs
│   ├── InventoryItem.cs
│   ├── InventoryManager.cs
│   ├── MinimapPlayerCamera.cs
│   ├── MissileScript.cs
│   └── PlayerMovement.cs
└── Sounds/
    ├── CrowCricketSoundScript.cs
    ├── GramophoneScript.cs
    ├── OceanSoundScript.cs
    ├── SoundManager.cs
    ├── SoundTriggerScript.cs
    └── SwampFootstepsSoundScript.cs

```

---

## Media - Click on the image to watch the trailer on Youtube

<p align="center">
  <a href="https://www.youtube.com/watch?v=W72o7VArAW4">
    <img src="thumbnail.png" width="35%">
  </a><br>
  <em>Click on the image to watch the short trailer on Youtube</em>
</p>

---

## Links
[Play on Itch.io](https://gamedevrafael.itch.io/locked-with-me)  
[Visit my LinkedIn](https://www.linkedin.com/in/rgtdfaustino)
