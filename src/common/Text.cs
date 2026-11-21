using System.Collections.Generic;
using System.Globalization;

namespace BallanceHD
{
    static class Version
    {
        public const string Text = "1.3.2";
        public const string BallancePlayer = "0.3.9";
        public const string BmlPlus = "0.3.14";
    }

    /// <summary>UI texts, German or English: the Windows display language, or the choice under Info.</summary>
    static class T
    {
        public static readonly bool WindowsGerman = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
        public static bool German = WindowsGerman;
        public static string Language = "auto";     // auto | en | de

        public static void SetLanguage(string lang)
        {
            lang = (lang ?? "auto").Trim().ToLowerInvariant();
            Language = lang == "en" || lang == "de" ? lang : "auto";
            German = Language == "auto" ? WindowsGerman : Language == "de";
        }

        public static string Get(string key, params object[] args)
        {
            string[] pair;
            if (!Texts.TryGetValue(key, out pair))
                return key;
            var s = German ? pair[0] : pair[1];
            return args.Length > 0 ? string.Format(s, args) : s;
        }

        static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
        {
            // game check
            { "chk_nodir", new[] { "Ordner nicht gefunden.", "Folder not found." } },
            { "chk_missing", new[] { "{0} fehlt.", "{0} is missing." } },
            { "chk_gog", new[] { "Ballance (GOG-Version) erkannt.", "Ballance (GOG release) detected." } },
            { "chk_steam", new[] { "Ballance (Steam-Version) erkannt.", "Ballance (Steam release) detected." } },
            { "chk_other", new[] { "Ballance erkannt (unbekannte Version – sollte funktionieren).", "Ballance detected (unknown release – should work)." } },
            { "chk_ck2", new[] { "Hinweis: unbekannte Render-Engine (CK2_3D.dll) – das 16:9-Bild ohne Ränder funktioniert evtl. nicht.", "Note: unknown render engine (CK2_3D.dll) – the borderless 16:9 picture may not work." } },
            { "chk_vcrt", new[] { "Microsoft Visual C++ 2015–2022 (x86) fehlt – wird für den Mod Loader benötigt.", "Microsoft Visual C++ 2015–2022 (x86) is missing – the mod loader needs it." } },

            // launcher frame
            { "nav_gfx", new[] { "Grafik", "Graphics" } },
            { "nav_pad", new[] { "Controller", "Controller" } },
            { "nav_map", new[] { "Belegung", "Layout" } },
            { "nav_game", new[] { "Spiel", "Game" } },
            { "nav_info", new[] { "Info", "About" } },
            { "motto", new[] { "„Balance ist alles.“", "\"Balance is everything.\"" } },
            { "btn_play", new[] { "SPIELEN", "PLAY" } },
            { "btn_apply", new[] { "Übernehmen", "Apply" } },
            { "btn_close", new[] { "Schließen", "Close" } },
            { "st_saved", new[] { "Einstellungen gespeichert.", "Settings saved." } },
            { "st_running", new[] { "Ballance läuft bereits.", "Ballance is already running." } },
            { "st_padplay", new[] { "Controller: A / Kreuz = Spielen", "Controller: A / Cross = Play" } },

            // graphics
            { "gfx_display", new[] { "Bildschirm", "Display" } },
            { "gfx_display_item", new[] { "{0}:  {2} × {3}, {4} Hz{5}  ·  {1}", "{0}:  {2} × {3}, {4} Hz{5}  ·  {1}" } },
            { "gfx_primary", new[] { " (Haupt)", " (primary)" } },
            { "gfx_mode", new[] { "Anzeigemodus", "Display mode" } },
            { "gfx_mode_bl", new[] { "Randloses Vollbild (empfohlen)", "Borderless fullscreen (recommended)" } },
            { "gfx_mode_fs", new[] { "Exklusives Vollbild", "Exclusive fullscreen" } },
            { "gfx_mode_win", new[] { "Fenster", "Window" } },
            { "gfx_mode_hint", new[] { "Randlos: native Auflösung, kein Tearing, Alt+Tab ohne Flackern. Exklusiv: mit Tearing (VSync wirkt bei Direct3D 8 unter Windows 10/11 nicht).", "Borderless: native resolution, no tearing, smooth Alt+Tab. Exclusive: tears (vsync has no effect for Direct3D 8 on Windows 10/11)." } },
            { "gfx_res", new[] { "Auflösung", "Resolution" } },
            { "gfx_res_native", new[] { "Nativ ({0} × {1})", "Native ({0} × {1})" } },
            { "gfx_aa", new[] { "Kantenglättung", "Anti-aliasing" } },
            { "gfx_off", new[] { "Aus", "Off" } },
            { "gfx_fps", new[] { "Bildrate", "Frame rate" } },
            { "gfx_fps_auto", new[] { "Automatisch (VSync, {0} Hz)", "Automatic (VSync, {0} Hz)" } },
            { "gfx_fps_n", new[] { "{0} FPS", "{0} FPS" } },
            { "gfx_fps_off", new[] { "Unbegrenzt", "Unlimited" } },
            { "gfx_wide", new[] { "Breitbild ohne Ränder (16:9, 21:9 …)", "Widescreen without bars (16:9, 21:9 …)" } },
            { "gfx_wide_hint", new[] { "Füllt den ganzen Bildschirm; das Sichtfeld wird seitlich erweitert, oben und unten fehlt nichts.", "Fills the whole screen; the view is widened at the sides, nothing is cut off at the top or bottom." } },
            { "gfx_note", new[] { "Immer aktiv: 32-Bit-Farben und -Texturen statt 16 Bit, scharfe HUD-Grafik in nativer Auflösung.", "Always on: 32-bit colour and textures instead of 16-bit, sharp HUD at native resolution." } },

            // controller
            { "pad_use", new[] { "Controller benutzen", "Use controller" } },
            { "pad_use_hint", new[] { "Xbox, DualShock 4 und DualSense – USB oder Bluetooth, ohne Steam oder DS4Windows.", "Xbox, DualShock 4 and DualSense – USB or Bluetooth, no Steam or DS4Windows needed." } },
            { "pad_none", new[] { "Kein Controller verbunden.", "No controller connected." } },
            { "pad_xbox", new[] { "Xbox-Controller verbunden.", "Xbox controller connected." } },
            { "pad_sony", new[] { "PlayStation-Controller verbunden.", "PlayStation controller connected." } },
            { "pad_prompts", new[] { "Tastensymbole im Spiel einblenden (Menüs, Pause, Levelstart)", "Show button prompts in the game (menus, pause, level start)" } },
            { "pad_analog", new[] { "Kugel stufenlos rollen (linker Stick)", "Roll the ball steplessly (left stick)" } },
            { "pad_freecam", new[] { "Freie Kamera (rechter Stick dreht und neigt, View wechselt den Abstand)", "Free camera (right stick turns and tilts, View changes the distance)" } },
            { "pad_invert", new[] { "Kamera-Neigung umkehren", "Invert camera tilt" } },
            { "pad_camspeed", new[] { "Kamera-Tempo", "Camera speed" } },
            { "pad_ingame_hint", new[] { "Diese Einstellungen und die Tastenbelegung gibt es auch im Spiel: Optionen → Steuerung, dort mit Y / Dreieck.", "These settings and the button layout are also in the game: Options → Controls, there with Y / Triangle." } },
            { "pad_launcher_hint", new[] { "Launcher mit dem Controller: Steuerkreuz wählt aus, links/rechts ändert, A bestätigt, LB/RB wechselt die Seite, Y übernimmt, Start spielt.", "The launcher with a controller: d-pad selects, left/right changes, A confirms, LB/RB switch pages, Y applies, Start plays." } },
            { "act_freecam", new[] { "Kamera frei drehen und neigen", "Turn and tilt the camera" } },
            { "map_reset", new[] { "Standardbelegung", "Default layout" } },
            { "map_hint", new[] { "Linker Stick und Steuerkreuz bewegen auch in den Menüs. Im Spiel: Optionen → Steuerung (Y / Dreieck).", "Left stick and d-pad also move through the menus. In the game: Options → Controls (Y / Triangle)." } },
            { "st_padnav", new[] { "Controller: LB/RB Seite · A wählen · Start spielen", "Controller: LB/RB page · A select · Start play" } },
            { "pad_dead", new[] { "Stick-Totzone", "Stick dead zone" } },
            { "pad_layout", new[] { "Belegung", "Layout" } },
            { "pad_layout_hint", new[] { "Gilt für die Standard-Tasten des Spiels.", "Uses the game's default keys." } },
            { "pad_l_stick", new[] { "Linker Stick / Steuerkreuz", "Left stick / D-pad" } },
            { "pad_r_stick_x", new[] { "Rechter Stick ← →", "Right stick ← →" } },
            { "pad_r_stick_y", new[] { "Rechter Stick ↑", "Right stick ↑" } },
            { "pad_shoulders", new[] { "LB / RB  ·  L1 / R1", "LB / RB  ·  L1 / R1" } },
            { "pad_triggers", new[] { "LT / RT  ·  L2 / R2", "LT / RT  ·  L2 / R2" } },
            { "pad_a", new[] { "A / Kreuz", "A / Cross" } },
            { "pad_b", new[] { "B / Kreis", "B / Circle" } },
            { "pad_x", new[] { "X / Quadrat", "X / Square" } },
            { "pad_y", new[] { "Y / Dreieck", "Y / Triangle" } },
            { "pad_start", new[] { "Start / Options", "Start / Options" } },
            { "pad_back", new[] { "View / Share", "View / Share" } },
            { "act_roll", new[] { "Kugel rollen · Menü", "Roll the ball · menus" } },
            { "act_cam", new[] { "Kamera drehen", "Rotate camera" } },
            { "act_lift", new[] { "Kamera anheben (halten)", "Raise camera (hold)" } },
            { "act_confirm", new[] { "Bestätigen", "Confirm" } },
            { "act_back", new[] { "Zurück", "Back" } },
            { "act_skip", new[] { "Einführung überspringen", "Skip tutorial" } },
            { "act_pause", new[] { "Pause-Menü", "Pause menu" } },
            { "act_reset", new[] { "Kugel neu setzen (R)", "Respawn the ball (R)" } },
            { "pad_menu_hint", new[] { "Im Launcher: A / Kreuz startet das Spiel.", "In the launcher: A / Cross starts the game." } },

            // game
            { "game_lang", new[] { "Spielsprache", "Game language" } },
            { "game_skip", new[] { "Atari-/Logo-Intro überspringen", "Skip the Atari / logo intro" } },
            { "game_fps", new[] { "FPS-Anzeige oben links", "FPS counter top left" } },
            { "game_title", new[] { "Mod-Loader-Versionszeile oben anzeigen", "Show the mod loader version line at the top" } },
            { "game_lifeball", new[] { "Hänger beim Einsammeln von Extraleben beheben", "Fix the freeze when picking up extra lives" } },
            { "game_ui_lang", new[] { "Sprache des Launchers", "Launcher language" } },
            { "lang_auto", new[] { "Automatisch (Windows)", "Automatic (Windows)" } },
            { "game_save_hint", new[] { "Spielstände und Highscores bleiben in Database.tdb und werden nicht verändert.", "Save games and high scores stay in Database.tdb and are not touched." } },

            // info
            { "info_text", new[] {
                "Inoffizieller HD-Patch für Ballance (2004, Cyparade / Atari).\n\n" +
                "• BallancePlayer {0} von doyaGu – neue Spiel-EXE: 32 Bit, hohe Auflösungen, Breitbild, randloses Vollbild (MIT-Lizenz)\n" +
                "• Ballance Mod Loader Plus {1} von doyaGu & Gamepiaynmo – Breitbild-Sichtfeld, Bildratenbegrenzung, Fixes (MIT-Lizenz)\n" +
                "• Ballance HD (dieses Paket) – Setup, Launcher, Controller, randloses 16:9-Bild\n" +
                "• Tastensymbole: UI Elements von Arks (@ScissorMarks, arks.itch.io) – CC BY 4.0\n\n" +
                "Deinstallieren stellt den Spielordner wieder her. Spielstände bleiben erhalten.",
                "Unofficial HD patch for Ballance (2004, Cyparade / Atari).\n\n" +
                "• BallancePlayer {0} by doyaGu – new game executable: 32-bit, high resolutions, widescreen, borderless fullscreen (MIT licence)\n" +
                "• Ballance Mod Loader Plus {1} by doyaGu & Gamepiaynmo – widescreen field of view, frame limiter, fixes (MIT licence)\n" +
                "• Ballance HD (this package) – setup, launcher, controller, borderless 16:9 picture\n" +
                "• Button glyphs: UI Elements by Arks (@ScissorMarks, arks.itch.io) – CC BY 4.0\n\n" +
                "Uninstalling restores the game folder. Save games are kept." } },
            { "info_folder", new[] { "Spielordner öffnen", "Open game folder" } },
            { "info_uninstall", new[] { "HD-Patch entfernen …", "Remove HD patch …" } },

            // setup
            { "su_title", new[] { "Ballance – HD-Patch Setup", "Ballance – HD Patch Setup" } },
            { "su_folder", new[] { "Spielordner", "Game folder" } },
            { "su_browse", new[] { "Durchsuchen …", "Browse …" } },
            { "su_browse_desc", new[] { "Ordner von Ballance wählen (enthält base.cmo und den Ordner Bin)", "Choose the Ballance folder (contains base.cmo and the Bin folder)" } },
            { "su_desktop", new[] { "Verknüpfung auf dem Desktop", "Desktop shortcut" } },
            { "su_startmenu", new[] { "Einträge im Startmenü", "Start menu entries" } },
            { "su_install", new[] { "INSTALLIEREN", "INSTALL" } },
            { "su_update", new[] { "AKTUALISIEREN", "UPDATE" } },
            { "su_uninstall", new[] { "Entfernen", "Remove" } },
            { "su_launch", new[] { "LAUNCHER STARTEN", "START LAUNCHER" } },
            { "su_bad", new[] { "Hier ist kein vollständiges Ballance:", "This is not a complete Ballance installation:" } },
            { "su_note", new[] { "Geänderte Originaldateien werden in „_HDPatch\\backup“ gesichert; „Entfernen“ stellt sie wieder her.", "Changed original files are backed up to \"_HDPatch\\backup\"; \"Remove\" restores them." } },
            { "chk_vcrt_bundled", new[] { "Microsoft Visual C++ (x86) fehlt noch – das Setup installiert es mit (Windows fragt einmal nach Administratorrechten).", "Microsoft Visual C++ (x86) is missing – the setup installs it as well (Windows asks once for administrator rights)." } },
            { "su_success", new[] { "Der HD-Patch ist installiert.\n\nStarte Ballance ab jetzt über „Ballance - HD Launcher“.", "The HD patch is installed.\n\nFrom now on, start Ballance with \"Ballance - HD Launcher\"." } },
            { "su_uninstall_confirm", new[] { "HD-Patch entfernen und die Originaldateien wiederherstellen?\nSpielstände bleiben erhalten.", "Remove the HD patch and restore the original files?\nSave games are kept." } },
            { "su_removed", new[] { "Der HD-Patch wurde entfernt.", "The HD patch has been removed." } },
            { "lnk_uninstall", new[] { "Ballance – HD-Patch entfernen", "Ballance - Remove HD Patch" } },

            // install log
            { "log_backup", new[] { "Originaldateien gesichert: {0}", "Original files backed up: {0}" } },
            { "log_update", new[] { "Aktualisierung – Sicherung und Einstellungen bleiben.", "Update – backup and settings are kept." } },
            { "log_copy", new[] { "kopiert: {0}", "copied: {0}" } },
            { "log_keep", new[] { "behalten: {0}", "kept: {0}" } },
            { "log_aside", new[] { "deaktiviert: {0} (dgVoodoo – stört die Darstellung)", "disabled: {0} (dgVoodoo – breaks rendering)" } },
            { "log_settings", new[] { "Grafik eingestellt: {0} × {1}, {2} Hz mit VSync, randlos", "Graphics set: {0} × {1}, {2} Hz with vsync, borderless" } },
            { "log_shortcuts", new[] { "Verknüpfungen angelegt.", "Shortcuts created." } },
            { "log_done", new[] { "Fertig.", "Done." } },
            { "log_vcrt", new[] { "Installiere Microsoft Visual C++ 2015–2022 (x86) …", "Installing Microsoft Visual C++ 2015–2022 (x86) …" } },
            { "log_vcrt_done", new[] { "Visual C++ installiert.", "Visual C++ installed." } },
            { "err_vcrt_declined", new[] { "Die Installation von Microsoft Visual C++ (x86) wurde abgebrochen. Sie wird für den Mod Loader benötigt – bitte erneut installieren und die Windows-Abfrage bestätigen.", "Installing Microsoft Visual C++ (x86) was cancelled. The mod loader needs it – please install again and confirm the Windows prompt." } },
            { "err_vcrt", new[] { "Microsoft Visual C++ (x86) konnte nicht installiert werden (Fehler {0}).", "Microsoft Visual C++ (x86) could not be installed (error {0})." } },
            { "log_restore", new[] { "wiederhergestellt: {0}", "restored: {0}" } },
            { "log_delete", new[] { "entfernt: {0}", "removed: {0}" } },
            { "log_leftover", new[] { "Ordner {0} konnte nicht gelöscht werden: {1}", "Could not delete folder {0}: {1}" } },
            { "log_uninstalled", new[] { "HD-Patch entfernt.", "HD patch removed." } },

            // errors
            { "err_nogame", new[] { "Ballance wurde nicht gefunden. Bitte den Spielordner wählen.", "Ballance was not found. Please choose the game folder." } },
            { "err_running", new[] { "Ballance läuft gerade – bitte zuerst beenden.", "Ballance is running – please quit it first." } },
            { "err_check", new[] { "Der Ordner enthält kein vollständiges Ballance:", "The folder does not contain a complete Ballance:" } },
            { "err_nobackup", new[] { "Der HD-Patch ist in diesem Ordner nicht installiert.", "The HD patch is not installed in this folder." } },
            { "err_nowrite", new[] { "Keine Schreibrechte für {0}.", "No write access to {0}." } },
            { "err_notinstalled", new[] { "Der HD-Patch ist nicht (vollständig) installiert. Bitte das Setup erneut ausführen.", "The HD patch is not (completely) installed. Please run the setup again." } },
            { "err_start", new[] { "Ballance konnte nicht gestartet werden:\n{0}", "Could not start Ballance:\n{0}" } },
        };
    }
}
