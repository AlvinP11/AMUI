# Changelog

## v1.1.1 (2026-10-03)

- Fixed: after switching Roblox accounts, a card could show the previous account's name and avatar. AMUI was trusting a browser-tracker cookie that all Roblox clients share. It now only uses the account Roblox reports when a client joins a game.
- A client that hasn't joined a game yet now shows *Unknown account* instead of guessing.

## v1.1.0 (2026-10-03)

AMUI now has a window that shows every open Roblox client.

- **Account list.** Each client gets a card with its avatar, display name and @username.
- **Live status.** Shows the game each account is in (with its icon), or whether it's starting, joining, in the menus, or disconnected. Kicks and lost connections show the reason and error code.
- **Power button** on every card closes that one client. **Close all** closes them all.
- **Click a card** to switch to that client's window. **Right-click** for: open profile, open game page, copy username, copy user ID.
- RAM and uptime for each client, plus the total in the footer.
- If Roblox is already open when AMUI starts, AMUI no longer quits. It shows a banner and turns multi-instance on once every client is closed.
- Closing the window keeps AMUI running in the tray. Starting AMUI again brings the window back.
- New settings: *Always on top*, *Start with Windows* and *Keep running in the tray when closed*.
- Shows a banner when a newer version of AMUI is released.
- Dark title bar and dark scrollbars on Windows 11.

## v1.0.0 (2026-10-03)

First release.

- Lets you run multiple Roblox clients at once by holding Roblox's single-instance names.
- Holds both of Roblox's single-instance names (`ROBLOX_singletonMutex` and `ROBLOX_singletonEvent`).
- Lives in the system tray, with a live count of open Roblox clients.
- Warns you if Roblox is already open when AMUI starts.
