# Install Insighter Agent MSIX

This release is signed with a development certificate for internal testing.

## Install steps

1. Download `Insighter.Agent_<version>_x64.cer` and `Insighter.Agent_<version>_x64.msix`.
2. Double-click the `.cer` file.
3. Choose `Install Certificate`.
4. Install it for `Current User`.
5. Select `Place all certificates in the following store`.
6. Choose `Trusted People`.
7. Finish the import wizard.
8. Double-click the `.msix` file to install `Insighter Agent`.

## Notes

- `Insighter Agent` runs in the tray and hosts the local dashboard on `127.0.0.1`.
- Manual launch opens the dashboard in your default browser.
- Login launch is controlled through the tray menu and uses the packaged `StartupTask`.
- Login launch starts the tray app silently in the background.
