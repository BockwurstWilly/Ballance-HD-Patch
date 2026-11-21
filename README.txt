Ballance - HD Patch 1.3.1
=========================

DEUTSCH
-------
Für Ballance (GOG oder Steam) unter Windows 10/11.

Installation
  BallanceHD_Setup.exe starten. Das Setup findet das Spiel automatisch (GOG/Steam-Registry,
  Desktop-Verknüpfung), prüft die Spieldateien und installiert. Danach Ballance über
  "Ballance - HD Launcher" (Desktop / Startmenü / Spielordner) starten.
  Alles Nötige ist im Setup enthalten: fehlt die Microsoft Visual C++ 2015-2022 Laufzeit (x86),
  installiert das Setup sie mit (Windows fragt dafür einmal nach Administratorrechten).
  Deinstallation: Startmenü "Ballance - HD-Patch entfernen", Launcher -> Info -> "HD-Patch
  entfernen", Windows "Apps & Features" oder das Setup erneut starten. Alle Originaldateien
  werden wiederhergestellt; Spielstände und Highscores (Database.tdb) bleiben unangetastet.

Was der Patch macht
  * Neue Spiel-EXE "BallancePlayer" (doyaGu): 32-Bit-Farben, beliebige Auflösungen bis 4K und
    höher, Breitbild, randloses Vollbild, keine Registry, keine Windows-98-Kompatibilitätsmodi.
  * Mod Loader "BML+" (doyaGu & Gamepiaynmo): korrektes Breitbild-Sichtfeld (Hor+),
    Bildratenbegrenzung, Fix für den Hänger beim Einsammeln von Extraleben.
  * Ballance-HD-Mod (dieses Paket): passt die Kameras des Spiels an das Bildformat an - das Bild
    füllt den ganzen 16:9-/21:9-Bildschirm, ohne schwarze Ränder links und rechts.
  * 32-Bit-Texturen statt 16 Bit (kein Farbbanding am Himmel), Kantenglättung bis 8x MSAA.
  * Randloses Vollbild in nativer Auflösung (empfohlen): kein Tearing, Alt+Tab ohne Flackern,
    Bildrate automatisch = Bildwiederholrate des Bildschirms. VSync wirkt bei Direct3D 8 unter
    Windows 10/11 im exklusiven Vollbild nicht - deshalb ist randlos die Voreinstellung.
  * Kein "DirectPlay installieren?"-Fenster mehr beim Start (das Spiel lädt dpnhpast.dll nur
    zur DirectX-Versionsprüfung; der Patch legt dafür eine leere DLL in den Ordner Bin).
  * dgVoodoo 2.79 (von GOG/Steam mitgeliefert) wird abgeschaltet: mit dem neuen Player zeichnet
    es Menüs und Himmel schwarz. Bin\D3D8.dll wird gesichert und beim Entfernen zurückgelegt.

Controller (Xbox, DualShock 4, DualSense - USB oder Bluetooth)
  Ballance kennt nur die Tastatur. Der Launcher bleibt beim Spielen unsichtbar im Hintergrund,
  übersetzt die Knöpfe in die Tasten des Spiels und reicht die Sticks an den Ballance-HD-Mod
  weiter. PlayStation-Controller werden direkt gelesen - ohne Steam oder DS4Windows.
    Linker Stick                      Kugel STUFENLOS rollen (Richtung und Kraft), Menüs
    Steuerkreuz                       Menüs, Rollen wie mit den Pfeiltasten
    Rechter Stick                     Kamera frei um die Kugel drehen und neigen - feste Distanz
    LB / RB  (L1 / R1)                Kamera in 90°-Schritten drehen (wie Shift + Pfeil)
    Y / Dreieck                       Kamera anheben (halten)
    A / Kreuz                         Bestätigen
    B / Kreis                         Zurück
    Start / Options                   Pause-Menü
    X / Quadrat                       Einführung überspringen
    View / Share                      Kameraabstand wechseln (Nah / Normal / Weit / Sehr weit)
  Die Knöpfe lassen sich umbelegen: im Spiel unter Optionen -> Steuerung (rechts das Feld
  "Controller", mit Y / Dreieck bearbeiten, B / Kreis fertig) oder im Launcher (Seite
  "Belegung"). Dort auch: stufenlos rollen, freie Kamera, Neigung umkehren, Tempo, Abstand.
  Tastensymbole im Spiel: In Menüs und im Pause-Menü zeigt eine Leiste unten rechts die Knöpfe,
  am Levelanfang und in der Pause oben links eine Steuerungsübersicht - jeweils 10 Sekunden,
  dann blenden sie aus (in Menüs nach jedem Bestätigen/Zurück wieder). Xbox- oder PlayStation-
  Symbole je nach angeschlossenem Controller. Abschaltbar im Launcher.
  Der Launcher selbst ist per Controller bedienbar: Steuerkreuz wählt, links/rechts ändert,
  A bestätigt, LB/RB wechselt die Seite, Y übernimmt, Start startet das Spiel - ideal am TV.
  Wichtig: Für den Controller Ballance über den HD Launcher starten (nicht direkt über GOG
  Galaxy - dort läuft das Spiel zwar auch in HD, aber ohne Controller).
