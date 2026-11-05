# UML Studio

Windows와 macOS에서 쓰는 크로스플랫폼 UML 모델링 도구

[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Avalonia 11](https://img.shields.io/badge/Avalonia-11-8B45C3)](https://avaloniaui.net/)
[![C#](https://img.shields.io/badge/C%23-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Windows](https://img.shields.io/badge/Windows-x64-0078D6?logo=windows&logoColor=white)](#windows)
[![macOS](https://img.shields.io/badge/macOS-Apple%20Silicon%20%7C%20Intel-000000?logo=apple&logoColor=white)](#macos)

.NET 8과 Avalonia 11로 만든 데스크톱 UML 모델러입니다. 클래스·시퀀스·유스케이스·액티비티·상태·컴포넌트 다이어그램을 그리고, 프로젝트를 `.umlproj`(JSON)로 저장합니다. 1.x 파일은 열 때 자동으로 변환됩니다.

## 기능

- **리본:** 시작 / 디자인 / 레이아웃 / 발행. 큰 버튼과 작은 버튼을 그룹 라벨로 묶습니다.
- **프로젝트 브라우저:** 모델 루트 → 패키지(중첩) → 요소·다이어그램. 특성과 오퍼레이션까지 펼치기, 필터 검색, 우클릭 메뉴(열기/추가/이름 바꾸기/속성/삭제), 더블클릭으로 다이어그램 열기, 트리에서 고른 요소를 다이어그램에 강조.
- **도구상자:** 다이어그램 종류에 맞춰 바뀝니다. 공통 / 요소 / 관계 섹션, 아이콘과 이름, 현재 도구 표시.
- **다이어그램:** 탭, 격자 용지, 다이어그램 프레임(예: `class 도메인 모델`), 연노랑 클래스·그라데이션·그림자, «스테레오타입», 구획(특성/오퍼레이션), 추상 클래스는 기울임, 직교 라우팅, UML 화살표(일반화·실현·합성·집합·의존·메시지), 다중성과 역할.
- **편집:** 고무줄(범위) 선택, Ctrl 다중 선택, 이동, 8방향 크기 조절, 방향키 미세 이동, 정렬·간격 균등·같은 크기, 앞/뒤 순서, 복제, 실행 취소/다시 실행.
- **보기:** Ctrl+휠 확대/축소(커서 기준), 가운데 버튼 또는 Space+드래그로 화면 이동, 전체 보기, 격자 표시·맞춤.
- **속성:** 일반 / 모양 / 피처 탭. 선택에 따라 이름, 스테레오타입, 별칭, 상태, 가시성, 버전, 위치·크기, 색, 특성·오퍼레이션, 연결선 종류·다중성·역할·가드가 바로 바뀝니다.
- **노트:** 선택한 요소, 연결선, 다이어그램의 설명을 편집합니다.
- **상태 표시줄:** 메시지, 모드, 다이어그램 종류, 선택 개수, 마우스 좌표, 확대율 슬라이더.
- **시작 페이지:** 최근 파일, 열기, 빈 프로젝트, 모델 빌더(요구사항/도메인/상호작용/상태/흐름/아키텍처 패키지를 한 번에 만듭니다).
- **테마:** 어두운 크롬과 흰색 다이어그램 용지.

지원하는 다이어그램: 클래스, 시퀀스(활성 막대, 메시지 번호, 자기 호출), 유스케이스(시스템 경계, «include»/«extend»), 액티비티(결정/병합, 포크/조인, 파티션), 상태 머신, 컴포넌트(노드, 아티팩트, 인터페이스).

내보내기: PNG(2배 해상도), SVG(벡터), 모든 다이어그램 일괄 PNG.

## 단축키

Ctrl+N 새로 만들기 · Ctrl+O 열기 · Ctrl+S 저장 · Ctrl+Shift+S 다른 이름으로 저장 · Ctrl+Z / Ctrl+Y 실행 취소/다시 실행 · Delete 삭제 · Ctrl+A 모두 선택 · Ctrl+D 복제 · F2 속성/이름 편집 · Ctrl+F 모델 검색 · Ctrl+= / Ctrl+- / Ctrl+0 확대/축소/100% · Ctrl+Shift+F 전체 보기 · Esc 선택 도구로 돌아가기

## 실행

공통으로 .NET 런타임을 따로 설치하지 않아도 됩니다. 아래 게시 결과는 self-contained 단일 실행 파일이고, 설치 프로그램은 없습니다. 경로에 한글이 있어도 됩니다. 시작 페이지의 **데모 프로젝트**를 누르거나 `samples/demo.umlproj`를 **열기**(Ctrl+O)로 엽니다. `.umlproj`를 실행 파일 위로 끌어다 놓거나 명령줄 인자로 넘기면 그 프로젝트가 열립니다.

### Windows

요구 사항: Windows 10/11 x64.

`publish/win-x64/UMLStudio.exe`를 더블클릭합니다.

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
```

#### Windows SmartScreen 경고

코드 서명이 없는 exe라서 처음 실행할 때 "Windows의 PC 보호" 창이 뜰 수 있습니다. **추가 정보 → 실행**을 누르면 됩니다. 회사 PC에서 막혀 있다면 파일 속성의 **차단 해제**에 체크하세요.

### macOS

Apple Silicon과 Intel을 각각 게시합니다. 결과물은 `.app` 번들이 아니라 서명되지 않은 단일 실행 파일 `UMLStudio`입니다.

```
# Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/osx-arm64

# Intel
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/osx-x64
```

실행:

```
cd publish/osx-arm64   # Intel이면 publish/osx-x64
chmod +x UMLStudio
./UMLStudio
```

#### Gatekeeper

서명이 없어서 처음 실행이 막힐 수 있습니다. Finder에서 `UMLStudio`를 **우클릭 → 열기**한 뒤 확인하거나, **시스템 설정 → 개인정보 보호 및 보안**에서 해당 실행 파일의 열기를 허용하세요. 그래도 거절되면 터미널에서 `./UMLStudio`로 실행해 메시지를 확인합니다.

## 현재 범위 / 미지원

- 코드 생성, 리버스 엔지니어링, RTF/HTML 문서, 시뮬레이션은 없습니다.
- 공유 저장소, 버전 관리, 협업, 보안 잠금, 프로파일, 스크립팅은 없습니다.
- 도킹 창은 크기만 조절됩니다. 창을 떼어 내거나 다시 배치하지는 못합니다.
- 시퀀스 결합 조각(alt/loop)과 BPMN·SysML 같은 UML 밖 표기법은 지원하지 않습니다.
- 연결선은 직선과 직교만 가능하고, 꺾임점을 직접 편집할 수 없습니다.
- 요소는 그 다이어그램에 속합니다. 같은 요소를 여러 다이어그램에서 재사용하지는 않습니다.
- Windows 실행 파일은 서명되지 않았고 MSI는 없습니다. macOS는 `.app`이 아닌 서명되지 않은 실행 파일입니다.

## 소스에서 빌드

.NET 8 SDK가 필요합니다. 저장소 루트(`UmlStudio.csproj`)에서:

```
dotnet build
dotnet run
```

`dotnet run`은 데스크톱 창을 엽니다. 샘플은 `samples/demo.umlproj`입니다.

단일 파일로 게시합니다. `publish/`는 git에 넣지 않습니다.

```
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64

# macOS Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/osx-arm64

# macOS Intel
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/osx-x64
```

Windows 결과물은 `publish/win-x64/UMLStudio.exe`입니다. macOS 결과물은 `publish/osx-arm64/UMLStudio` 또는 `publish/osx-x64/UMLStudio`이며, 실행 전에 `chmod +x UMLStudio`가 필요합니다.
