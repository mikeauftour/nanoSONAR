# nanoSONAR

![nanoSONAR-Icon](nanoSONAR-icon.png)

**nanoSONAR** ist ein kompakter MIDI-Mixer für **SteelSeries GG Sonar** unter Windows 11. Die Anwendung verbindet einen **KORG nanoKONTROL2** mit den Lautstärke- und Stummschaltfunktionen von Sonar. So lassen sich Audiokanäle über echte Fader, Drehregler und Tasten steuern, ohne ständig das Sonar-Fenster öffnen zu müssen.

**Version:** 1.0.0 · **Plattform:** Windows 11 (x64) · **Technik:** C# / .NET 8 / WinForms

## Funktionen

- **Streaming-Mix:** Hardware-Fader zur Lautstärkeregelung.
- **Monitoring-Mix:** Hardware-Drehregler zur getrennten Lautstärkeregelung.
- **Getrennte Mute-Steuerung:** S-Tasten für Streaming, M-Tasten für Monitoring; Statusanzeige in der Anwendung und über die KORG-LEDs.
- **Mixer-Oberfläche:** Acht Kanalzüge mit grafischen Fadern, Drehreglern und Mute-Anzeigen.
- **Automatische Verbindung:** MIDI-Eingang, MIDI-Ausgang und SteelSeries Sonar werden beim Start verbunden; Wiederverbindungsversuche sind vorgesehen.
- **System-Tray:** Im Hintergrund weiterlaufen und über das Tray-Menü öffnen oder beenden.
- **Windows-Autostart:** Wahlweise mit sichtbarem Fenster oder direkt im Tray.
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

Die **portable Windows-Version** wird über die [GitHub Releases](https://github.com/mikeauftour/nanoSONAR/releases) bereitgestellt, sobald ein Release veröffentlicht wurde.

1. Das ZIP `nanoSONAR-v1.0-win-x64.zip` aus dem jeweiligen Release herunterladen.
2. An einem **dauerhaften Speicherort** entpacken.
3. `nanoSONAR.exe` starten.

**Kein Installer und kein separat installiertes .NET erforderlich**, sofern die veröffentlichte EXE als Self-contained-Build erstellt wurde.

Für die Steuerung werden **SteelSeries GG mit aktiviertem Sonar** und ein entsprechend eingerichteter **KORG nanoKONTROL2** benötigt. Für die Tasten-LEDs muss im KORG Kontrol Editor **LED Mode = External** eingestellt sein; die MIDI-CC-Zuordnung muss zum gewählten Profil passen.

> **Autostart-Hinweis:** Die Anwendung am endgültigen Speicherort starten, bevor der Autostart aktiviert bzw. gespeichert wird. Wird die EXE später verschoben, muss der Autostart-Pfad aktualisiert werden.

## Aus Quellcode bauen

Voraussetzung: **.NET 8 SDK** auf Windows 11.

Im Repository-Ordner `BUILD-RELEASE.cmd` ausführen. Alternativ in PowerShell:

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o publish
```

Die fertige Anwendung liegt in `publish/nanoSONAR.exe`. Weitere Hinweise zur Erstellung einer ZIP-Version stehen in [BUILD.md](BUILD.md).

## Wichtige Hinweise

- nanoSONAR verwendet eine **lokale, nicht offiziell dokumentierte Sonar-Schnittstelle**. Änderungen an SteelSeries GG können die Kompatibilität beeinträchtigen.
- **Noch nicht umgesetzt (geplant):** Wenn die Hardware-Fader am KORG nanoKONTROL2 bewegt werden, ändert nanoSONAR bereits die Lautstärke in Sonar. Die **sichtbare Bewegung bzw. Aktualisierung der Fader in der originalen SteelSeries-GG-Sonar-Oberfläche** funktioniert derzeit jedoch noch nicht. Diese Funktion soll mit einem zukünftigen Update nachgereicht werden.
- Die Anwendung ist ein **unabhängiges Community-Projekt** und steht in keiner offiziellen Verbindung zu SteelSeries oder KORG.
- Die Bedienung und insbesondere MIDI-LEDs hängen von Gerätekonfiguration, Treibern und Sonar-Modus ab.

## Projekt

Entwicklung: **Mike auf Tour**

Fehlerberichte und Verbesserungsvorschläge bitte über [GitHub Issues](https://github.com/mikeauftour/nanoSONAR/issues) einreichen.
