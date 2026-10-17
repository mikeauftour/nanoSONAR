# nanoSONAR v0.7 – UI-Update

Basiert auf v0.6.1. Neue Oberfläche mit zweizeiliger Toolbar, acht grafischen Drehregler-Anzeigen (Monitoring), acht vertikalen Fadern (Streaming) und Mute-Tasten. Die Drehregler-Anzeigen sind zunächst reine Zustandsanzeigen; MIDI-Hardware sendet die Werte. Die Zuordnungen bleiben über JSON-Profile konfigurierbar.

## Start (Windows 11)
Im Ordner `NanoSonarController`:

```powershell
dotnet restore
dotnet run
```

## EXE erstellen
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Die Darstellung und MIDI-Funktion müssen auf Windows mit dem nanoKONTROL2 getestet werden. Die LEDs benötigen einen verbundenen MIDI-Ausgang und den passenden External-LED-Modus am Gerät. Die SteelSeries-GG-Oberfläche muss API-Änderungen nicht unmittelbar visuell übernehmen.


## v0.7.1 – M/S/R-Fix
- Only M (CC 48–55) toggles mute; S (32–39) and R (64–71) are ignored.
- Only M LEDs reflect mute; S and R LEDs are explicitly switched off.
- New profile path `%APPDATA%\nanoSONAR\profiles-v071.json` prevents older mappings from being reused.
- KORG Kontrol Editor: LED Mode must be External and MIDI CC numbers must match.
- The default profile uses M for streaming mute; the Classic profile uses Classic mute.

## v0.7.2 – Separate Mute-Tasten
- S (CC 32–39) steuert Streaming-Mute, M (CC 48–55) steuert Monitoring-Mute, R (CC 64–71) bleibt frei.
- S- und M-LEDs spiegeln jeweils ihren eigenen Mute-Zustand; R-LEDs bleiben aus.
- Im Classic-Modus steuern S und M beide Classic, weil dort keine getrennten Mixe vorhanden sind.
- Neue Profildatei `%APPDATA%\nanoSONAR\profiles-v072.json`, damit alte Profile nicht übernommen werden.
- Voraussetzung: KORG-Tasten senden die genannten CCs, LED Mode = External.


## v0.7.3: Monitoring-Mute visuell
- Der Monitoring-Drehregler wird rot und zeigt **MUTE**, wenn sein zugeordneter Monitoring-Kanal stumm ist. Der Prozentwert bleibt erhalten und erscheint nach Unmute wieder.
- Unter jedem Kanal sind **S · Stream** und **M · Monitor** getrennt sichtbar; stummgeschaltete Tasten werden rot und zeigen **STUMM**.
- Die Anzeige folgt den aus Sonar gelesenen Zuständen (Aktualisierung ca. 800 ms), nicht nur den zuletzt gedrückten Tasten.
- Hardware-LEDs bleiben über die bestehenden MIDI-Ausgangszuordnungen gesteuert.
- In Classic zeigen beide Tasten denselben Classic-Mute-Zustand.

Start: `dotnet restore` und `dotnet run` im Projektordner. Windows-Build und Hardware noch nicht verifiziert.


## v0.7.4
- Streaming-Fader zeigen bei aktivem Streaming-Mute eine rote Fläche und `STREAM STUMM` an.
- Monitoring-Drehregler behalten ihre separate rote MUTE-Anzeige.
- Die Faderposition bleibt beim Stummschalten erhalten.
- Die Anzeigen folgen dem aus der Sonar-API ausgelesenen Zustand (Polling).


## v0.7.5 Automatische Verbindungen
Beim Start verbindet nanoSONAR MIDI IN, MIDI OUT und Sonar automatisch. Die zuletzt verwendeten MIDI-Gerätenamen werden in `%APPDATA%\nanoSONAR\devices.json` gespeichert. Fehlende MIDI-Geräte werden alle 5 Sekunden erneut gesucht. Sonar nutzt weiterhin die bestehende automatische Wiederverbindung. Manuelle Tasten bleiben verfügbar. Hinweis: Ein während des Betriebs abgezogenes MIDI-Gerät kann je nach Windows-MIDI-Treiber erst nach einem Neustart der Anwendung wieder verwendbar sein.


## v0.7.6 UI
- Mute-Schaltflächen mit kontrastreicher Schrift und expliziten Farben, auch wenn sie nicht verfügbar sind.
- Feste Kanalbreite und horizontales Scrollen bei schmalen Fenstern, damit Beschriftungen nicht abgeschnitten werden.
- Fensterposition, Größe und Maximierungszustand in `%APPDATA%\nanoSONAR\window.json` speichern; beim nächsten Start wiederherstellen.
- Beenden über das Tray-Menü; Schließen blendet weiterhin ins Tray aus.


## v0.7.7 Responsive Layout
Mixer columns distribute evenly across the available viewport width. Below 104px per channel the mixer scrolls horizontally, preventing clipped controls. In compact mode labels shorten to STREAM and MONITOR. Window persistence remains as in v0.7.6.


## v0.7.8 Fensterlayout
- Fensterhoehe ist fest auf 850 Pixel gesetzt; nur die Breite ist veraenderbar.
- Mindestbreite 1150 Pixel verhindert zusammengedrueckte Kanalspalten.
- Fensterbreite und Position werden weiterhin in `%APPDATA%\nanoSONAR\window.json` gespeichert.
- Ein alter maximierter Fensterzustand wird nicht wiederhergestellt.
- Bei Windows-DPI-Skalierung kann die tatsaechliche Bildschirmgroesse abweichen.

## v0.7.9
Unteren Leerraum reduziert: feste Fensterhöhe 770 statt 850 Pixel, Mixerhöhe 535 Pixel, Fußzeile 28 Pixel. Fensterbreite/Position werden weiter gespeichert; alte gespeicherte Höhe wird bewusst ignoriert.
