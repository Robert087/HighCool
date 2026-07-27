!macro HIGHCOOL_FIND_PROCESS processName
  !if "${INSTALLMODE}" == "currentUser"
    nsis_tauri_utils::FindProcessCurrentUser "${processName}"
  !else
    nsis_tauri_utils::FindProcess "${processName}"
  !endif
!macroend

!macro HIGHCOOL_REQUIRE_PROCESS_CLOSED processName displayName
  !define UniqueID ${__LINE__}

  highcool_process_check_${UniqueID}:
    !insertmacro HIGHCOOL_FIND_PROCESS "${processName}"
    Pop $R0

    ${If} $R0 = 0
      IfSilent highcool_process_silent_${UniqueID} 0

      ${If} $PassiveMode = 1
        Goto highcool_process_silent_${UniqueID}
      ${EndIf}

      MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION|MB_DEFBUTTON1 \
        "HighCool is still running. Close ${displayName}, then click Retry to continue installation." \
        IDRETRY highcool_process_check_${UniqueID} \
        IDCANCEL highcool_process_cancel_${UniqueID}

      highcool_process_cancel_${UniqueID}:
        Abort "HighCool must be closed before installation can continue."

      highcool_process_silent_${UniqueID}:
        Abort "HighCool must be closed before installation can continue."
    ${EndIf}

  !undef UniqueID
!macroend

!macro NSIS_HOOK_PREINSTALL
  !insertmacro HIGHCOOL_REQUIRE_PROCESS_CLOSED "${MAINBINARYNAME}.exe" "HighCool"
  !insertmacro HIGHCOOL_REQUIRE_PROCESS_CLOSED "ERP.Api.exe" "the HighCool local backend"
!macroend

!macro NSIS_HOOK_PREUNINSTALL
  !insertmacro HIGHCOOL_REQUIRE_PROCESS_CLOSED "${MAINBINARYNAME}.exe" "HighCool"
  !insertmacro HIGHCOOL_REQUIRE_PROCESS_CLOSED "ERP.Api.exe" "the HighCool local backend"
!macroend
