Ballance - HD Patch 2.0.1
=========================

DEUTSCH
-------
Für Ballance (GOG oder Steam) unter Windows 10/11.

Installation
  BallanceHD_Setup.exe starten. Das Setup findet das Spiel automatisch (GOG/Steam-Registry,
  Desktop-Verknüpfung), prüft die Spieldateien und installiert. Danach Ballance über
  "Ballance - HD Launcher" (Desktop / Startmenü / Spielordner) starten.
  Es wird nichts zusätzlich benötigt (keine Visual-C++-Laufzeit, keine Administratorrechte).
  Ein Update von 1.x übernimmt Einstellungen und Tastenbelegung und entfernt den alten Mod Loader.
  Deinstallation: Startmenü "Ballance - HD-Patch entfernen", Launcher -> Info -> "HD-Patch
  entfernen", Windows "Apps & Features" oder das Setup erneut starten. Alle Originaldateien
  werden wiederhergestellt; Spielstände und Highscores (Database.tdb) bleiben unangetastet.

Starten - empfohlen: über den HD Launcher
  Am besten Ballance immer über "Ballance - HD Launcher" starten (A / Kreuz oder Start am
  Controller startet das Spiel). Dort werden Auflösung, Bildschirm, Anzeigemodus,
  Kantenglättung, Bildrate und Spielsprache eingestellt.
  Startet man das Spiel direkt (GOG Galaxy "Spielen", GOG-Desktopverknüpfung, Bin\Player.exe),
  läuft es trotzdem vollständig in HD mit Controller - mit den zuletzt im Launcher gespeicherten
  Einstellungen. Controller-Einstellungen gibt es auch im Spiel (Optionen -> Steuerung).
  Hinweis: Das GOG-Werkzeug "Ballance Language Selection" wirkt mit dem neuen Player nicht mehr;
  die Spielsprache wird im Launcher (Seite "Spiel") eingestellt.

Was der Patch macht
  * Neue Spiel-EXE "BallancePlayer" (doyaGu): 32-Bit-Farben, beliebige Auflösungen bis 4K und
    höher, randloses Vollbild, keine Registry, keine Windows-98-Kompatibilitätsmodi.
  * Eigenes Spiel-Plugin (Managers\BallanceHD.dll), läuft direkt in der Engine des Spiels:
    - Breitbild ohne Ränder: das Bild füllt den ganzen 16:9-/21:9-Bildschirm, das Sichtfeld wird
      seitlich erweitert (Hor+), oben und unten fehlt nichts.
    - Bildrate: VSync mit der Bildwiederholrate des Bildschirms - gleichmäßig 16,7 ms pro Bild bei
      60 Hz, keine Mikroruckler.
    - Controller-Steuerung, Tastensymbole und Controller-Einstellungen im Spiel (siehe unten).
  * 32-Bit-Texturen statt 16 Bit (kein Farbbanding am Himmel), Kantenglättung bis 8x MSAA.
  * Randloses Vollbild in nativer Auflösung (empfohlen): kein Tearing, Alt+Tab ohne Flackern.
    Im exklusiven Vollbild wirkt VSync unter Direct3D 8 nicht - dort begrenzt das Plugin die Bildrate.
  * Kein "DirectPlay installieren?"-Fenster mehr beim Start (das Spiel lädt dpnhpast.dll nur
    zur DirectX-Versionsprüfung; der Patch legt dafür eine leere DLL in den Ordner Bin).
  * dgVoodoo 2.79 (von GOG/Steam mitgeliefert) wird abgeschaltet: mit dem neuen Player zeichnet
    es Menüs und Himmel schwarz. Bin\D3D8.dll wird gesichert und beim Entfernen zurückgelegt.

Controller (Xbox, DualShock 4, DualSense - USB oder Bluetooth)
  Das Plugin liest die Controller direkt im Spiel - PlayStation-Controller ohne Steam oder
  DS4Windows. Das funktioniert auch, wenn das Spiel direkt (z. B. über GOG Galaxy) gestartet wird.
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
  am Levelanfang oben links eine Steuerungsübersicht - jeweils 10 Sekunden, dann blenden sie aus
  (in Menüs nach jedem Bestätigen/Zurück wieder). Xbox- oder PlayStation-Symbole je nach
  angeschlossenem Controller. Abschaltbar im Launcher.
  Der Launcher selbst ist per Controller bedienbar: Steuerkreuz wählt, links/rechts ändert,
  A bestätigt, LB/RB wechselt die Seite, Y übernimmt, Start startet das Spiel - ideal am TV.
