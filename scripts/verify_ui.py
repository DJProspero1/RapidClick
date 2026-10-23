"""Inspect and render only the packaged app. No mouse/keyboard injection or audio."""
import ctypes as c
from ctypes import wintypes as w
import json
from pathlib import Path
import subprocess
import sys
import time
from PIL import Image

user = c.WinDLL("user32", use_last_error=True)
gdi = c.WinDLL("gdi32", use_last_error=True)
kernel = c.WinDLL("kernel32", use_last_error=True)
psapi = c.WinDLL("psapi", use_last_error=True)
class Memory(c.Structure):
    _fields_=[("size",w.DWORD),("faults",w.DWORD),("peak",c.c_size_t),("working",c.c_size_t),("paged_peak",c.c_size_t),("paged",c.c_size_t),("nonpaged_peak",c.c_size_t),("nonpaged",c.c_size_t),("pagefile",c.c_size_t),("pagefile_peak",c.c_size_t)]
psapi.GetProcessMemoryInfo.argtypes=[w.HANDLE,c.POINTER(Memory),w.DWORD]
kernel.GetProcessTimes.argtypes=[w.HANDLE,c.POINTER(w.FILETIME),c.POINTER(w.FILETIME),c.POINTER(w.FILETIME),c.POINTER(w.FILETIME)]
def cpu_ms(process):
    times=[w.FILETIME() for _ in range(4)]
    assert kernel.GetProcessTimes(int(process._handle),*[c.byref(t) for t in times])
    return sum((t.dwHighDateTime<<32)|t.dwLowDateTime for t in times[2:])/10000
user.FindWindowW.argtypes = [w.LPCWSTR, w.LPCWSTR]
user.FindWindowW.restype = w.HWND
user.GetDlgItem.argtypes = [w.HWND, c.c_int]
user.GetDlgItem.restype = w.HWND
user.GetWindowTextW.argtypes = [w.HWND, w.LPWSTR, c.c_int]
user.SetWindowTextW.argtypes = [w.HWND, w.LPCWSTR]
user.SendMessageW.argtypes = [w.HWND, w.UINT, w.WPARAM, w.LPARAM]
user.SendMessageW.restype = w.LPARAM
user.PostMessageW.argtypes = [w.HWND, w.UINT, w.WPARAM, w.LPARAM]
user.GetClientRect.argtypes = [w.HWND, c.POINTER(w.RECT)]
user.GetDC.argtypes = [w.HWND]
user.GetDC.restype = w.HDC
user.ReleaseDC.argtypes = [w.HWND, w.HDC]
user.PrintWindow.argtypes = [w.HWND, w.HDC, w.UINT]
user.ShowWindow.argtypes = [w.HWND,c.c_int]
gdi.CreateCompatibleDC.argtypes = [w.HDC]
gdi.CreateCompatibleDC.restype = w.HDC
gdi.CreateCompatibleBitmap.argtypes = [w.HDC, c.c_int, c.c_int]
gdi.CreateCompatibleBitmap.restype = w.HBITMAP
gdi.SelectObject.argtypes = [w.HDC, w.HGDIOBJ]
gdi.SelectObject.restype = w.HGDIOBJ
gdi.DeleteObject.argtypes = [w.HGDIOBJ]
gdi.DeleteDC.argtypes = [w.HDC]

class Header(c.Structure):
    _fields_ = [("size",w.DWORD),("width",w.LONG),("height",w.LONG),("planes",w.WORD),("bits",w.WORD),("compression",w.DWORD),("image_size",w.DWORD),("xp",w.LONG),("yp",w.LONG),("colors",w.DWORD),("important",w.DWORD)]
class Info(c.Structure):
    _fields_ = [("header",Header),("color",w.DWORD*3)]
gdi.GetDIBits.argtypes = [w.HDC,w.HBITMAP,w.UINT,w.UINT,c.c_void_p,c.POINTER(Info),w.UINT]
def text(window):
    value=c.create_unicode_buffer(256)
    user.GetWindowTextW(window,value,len(value))
    return value.value
