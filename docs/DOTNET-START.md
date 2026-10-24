# nanoSONAR v1.2 - Windows .NET-Startvariante

Diese Variante startet nanoSONAR ueber `dotnet nanoSONAR.dll`, ohne eine eigene nanoSONAR.exe. Sie ist fuer Rechner gedacht, auf denen die EXE von Smart App Control blockiert wird. Das ist keine Garantie, dass jede Windows-Sicherheitsrichtlinie den Start erlaubt.

## Einmal erstellen
`scripts\BUILD-DOTNET-RELEASE.cmd` im Repository ausfuehren (benoetigt .NET 8 SDK).

## Danach starten
`dist\dotnet\nanoSONAR-starten.cmd` doppelklicken. Fuer weitere Starts sind weder SDK noch Neubuild erforderlich, aber die **.NET 8 Desktop Runtime x64** muss installiert sein.

Die gesamte erzeugte Mappe kann verschoben werden. ZIP: `dist\releases\nanoSONAR-v1.2-dotnet-win-x64.zip`.

Die bestehende v1.1-Installation und das offizielle v1.2.0-Single-File-Release werden nicht veraendert.
