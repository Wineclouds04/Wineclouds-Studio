Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetDatablockOptimize on

!ifndef MyAppVersion
  !define MyAppVersion "1.0.0"
!endif

!ifndef MySourceDir
  !error "MySourceDir must point to the published application directory."
!endif

!ifndef MyOutputDir
  !error "MyOutputDir must point to the installer output directory."
!endif

!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"

!define APP_NAME "Wineclouds Studio"
!define APP_EXE "WinecloudsStudio.exe"
!define APP_REG_KEY "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\WinecloudsStudio"
!define APP_INSTALL_KEY "Software\\WinecloudsStudio"

Name "${APP_NAME}"
OutFile "${MyOutputDir}\\WinecloudsStudio-Setup-${MyAppVersion}-win-x64.exe"
InstallDir "$PROGRAMFILES64\\Wineclouds Studio"
InstallDirRegKey HKLM "${APP_INSTALL_KEY}" "InstallPath"
Icon "..\\src\\WinecloudsStudio\\Assets\\AppIcon.ico"
UninstallIcon "..\\src\\WinecloudsStudio\\Assets\\AppIcon.ico"
BrandingText "Wineclouds Studio · Desktop Service Platform"
ShowInstDetails show
ShowUninstDetails show

Var CreateDesktopShortcut
Var CreateDesktopShortcutState
Var DialogHandle

!define MUI_ABORTWARNING
!define MUI_ICON "..\\src\\WinecloudsStudio\\Assets\\AppIcon.ico"
!define MUI_UNICON "..\\src\\WinecloudsStudio\\Assets\\AppIcon.ico"
!define MUI_WELCOMEPAGE_TITLE "欢迎使用 Wineclouds Studio"
!define MUI_WELCOMEPAGE_TEXT "此向导将为你安装 Wineclouds Studio。$\r$\n$\r$\n请先关闭正在运行的 Wineclouds Studio，然后点击“下一步”继续。"
!define MUI_FINISHPAGE_RUN "$INSTDIR\\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "立即启动 Wineclouds Studio"

!insertmacro MUI_PAGE_WELCOME
Page custom CreateOptionsPage LeaveOptionsPage
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "SimpChinese"

Function .onInit
  SetShellVarContext all
  SetRegView 64
FunctionEnd

Function un.onInit
  SetShellVarContext all
  SetRegView 64
FunctionEnd

Function CreateOptionsPage
  !insertmacro MUI_HEADER_TEXT "安装选项" "选择 Wineclouds Studio 的快捷方式设置"
  nsDialogs::Create 1018
  Pop $DialogHandle

  ${If} $DialogHandle == error
    Abort
  ${EndIf}

  ${NSD_CreateLabel} 0 0 100% 28u "Wineclouds Studio 将以自包含方式安装，运行时文件会随应用一起部署。"
  Pop $0
  ${NSD_CreateCheckbox} 0 40u 100% 12u "在桌面创建快捷方式"
  Pop $CreateDesktopShortcut
  ${NSD_Check} $CreateDesktopShortcut

  nsDialogs::Show
FunctionEnd

Function LeaveOptionsPage
  ${NSD_GetState} $CreateDesktopShortcut $CreateDesktopShortcutState
FunctionEnd

Section "Install Wineclouds Studio" SEC_MAIN
  SetOutPath "$INSTDIR"
  File /r "${MySourceDir}\\*"

  WriteRegStr HKLM "${APP_INSTALL_KEY}" "InstallPath" "$INSTDIR"
  WriteRegStr HKLM "${APP_REG_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${APP_REG_KEY}" "DisplayVersion" "${MyAppVersion}"
  WriteRegStr HKLM "${APP_REG_KEY}" "Publisher" "Wineclouds Studio"
  WriteRegStr HKLM "${APP_REG_KEY}" "DisplayIcon" "$INSTDIR\\${APP_EXE}"
  WriteRegStr HKLM "${APP_REG_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${APP_REG_KEY}" "UninstallString" "$INSTDIR\\Uninstall.exe"
  WriteRegDWORD HKLM "${APP_REG_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${APP_REG_KEY}" "NoRepair" 1

  CreateDirectory "$SMPROGRAMS\\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\\${APP_NAME}\\${APP_NAME}.lnk" "$INSTDIR\\${APP_EXE}" "" "$INSTDIR\\${APP_EXE}" 0
  CreateShortcut "$SMPROGRAMS\\${APP_NAME}\\卸载 ${APP_NAME}.lnk" "$INSTDIR\\Uninstall.exe"

  ${If} $CreateDesktopShortcutState == ${BST_CHECKED}
    CreateShortcut "$DESKTOP\\${APP_NAME}.lnk" "$INSTDIR\\${APP_EXE}" "" "$INSTDIR\\${APP_EXE}" 0
  ${EndIf}

  WriteUninstaller "$INSTDIR\\Uninstall.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\\${APP_NAME}\\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\\${APP_NAME}\\卸载 ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\\${APP_NAME}"

  DeleteRegKey HKLM "${APP_REG_KEY}"
  DeleteRegKey HKLM "${APP_INSTALL_KEY}"
  RMDir /r "$INSTDIR"
SectionEnd
