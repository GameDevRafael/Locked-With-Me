# Locked With Me

A **local co-op horror game for Android** I built in Unity with C#.  
Escape a locked house, survive the AI enemies and destroy the dam that's trapping you on the island!  
Playable **solo** or with a friend in 10–15 minutes.

I originally built this game for a university class, but i decided to treat it as a **larger-scope project** and expanded it well beyond the requirements after the submission and managed to earn a **19/20 grade**.

---

## Core Features

### Gameplay
- Singleplayer or **local co-op** (Mirror Networking)
- **Enemy AI** that reacts to noise and searches dynamically
- **Hiding mechanic** (chests) to avoid the enemies' detection
- Indoor & outdoor exploration with atmospheric swamp
- Final boss fight with **rocket launcher** + dam destruction

### Technical
- Unity URP and use of LODs for better performance.
- **Light probes** & baked lightmaps for indoor lighting
- **Occlusion culling** for render optimization
- Custom terrain & main house modeled in Blender (by myself)
- Progressive world loading for mobile performance

### Atmosphere & Audio
- Dynamic music by location (tense creepy indoor vs. curious mysterious outdoor)
- Layered ambient audio: rain, waves, swamp wildlife and distant creatures
- Adaptive sound cues to guide player awareness

---

## Built With
- **Unity (C#)**
- **Mirror Networking**
- **Blender** 

---

## My Role
- All of the programming: player logic, AI, multiplayer networking
- Built the terrain and the UI using free assets.
- Integrated audio/visual effects
- Modeled the main house in Blender

---

## Explore the Code
Core scripts are in [`Scripts`](Scripts):

- **UIManager.cs** – Menus, network discovery, terrain spawning, interaction system
- **PlayerMovement.cs** – Full player controller, animations, inventory references, multiplayer sync
- **NPCScript.cs** – AI pathfinding, sight/sound detection, state management
- **GameManager.cs** – Game state, player tracking, victory conditions

---

## ▶ Play the Game
**[Play on Itch.io](https://yourgame.itch.io/locked-with-me)**

---

## GIFs
| Hiding from AI | Unlocking the Door | Using the RocketLauncher | Destroying the Dam |
|----------------|--------------------|---------------------|--------------------|
| ![Inside the Chest](gif/gif1.png) | ![Using the Key on the Lock](gif/gif2.png) | ![Killing the Enemies](imagifges/gif3.png) | ![Dam explosion](gif/gif4.png)

---

## Connect
**LinkedIn:** [Rafael Faustino](https://www.linkedin.com/in/rgtdfaustino)
