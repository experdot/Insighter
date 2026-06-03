# Insighter.Agent.Package

This folder contains the MSIX packaging template and assets for `Insighter.Agent`.

The actual `.msix` is produced by CI using `scripts/package-msix.ps1`.

The packaged layout includes:

- `Insighter.Agent.exe` as the tray-resident full-trust app
- `Insighter.Agent.Startup.exe` as the MSIX `StartupTask` shim that launches the tray app with `--background`
