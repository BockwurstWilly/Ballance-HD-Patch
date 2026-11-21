Ballance - HD Patch 1.0.0
=========================

DEUTSCH
-------
Für Ballance (GOG oder Steam) unter Windows 10/11.

Installation
  BallanceHD_Setup.exe starten. Das Setup findet das Spiel automatisch (GOG/Steam-Registry,
  Desktop-Verknüpfung), prüft die Spieldateien und installiert. Danach Ballance über
  "Ballance - HD Launcher" (Desktop / Startmenü / Spielordner) starten.
  Voraussetzung: Microsoft Visual C++ 2015-2022 (x86) - fehlt sie, zeigt das Setup einen
  Download-Knopf (https://aka.ms/vs/17/release/vc_redist.x86.exe).
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
  Ballance kennt nur die Tastatur. Der Launcher bleibt beim Spielen unsichtbar im Hintergrund
  und übersetzt den Controller in die Standard-Tasten des Spiels (nur solange das Spielfenster
  vorne ist). PlayStation-Controller werden direkt gelesen - ohne Steam oder DS4Windows.
    Linker Stick / Steuerkreuz        Kugel rollen, Menüs (Pfeiltasten)
    Rechter Stick links/rechts, LB/RB Kamera drehen (Shift + Pfeil)
    Rechter Stick hoch, LT/RT, Y      Kamera anheben (Leertaste, halten)
    A / Kreuz                         Bestätigen (Enter)
    B / Kreis, Start / Options        Zurück / Pause (Esc)
    X / Quadrat                       Einführung überspringen (Q)
    View / Share                      Kugel neu setzen (R)
  Im Launcher startet A / Kreuz das Spiel - ideal am TV.
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
  Requires Microsoft Visual C++ 2015-2022 (x86); the setup offers the download if it is missing.
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
  Ballance only knows the keyboard; while you play, the launcher translates the controller into
  the game's default keys (only while the game window is in front). Layout: left stick / d-pad
  roll and menus, right stick left/right or LB/RB rotate the camera, right stick up / LT / RT / Y
  raise the camera, A confirm, B / Start back / pause, X skip tutorial, View / Share respawn.
  Start the game from the HD Launcher to use a controller.

Credits
  Presented by CompiWare - made by BockwurstWilly using Claude.
  BallancePlayer and BML+ by doyaGu, Gamepiaynmo and the Ballance modding community (MIT).
  Ballance (c) 2004 Cyparade / Atari, today Ziggurat Interactive. Unofficial fan project; it
  contains no game files.
