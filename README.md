<p align="center">
  <img src="assets/amui.png" width="128" alt="AMUI logo">
</p>

<h1 align="center">AMUI</h1>
<p align="center"><b>Alvin's Roblox Multi-Instance</b><br>Run as many Roblox clients as you want, on one PC, at the same time.</p>

<p align="center">
  <a href="https://github.com/AlvinP11/AMUI/releases/latest"><img src="https://img.shields.io/github/v/release/AlvinP11/AMUI?style=flat-square&color=6d28d9" alt="Latest release"></a>
  <a href="https://github.com/AlvinP11/AMUI/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/AlvinP11/AMUI/build.yml?style=flat-square" alt="Build"></a>
  <a href="https://github.com/AlvinP11/AMUI/releases"><img src="https://img.shields.io/github/downloads/AlvinP11/AMUI/total?style=flat-square&color=2563eb" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/platform-Windows-0078d4?style=flat-square" alt="Windows">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/AlvinP11/AMUI?style=flat-square" alt="MIT License"></a>
</p>

---

Normally, opening Roblox on a second account closes the first one. AMUI stops that from happening. It sits in your system tray and keeps every client open.

- **Tiny.** One ~25 KB `.exe`, no installer, no dependencies.
- **Hands-off.** Never touches, reads or injects into Roblox. No network access.
- **Readable.** The whole app is [one C# file](src/AMUI.cs) of about 200 lines.
- **Verifiable.** Every release is built by GitHub Actions directly from this source.

## Download

Grab **`AMUI.exe`** from the [latest release](https://github.com/AlvinP11/AMUI/releases/latest).

## How to use

1. Close every Roblox window.
2. Run `AMUI.exe`. An icon appears in your system tray.
3. Open Roblox as many times as you want, on different accounts.
4. When you're done, right-click the tray icon and pick **Exit**.

Keep AMUI running for as long as you want more than one client open.

## How it works

When a Roblox client starts, it claims two named Windows objects: a mutex called `ROBLOX_singletonMutex` and an event called `ROBLOX_singletonEvent`. If a new client finds those names already taken, it fires the event to tell the old client to shut down. That's why your first account closes.

AMUI claims both names *before* Roblox does and holds on to them. Roblox can't create its event, so no client can tell another one to close.

```
Without AMUI                          With AMUI
────────────                          ─────────
Client 1 claims the name              AMUI claims the name
Client 2 sees it → closes Client 1    Client 1 can't claim it → runs normally
                                      Client 2 can't claim it → runs normally
```

## Is it safe?

AMUI is open source so you don't have to take anyone's word for it:

- Read [`src/AMUI.cs`](src/AMUI.cs). It's the entire program.
- Every release `.exe` is built by [GitHub Actions](https://github.com/AlvinP11/AMUI/actions) from the tagged commit, and the release notes list its SHA-256 hash.
- Or skip the download and [build it yourself](#build-from-source).

## Build from source

No Visual Studio or SDK needed. Windows already ships with the C# compiler AMUI uses.

```bat
git clone https://github.com/AlvinP11/AMUI.git
cd AMUI
build.bat
```

The result is `bin\AMUI.exe`.

## FAQ

**Can I get banned for this?**
AMUI doesn't modify Roblox or touch its process. It only reserves two names in Windows. That said, Roblox doesn't officially support running multiple clients, so use it at your own risk.

**It stopped working after a Roblox update.**
Roblox might have changed how it detects other clients. [Open an issue](https://github.com/AlvinP11/AMUI/issues/new/choose) and include your Roblox version.

**It says "Roblox is already open" but I closed it.**
Roblox sometimes keeps running in the background. Open Task Manager, end every `RobloxPlayerBeta.exe`, then click Retry.

**Does it work with Bloxstrap / Fishstrap?**
It should. They launch the same `RobloxPlayerBeta.exe`.

## Disclaimer

AMUI is a fan-made project. It is not affiliated with, endorsed by or connected to Roblox Corporation. "Roblox" is a trademark of Roblox Corporation.

## License

[MIT](LICENSE) © 2026 Alvino