Launcher
  Grafik: Bildschirm (Mehrschirm/TV), Anzeigemodus, Auflösung, Kantenglättung, Bildrate,
  Breitbild ohne Ränder. Controller: an/aus, Stick-Totzone, Belegung. Spiel: Sprache (DE, EN,
  ES, IT, FR), Intro überspringen, FPS-Anzeige, Extraleben-Fix.
  Hotkeys im Spiel (BallancePlayer): Alt+Enter Vollbild/Fenster, Alt+F4 beenden.
  Mod-Menü (BML+): im Spiel unter Optionen -> Mods; "/" öffnet die Befehlszeile.

ENGLISH
-------
For Ballance (GOG or Steam) on Windows 10/11.

Installation
  Run BallanceHD_Setup.exe. It finds the game (GOG/Steam registry, desktop shortcut), checks the
  game files and installs. Then start Ballance with "Ballance - HD Launcher".
  Everything is included: if the Microsoft Visual C++ 2015-2022 runtime (x86) is missing, the
  setup installs it as well (Windows asks once for administrator rights).
  Uninstall: start menu "Ballance - Remove HD Patch", launcher -> About, Apps & Features, or run
  the setup again. All original files are restored; save games / high scores are never touched.

What it does
  * BallancePlayer (doyaGu): 32-bit colour, any resolution, widescreen, borderless fullscreen.
  * BML+ mod loader (doyaGu & Gamepiaynmo): Hor+ widescreen field of view, frame limiter, fixes.
  * Ballance HD mod: matches the game cameras to the screen - full 16:9 / 21:9, no black bars.
  * 32-bit textures instead of 16-bit, anti-aliasing up to 8x MSAA.
  * Borderless fullscreen at native resolution (default): no tearing, frame rate = refresh rate.
  * No more "install DirectPlay?" prompt (empty dpnhpast.dll in Bin).
  * dgVoodoo 2.79 shipped by GOG/Steam is disabled (draws menus and sky black with the new
    player); Bin\D3D8.dll is backed up and restored on uninstall.

Controller (Xbox, DualShock 4, DualSense - USB or Bluetooth)
  Ballance only knows the keyboard; while you play, the launcher turns the buttons into the
  game's keys and hands the sticks to the Ballance HD mod: the left stick rolls the ball
  steplessly (direction and force), the right stick turns and tilts the camera freely around
  the ball, LB / RB turn it in 90° steps, Y raises it, A confirm, B back, Start pause, X skip
  tutorial, View / Share camera distance. Buttons can be remapped in the launcher ("Layout") or in the
  game: Options -> Controls (panel on the right, Y to edit). Button prompts (Xbox or PlayStation glyphs) show for
  10 seconds in menus, the pause menu and at level start, then fade out.
  The launcher itself works with a controller: d-pad, A, LB / RB pages, Y apply, Start play.
  Start the game from the HD Launcher to use a controller.
Credits
  Presented by CompiWare - made by BockwurstWilly using Claude.
  BallancePlayer and BML+ by doyaGu, Gamepiaynmo and the Ballance modding community (MIT).
  UI Elements (button glyphs) by Mikkel Julian "Arks" Petersen - Arks @ScissorMarks,
  https://arks.itch.io - CC BY 4.0.
  Ballance (c) 2004 Cyparade / Atari, today Ziggurat Interactive. Unofficial fan project; it
  contains no game files.
