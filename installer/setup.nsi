Unicode true
; KOCH 3Dconnexion Keys - installer (KOCH Engineering)
; Build: build both projects (Release), then  makensis installer\setup.nsi
!ifndef LOADER
  !define LOADER "..\Source\Loader\bin\Release\net48"
!endif
!ifndef CORE
  !define CORE "..\Source\Core\bin\Release\net48"
!endif
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"

!define APPNAME   "KOCH 3Dconnexion Keys (Alibre add-on)"
!define VERSION   "0.6.2"
!define PUBLISHER "KOCH Engineering"
!define ADDONID   "{C79D2D04-31E5-4887-83CC-6B2F46C2E768}"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\KochSpaceMouse"

Name "${APPNAME}"
OutFile "KOCH-3Dconnexion-Keys-Setup-${VERSION}.exe"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${PUBLISHER} - Henrik J. Koch, Denmark"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "FileDescription" "Installer for ${APPNAME}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "(c) 2026 ${PUBLISHER}"
VIAddVersionKey "Comments" "Developed by ${PUBLISHER} (Henrik J. Koch, Denmark)"

!define MUI_ICON   "${LOADER}\SpaceMouse.ico"
!define MUI_UNICON "${LOADER}\SpaceMouse.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "KOCH 3Dconnexion Keys for Alibre Design"
!define MUI_WELCOMEPAGE_TEXT "KOCH Engineering - Henrik J. Koch - Denmark - © 2026$\r$\nWith a lot of help from Claude (Anthropic).$\r$\n$\r$\nChoose what the 12 LCD keys of a 3Dconnexion SpaceMouse Enterprise and the 12 keys of a Keyboard Pro do in Alibre Design - with names, icons and one layout per Part, Assembly, Sketch and Drawing.$\r$\n$\r$\nRequires Alibre Design (64-bit) and 3Dconnexion 3DxWare 10.$\r$\n$\r$\nPlease close Alibre Design before you continue."
!define MUI_FINISHPAGE_TEXT "KOCH 3Dconnexion Keys is installed. Thank you for using a tool from KOCH Engineering.$\r$\n$\r$\nStart Alibre Design and click Add-on > 3Dconnexion > 3Dconnexion keys..."
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\README.txt"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Show README"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!define MUI_FINISHPAGE_LINK "KOCH Engineering on LinkedIn"
!define MUI_FINISHPAGE_LINK_LOCATION "https://www.linkedin.com/in/henrikjkoch/"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

!macro CheckAlibre
  check_again:
  nsExec::ExecToStack 'cmd /c tasklist /FI "IMAGENAME eq Alibre Design.exe" /NH | find /I "Alibre Design.exe"'
  Pop $0
  Pop $1
  ${If} $0 == 0
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "Alibre Design is running. Close Alibre Design and click Retry." IDRETRY check_again
    Abort
  ${EndIf}
!macroend

Function .onInit
  SetShellVarContext all
  StrCpy $INSTDIR "$APPDATA\Alibre AddOns\KochSpaceMouse"
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Alibre Design requires 64-bit Windows."
    Abort
  ${EndIf}
  SetRegView 64
  ; Alibre Design installed?
  StrCpy $R0 0
  FindFirst $R1 $R2 "$PROGRAMFILES64\Alibre Design*"
  loop:
    StrCmp $R2 "" done
    IfFileExists "$PROGRAMFILES64\$R2\Program\AlibreAddOn.dll" 0 +2
      StrCpy $R0 1
    FindNext $R1 $R2
    Goto loop
  done:
  FindClose $R1
  ${If} $R0 == 0
    MessageBox MB_YESNO|MB_ICONQUESTION "No Alibre Design installation was found on this computer. Install anyway?" IDYES +2
    Abort
  ${EndIf}
  ; 3DxWare installed?
  ${IfNot} ${FileExists} "$PROGRAMFILES64\3Dconnexion\3DxWare\*.*"
    MessageBox MB_YESNO|MB_ICONQUESTION "3Dconnexion 3DxWare was not found. The add-on needs 3DxWare 10 to talk to your SpaceMouse / Keyboard Pro.$\r$\n$\r$\nInstall anyway?" IDYES +2
    Abort
  ${EndIf}
FunctionEnd

Section "Install"
  !insertmacro CheckAlibre
  SetRegView 64
  SetOutPath "$INSTDIR"
  SetOverwrite on
  File "${LOADER}\KochSpaceMouse.dll"
  File "${LOADER}\KochSpaceMouse.adc"
  File "${LOADER}\SpaceMouse.ico"
  File "${CORE}\KochSpaceMouse.Core.dll"
  File "README.txt"
  File "clean-profile.ps1"
  File "/oname=LICENSE.txt" "..\LICENSE"
  SetOutPath "$INSTDIR\defaults"
  File "${CORE}\defaults\settings.xml"
  SetOutPath "$INSTDIR\defaults\icons"
  File "${CORE}\defaults\icons\*.png"
  ; leftovers from development builds
  RMDir /r "$INSTDIR\images"

  ; Register the add-on with Alibre
  WriteRegStr HKLM "SOFTWARE\Alibre Design Add-Ons" "${ADDONID}" "$INSTDIR"

  ; Uninstall entry in Windows Settings
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINSTKEY}" "URLInfoAbout" "https://github.com/KOCH-Engineering/alibre-3dconnexion-keys"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\SpaceMouse.ico"
  WriteRegStr HKLM "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "EstimatedSize" 600
SectionEnd

Section "Uninstall"
  !insertmacro CheckAlibre
  SetRegView 64
  ; put the 3DxWare keys back to their defaults (current user's profile)
  nsExec::ExecToLog 'powershell -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\clean-profile.ps1"'
  DeleteRegValue HKLM "SOFTWARE\Alibre Design Add-Ons" "${ADDONID}"
  DeleteRegKey HKLM "${UNINSTKEY}"
  RMDir /r "$INSTDIR\defaults"
  Delete "$INSTDIR\KochSpaceMouse.dll"
  Delete "$INSTDIR\KochSpaceMouse.Core.dll"
  Delete "$INSTDIR\KochSpaceMouse.adc"
  Delete "$INSTDIR\SpaceMouse.ico"
  Delete "$INSTDIR\README.txt"
  Delete "$INSTDIR\LICENSE.txt"
  Delete "$INSTDIR\clean-profile.ps1"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir  "$INSTDIR"
SectionEnd

Function un.onInit
  SetShellVarContext all
  SetRegView 64
FunctionEnd
