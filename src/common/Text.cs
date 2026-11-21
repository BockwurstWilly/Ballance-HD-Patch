using System.Collections.Generic;
using System.Globalization;

namespace BallanceHD
{
    static class Version
    {
        public const string Text = "2.2.0";
        public const string BallancePlayer = "0.3.9";
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
            { "gfx_res", new[] { "Auflösung", "Resolution" } },
            { "gfx_res_native", new[] { "Nativ ({0} × {1})", "Native ({0} × {1})" } },
            { "gfx_aa", new[] { "Kantenglättung", "Anti-aliasing" } },
            { "gfx_off", new[] { "Aus", "Off" } },
            { "gfx_fps", new[] { "Bildrate", "Frame rate" } },
            { "gfx_fps_auto", new[] { "Automatisch (VSync, {0} Hz)", "Automatic (VSync, {0} Hz)" } },
            { "gfx_fps_n", new[] { "{0} FPS", "{0} FPS" } },
            { "gfx_fps_off", new[] { "Unbegrenzt", "Unlimited" } },
            { "gfx_wide", new[] { "Breitbild ohne Ränder", "Widescreen without bars" } },

            // controller
            { "pad_use", new[] { "Controller-Support", "Controller support" } },
            { "pad_none", new[] { "Kein Controller verbunden.", "No controller connected." } },
            { "pad_xbox", new[] { "Xbox-Controller verbunden.", "Xbox controller connected." } },
            { "pad_sony", new[] { "PlayStation-Controller verbunden.", "PlayStation controller connected." } },
            { "pad_prompts", new[] { "Tastensymbole im Spiel", "Button prompts in the game" } },
            { "pad_analog", new[] { "Stufenlos rollen (linker Stick)", "Stepless rolling (left stick)" } },
            { "pad_freecam", new[] { "Freie Kamera (rechter Stick)", "Free camera (right stick)" } },
            { "pad_invert", new[] { "Kamera-Neigung umkehren", "Invert camera tilt" } },
            { "pad_camspeed", new[] { "Kamera-Tempo", "Camera speed" } },
            { "pad_ingame_hint", new[] { "Diese Einstellungen und die Tastenbelegung gibt es auch im Spiel: Optionen → Steuerung, dort mit Y / Dreieck.", "These settings and the button layout are also in the game: Options → Controls, there with Y / Triangle." } },
            { "act_freecam", new[] { "Kamera frei drehen und neigen", "Turn and tilt the camera" } },
            { "map_reset", new[] { "Standardbelegung", "Default layout" } },
            { "map_hint", new[] { "Linker Stick und Steuerkreuz bewegen auch in den Menüs. Im Spiel: Optionen → Steuerung (Y / Dreieck).", "Left stick and d-pad also move through the menus. In the game: Options → Controls (Y / Triangle)." } },
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
            { "game_fps", new[] { "FPS-Anzeige", "FPS counter" } },
            { "game_ui_lang", new[] { "Sprache des Launchers", "Launcher language" } },
            { "lang_auto", new[] { "Automatisch (Windows)", "Automatic (Windows)" } },

            // info
            { "info_text", new[] {
                "Inoffizieller HD-Patch für Ballance (2004, Cyparade / Atari).\n\n" +
                "• Ballance HD von BockwurstWilly – Spiel-Plugin (Breitbild, VSync, Controller, Tastensymbole), Setup und Launcher\n" +
                "• BallancePlayer von doyaGu (MIT-Lizenz)\n" +
                "• Tastensymbole: UI Elements von Arks (@ScissorMarks, arks.itch.io) – CC BY 4.0\n\n" +
                "Deinstallieren stellt den Spielordner wieder her. Spielstände bleiben erhalten.",
                "Unofficial HD patch for Ballance (2004, Cyparade / Atari).\n\n" +
                "• Ballance HD by BockwurstWilly – game plugin (widescreen, vsync, controller, button prompts), setup and launcher\n" +
                "• BallancePlayer by doyaGu (MIT licence)\n" +
                "• Button glyphs: UI Elements by Arks (@ScissorMarks, arks.itch.io) – CC BY 4.0\n\n" +
                "Uninstalling restores the game folder. Save games are kept." } },
            { "on", new[] { "An", "On" } },
            { "off", new[] { "Aus", "Off" } },
            { "hint_page", new[] { "Seite", "Page" } },
            { "hint_select", new[] { "Wählen", "Select" } },
            { "hint_apply", new[] { "Übernehmen", "Apply" } },
            { "hint_play", new[] { "Spielen", "Play" } },
            { "info_folder", new[] { "Spielordner öffnen", "Open game folder" } },
            { "info_uninstall", new[] { "HD-Patch entfernen …", "Remove HD patch …" } },

            // setup
            { "su_title", new[] { "Ballance – HD-Patch Setup", "Ballance – HD Patch Setup" } },
            { "su_folder", new[] { "Spielordner", "Game folder" } },
            { "su_browse", new[] { "Durchsuchen …", "Browse …" } },
            { "su_browse_desc", new[] { "Ordner von Ballance wählen (enthält base.cmo und den Ordner Bin)", "Choose the Ballance folder (contains base.cmo and the Bin folder)" } },
            { "su_desktop", new[] { "Verknüpfung auf dem Desktop", "Desktop shortcut" } },
            { "su_startmenu", new[] { "Einträge im Startmenü", "Start menu entries" } },
            { "su_bonus", new[] { "Bonus-Level 13 „SuDu“ hinzufügen", "Add bonus level 13 \"SuDu\"" } },
            { "su_bonus_note", new[] { "Braucht eine Internetverbindung: Das offizielle Zusatzlevel von 2004 (2,2 MB) wird von der archivierten Ballance-Webseite geladen, weil der Patch keine Spieldateien mitliefert. Ohne Haken fehlt nur dieser Bonus.", "Needs an Internet connection: the official add-on level from 2004 (2.2 MB) is downloaded from the archived Ballance website, because the patch ships no game files. Without it, only this bonus is missing." } },
            { "su_install", new[] { "INSTALLIEREN", "INSTALL" } },
            { "su_update", new[] { "AKTUALISIEREN", "UPDATE" } },
            { "su_uninstall", new[] { "Entfernen", "Remove" } },
            { "su_launch", new[] { "LAUNCHER STARTEN", "START LAUNCHER" } },
            { "su_bad", new[] { "Hier ist kein vollständiges Ballance:", "This is not a complete Ballance installation:" } },
            { "su_note", new[] { "Geänderte Originaldateien werden in „_HDPatch\\backup“ gesichert; „Entfernen“ stellt sie wieder her.", "Changed original files are backed up to \"_HDPatch\\backup\"; \"Remove\" restores them." } },
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
            { "log_bonus_load", new[] { "Lade Bonus-Level 13 von web.archive.org …", "Downloading bonus level 13 from web.archive.org …" } },
            { "log_bonus_done", new[] { "Bonus-Level 13 installiert.", "Bonus level 13 installed." } },
            { "log_bonus_fail", new[] { "Bonus-Level 13 konnte nicht geladen werden ({0}). Alles andere ist installiert; das Setup kann es später nachholen.", "Bonus level 13 could not be downloaded ({0}). Everything else is installed; run the setup again later to add it." } },
            { "log_bonus_own", new[] { "Bonus-Level 13 ist bereits vorhanden (selbst installiert) – bleibt unverändert.", "Bonus level 13 is already present (installed by hand) – left as it is." } },
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
