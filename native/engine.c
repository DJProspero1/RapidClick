#include "engine.h"
#include <string.h>
#include <wchar.h>

LONG rc_effective_rate(LONG requested,BOOL cell_profile){return cell_profile && requested>50?50:requested;}
BOOL rc_is_cell_window(HWND window){
    DWORD pid=0;GetWindowThreadProcessId(window,&pid);if(!pid)return FALSE;
    HANDLE process=OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,FALSE,pid);if(!process)return FALSE;
    wchar_t path[2048];DWORD length=2048;BOOL found=FALSE;
    if(QueryFullProcessImageNameW(process,0,path,&length)){const wchar_t *name=wcsrchr(path,L'\\');found=_wcsicmp(name?name+1:path,L"CellToSingularity.exe")==0;}
    CloseHandle(process);return found;
}

static LONG64 ticks(void) { LARGE_INTEGER t; QueryPerformanceCounter(&t); return t.QuadPart; }
static DWORD WINAPI rc_worker(void *context) {
    RcEngine *e=(RcEngine*)context;
    HANDLE timer=CreateWaitableTimerExW(NULL,NULL,CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,TIMER_ALL_ACCESS);
    if(!timer)timer=CreateWaitableTimerW(NULL,FALSE,NULL);
    if(!timer){InterlockedExchange(&e->error,(LONG)GetLastError());goto finished;}
    HANDLE waits[2]={e->stop,timer};
    if(e->delay_ms && WaitForSingleObject(e->stop,e->delay_ms)==WAIT_OBJECT_0)goto close_timer;
    POINT initial_pointer;HWND profile_target=e->target;
    if(!profile_target && GetCursorPos(&initial_pointer))profile_target=WindowFromPoint(initial_pointer);
    if(rc_is_cell_window(profile_target)){e->rate=rc_effective_rate(e->rate,TRUE);e->direct=FALSE;InterlockedExchange(&e->game_profile,1);}
    if(e->direct && !e->target){POINT p;if(!GetCursorPos(&p)){InterlockedExchange(&e->error,-1);goto close_timer;}e->target=WindowFromPoint(p);if(!e->target || GetAncestor(e->target,GA_ROOT)==e->avoid || !ScreenToClient(e->target,&p)){InterlockedExchange(&e->error,-1);goto close_timer;}e->position=MAKELPARAM(p.x,p.y);}
    LARGE_INTEGER f;QueryPerformanceFrequency(&f);
    INPUT events[64];memset(events,0,sizeof(events));
    for(int i=0;i<64;i++){events[i].type=INPUT_MOUSE;events[i].mi.dwFlags=e->flags;events[i].mi.dwExtraInfo=RC_SIGNATURE;}
    LONG64 start=ticks(),scheduled=0;
    InterlockedExchange64(&e->started,start);
    while(WaitForSingleObject(e->stop,0)!=WAIT_OBJECT_0 && !(GetAsyncKeyState(VK_F8)&0x8000)) {
        POINT p;
        if(!e->direct && e->avoid && GetCursorPos(&p) && GetAncestor(WindowFromPoint(p),GA_ROOT)==e->avoid) {InterlockedExchange(&e->error,-1);break;}
        LONG64 now=ticks();
        LONG64 wanted=(LONG64)((double)(now-start)*e->rate/f.QuadPart)+1;
        LONG64 due=wanted-scheduled;
        if(due>0) {
            UINT maximum=e->direct?32:8;
            UINT n=(UINT)(due>maximum?maximum:due);
            LONG64 count=InterlockedCompareExchange64(&e->clicks,0,0);
            if(e->limit && n>e->limit-count)n=(UINT)(e->limit-count);
            UINT sent=0;
            if(e->direct) {
                UINT down=e->flags&MOUSEEVENTF_RIGHTDOWN?WM_RBUTTONDOWN:e->flags&MOUSEEVENTF_MIDDLEDOWN?WM_MBUTTONDOWN:WM_LBUTTONDOWN;
                UINT up=down+1;WPARAM key=down==WM_RBUTTONDOWN?MK_RBUTTON:down==WM_MBUTTONDOWN?MK_MBUTTON:MK_LBUTTON;
                for(UINT i=0;i<n;i++) {
                    if(WaitForSingleObject(e->stop,0)==WAIT_OBJECT_0 || GetAsyncKeyState(VK_F8)&0x8000)break;
                    DWORD_PTR ignored;
                    if(!IsWindow(e->target) || !SendMessageTimeoutW(e->target,down,key,e->position,SMTO_ABORTIFHUNG|SMTO_ERRORONEXIT,8,&ignored)){PostMessageW(e->target,up,0,e->position);break;}
                    if(!SendMessageTimeoutW(e->target,up,0,e->position,SMTO_ABORTIFHUNG|SMTO_ERRORONEXIT,8,&ignored)){PostMessageW(e->target,up,0,e->position);break;}
                    sent++;
                }
            } else sent=SendInput(n,events,sizeof(INPUT));
            InterlockedAdd64(&e->clicks,sent);
            if(sent!=n){if(WaitForSingleObject(e->stop,0)!=WAIT_OBJECT_0)InterlockedExchange(&e->error,GetLastError()?(LONG)GetLastError():ERROR_TIMEOUT);break;}
            scheduled=wanted; // drop timing debt rather than saturating the input queue
            if(e->limit && count+sent>=e->limit)break;
        }
        // Batch high rates at 1 ms; low rates sleep until the next click. Never busy-spin.
        LONG64 next=e->rate>1000 ? now+f.QuadPart/1000 : start+(LONG64)(scheduled*(double)f.QuadPart/e->rate);
        LONG64 remaining=next-ticks();
        if(remaining>0) {
            LARGE_INTEGER due_time;due_time.QuadPart=-(LONG64)((double)remaining*10000000/f.QuadPart);
            if(due_time.QuadPart==0)due_time.QuadPart=-1;
            // Poll F8 within 4 ms even at very low click rates.
            if(due_time.QuadPart < -40000)due_time.QuadPart=-40000;
            if(!SetWaitableTimer(timer,&due_time,0,NULL,NULL,FALSE)){InterlockedExchange(&e->error,(LONG)GetLastError());break;}
            if(WaitForMultipleObjects(2,waits,FALSE,INFINITE)==WAIT_OBJECT_0)break;
        }
    }
    InterlockedExchange64(&e->ended,ticks());
close_timer:
    CloseHandle(timer);
finished:
    InterlockedExchange(&e->active,0);return 0;
}
BOOL rc_start(RcEngine *e,LONG rate,DWORD flags,DWORD delay_ms,HWND avoid,LONG64 limit,BOOL direct,HWND target,LPARAM position) {
    if(rate<1 || rate>10000 || limit<0 || InterlockedCompareExchange(&e->active,0,0))return FALSE;
    if(e->thread){CloseHandle(e->thread);e->thread=NULL;}if(e->stop){CloseHandle(e->stop);e->stop=NULL;}
    e->stop=CreateEventW(NULL,TRUE,FALSE,NULL);if(!e->stop)return FALSE;
    e->rate=rate;e->flags=flags;e->delay_ms=delay_ms;e->avoid=avoid;e->limit=limit;e->direct=direct;e->target=target;e->position=position;
    e->clicks=0;e->started=0;e->ended=0;e->error=0;e->game_profile=0;e->active=1;
    e->thread=CreateThread(NULL,0,rc_worker,e,0,NULL);
    if(!e->thread){e->active=0;CloseHandle(e->stop);e->stop=NULL;return FALSE;}return TRUE;
}
void rc_stop(RcEngine *e){if(e->stop)SetEvent(e->stop);}
void rc_dispose(RcEngine *e){rc_stop(e);if(e->thread){WaitForSingleObject(e->thread,INFINITE);CloseHandle(e->thread);}if(e->stop)CloseHandle(e->stop);memset(e,0,sizeof(*e));}
double rc_elapsed(RcEngine *e){LONG64 start=InterlockedCompareExchange64(&e->started,0,0);if(!start)return 0;LONG64 end=InterlockedCompareExchange64(&e->ended,0,0);LARGE_INTEGER f;QueryPerformanceFrequency(&f);return (double)((end?end:ticks())-start)/f.QuadPart;}