Launcher
  Grafik: Bildschirm (Mehrschirm/TV), Anzeigemodus, Auflösung, Kantenglättung, Bildrate,
  Breitbild ohne Ränder. Controller: an/aus, Stick-Totzone, Belegung. Spiel: Sprache (DE, EN,
  ES, IT, FR), Intro überspringen, FPS-Anzeige.
  Hotkeys im Spiel (BallancePlayer): Alt+Enter Vollbild/Fenster, Alt+F4 beenden.
  Einstellungen des Plugins: BallanceHD\Settings.ini im Spielordner (Launcher und Spiel teilen
  sie sich), Protokoll: BallanceHD\plugin.log.

ENGLISH
-------
For Ballance (GOG or Steam) on Windows 10/11.

Installation
  Run BallanceHD_Setup.exe. It finds the game (GOG/Steam registry, desktop shortcut), checks the
  game files and installs. Then start Ballance with "Ballance - HD Launcher".
  Nothing else is needed (no Visual C++ runtime, no administrator rights). An update from 1.x
  keeps your settings and button layout and removes the old mod loader.
  Uninstall: start menu "Ballance - Remove HD Patch", launcher -> About, Apps & Features, or run
  the setup again. All original files are restored; save games / high scores are never touched.

Starting - recommended: through the HD Launcher
  Best always start Ballance with "Ballance - HD Launcher" (A / Cross or Start on the controller
  starts the game); resolution, display, display mode, anti-aliasing, frame rate and game
  language are set there. Started directly (GOG Galaxy "Play", the GOG desktop shortcut,
  Bin\Player.exe) the game still runs fully in HD with the controller, using the settings last
  saved in the launcher; controller settings are also in the game (Options -> Controls).
  Note: GOG's "Ballance Language Selection" tool no longer has any effect with the new player;
  set the game language in the launcher ("Game" page).

What it does
  * BallancePlayer (doyaGu): 32-bit colour, any resolution, borderless fullscreen.
  * Its own game plugin (Managers\BallanceHD.dll), running inside the game engine: Hor+
    widescreen without bars (16:9 / 21:9), vsync at the display's refresh rate (even frames, no
    micro-stutter), controller support, button prompts and controller settings in the game.
  * 32-bit textures instead of 16-bit, anti-aliasing up to 8x MSAA.
  * Borderless fullscreen at native resolution (default): no tearing, smooth Alt+Tab.
  * No more "install DirectPlay?" prompt (empty dpnhpast.dll in Bin).
  * dgVoodoo 2.79 shipped by GOG/Steam is disabled (draws menus and sky black with the new
    player); Bin\D3D8.dll is backed up and restored on uninstall.

Controller (Xbox, DualShock 4, DualSense - USB or Bluetooth)
  The plugin reads the controllers inside the game (PlayStation pads without Steam or
  DS4Windows), so the game can also be started directly. The left stick rolls the ball
  steplessly (direction and force), the right stick turns and tilts the camera freely around
  the ball, LB / RB turn it in 90° steps, Y raises it, A confirm, B back, Start pause, X skip
  tutorial, View / Share camera distance. Buttons can be remapped in the launcher ("Layout") or in
  the game: Options -> Controls (panel on the right, Y to edit). Button prompts (Xbox or
  PlayStation glyphs) show for 10 seconds in menus, the pause menu and at level start, then fade.
  The launcher itself works with a controller: d-pad, A, LB / RB pages, Y apply, Start play.
Credits
  Presented by CompiWare - made by BockwurstWilly using Claude.
  BallancePlayer by doyaGu and the Ballance modding community (MIT).
  In-game font: BML Sans SC (IBM Plex Sans SC, (c) IBM Corp.) - SIL Open Font License 1.1.
  UI Elements (button glyphs) by Mikkel Julian "Arks" Petersen - Arks @ScissorMarks,
  https://arks.itch.io - CC BY 4.0.
  Ballance (c) 2004 Cyparade / Atari, today Ziggurat Interactive. Unofficial fan project; it
  contains no game files.
