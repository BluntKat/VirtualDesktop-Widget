#NoEnv
#SingleInstance Force
SetBatchLines, -1
SendMode Input
SetTitleMatchMode, 2

dll := "VirtualDesktopAccessor.dll"
hModule := DllCall("LoadLibrary", "Str", dll, "Ptr")
if !hModule {
    MsgBox, 16, Error, Failed to load %dll%. Make sure the DLL is in the script folder.
    ExitApp
}

GoToDesktopNumber := DllCall("GetProcAddress", "Ptr", hModule, "AStr", "GoToDesktopNumber", "Ptr")
if !GoToDesktopNumber {
    MsgBox, 16, Error, Failed to get GoToDesktopNumber function address.
    ExitApp
}

MoveWindowToDesktopNumber := DllCall("GetProcAddress", "Ptr", hModule, "AStr", "MoveWindowToDesktopNumber", "Ptr")
if !MoveWindowToDesktopNumber {
    MsgBox, 16, Error, Failed to get MoveWindowToDesktopNumber function address.
    ExitApp
}

; Map desktop numbers for hotkeys 1-9
desktops := [0,1,2,3,4,5,6,7,8]

; Register hotkeys Win+1..9
#1::GoToDesktop(0)
#2::GoToDesktop(1)
#3::GoToDesktop(2)
#4::GoToDesktop(3)
#5::GoToDesktop(4)
#6::GoToDesktop(5)
#7::GoToDesktop(6)
#8::GoToDesktop(7)
#9::GoToDesktop(8)

; Register hotkeys Win+Shift+1..9 to move window
#+1::MoveWindowToDesktop(0)
#+2::MoveWindowToDesktop(1)
#+3::MoveWindowToDesktop(2)
#+4::MoveWindowToDesktop(3)
#+5::MoveWindowToDesktop(4)
#+6::MoveWindowToDesktop(5)
#+7::MoveWindowToDesktop(6)
#+8::MoveWindowToDesktop(7)
#+9::MoveWindowToDesktop(8)

return

GoToDesktop(num) {
    global GoToDesktopNumber
    DllCall(GoToDesktopNumber, "UInt", num)
}

MoveWindowToDesktop(num) {
    global MoveWindowToDesktopNumber
    hwnd := WinExist("A")
    if !hwnd {
        MsgBox, 48, Info, No active window detected.
        return
    }
    DllCall(MoveWindowToDesktopNumber, "Ptr", hwnd, "UInt", num)
}

#t:: ; Win + T
Run, wt.exe
return