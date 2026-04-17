# PDF 편집기 (C# WinForms)

PDF 파일의 텍스트를 워드처럼 직접 클릭하여 수정할 수 있는 Windows PDF 편집기입니다.

---

## 📦 필요 환경

| 항목 | 버전 |
|------|------|
| .NET SDK | 8.0 이상 |
| OS | Windows 10 / 11 |
| Visual Studio | 2022 (선택) |

---

## 🚀 빌드 및 실행

### 방법 1 – 명령 프롬프트 / PowerShell

```powershell
cd PdfEditor

# NuGet 패키지 복원 + 빌드 + 실행
dotnet run
```

### 방법 2 – 릴리스 빌드 후 EXE 실행

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
# → bin\Release\net48\publish\QPDFEditor.exe
```

### 방법 3 – Visual Studio 2022

1. `PdfEditor.csproj` 파일을 VS2022 로 열기
2. F5 또는 **빌드 → 실행**

---

## 🎯 주요 기능

| 기능 | 단축키 |
|------|--------|
| 파일 열기 | Ctrl + O |
| 저장 | Ctrl + S |
| 다른 이름으로 저장 | Ctrl + Shift + S |
| 찾기 / 바꾸기 | Ctrl + H |
| 확대 / 축소 | Ctrl + +/- |
| 기본 크기 (100%) | Ctrl + 0 |
| 이전 페이지 | ← / PageUp |
| 다음 페이지 | → / PageDown |

---

## ✏️ 텍스트 편집 방법

1. PDF 파일을 엽니다.
2. 페이지에 **파란색 영역** (텍스트 블록)이 표시됩니다.
3. 파란 영역을 **클릭**하면 인라인 텍스트 에디터가 활성화됩니다.
4. 텍스트를 수정 후:
   - **Enter** – 변경 확정
   - **Esc** – 변경 취소
5. 수정된 블록은 **초록색**으로 표시됩니다.
6. **Ctrl+S** 로 저장하면 수정된 블록만 PDF에 반영됩니다.

---

## ⚙️ 기술 스택

| 라이브러리 | 용도 |
|-----------|------|
| **Docnet.Core** | PDFium 기반 PDF 페이지 렌더링 |
| **iText7** | 텍스트 추출 (좌표 포함) 및 PDF 저장 |
| **WinForms (.NET 8)** | UI 프레임워크 |

---

## ⚠️ 알려진 제한 사항

- **이미지 기반 PDF** (스캔본)은 텍스트 편집이 불가합니다. OCR 처리 필요.
- 저장 시 원본 폰트 대신 **맑은 고딕 → Arial → Helvetica** 순으로 대체 적용됩니다.
- 텍스트 길이가 크게 바뀌면 레이아웃이 흐트러질 수 있습니다 (PDF 특성상 자동 리플로우 없음).
- 암호화된 PDF는 먼저 비밀번호를 해제해야 합니다.

---

## 📁 프로젝트 구조

```
PdfEditor/
├── PdfEditor.csproj    ← 프로젝트 파일 (NuGet 패키지 포함)
├── Program.cs          ← 진입점
├── Models.cs           ← TextBlock 모델, iText7 이벤트 리스너
├── PdfService.cs       ← PDF 로드 / 렌더링 / 저장 서비스
├── PdfPageView.cs      ← 커스텀 WinForms 컨트롤 (편집 캔버스)
├── MainForm.cs         ← 메인 폼 (메뉴, 툴바, 상태바 등)
└── README.md           ← 이 파일
```
"# QPDF-Editor" 
