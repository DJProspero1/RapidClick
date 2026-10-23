#define UNICODE
#define _UNICODE
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdint.h>

#define SIGNATURE ((ULONG_PTR)0x52434C4B)
static HWND target;
static volatile LONG64 downs, ups, pings, max_latency;
static LARGE_INTEGER frequency;
static RECT old_clip;
static POINT old_pointer;
static volatile LONG canceled;
static double seconds(void) { LARGE_INTEGER t; QueryPerformanceCounter(&t); return (double)t.QuadPart / frequency.QuadPart; }
static LRESULT CALLBACK procedure(HWND w, UINT m, WPARAM a, LPARAM b) {
    if (m == WM_CLOSE) { InterlockedExchange(&canceled, 1); return 0; }
    if (m == WM_APP+1) { LONG64 delay = (LONG64)(seconds()*1000000) - (LONG64)b; InterlockedIncrement64(&pings); if (delay > max_latency) InterlockedExchange64(&max_latency, delay); return 0; }
    if (m >= WM_MOUSEFIRST && m <= WM_MOUSELAST) {
        if ((ULONG_PTR)GetMessageExtraInfo() == SIGNATURE) {
            if (m == WM_LBUTTONDOWN) InterlockedIncrement64(&downs);
            if (m == WM_LBUTTONUP) InterlockedIncrement64(&ups);
        }
        return 0;
    }
    if (m == WM_PAINT) { PAINTSTRUCT ps; HDC dc=BeginPaint(w,&ps); RECT r; GetClientRect(w,&r); SetBkMode(dc,TRANSPARENT); SetTextColor(dc,RGB(230,224,233)); DrawTextW(dc,L"Silent input benchmark\nF8 cancels immediately",-1,&r,DT_CENTER|DT_VCENTER|DT_WORDBREAK); EndPaint(w,&ps); return 0; }
    return DefWindowProcW(w,m,a,b);
}
static DWORD WINAPI work(void *unused) {
    (void)unused;
    INPUT input[128]={0};
    for(int i=0;i<128;i++){ input[i].type=INPUT_MOUSE; input[i].mi.dwFlags=MOUSEEVENTF_LEFTDOWN|MOUSEEVENTF_LEFTUP; input[i].mi.dwExtraInfo=SIGNATURE; }
    Sleep(200);
    printf("{\"cases\":[\n");
    int batches[]={1,2,5,10,20,50};
    for(int test=0;test<6 && !canceled;test++) {
        int n=batches[test]; LONG64 sent=0; InterlockedExchange64(&downs,0);InterlockedExchange64(&ups,0);InterlockedExchange64(&pings,0);InterlockedExchange64(&max_latency,0);
        double start=seconds(), end=start+0.3, worst=0; int calls=0;
        while(seconds()<end && !canceled) {
            if(GetAsyncKeyState(VK_F8)&0x8000){canceled=1;break;}
            POINT p;GetCursorPos(&p); if(WindowFromPoint(p)!=target){canceled=1;break;}
            PostMessageW(target,WM_APP+1,0,(LPARAM)(LONG64)(seconds()*1000000));
            double t=seconds(); UINT accepted=SendInput(n,input,sizeof(INPUT)); double cost=seconds()-t;
            if(cost>worst)worst=cost; if(accepted!=(UINT)n){canceled=1;break;}sent+=n;calls++;
        }
        double elapsed=seconds()-start;
        Sleep(150);
        printf("%s{\"batch\":%d,\"sent\":%lld,\"received\":%lld,\"released\":%lld,\"seconds\":%.6f,\"cps\":%.1f,\"maxSendMs\":%.3f,\"maxPingMs\":%.3f,\"calls\":%d}",test?",\n":"",n,sent,downs,ups,elapsed,sent/elapsed,worst*1000,max_latency/1000.0,calls);fflush(stdout);
    }
    printf("\n],\"canceled\":%s}\n",canceled?"true":"false");
    ClipCursor(&old_clip);SetCursorPos(old_pointer.x,old_pointer.y);PostMessageW(target,WM_QUIT,0,0);return 0;
}
int main(void) {
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);QueryPerformanceFrequency(&frequency);
    HINSTANCE instance=GetModuleHandleW(NULL);WNDCLASSW c={0};c.lpfnWndProc=procedure;c.hInstance=instance;c.lpszClassName=L"RapidClickSilentProbe";c.hbrBackground=CreateSolidBrush(RGB(20,18,24));RegisterClassW(&c);
    target=CreateWindowExW(WS_EX_TOPMOST,c.lpszClassName,L"RapidClick silent benchmark",WS_OVERLAPPED|WS_CAPTION,400,250,420,240,NULL,NULL,instance,NULL);
    ShowWindow(target,SW_SHOW);SetForegroundWindow(target);UpdateWindow(target);
    GetClipCursor(&old_clip);GetCursorPos(&old_pointer);RECT r;GetClientRect(target,&r);POINT p={(r.right-r.left)/2,(r.bottom-r.top)/2};ClientToScreen(target,&p);SetCursorPos(p.x,p.y);RECT clip={p.x-2,p.y-2,p.x+3,p.y+3};ClipCursor(&clip);
    HANDLE thread=CreateThread(NULL,0,work,NULL,0,NULL);MSG message;while(GetMessageW(&message,NULL,0,0)>0){TranslateMessage(&message);DispatchMessageW(&message);}WaitForSingleObject(thread,INFINITE);CloseHandle(thread);ClipCursor(&old_clip);SetCursorPos(old_pointer.x,old_pointer.y);DestroyWindow(target);DeleteObject(c.hbrBackground);return canceled?1:0;
}
