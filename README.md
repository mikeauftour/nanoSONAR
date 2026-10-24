# nanoSONAR

![nanoSONAR-Icon](assets/nanoSONAR-icon.png)

**nanoSONAR** ist ein kompakter MIDI-Mixer für **SteelSeries GG Sonar** unter Windows 11. Die Anwendung verbindet einen **KORG nanoKONTROL2** mit den Lautstärke- und Stummschaltfunktionen von Sonar. So lassen sich Audiokanäle über echte Fader, Drehregler und Tasten steuern, ohne ständig das Sonar-Fenster öffnen zu müssen.

**Aktuelle Version:** 1.2.0 · **Streamer-Live-Feedback:** experimentell · **Plattform:** Windows 11 (x64) · **Technik:** C# / .NET 8 / WinForms

## Funktionen

- **Streaming-Mix:** Hardware-Fader zur Lautstärkeregelung.
- **Monitoring-Mix:** Hardware-Drehregler zur getrennten Lautstärkeregelung.
- **Getrennte Mute-Steuerung:** S-Tasten für Streaming, M-Tasten für Monitoring; Statusanzeige in der Anwendung und über die KORG-LEDs.
- **Mixer-Oberfläche:** Acht Kanalzüge mit grafischen Fadern, Drehreglern und Mute-Anzeigen.
- **Automatische Verbindung:** MIDI-Eingang, MIDI-Ausgang und SteelSeries Sonar werden beim Start verbunden; Wiederverbindungsversuche sind vorgesehen.
- **System-Tray:** Im Hintergrund weiterlaufen und über das Tray-Menü öffnen oder beenden.
- **Neu in v1.1:** Kleines OSD bei MIDI-Fader-, Drehregler- und Mute-Bedienung mit Kanal, Mix, Prozentwert und Stummstatus. Standard: oben rechts, drei Sekunden, nur bei ausgeblendetem Mixer. Im Einstellungsfenster stehen sechs Positionen zur Auswahl: oben links, oben Mitte, oben rechts, unten links, unten Mitte und unten rechts.
- **v1.2-Testfunktion (Streamer-Modus):** Nach Lautstärke- und Mute-Änderungen sendet nanoSONAR zusätzlich ein GG-Eventing-Signal, damit sich die Regler in der SteelSeries-GG-Oberfläche live aktualisieren. Für Master, Game, Chat, Media und Aux; Mic/ChatCapture ist noch nicht über GG-Aktionen abgedeckt. Diese Funktion muss auf Windows mit SteelSeries GG getestet werden.\n- **Windows-Autostart:** Wahlweise mit sichtbarem Fenster oder direkt im Tray.
- **Konfigurierbare MIDI-Profile** und gespeicherte Einstellungen.

### Standardbelegung (KORG nanoKONTROL2)

| Bedienelement | Funktion |
| --- | --- |
| Fader 1–6 | Streaming-Lautstärke: Master, Game, Chat, Media, Aux, Mic |
| Drehregler 1–6 | Monitoring-Lautstärke derselben Kanäle |
| S-Tasten 1–6 | Streaming stummschalten / Stummschaltung aufheben |
| M-Tasten 1–6 | Monitoring stummschalten / Stummschaltung aufheben |
| R-Tasten | Nicht belegt |

Die weiteren Kanalzüge sind in der Oberfläche vorhanden; ihre Nutzung hängt vom Profil und dem aktuellen Implementierungsstand ab.

## Download & Nutzung

Die **portable Windows-Version** wird über die [GitHub Releases](https://github.com/mikeauftour/nanoSONAR/releases) bereitgestellt.

1. Das ZIP `nanoSONAR-v1.2-win-x64.zip` aus dem v1.2.0-Release herunterladen.
2. An einem **dauerhaften Speicherort** entpacken.
3. `nanoSONAR.exe` starten.

**Kein Installer und kein separat installiertes .NET erforderlich**, sofern die veröffentlichte EXE als Self-contained-Build erstellt wurde.

Für die Steuerung werden **SteelSeries GG mit aktiviertem Sonar** und ein entsprechend eingerichteter **KORG nanoKONTROL2** benötigt. Für die Tasten-LEDs muss im KORG Kontrol Editor **LED Mode = External** eingestellt sein; die MIDI-CC-Zuordnung muss zum gewählten Profil passen.

> **Autostart-Hinweis:** Die Anwendung am endgültigen Speicherort starten, bevor der Autostart aktiviert bzw. gespeichert wird. Wird die EXE später verschoben, muss der Autostart-Pfad aktualisiert werden.

## Aus Quellcode bauen

Voraussetzung: **.NET 8 SDK** auf Windows 11.

Im Repository-Ordner `scripts\BUILD-RELEASE.cmd` ausführen. Alternativ in PowerShell:

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o dist/portable
```

Die fertige Anwendung liegt in `dist/portable/nanoSONAR.exe`. Weitere Hinweise zur Erstellung einer ZIP-Version stehen in [BUILD.md](docs/BUILD.md).

## Wichtige Hinweise

- nanoSONAR verwendet eine **lokale, nicht offiziell dokumentierte Sonar-Schnittstelle**. Änderungen an SteelSeries GG können die Kompatibilität beeinträchtigen.
- **Streamer-Live-Feedback in Entwicklung:** Die neue GG-Eventing-Anbindung soll sichtbare Slider-Aktualisierungen ermöglichen; sie ist noch nicht unter Windows validiert. Classic-Modus und ChatCapture/Mic erhalten vorerst kein Eventing-Feedback. Die Sonar-API wird weiterhin regelmäßig abgefragt, sodass Änderungen aus SteelSeries GG in nanoSONAR übernommen werden.
- Die Anwendung ist ein **unabhängiges Community-Projekt** und steht in keiner offiziellen Verbindung zu SteelSeries oder KORG.
- Die Bedienung und insbesondere MIDI-LEDs hängen von Gerätekonfiguration, Treibern und Sonar-Modus ab.

## Projekt

Entwicklung: **Mike auf Tour**

Fehlerberichte und Verbesserungsvorschläge bitte über [GitHub Issues](https://github.com/mikeauftour/nanoSONAR/issues) einreichen.

## Code signing policy

Free code signing provided by SignPath.io, certificate by SignPath Foundation **if the project is accepted**. Signing is not yet active; current downloadable builds are unsigned.

- Committer, reviewer and signing approver: [mikeauftour](https://github.com/mikeauftour).
- Changes from external contributors require review by the maintainer before merging.
- Signing requests will require explicit approval by the maintainer.
- Privacy: nanoSONAR communicates with local SteelSeries GG/Sonar services via loopback and stores its settings locally. It does not intentionally transfer user data to external networked systems. External services, if configured by the user, are subject to their own privacy policies.
- Security and release process: source changes and Windows release builds are tracked in this public GitHub repository. [Windows builds](https://github.com/mikeauftour/nanoSONAR/actions) and [releases](https://github.com/mikeauftour/nanoSONAR/releases) are public. Signed releases will be identified as such only after signing has been configured and verified.
- Installation: extract the portable ZIP and start the EXE. Uninstallation: disable the app's Windows autostart setting, close the application, and delete the extracted folder. User settings stored separately can be deleted manually.

See [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md).
