

## PersistentWindows 빠른 도움말
[English](Help.md) | **한국어**

### 명령줄 옵션
  | 명령줄 옵션 | 의미 |
  | --- | --- |
  | -basic_features | 웹페이지 커맨더, 새 창 위치 즉시 복원, Alt 활성화로 창 위치 맞바꾸기처럼 호불호가 갈리는 기능과 앞으로 추가될 확장 기능을 끕니다
  | -portable_mode | 앱 데이터/설정 파일(.db, .xml 등)을 프로그램 폴더 안의 "user_data" 하위 폴더에 저장합니다
  | -gui=0 | 시스템 트레이에 PersistentWindows 아이콘을 표시하지 않습니다. 사실상 서비스처럼 실행됩니다
  | -splash=0       | PersistentWindows 시작 시 스플래시 창을 띄우지 않습니다
  | -legacy_icon    | 원래 아이콘으로 바꿉니다 ![pwIcon2small](https://github.com/user-attachments/assets/4827f67a-2ce1-4a83-86da-b4bfa6835026)
  | -silent         | 스플래시 창, 풍선 도움말, 이벤트 기록을 모두 끕니다
  | -capture_floating_window=0 | 떠 있는 자식 창과 대화 상자 창의 위치 캡처를 끕니다
  | -ignore_process "notepad.exe;EXCEL" | notepad.exe와 EXCEL.EXE 프로세스의 창은 복원하지 않습니다
  | -care_process "notepad.exe;EXCEL" | notepad.exe와 EXCEL.EXE 프로세스의 창만 복원합니다
  | -debug_process "notepad.exe;EXCEL" | notepad.exe와 EXCEL.EXE 프로세스의 창 위치 이벤트 로그를 이벤트 뷰어에 기록합니다. 모든 프로세스를 디버그하려면 와일드카드 값 "*"를 지정하세요
  | -no_inherit_process "notepad.exe;EXCEL" | notepad와 EXCEL 프로세스에 대해서는 새 창 자동 복원을 끕니다
  | -pause_process "game.exe;mstsc" | game.exe 또는 mstsc.exe가 실행 중인 동안 캡처와 자동 복원을 일시중지하고, 모두 종료되면 이전 배치를 복원합니다. 이번 실행에 한해 트레이 메뉴의 일시중지 목록에 더해집니다
  | -exclude_process "notepad.exe;EXCEL" | notepad.exe와 EXCEL.EXE 프로세스의 창을 캡처·이동·복원하지 않습니다. -ignore_process와 달리 캡처도 하지 않습니다. 이번 실행에 한해 트레이 메뉴의 제외 목록에 더해집니다
  | -exclude_title "Picture in picture;Sticky Notes" | 제목에 지정한 텍스트가 들어 있는 창을 캡처·이동·복원하지 않습니다(대소문자 구분 없음). 이번 실행에 한해 트레이 메뉴의 제외 목록에 더해집니다
  | -foreground_background_dual_position=0 | 이중 위치 전환을 끕니다
  | -swap_window_pos_when_alt_activate=0 | 배경 창을 Alt+클릭할 때 전경 창과 위치를 맞바꾸는 기능을 끕니다
  | -webpage_commander_window=0 | Alt+W 단축키 등록을 해제하고 웹페이지 커맨더 기능을 끕니다
  | -hotkey "Q" | 웹페이지 커맨더 창을 켜고 끄는 단축키로 Alt + Q를 등록합니다. 기본 단축키는 "W"(Alt + W)입니다
  | -ctrl_minimize_to_tray=0 | Ctrl을 누른 채 최소화하면 알림 영역으로 숨기는 기능을 끕니다
  | -prompt_session_restore | 마지막 세션을 다시 시작할 때 창 배치를 복원하기 전에 사용자에게 묻습니다. 인터넷이 느린 원격 데스크톱 세션에서 전체 복원 시간을 줄이는 데 도움이 될 수 있습니다
  | -delay_restart 5 | 5초 뒤에 PersistentWindows를 다시 시작합니다. 이 옵션은 PersistentWindows가 정상적으로 시작되지 않을 때만 사용하세요
  | -delay_auto_capture 1.0 | 창 이동 이벤트와 자동 캡처 사이의 지연을 1.0초로 조정합니다. 기본 지연은 3~4초입니다
  | *-delay_auto_restore 2.5* | 모니터 켜짐/꺼짐 이벤트와 자동 복원 사이의 지연을 2.5초로 조정합니다(기본 지연은 1초). Windows 자체 복원과 PW 복원이 충돌할 때 우회하는 데 도움이 됩니다. 복원이 너무 일찍 시작되어 모니터가 절전 모드로 들어가지 못할 때도 도움이 될 수 있습니다
  | -redraw_desktop | 일부 창의 작업 영역이 새로 고쳐지지 않는 경우를 대비해, 복원 뒤에 바탕 화면 전체를 다시 그립니다
  | -fix_zorder=1   | 자동 복원에서도 창의 Z 순서를 보존합니다. Z 순서는 겹쳐진 창들 사이에서 창이 놓인 앞뒤 위치를 뜻합니다
  | -fix_offscreen_window=0 | 화면 밖으로 나간 창의 자동 보정을 끕니다
  | -fix_unminimized_window=0 | 최소화에서 복원된 창의 자동 복원을 끕니다. 창을 활성화할 때 원치 않게 창이 움직이는 현상(이벤트 뷰어의 이벤트 ID 9999: "restore minimized window ....")을 피하려면 이 스위치를 사용하세요
  |-auto_restore_new_display_session_from_db=0| PC 시작 시 또는 디스플레이를 처음 전환할 때 DB에서 창을 복원하는 기능을 끕니다
  |-auto_restore_existing_window_to_last_capture=1 | PW 시작 시 이미 열려 있는 창을 마지막 캡처로 자동 복원합니다
  |-auto_restore_new_window_to_last_capture=0 | 새 창을 마지막으로 닫힌 위치로 자동 복원하는 기능을 끕니다
  |-pos_match_threshold 80 | 새 창 위치가 마지막으로 닫힌 위치에서 80픽셀 이내이면 자동으로 보정합니다. 기본값은 40입니다
  | ‑auto_restore_missing_windows=1 | 디스크에서 복원할 때, 없는 창을 사용자에게 묻지 않고 복원합니다
  | ‑auto_restore_missing_windows=2 | 시작할 때 디스크에서 없는 창을 자동으로 복원합니다. 없는 창을 복원하기 전에 창마다 사용자에게 묻습니다
  | ‑auto_restore_missing_windows=3 | 시작할 때 디스크에서 없는 창을 사용자에게 묻지 않고 자동으로 복원합니다
  | -invoke_multi_window_process_only_once=0 | 같은 프로세스의 창 여러 개를 데이터베이스에서 복원해야 할 때 응용 프로그램을 여러 번 실행합니다
  | -check_upgrade=0 | PersistentWindows 업그레이드 확인을 끕니다
  | -auto_upgrade=1 | 사용자 개입 없이 PersistentWindows를 자동으로 업그레이드합니다
  | -dump_window_position_history=0 | 창 위치 기록 저장을 끕니다
  | -restore_snapshot "0" | 스냅샷 0을 복원하고 종료합니다. 스냅샷 ID의 범위는 [0-9a-z]와 "~" 또는 "`"이며, 뒤의 두 특수 ID는 마지막 자동 복원을 뜻합니다. 이 방법으로는 여러 단계에 걸친 복원을 할 수 없어 창의 Z 순서를 완전히 복원하지 못합니다
  | -capture_snapshot "0" | 스냅샷 0을 캡처하고 종료합니다
  | -restore_from_disk "name_of_capture" | 하드 드라이브의 (이름 붙은) 캡처에서 창을 복원하고 종료합니다. 첫 복원 중에 새로 실행된 창까지 복원하려면 같은 명령줄로 한 번 더 복원해야 합니다
  | -capture_to_disk "name_of_capture" | 창을 하드 드라이브의 (이름 붙은) 캡처로 저장하고 종료합니다
---

### 스냅샷 캡처/복원 단축 동작
  | 스냅샷 명령 | 단축 동작 |
  | --- | --- |
  | 스냅샷 0 캡처 | PersistentWindows 아이콘을 두 번 클릭
  | 스냅샷 0 복원 | PersistentWindows 아이콘을 클릭
  | 스냅샷 X 캡처 | PersistentWindows 아이콘을 두 번 클릭한 뒤 곧바로 X 키를 누름(X는 숫자 [0-9] 또는 글자 [a-z])
  | 스냅샷 X 복원 | PersistentWindows 아이콘을 클릭한 뒤 곧바로 X 키를 누름
  | 마지막 스냅샷 복원 취소 | PersistentWindows 아이콘을 Alt + 클릭

### 디스크 캡처/복원 단축 동작
  * 이름을 붙여 디스크에 캡처하려면 "창 배치를 디스크에 저장" 메뉴를 Ctrl + 클릭한 뒤, 나타나는 대화 상자에 이름을 입력합니다
  * 이름 붙은 캡처를 디스크에서 복원하려면 "디스크에서 창 배치 복원" 메뉴를 Ctrl + 클릭한 뒤, 이전에 저장한 캡처의 이름을 대화 상자에 입력합니다
  * 다른 디스플레이 구성의 캡처를 복원하려면 "디스크에서 창 배치 복원" 메뉴를 Shift + 클릭합니다

---
### 창 위치를 다루는 단축 동작
* 이중 위치 전환(Dual Position Switching)을 쓰면 창이 위치와 크기가 서로 다른 전경 모드와 배경 모드 사이를 오갈 수 있습니다

  * 이중 위치 전환을 켜려면:
    * Ctrl을 누른 채 창을 옮기거나 크기를 바꿉니다.

  * 이중 위치 전환의 동작:
    * 바탕 화면을 클릭하면 전경 창이 이전의 배경 위치와 Z 순서로 돌아갑니다.
    * 바탕 화면을 Ctrl + 클릭하면 전경 창이 현재 위치와 크기를 유지한 채 이전 Z 순서로 돌아갑니다.

  * 이중 위치 전환을 취소하려면:
    * (Ctrl 키를 누르지 않고) 창을 옮기거나 크기를 바꿉니다.
  * 배경에 있는 이중 위치 창을 이전 전경 위치로 복원하지 *않고* 전경으로 가져오려면:
    * 창을 활성화할 때 Ctrl/Shift/Alt 키 중 아무거나 누릅니다
* 전경 창과 배경 창의 위치를 맞바꾸려면
  * 배경 창을 Alt + 클릭
* (보이지 않거나 화면 밖에 있는) 전경 창을 주 모니터 가운데로 옮기려면
  * PersistentWindows 아이콘을 Shift + 클릭
* 전경 창을 다른 모든 창 뒤로 보내려면(Windows가 제공하는 Alt+Esc 단축키와 비슷하지만, 그 단축키는 원격 데스크톱 창 안의 창에는 동작하지 않습니다):
  * 바탕 화면을 Alt + 클릭하면 전경 창이 Z 순서의 맨 아래로 갑니다.
  * PersistentWindows 아이콘을 Ctrl + Alt + 클릭하면 *최대화된* 전경 창이 Z 순서의 맨 아래로 갑니다.
* 창을 작업 표시줄의 알림 영역으로 숨기려면:
  * 최소화 단추를 Ctrl + 클릭
* 창을 닫고 그 위치 기록을 영구히 잊게 하려면
  * Ctrl을 누른 채 창을 닫습니다
* 최상위가 아닌 창(자식 창이나 대화 상자 창)의 자동 복원을 켜거나 끄려면:
  * 자식/대화 상자 창을 자동 캡처/복원에 포함하려면 마우스로 그 창을 한 번 옮깁니다
  * 창을 자동 캡처/복원에서 제외하려면 Ctrl + Shift를 누른 채 창을 옮깁니다

### 일시중지 / 제외 목록
* PersistentWindows 메뉴에서 "일시중지 / 제외 목록..."을 고르면 세 가지 목록을 한 줄에 하나씩 편집할 수 있습니다. 목록은 저장하는 즉시 적용됩니다.
  | 목록 | 동작 |
  | --- | --- |
  | 이 프로세스가 실행 중이면 일시중지 | 목록의 프로세스가 하나라도 실행 중이면 캡처와 자동 복원을 멈춥니다. 모두 종료되면 이전 배치를 복원합니다
  | 캡처·조작에서 제외할 프로세스 | 목록에 있는 프로세스의 창은 캡처·이동·복원하지 않습니다
  | 캡처·조작에서 제외할 창 제목 | 제목에 목록의 텍스트가 들어 있는 창은 캡처·이동·복원하지 않습니다
* 프로세스 이름은 ".exe"를 붙여도 되고 빼도 되며(예: `notepad`), 대소문자를 구분하지 않습니다.
* "실행 중인 창에서 추가" 단추로 현재 열려 있는 창에서 골라 넣을 수 있습니다.
* 프로세스 때문에 일시중지된 동안에는 "자동 복원 일시중지" 메뉴 항목에 그 프로세스 이름이 표시됩니다. 실행 중인 프로세스는 3초마다 확인합니다.
* 목록은 `C:\Users\[사용자]\AppData\Local\PersistentWindows\rules.json`에 저장됩니다. 명령줄 옵션 -pause_process, -exclude_process, -exclude_title로 준 항목은 이번 실행에만 더해지며 대화 상자에는 표시되지 않습니다.

### 웹페이지 커맨더 창
* 웹페이지 커맨더 창은 (한 손으로 쓸 수 있도록 세심하게 설계된) 명령 단축키를 받아 아래에 있는 웹 브라우저 창에 맞는 동작으로 바꿔 전달해, 웹 탐색 효율을 최대로 높입니다.
* 아무 브라우저 창(Chrome, Edge, Firefox, Opera, Brave, Vivaldi 등)에서 Alt + W를 누르면 웹페이지 커맨더 창이 켜지거나 꺼집니다
* 켜지면 파란색의 작은 커맨더 창이 마우스 커서를 따라다니며 표시됩니다.
  | 명령 단축키 | 변환되는 키 | 의미 |
  | --- | --- | --- |
  | 1-8 | Ctrl + #n | n번째 탭 선택
  | 9 | Ctrl + 9 | 맨 오른쪽 탭 선택
  | TAB | Ctrl + TAB | 오른쪽의 다음 탭 선택
  | Q | Shift + Ctrl + TAB | 왼쪽의 이전 탭 선택
  | W | Ctrl + W | 탭 닫기
  | T | Ctrl + T | 새 탭
  | R | Ctrl + R | 웹 페이지 새로 고침(Reload)
  | A | Ctrl + L | 웹 주소(Address) 편집
  | S | Ctrl + F | 현재 페이지에서 찾기(Search)
  | X (또는 /) | / | 웹 검색
  | E | Home | 페이지 맨 위로 스크롤
  | D | End | 페이지 맨 아래로 스크롤
  | F | Alt + Right | 다음 URL로 앞으로(Forward) 가기
  | B | Alt + Left | 이전 URL로 뒤로(Backward) 가기
  | G | Ctrl + Shift + A | 탭 하나로 이동(Goto, 선택) (Chrome/Edge/Brave 전용)
  | V | | 마지막으로 방문한(Visited) 탭으로 이동
  | C | | 현재 탭 복제(Copy)
  | U (또는 Shift + T) | Ctrl + Shift + T | 닫은 탭 되살리기(Undo)
  | N | Ctrl + N | 새(New) 브라우저 창
  | H | Left | 왼쪽으로 스크롤
  | J | Down | 아래로 스크롤
  | K | Up | 위로 스크롤
  | L | Right | 오른쪽으로 스크롤
  | Space (또는 커맨더 창에서 왼쪽 클릭) | PgDn | 한 페이지 아래로
  | P (또는 커맨더 창에서 오른쪽 클릭) | PgUp | 한 페이지 위로
  | Z | | 다음 배경 웹 브라우저 창을 전경으로 가져오기
  | ~ | | 두 번째 배경 웹 브라우저 창을 전경으로 가져오기
  | 커맨더 창을 Alt + 클릭 || 마우스 클릭을 아래의 브라우저 창으로 전달

### 사용자 지정 아이콘 사용
* 기본 앱 아이콘을 원하는 아이콘으로 바꾸려면:
  * 원하는 .ico(또는 .png) 파일의 이름을 `pwIcon.ico`(또는 `pwIcon.png`)로 바꿔 PersistentWindows 프로그램 폴더에 복사합니다. `C:/Users/<YOUR_ID>/AppData/Local/PersistentWindows/`에 넣어도 됩니다.
  * 다른 ico/png 파일을 같은 폴더에 복사하고 이름을 `pwIconBusy.*`로 바꿉니다. 이 아이콘은 PersistentWindows가 창을 복원하느라 바쁠 때 표시됩니다.
  * 또 다른 ico/png 파일을 같은 폴더에 복사하고 이름을 `pwIconUpdate.*`로 바꿉니다. 이 아이콘은 새 PersistentWindows 릴리스가 있을 때 표시됩니다.

### 메뉴와 메시지 상자의 텍스트를 새 언어로 번역하기
* 설치 폴더의 translations.json을 C:\Users\\[사용자]\AppData\Local\PersistentWindows에 복사한 뒤, 그 복사본에 새 언어 번역을 추가하고 PersistentWindows를 다시 실행합니다.
