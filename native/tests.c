#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <math.h>
#include "engine.h"

static HWND target;
static volatile LONG64 down_count,up_count;
static volatile LONG failures;
static volatile LONG slow_once;
static HANDLE ping_event;
static volatile LONG64 ping_microseconds;
static double frequency;
static double now(void){LARGE_INTEGER t;QueryPerformanceCounter(&t);return t.QuadPart/frequency;}
static LRESULT CALLBACK receiver(HWND w,UINT m,WPARAM a,LPARAM b){
    if(m==WM_APP+2){InterlockedExchange64(&ping_microseconds,(LONG64)(now()*1000000)-(LONG64)b);SetEvent(ping_event);return 0;}
    if(m==WM_LBUTTONDOWN || m==WM_RBUTTONDOWN || m==WM_MBUTTONDOWN){InterlockedIncrement64(&down_count);if(InterlockedExchange(&slow_once,0))Sleep(50);return 0;}
    if(m==WM_LBUTTONUP || m==WM_RBUTTONUP || m==WM_MBUTTONUP){InterlockedIncrement64(&up_count);return 0;}
    return DefWindowProcW(w,m,a,b);
}
static void check(BOOL ok,const char *name){printf("%s %s\n",ok?"PASS":"FAIL",name);if(!ok)InterlockedIncrement(&failures);}
static double percentile(double *samples,int n,double p){for(int i=1;i<n;i++){double v=samples[i];int j=i-1;for(;j>=0 && samples[j]>v;j--)samples[j+1]=samples[j];samples[j+1]=v;}return n?samples[(int)((n-1)*p)]:0;}
static DWORD WINAPI cases(void *unused){
    (void)unused;RcEngine engine={0};FILE *report=fopen("test-results/native-v2.json","w");if(!report){failures++;PostMessageW(target,WM_QUIT,0,0);return 1;}
    fprintf(report,"{\"transport\":\"SendMessageTimeout\",\"silent\":true,\"cases\":[\n");
    int rates[]={1000,10000};
    check(rc_effective_rate(10000,TRUE)==50 && rc_effective_rate(30,TRUE)==30 && rc_effective_rate(10000,FALSE)==10000,"Cell to Singularity profile respects the game tap cap");
    for(int c=0;c<2;c++){
        down_count=up_count=0;LONG rate=rates[c];LONG64 limit=rate*3;double heartbeat[2048],ui_latency[2048],last=now(),max_gap=0,max_ui=0;int sample_count=0;
        FILETIME created,exited,kernel_before,user_before,kernel_after,user_after;GetProcessTimes(GetCurrentProcess(),&created,&exited,&kernel_before,&user_before);
        check(rc_start(&engine,rate,MOUSEEVENTF_LEFTDOWN|MOUSEEVENTF_LEFTUP,0,NULL,limit,TRUE,target,MAKELPARAM(10,10)),"start direct run");
        while(engine.active){Sleep(5);double t=now(),gap=(t-last)*1000;last=t;if(gap>max_gap)max_gap=gap;ResetEvent(ping_event);PostMessageW(target,WM_APP+2,0,(LPARAM)(LONG64)(now()*1000000));BOOL responds=WaitForSingleObject(ping_event,20)==WAIT_OBJECT_0;double ping=responds?ping_microseconds/1000.0:20;if(ping>max_ui)max_ui=ping;if(sample_count<2048){ui_latency[sample_count]=ping;heartbeat[sample_count++]=gap;}if(rc_elapsed(&engine)>8){rc_stop(&engine);break;}}
        double elapsed=rc_elapsed(&engine),rate_actual=engine.clicks/elapsed;
        GetProcessTimes(GetCurrentProcess(),&created,&exited,&kernel_after,&user_after);ULARGE_INTEGER kb,ub,ka,ua;kb.LowPart=kernel_before.dwLowDateTime;kb.HighPart=kernel_before.dwHighDateTime;ub.LowPart=user_before.dwLowDateTime;ub.HighPart=user_before.dwHighDateTime;ka.LowPart=kernel_after.dwLowDateTime;ka.HighPart=kernel_after.dwHighDateTime;ua.LowPart=user_after.dwLowDateTime;ua.HighPart=user_after.dwHighDateTime;double cpu_ms=(ka.QuadPart+ua.QuadPart-kb.QuadPart-ub.QuadPart)/10000.0;
        check(engine.clicks==limit && down_count==limit && up_count==limit,"exact messages and releases");
        check(rate_actual>rate*0.97 && rate_actual<rate*1.03,"requested throughput within 3 percent");
        check(max_gap<100,"monitor thread stays responsive");
        check(max_ui<20,"receiver message loop stays responsive");
        fprintf(report,"%s{\"requestedCps\":%ld,\"received\":%lld,\"released\":%lld,\"seconds\":%.6f,\"actualCps\":%.1f,\"monitorP95Ms\":%.3f,\"monitorMaxMs\":%.3f,\"receiverP95Ms\":%.3f,\"receiverMaxMs\":%.3f,\"cpuMs\":%.3f}",c?",\n":"",rate,down_count,up_count,elapsed,rate_actual,percentile(heartbeat,sample_count,0.95),max_gap,percentile(ui_latency,sample_count,0.95),max_ui,cpu_ms);fflush(report);
    }
    down_count=up_count=0;rc_start(&engine,10000,MOUSEEVENTF_RIGHTDOWN|MOUSEEVENTF_RIGHTUP,0,NULL,137,TRUE,target,0);while(engine.active)Sleep(1);check(down_count==137 && up_count==137,"right click finite count");
    down_count=up_count=0;rc_start(&engine,10000,MOUSEEVENTF_MIDDLEDOWN|MOUSEEVENTF_MIDDLEUP,0,NULL,101,TRUE,target,0);while(engine.active)Sleep(1);check(down_count==101 && up_count==101,"middle click finite count");
    down_count=up_count=0;rc_start(&engine,10000,MOUSEEVENTF_LEFTDOWN|MOUSEEVENTF_LEFTUP,500,NULL,0,TRUE,target,0);Sleep(20);rc_stop(&engine);while(engine.active)Sleep(1);check(!down_count,"cancel delay sends no clicks");
    rc_start(&engine,10000,MOUSEEVENTF_LEFTDOWN|MOUSEEVENTF_LEFTUP,0,NULL,0,TRUE,target,0);Sleep(100);double stop_start=now();rc_stop(&engine);WaitForSingleObject(engine.thread,500);double stop_ms=(now()-stop_start)*1000;LONG64 settled=down_count;Sleep(50);check(stop_ms<25 && settled==down_count,"stop promptly and remain stopped");
    check(!rc_start(&engine,0,MOUSEEVENTF_LEFTDOWN|MOUSEEVENTF_LEFTUP,0,NULL,0,TRUE,target,0),"invalid speed rejected");
    rc_start(&engine,1000,MOUSEEVENTF_LEFTDOWN|MOUSEEVENTF_LEFTUP,100,NULL,0,TRUE,target,0);check(!rc_start(&engine,1000,MOUSEEVENTF_LEFTDOWN|MOUSEEVENTF_LEFTUP,0,NULL,0,TRUE,target,0),"duplicate worker rejected");rc_stop(&engine);while(engine.active)Sleep(1);
    down_count=up_count=0;slow_once=1;rc_start(&engine,1000,MOUSEEVENTF_LEFTDOWN|MOUSEEVENTF_LEFTUP,0,NULL,0,TRUE,target,0);WaitForSingleObject(engine.thread,500);check(!engine.active && engine.error!=0,"unresponsive target stops instead of queuing clicks");Sleep(100);check(down_count==1 && up_count==1,"timed-out click still releases the target button");
    fprintf(report,"\n],\"stopMs\":%.3f,\"failures\":%ld}\n",stop_ms,failures);fclose(report);rc_dispose(&engine);PostMessageW(target,WM_QUIT,0,0);return 0;
}
int main(void){
    LARGE_INTEGER f;QueryPerformanceFrequency(&f);frequency=(double)f.QuadPart;CreateDirectoryW(L"test-results",NULL);ping_event=CreateEventW(NULL,FALSE,FALSE,NULL);
    WNDCLASSW wc={0};wc.lpfnWndProc=receiver;wc.hInstance=GetModuleHandleW(NULL);wc.lpszClassName=L"RapidClickSilentTests";RegisterClassW(&wc);
    target=CreateWindowExW(0,wc.lpszClassName,L"",0,0,0,0,0,HWND_MESSAGE,NULL,wc.hInstance,NULL);
    HANDLE thread=CreateThread(NULL,0,cases,NULL,0,NULL);MSG m;while(GetMessageW(&m,NULL,0,0)>0){TranslateMessage(&m);DispatchMessageW(&m);}WaitForSingleObject(thread,INFINITE);CloseHandle(thread);CloseHandle(ping_event);DestroyWindow(target);return failures?1:0;
}
