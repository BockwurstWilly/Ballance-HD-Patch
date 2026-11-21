/*
 * Ballance HD plugin - the Windows side (win.c), as a plain C interface.
 * win.c is compiled with the normal Windows headers (GNU target); the C++ part that talks to the Virtools engine
 * is compiled for the Microsoft C++ ABI and cannot include them. Plain C calls work the same in both.
 */
#ifndef BALLANCEHD_WIN_H
#define BALLANCEHD_WIN_H

#ifdef __cplusplus
extern "C" {
#endif

/* ---- files, log, time ---- */
void hd_init(void);                               /* paths, log, controller threads */
void hd_shutdown(void);
void hd_log(const char *text);
void hd_logi(const char *text, int a, int b);
double hd_time(void);                             /* seconds, high resolution */
int hd_file_exists(const char *relative);         /* relative to the game folder, e.g. "BallanceHD\\glyphs\\x.tga" */
const char *hd_game_path(const char *relative);   /* full path (static buffer, valid until the next call) */
int hd_read_file(const char *relative, void *buf, int size);   /* bytes read, 0 if missing */

/* ---- settings: <game>\BallanceHD\Settings.ini ---- */
int hd_ini_int(const char *section, const char *key, int def);
void hd_ini_str(const char *section, const char *key, const char *def, char *out, int size);
void hd_ini_set_int(const char *section, const char *key, int value);
void hd_ini_set_str(const char *section, const char *key, const char *value);

/* ---- controllers: Xbox (XInput) and PlayStation (DualShock 4 / DualSense over HID, USB or Bluetooth) ---- */
enum {
    HD_DPAD_UP = 0x0001, HD_DPAD_DOWN = 0x0002, HD_DPAD_LEFT = 0x0004, HD_DPAD_RIGHT = 0x0008,
    HD_START = 0x0010, HD_BACK = 0x0020, HD_LTHUMB = 0x0040, HD_RTHUMB = 0x0080,
    HD_LB = 0x0100, HD_RB = 0x0200, HD_A = 0x1000, HD_B = 0x2000, HD_X = 0x4000, HD_Y = 0x8000,
    HD_LT = 0x10000, HD_RT = 0x20000
};
enum { HD_PAD_NONE = 0, HD_PAD_XBOX = 1, HD_PAD_PS = 2 };
typedef struct hd_pad {
    float lx, ly, rx, ry;     /* -1..1, y up */
    int buttons;              /* HD_* bits, triggers as HD_LT / HD_RT */
    int kind;                 /* HD_PAD_* */
} hd_pad;
void hd_pad_poll(hd_pad *out);

/* ---- keyboard: scan codes as DirectInput reads them (0x1xx = extended) ---- */
void hd_key(unsigned short scan, int down);
void hd_release_keys(void);
int hd_game_in_front(void);
int hd_mouse_click(int *x, int *y);           /* left click in the game window (client pixels) */

#ifdef __cplusplus
}
#endif

#endif
