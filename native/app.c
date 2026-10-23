#ifndef UNICODE
#define UNICODE
#endif
#define _UNICODE
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <windowsx.h>
#include <dwmapi.h>
#include <commctrl.h>
#include <stdlib.h>
#include <wchar.h>
#include "engine.h"

#define ID_RATE 101
#define ID_BUTTON 102
#define ID_START 103
#define ID_STATUS 104
#define ID_METHOD 105
static RcEngine engine;
static HWND main_window,rate_edit,button_choice,start_button,status_label,method_choice;
static HFONT font,large_font;
static HBRUSH background,field;
static int scale=96;
static BOOL ready;
static BOOL verify_ui;
static LONG64 previous_count;
static DWORD previous_time;
static int px(int n){return MulDiv(n,scale,96);}
static void status(const wchar_t *text){SetWindowTextW(status_label,text);}
static void refresh(void){
    BOOL active=InterlockedCompareExchange(&engine.active,0,0)!=0;
    EnableWindow(rate_edit,!active);EnableWindow(button_choice,!active);EnableWindow(method_choice,!active);
    EnableWindow(start_button,ready);
    SetWindowTextW(start_button,active?L"Stop  (F8)":L"Start  (F6)");InvalidateRect(start_button,NULL,FALSE);
}
static void stop(void){rc_stop(&engine);if(engine.active){SetWindowTextW(start_button,L"Stopping...");EnableWindow(start_button,FALSE);SetTimer(main_window,1,15,NULL);}}
static void toggle(BOOL mouse){
    if(engine.active){stop();return;}
    if(!ready){status(L"F6 or F8 is in use by another app.");return;}
    wchar_t value[32],*end;GetWindowTextW(rate_edit,value,32);long rate=wcstol(value,&end,10);
    if(end==value || *end || rate<1 || rate>10000){status(L"Speed must be 1 to 10,000.");SetFocus(rate_edit);return;}
    if(verify_ui){status(L"Valid settings.");return;}
    LRESULT selection=SendMessageW(button_choice,CB_GETCURSEL,0,0);
    DWORD flags=selection==1?MOUSEEVENTF_RIGHTDOWN|MOUSEEVENTF_RIGHTUP:selection==2?MOUSEEVENTF_MIDDLEDOWN|MOUSEEVENTF_MIDDLEUP:MOUSEEVENTF_LEFTDOWN|MOUSEEVENTF_LEFTUP;
    BOOL direct=SendMessageW(method_choice,CB_GETCURSEL,0,0)==0;POINT point;if(!GetCursorPos(&point)){status(L"Could not read the pointer position.");return;}HWND target=WindowFromPoint(point);
    if(direct && !mouse && (!target || GetAncestor(target,GA_ROOT)==main_window)){status(L"Point at your target and press F6.");return;}
    ScreenToClient(target,&point);
    if(rc_start(&engine,rate,flags,mouse?700:0,main_window,0,direct,mouse?NULL:target,MAKELPARAM(point.x,point.y))){
        previous_count=0;previous_time=GetTickCount();status(mouse?L"Starting... move pointer to target.":L"Running");SetTimer(main_window,1,500,NULL);refresh();
    }else status(L"Could not start the click worker.");
}
static LRESULT CALLBACK edit_proc(HWND w,UINT m,WPARAM a,LPARAM b,UINT_PTR id,DWORD_PTR data){
    (void)id;(void)data;
    if(m==WM_KEYDOWN && a==VK_RETURN){toggle(FALSE);return 0;}
    if(m==WM_CHAR && (a==VK_RETURN || a==VK_ESCAPE))return 0;
    if(m==WM_KEYDOWN && a==VK_ESCAPE){stop();return 0;}
    return DefSubclassProc(w,m,a,b);
}
static HWND control(const wchar_t *type,const wchar_t *text,DWORD style,int x,int y,int width,int height,int id){
    HWND w=CreateWindowExW(0,type,text,WS_CHILD|WS_VISIBLE|style,px(x),px(y),px(width),px(height),main_window,(HMENU)(INT_PTR)id,GetModuleHandleW(NULL),NULL);
    SendMessageW(w,WM_SETFONT,(WPARAM)font,FALSE);return w;
}
static LRESULT CALLBACK window_proc(HWND w,UINT m,WPARAM a,LPARAM b){
    switch(m){
    case WM_CREATE:{
        main_window=w;scale=(int)GetDpiForWindow(w);
        font=CreateFontW(-px(14),0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
        large_font=CreateFontW(-px(23),0,0,0,FW_SEMIBOLD,FALSE,FALSE,FALSE,DEFAULT_CHARSET,0,0,CLEARTYPE_QUALITY,0,L"Segoe UI");
        DWORD rate=1000,choice=0,size=sizeof(DWORD);if(!verify_ui){RegGetValueW(HKEY_CURRENT_USER,L"Software\\RapidClick",L"Rate",RRF_RT_REG_DWORD,NULL,&rate,&size);size=sizeof(DWORD);RegGetValueW(HKEY_CURRENT_USER,L"Software\\RapidClick",L"Button",RRF_RT_REG_DWORD,NULL,&choice,&size);}
        if(rate<1||rate>10000)rate=1000;if(choice>2)choice=0;
        control(L"STATIC",L"Clicks / second",0,20,17,180,20,0);
        wchar_t text[20];swprintf(text,20,L"%lu",rate);
        rate_edit=control(L"EDIT",text,WS_TABSTOP|ES_AUTOHSCROLL|WS_BORDER,20,43,166,40,ID_RATE);SendMessageW(rate_edit,WM_SETFONT,(WPARAM)large_font,FALSE);SendMessageW(rate_edit,EM_SETLIMITTEXT,5,0);SetWindowSubclass(rate_edit,edit_proc,1,0);
        button_choice=control(L"COMBOBOX",L"",WS_TABSTOP|CBS_DROPDOWNLIST|CBS_OWNERDRAWFIXED|CBS_HASSTRINGS,200,43,130,140,ID_BUTTON);
        SendMessageW(button_choice,CB_ADDSTRING,0,(LPARAM)L"Left click");SendMessageW(button_choice,CB_ADDSTRING,0,(LPARAM)L"Right click");SendMessageW(button_choice,CB_ADDSTRING,0,(LPARAM)L"Middle click");SendMessageW(button_choice,CB_SETCURSEL,choice,0);SendMessageW(button_choice,CB_SETITEMHEIGHT,-1,px(34));SendMessageW(button_choice,CB_SETITEMHEIGHT,0,px(30));
        method_choice=control(L"COMBOBOX",L"",WS_TABSTOP|CBS_DROPDOWNLIST|CBS_OWNERDRAWFIXED|CBS_HASSTRINGS,20,98,310,100,ID_METHOD);
        SendMessageW(method_choice,CB_ADDSTRING,0,(LPARAM)L"Direct to window (fast)");SendMessageW(method_choice,CB_ADDSTRING,0,(LPARAM)L"Standard input (compatibility)");SendMessageW(method_choice,CB_SETCURSEL,0,0);SendMessageW(method_choice,CB_SETITEMHEIGHT,-1,px(28));SendMessageW(method_choice,CB_SETITEMHEIGHT,0,px(28));
        start_button=control(L"BUTTON",L"Start  (F6)",WS_TABSTOP|BS_OWNERDRAW,20,147,310,40,ID_START);
        status_label=control(L"STATIC",L"Point at target. F6 starts. F8 stops.",SS_LEFT,20,203,315,38,ID_STATUS);
        BOOL dark=TRUE;DwmSetWindowAttribute(w,20,&dark,sizeof(dark));
        BOOL first=verify_ui?TRUE:RegisterHotKey(w,1,MOD_NOREPEAT,VK_F6),second=verify_ui?TRUE:RegisterHotKey(w,2,MOD_NOREPEAT,VK_F8);ready=first&&second;if(!ready)status(L"F6 or F8 is in use by another app.");
        return 0;}
    case WM_HOTKEY:if(a==1)toggle(FALSE);if(a==2)stop();return 0;
    case WM_COMMAND:if(LOWORD(a)==ID_START && HIWORD(a)==BN_CLICKED)toggle(TRUE);return 0;
    case WM_KEYDOWN:if(a==VK_ESCAPE)stop();return 0;
    case WM_TIMER:{
        if(!engine.active){KillTimer(w,1);refresh();status(engine.error==-1?L"Move pointer off RapidClick, then press F6.":engine.error?L"Windows blocked input. Stopped.":engine.game_profile?L"Stopped. Cell to Singularity limit: 50/s.":L"Stopped. F6 starts again.");}
        else if(engine.started){LONG64 count=InterlockedCompareExchange64(&engine.clicks,0,0);DWORD now=GetTickCount();double rate=(count-previous_count)*1000.0/(now-previous_time);wchar_t text[100];if(engine.game_profile){wchar_t value[20];swprintf(value,20,L"%ld",engine.rate);SetWindowTextW(rate_edit,value);SendMessageW(method_choice,CB_SETCURSEL,1,0);swprintf(text,100,L"Cell to Singularity: %.0f/s (game limit 50)",rate);}else swprintf(text,100,L"Running   %.0f clicks/s   F8 stops",rate);status(text);previous_count=count;previous_time=now;}
        return 0;}
    case WM_CTLCOLORSTATIC:SetTextColor((HDC)a,RGB(202,196,208));SetBkColor((HDC)a,RGB(20,18,24));return (LRESULT)background;
    case WM_CTLCOLOREDIT:case WM_CTLCOLORLISTBOX:SetTextColor((HDC)a,RGB(230,224,233));SetBkColor((HDC)a,RGB(43,41,48));return (LRESULT)field;
    case WM_MEASUREITEM:((MEASUREITEMSTRUCT*)b)->itemHeight=px(30);return TRUE;
    case WM_DRAWITEM:{
        DRAWITEMSTRUCT *d=(DRAWITEMSTRUCT*)b;BOOL action=d->CtlID==ID_START;HBRUSH brush=action?CreateSolidBrush(RGB(208,188,255)):field;FillRect(d->hDC,&d->rcItem,brush);if(action)DeleteObject(brush);
        SetBkMode(d->hDC,TRANSPARENT);SetTextColor(d->hDC,action?RGB(56,30,114):RGB(230,224,233));SelectObject(d->hDC,font);wchar_t text[64]=L"";if(action)GetWindowTextW(start_button,text,64);
        if(!action && d->itemID!=(UINT)-1)SendMessageW(d->hwndItem,CB_GETLBTEXT,d->itemID,(LPARAM)text);
        RECT r=d->rcItem;if(!action)r.left+=px(8);DrawTextW(d->hDC,text,-1,&r,DT_SINGLELINE|DT_VCENTER|(action?DT_CENTER:DT_LEFT));if(d->itemState&ODS_FOCUS)DrawFocusRect(d->hDC,&d->rcItem);return TRUE;}
    case WM_CLOSE:{
        wchar_t text[32],*end;GetWindowTextW(rate_edit,text,32);DWORD rate=(DWORD)wcstol(text,&end,10);DWORD choice=(DWORD)SendMessageW(button_choice,CB_GETCURSEL,0,0);HKEY key;
        if(!verify_ui && RegCreateKeyExW(HKEY_CURRENT_USER,L"Software\\RapidClick",0,NULL,0,KEY_SET_VALUE,NULL,&key,NULL)==ERROR_SUCCESS){if(!*end&&rate>=1&&rate<=10000)RegSetValueExW(key,L"Rate",0,REG_DWORD,(BYTE*)&rate,sizeof(rate));RegSetValueExW(key,L"Button",0,REG_DWORD,(BYTE*)&choice,sizeof(choice));RegCloseKey(key);}
        rc_dispose(&engine);DestroyWindow(w);return 0;}
    case WM_DESTROY:UnregisterHotKey(w,1);UnregisterHotKey(w,2);DeleteObject(font);DeleteObject(large_font);PostQuitMessage(0);return 0;
    }
    return DefWindowProcW(w,m,a,b);
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE previous,PWSTR arguments,int show){
    (void)previous;verify_ui=wcsstr(arguments,L"--verify-ui")!=NULL;SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    HANDLE singleton=CreateMutexW(NULL,TRUE,L"Local\\RapidClick.Desktop");if(GetLastError()==ERROR_ALREADY_EXISTS){HWND existing=FindWindowW(L"RapidClickNative",NULL);if(existing){ShowWindow(existing,SW_RESTORE);SetForegroundWindow(existing);}CloseHandle(singleton);return 0;}
    background=CreateSolidBrush(RGB(20,18,24));field=CreateSolidBrush(RGB(43,41,48));
    WNDCLASSW c={0};c.lpfnWndProc=window_proc;c.hInstance=instance;c.lpszClassName=L"RapidClickNative";c.hCursor=LoadCursorW(NULL,IDC_ARROW);c.hIcon=LoadIconW(instance,MAKEINTRESOURCEW(1));c.hbrBackground=background;RegisterClassW(&c);
    UINT dpi=GetDpiForSystem();RECT r={0,0,MulDiv(350,dpi,96),MulDiv(245,dpi,96)};AdjustWindowRectExForDpi(&r,WS_OVERLAPPED|WS_CAPTION|WS_SYSMENU|WS_MINIMIZEBOX,FALSE,0,dpi);
    HWND w=CreateWindowExW(0,c.lpszClassName,L"RapidClick",WS_OVERLAPPED|WS_CAPTION|WS_SYSMENU|WS_MINIMIZEBOX,CW_USEDEFAULT,CW_USEDEFAULT,r.right-r.left,r.bottom-r.top,NULL,NULL,instance,NULL);if(!w){CloseHandle(singleton);return 1;}
    ShowWindow(w,show);UpdateWindow(w);MSG msg;while(GetMessageW(&msg,NULL,0,0)>0){if(!IsDialogMessageW(w,&msg)){TranslateMessage(&msg);DispatchMessageW(&msg);}}
    DeleteObject(background);DeleteObject(field);CloseHandle(singleton);return 0;
}
