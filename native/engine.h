#ifndef RAPIDCLICK_ENGINE_H
#define RAPIDCLICK_ENGINE_H
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#define RC_SIGNATURE ((ULONG_PTR)0x52434C4B)
typedef struct RcEngine {
    HANDLE thread, stop;
    volatile LONG active;
    volatile LONG error;
    volatile LONG64 clicks;
    volatile LONG64 started, ended;
    LONG rate;
    DWORD flags;
    DWORD delay_ms;
    HWND avoid;
    LONG64 limit;
    BOOL direct;
    HWND target;
    LPARAM position;
    volatile LONG game_profile;
} RcEngine;
BOOL rc_is_cell_window(HWND window);
LONG rc_effective_rate(LONG requested, BOOL cell_profile);
BOOL rc_start(RcEngine *engine, LONG rate, DWORD flags, DWORD delay_ms, HWND avoid, LONG64 limit, BOOL direct, HWND target, LPARAM position);
void rc_stop(RcEngine *engine);
void rc_dispose(RcEngine *engine);
double rc_elapsed(RcEngine *engine);
#endif