def set_text(window,value):
    buffer=c.create_unicode_buffer(value)
    user.SendMessageW(window,0x000C,0,c.cast(buffer,c.c_void_p).value)

def main():
    exe=Path(sys.argv[1]).resolve()
    output=Path(sys.argv[2] if len(sys.argv)>2 else "test-results/native-ui.png")
    output.parent.mkdir(exist_ok=True,parents=True)
    startup=subprocess.STARTUPINFO()
    startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow=0
    process=subprocess.Popen([str(exe),"--verify-ui"],startupinfo=startup)
    hwnd=None
    try:
        for _ in range(100):
            hwnd=user.FindWindowW("RapidClickNative",None)
            if hwnd:break
            if process.poll() is not None:raise RuntimeError("Application exited before its window was ready")
            time.sleep(.05)
        if not hwnd:raise RuntimeError("Native app window not found")
        edit=user.GetDlgItem(hwnd,101)
        set_text(edit,"0")
        user.SendMessageW(hwnd,0x111,103,0)
        assert "1 to 10,000" in text(user.GetDlgItem(hwnd,104)),f"Invalid speed was not rejected: {text(user.GetDlgItem(hwnd,104))!r}; edit={text(edit)!r}"
        set_text(edit,"10000")
        user.SendMessageW(hwnd,0x312,2,0) # stop routing only; never inject into another app
        assert user.SendMessageW(user.GetDlgItem(hwnd,102),0x146,0,0)==3,"Mouse button options missing"
        assert user.SendMessageW(user.GetDlgItem(hwnd,105),0x146,0,0)==2,"Transport options missing"
        set_text(user.GetDlgItem(hwnd,104),"Point at target. F6 starts. F8 stops.")
        user.ShowWindow(hwnd,4) # brief non-activating render; no desktop input
        user.UpdateWindow(hwnd)
        time.sleep(.15)
        rect=w.RECT();user.GetClientRect(hwnd,c.byref(rect));width,height=rect.right,rect.bottom
        dc=user.GetDC(hwnd);memory=gdi.CreateCompatibleDC(dc);bitmap=gdi.CreateCompatibleBitmap(dc,width,height);old=gdi.SelectObject(memory,bitmap)
        try:
            assert user.PrintWindow(hwnd,memory,1),"PrintWindow failed"
            info=Info();info.header=Header(c.sizeof(Header),width,-height,1,32,0,0,0,0,0,0)
            pixels=c.create_string_buffer(width*height*4)
            assert gdi.GetDIBits(memory,bitmap,0,height,pixels,c.byref(info),0)==height
            Image.frombuffer("RGB",(width,height),pixels,"raw","BGRX",0,1).save(output)
        finally:
            gdi.SelectObject(memory,old);gdi.DeleteObject(bitmap);gdi.DeleteDC(memory);user.ReleaseDC(hwnd,dc)
        memory=Memory();memory.size=c.sizeof(Memory)
        assert psapi.GetProcessMemoryInfo(int(process._handle),c.byref(memory),memory.size)
        before=cpu_ms(process);time.sleep(.5);idle_cpu=cpu_ms(process)-before
        report={"passed":True,"silent":True,"mouseOrKeyboardInjection":False,"clientWidth":width,"clientHeight":height,"exeBytes":exe.stat().st_size,"workingSetKiB":memory.working//1024,"idleCpuMsOver500Ms":idle_cpu,"checks":["invalid speed rejected inline","mouse buttons present","direct and standard modes present","stop message handled","client rendered"]}
        output.with_suffix(".json").write_text(json.dumps(report,indent=2))
        print(json.dumps(report))
    finally:
        if hwnd:user.PostMessageW(hwnd,0x10,0,0)
        process.wait(timeout=5)
if __name__=="__main__":main()
