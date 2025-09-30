# VIRTUAL-WORLDS-DESIGN-DEV-Milestone-1

**Unity**: 6000.0.541f1  
**Netcode for GameObjects** + **Unity Transport** + **Multiplayer Mode**

## How to run (Windows)
1. Download `VirtualWorld-Netcode.zip` from Releases.
2. Extract the zip.
3. Run `VirtualWorld-Netcode.exe`.
4. In one window click **Host**. In the other window click **Client**.
5. You should see two avatars. Moving in one window replicates in the other.

## Controls
WASD move, Shift sprint, Space jump, Mouse look, Esc unlock cursor.

## Notes
- Player colors are deterministic per client id.
- Known issue: if you ever see a third grey capsule, it means a stray scene object was left (fixed in this build).

