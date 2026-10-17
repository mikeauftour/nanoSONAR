# nanoSONAR v1.0 – portable Windows 11 (x64)

.NET 8 SDK installieren. PowerShell in diesem Projektordner öffnen:

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o publish
```

Die fertige Datei heißt `publish\nanoSONAR.exe`. Für die portable Veröffentlichung **die EXE und ggf. weitere Dateien aus `publish`** in `nanoSONAR-v1.0-win-x64.zip` packen. Den ZIP-Inhalt an einen dauerhaften Ort entpacken und `nanoSONAR.exe` starten. Kein Installer nötig.

Wichtig: Falls Autostart aktiv ist, nach dem Verschieben die EXE am endgültigen Ort erneut starten und die Autostart-Einstellungen speichern, damit Windows den richtigen Pfad verwendet. Benutzereinstellungen bleiben unter `%APPDATA%\nanoSONAR`.

Das Programmsymbol wird als EXE-Icon, Fenster-Icon und Tray-Icon eingebunden. Windows-Build und Hardwaretest müssen auf Windows erfolgen.
