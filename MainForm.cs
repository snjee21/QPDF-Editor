using iText.Forms.Form.Element;
using iText.StyledXmlParser.Jsoup.Internal;
using QPDFEditor.Properties;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;

namespace QPDFEditor {
  public partial class MainForm : Form {
    private readonly PdfService _pdfService = new();
    private readonly EditHistory _history = new();
    private int _currentPage;
    private float _zoom = 1.0f;
    private static readonly int[] ZoomPercents = { 25, 50, 75, 100, 125, 150, 200, 300, 400, 500, 600, 800 };
    private bool _isEditMode = false;   // false = 보기 모드(기본), true = 편집 모드

    // 디자이너에 없을 수 있는 동적 편집 UI 항목
    private ToolStripButton? _btnUndo;
    private ToolStripButton? _btnRedo;
    private ToolStripMenuItem? _miUndo;
    private ToolStripMenuItem? _miRedo;
    private ToolStripButton? _btnModeEdit;
    private ToolStripButton? _btnModeSelect;
    private ToolStripButton? _btnModeAnnot;
    // 보기/편집 토글 버튼
    private ToolStripButton? _btnToggleEdit;
    // 썸네일 패널
    private SplitContainer? _splitMain;
    private Panel? _thumbnailPanel;
    private ListBox? _thumbnailList;

    private bool _updatingThumbnailLayout; // 재진입 방지 플래그 추가
    private HorizontalWheelMessageFilter? _horizontalWheelFilter;

    private bool _updatingFindPanelLayout;
    private int _thumbnailDropInsertIndex = -1;
    private bool _thumbnailDropActive;

    // 두 번째 툴바 (편집·페이지 관리 전용)
    //private ToolStrip? _toolbar2;

    // 페이지 관리 UI
    private ToolStripMenuItem? _miPage;
    private ToolStripMenuItem? _miPageMoveUp;
    private ToolStripMenuItem? _miPageMoveDown;
    private ToolStripMenuItem? _miPageDelete;
    private ToolStripMenuItem? _miPageInsertBefore;
    private ToolStripMenuItem? _miPageInsertAfter;
    private ToolStripMenuItem? _miPageInsertFirst;
    private ToolStripMenuItem? _miPageInsertLast;
    private ToolStripButton? _btnPageMoveUp;
    private ToolStripButton? _btnPageMoveDown;
    private ToolStripButton? _btnPageDelete;
    private ToolStripButton? _btnPageInsert;
    private ContextMenuStrip? _thumbContextMenu;

    private readonly struct ScrollSnapshot {
      public readonly float XRatio;
      public readonly float YRatio;

      public ScrollSnapshot(float xRatio, float yRatio) {
        XRatio = xRatio;
        YRatio = yRatio;
      }
    }

    // startupFile: 탐색기 컨텍스트 메뉴 / 파일 연결로 전달된 PDF 경로 (없으면 null)
    private readonly string? _startupFile;


    public MainForm(string? startupFile = null) {
      _startupFile = startupFile;

      InitializeComponent();

      // ── 두 번째 툴바 생성 (편집·페이지 관리 전용) ─────────────────
      // DockStyle.Top 컨트롤의 배치 순서는 Controls 컬렉션 인덱스(높을수록 위)에 의해 결정됨.
      // _toolbar2 를 _toolbar 아래에 표시하려면 _toolbar 보다 낮은 인덱스가 되어야 함.
      _toolbar2 = new ToolStrip {
        BackColor = SystemColors.MenuBar,
        GripStyle = ToolStripGripStyle.Hidden,
        ImageScalingSize = new Size(22, 22),
        Padding = new Padding(4, 2, 4, 2),
        Dock = DockStyle.Top,
      };
      _toolbar2.Renderer = new DarkToolStripRenderer();
      Controls.Add(_toolbar2);
      Controls.SetChildIndex(_toolbar2, 1);   // _toolbar(index 3 후) 바로 아래 위치

      _toolbar.Renderer = new DarkToolStripRenderer();
      EnsureEditingUi();
      _cmbZoom.SelectedItem = "100%";
      InitializeZoomUi();
      SetZoomText("100%");
      SetEditMode(EditMode.TextEdit);
      SetFileOpen(false);

      // 파일 경로가 전달된 경우 Form Load 후 자동으로 열기
      if (_startupFile != null)
        this.Load += (_, _) => TryOpenFile(_startupFile);

      // 썸네일 패널 + 보기/편집 토글 초기화
      BuildThumbnailPanel();
      if (_splitMain != null) {
        _splitMain.Panel2.Resize += (_, _) => CenterPage();
      }
      BuildToggleEditButton();
      BuildPageMenu();

      // 👇 1. 아이콘 동적 생성 및 할당 메서드 호출
      SetupIcons();

      // Undo/Redo 버튼 상태 연동
      _history.Changed += (_, _) => UpdateUndoRedoButtons();
      UpdateUndoRedoButtons();

      // PageView 에 히스토리 주입
      _pageView.History = _history;

      // 페이지/편집 이벤트 연결
      _pageContainer.Resize += PageContainer_Resize;
      _pageContainer.DragEnter += PageContainer_DragEnter;
      _pageContainer.DragDrop += PageContainer_DragDrop;

      _pageView.TextBlockCommitted += PageView_TextBlockCommitted;
      _pageView.ContentChanged += PageView_ContentChanged;
      _pageView.AnnotationAdded += PageView_AnnotationAdded;
      _pageView.AnnotationDeleted += PageView_AnnotationDeleted;
      _pageView.AnnotationEdited += PageView_AnnotationEdited;

      if (_splitMain != null) {
        _splitMain.Panel2.Resize += PageContainer_Resize;
        _splitMain.SplitterMoved += (_, _) => BeginInvoke((Action)(() => CenterPage()));
      }

      KeyPreview = true;
      KeyDown += OnGlobalKeyDown;

      this.MouseWheel += OnMouseWheelZoom;
      _pageContainer.MouseWheel += OnMouseWheelZoom;
      _pageView.MouseWheel += OnMouseWheelZoom;

      _horizontalWheelFilter = new HorizontalWheelMessageFilter(HandleHorizontalWheelMessage);
      Application.AddMessageFilter(_horizontalWheelFilter);

      Load += (_, _) => AdjustFindPanelLayout();
      Resize += (_, _) => AdjustFindPanelLayout();
      FontChanged += (_, _) => AdjustFindPanelLayout();
    }

    private int ScaleForDpi(int value) => Math.Max(1, (int)Math.Round(value * (DeviceDpi / 96f)));

    private void SetControlBoundsForFindBar(Control control, int width, int height) {
      control.MinimumSize = new Size(control.MinimumSize.Width, height);
      control.Size = new Size(Math.Max(width, control.MinimumSize.Width), height);
      control.Margin = new Padding(control.Margin.Left, Math.Max(2, ScaleForDpi(3)), control.Margin.Right, Math.Max(2, ScaleForDpi(3)));
    }

    private void AdjustFindPanelLayout() {
      if (_updatingFindPanelLayout || IsDisposed || layoutPanel1 == null || layoutPanel1.IsDisposed)
        return;

      _updatingFindPanelLayout = true;
      try {
        SuspendLayout();
        _findPanel.SuspendLayout();
        layoutPanel1.SuspendLayout();

        int rowHeight = Math.Max(ScaleForDpi(32), TextRenderer.MeasureText("가", Font).Height + ScaleForDpi(14));
        int panelHeight = rowHeight + _findPanel.Padding.Vertical + ScaleForDpi(6);

        _findPanel.MinimumSize = new Size(0, panelHeight);
        if (_findPanel.Height < panelHeight)
          _findPanel.Height = panelHeight;

        layoutPanel1.Height = Math.Max(1, panelHeight - _findPanel.Padding.Vertical);

        lblFind.Margin = new Padding(ScaleForDpi(4), 0, ScaleForDpi(4), 0);
        lblReplace.Margin = new Padding(ScaleForDpi(3), 0, ScaleForDpi(4), 0);
        _chkMatchCase.Margin = new Padding(ScaleForDpi(6), ScaleForDpi(4), ScaleForDpi(6), 0);
        _lblFindResult.Margin = new Padding(ScaleForDpi(6), ScaleForDpi(7), ScaleForDpi(4), 0);

        _lblFindResult.AutoSize = false;
        _lblFindResult.TextAlign = ContentAlignment.MiddleLeft;
        _lblFindResult.Height = rowHeight;

        int labelFindWidth = TextRenderer.MeasureText(lblFind.Text + "  ", lblFind.Font).Width;
        int labelReplaceWidth = TextRenderer.MeasureText(lblReplace.Text + "  ", lblReplace.Font).Width;
        int matchCaseWidth = _chkMatchCase.PreferredSize.Width;

        int availableWidth = Math.Max(layoutPanel1.ClientSize.Width, ClientSize.Width - _findPanel.Padding.Horizontal);
        int resultWidth = string.IsNullOrWhiteSpace(_lblFindResult.Text)
                  ? 0
                  : Math.Min(Math.Max(ScaleForDpi(100), availableWidth / 6), TextRenderer.MeasureText(_lblFindResult.Text + "  ", _lblFindResult.Font).Width);
        _lblFindResult.Width = resultWidth;

        int minTextWidth = ScaleForDpi(120);
        int buttonWidth = Math.Max(ScaleForDpi(96), Math.Min(ScaleForDpi(140), availableWidth / 8));
        int closeButtonWidth = Math.Max(ScaleForDpi(56), Math.Min(ScaleForDpi(90), availableWidth / 12));

        int horizontalSpacing = layoutPanel1.Controls.Cast<Control>()
                  .Sum(control => control.Margin.Left + control.Margin.Right);

        int fixedWidth = resultWidth + labelFindWidth + labelReplaceWidth + matchCaseWidth + (buttonWidth * 2) + closeButtonWidth + horizontalSpacing;
        int remainingWidth = Math.Max((availableWidth - fixedWidth) / 2, minTextWidth);

        SetControlBoundsForFindBar(_txtFind, remainingWidth, rowHeight);
        SetControlBoundsForFindBar(_txtReplace, remainingWidth, rowHeight);

        btnFindAll.MinimumSize = new Size(ScaleForDpi(96), rowHeight);
        btnReplaceAll.MinimumSize = new Size(ScaleForDpi(96), rowHeight);
        btnCloseFind.MinimumSize = new Size(ScaleForDpi(56), rowHeight);

        SetControlBoundsForFindBar(btnFindAll, buttonWidth, rowHeight);
        SetControlBoundsForFindBar(btnReplaceAll, buttonWidth, rowHeight);
        SetControlBoundsForFindBar(btnCloseFind, closeButtonWidth, rowHeight);
      } finally {
        layoutPanel1.ResumeLayout();
        _findPanel.ResumeLayout();
        ResumeLayout();
        _updatingFindPanelLayout = false;
      }
    }

    // ══════════════════════════════════════════════════════════════
    //  썸네일 패널 구성
    // ══════════════════════════════════════════════════════════════
    private void BuildThumbnailPanel() {
      // ── SplitContainer 생성 규칙 ─────────────────────────────────
      // 생성 시점에서 SplitContainer 는 아직 Form 에 추가되지 않아 Width = 0.
      // WinForms 내부 동작:
      //   FixedPanel setter → ApplyPanel2MinSize() → set_SplitterDistance()
      //   → Panel1MinSize ≤ SplitterDistance ≤ (Width - Panel2MinSize) 검증
      //   Width=0, Panel2MinSize > 0 → 우변이 음수 → 조건 불충족 → 예외
      //
      // 해결: 생성 시에는 MinSize 를 모두 0 으로, FixedPanel 도 None 으로 둠.
      //       Form.Load 이후(실제 크기 확정 후) 제약을 일괄 적용.

      _splitMain = new SplitContainer {
        Dock = DockStyle.Fill,
        Orientation = Orientation.Vertical,
        SplitterWidth = 4,
        BackColor = System.Drawing.Color.FromArgb(25, 25, 35),
        // FixedPanel, Panel1MinSize, Panel2MinSize, SplitterDistance 는
        // 모두 Form.Load 이벤트에서 설정 — 여기서 설정하면 Width=0 으로 예외 발생
      };

      this.Load += (_, _) => {
        if (_splitMain == null) return;
        // Form 이 화면에 표시된 후: 실제 ClientWidth 가 확정된 상태
        _splitMain.Panel1MinSize = 100;
        _splitMain.Panel2MinSize = 300;
        _splitMain.FixedPanel = FixedPanel.Panel1;
        _splitMain.SplitterDistance = 160;
      };

      // ── 좌측 썸네일 패널 ──────────────────────────────────────
      _thumbnailPanel = new Panel {
        Dock = DockStyle.Fill,
        BackColor = System.Drawing.Color.FromArgb(30, 30, 40),
      };

      var thumbLabel = new Label {
        Text = "페이지 목록",
        Dock = DockStyle.Top,
        Height = 26,
        ForeColor = System.Drawing.Color.FromArgb(180, 180, 180),
        BackColor = System.Drawing.Color.FromArgb(20, 20, 30),
        TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
        Font = new System.Drawing.Font("Malgun Gothic", 9f, System.Drawing.FontStyle.Bold),
      };

      _thumbnailList = new ListBox {
        Dock = DockStyle.Fill,
        BackColor = System.Drawing.Color.FromArgb(30, 30, 40),
        ForeColor = System.Drawing.Color.FromArgb(200, 200, 210),
        BorderStyle = BorderStyle.None,
        DrawMode = DrawMode.OwnerDrawFixed,
        ItemHeight = 130,
        Font = new System.Drawing.Font("Malgun Gothic", 8f),
        SelectionMode = SelectionMode.One,
      };
      _thumbnailList.DrawItem += ThumbnailList_DrawItem;
      _thumbnailList.SelectedIndexChanged += ThumbnailList_SelectedIndexChanged;
      _thumbnailList.Resize += (_, _) => SafeUpdateThumbnailLayout();
      _thumbnailList.AllowDrop = true;
      _thumbnailList.DragEnter += ThumbnailList_DragEnter;
      _thumbnailList.DragOver += ThumbnailList_DragOver;
      _thumbnailList.DragLeave += ThumbnailList_DragLeave;
      _thumbnailList.DragDrop += ThumbnailList_DragDrop;

      // 썸네일 컨텍스트 메뉴 (우클릭)
      _thumbContextMenu = BuildThumbnailContextMenu();
      _thumbnailList.ContextMenuStrip = _thumbContextMenu;
      _thumbnailList.MouseDown += (s, e) => {
        if (e.Button == MouseButtons.Right) {
          int idx = _thumbnailList!.IndexFromPoint(e.Location);
          if (idx >= 0) _thumbnailList.SelectedIndex = idx;
        }
      };

      _thumbnailPanel.Controls.Add(_thumbnailList);
      _thumbnailPanel.Controls.Add(thumbLabel);

      // ── 레이아웃 재배치 ───────────────────────────────────────
      // Form에서 _pageContainer 제거 후 SplitContainer 우측 패널에 재배치
      // Controls 순서가 DockStyle.Fill 배치를 결정하므로 순서 유지 필수

      _splitMain.Panel1.Controls.Add(_thumbnailPanel);

      // _pageContainer를 Form에서 분리 후 SplitContainer Panel2에 이동
      if (Controls.Contains(_pageContainer))
        Controls.Remove(_pageContainer);

      _pageContainer.Dock = DockStyle.Fill;
      _pageContainer.Padding = new Padding(10);
      _splitMain.Panel2.Controls.Add(_pageContainer);

      // _splitMain을 Form에 추가 — BringToFront()로 Fill 영역 확보
      // (_menu, _toolbar는 Top, _status/_findPanel은 Bottom이므로
      //  Fill인 _splitMain이 남은 공간을 자동으로 채움)
      Controls.Add(_splitMain);
      _splitMain.BringToFront();

      this.Load += (_, _) => SafeUpdateThumbnailLayout();
    }

    // ══════════════════════════════════════════════════════════════
    //  페이지 관리 메뉴 / 컨텍스트 메뉴 / 툴바
    // ══════════════════════════════════════════════════════════════
    private void BuildPageMenu() {
      // ── 메인 메뉴바에 "페이지" 메뉴 추가 ─────────────────────────
      _miPageMoveUp = new ToolStripMenuItem("위로 이동(&U)\tCtrl+Shift+Up", null, OnPageMoveUp);
      _miPageMoveDown = new ToolStripMenuItem("아래로 이동(&D)\tCtrl+Shift+Down", null, OnPageMoveDown);
      _miPageDelete = new ToolStripMenuItem("페이지 삭제(&X)", null, OnPageDelete);
      _miPageInsertBefore = new ToolStripMenuItem("이 페이지 앞에 PDF 삽입(&B)", null, OnPageInsertBefore);
      _miPageInsertAfter = new ToolStripMenuItem("이 페이지 뒤에 PDF 삽입(&A)", null, OnPageInsertAfter);
      _miPageInsertFirst = new ToolStripMenuItem("맨 앞에 PDF 삽입(&F)", null, OnPageInsertFirst);
      _miPageInsertLast = new ToolStripMenuItem("맨 뒤에 PDF 삽입(&L)", null, OnPageInsertLast);

      _miPage = new ToolStripMenuItem("페이지(&P)");
      _miPage.ForeColor = SystemColors.ControlText;
      _miPage.DropDownItems.AddRange(new ToolStripItem[] {
        _miPageMoveUp, _miPageMoveDown,
        new ToolStripSeparator(),
        _miPageDelete,
        new ToolStripSeparator(),
        _miPageInsertBefore, _miPageInsertAfter,
        new ToolStripSeparator(),
        _miPageInsertFirst, _miPageInsertLast,
      });

      // 메뉴바에서 "보기" 앞에 삽입
      int viewIdx = _menu.Items.IndexOf(miView);
      _menu.Items.Insert(viewIdx >= 0 ? viewIdx : _menu.Items.Count, _miPage);

      // ── 툴바2에 페이지 관리 버튼 추가 ────────────────────────────
      _btnPageMoveUp = new ToolStripButton("▲ 위로") { ToolTipText = "현재 페이지를 위로 이동 (Ctrl+Shift+↑)", Enabled = false };
      _btnPageMoveDown = new ToolStripButton("▼ 아래") { ToolTipText = "현재 페이지를 아래로 이동 (Ctrl+Shift+↓)", Enabled = false };
      _btnPageDelete = new ToolStripButton("🗑 삭제") { ToolTipText = "현재 페이지 삭제", Enabled = false };
      _btnPageInsert = new ToolStripButton("📄 병합") { ToolTipText = "다른 PDF 삽입", Enabled = false };

      _btnPageMoveUp.Click += OnPageMoveUp;
      _btnPageMoveDown.Click += OnPageMoveDown;
      _btnPageDelete.Click += OnPageDelete;
      _btnPageInsert.Click += OnPageInsertMenu;

      if (_toolbar2 != null) {
        foreach (var btn in new ToolStripItem[] {
          new ToolStripSeparator(), _btnPageMoveUp, _btnPageMoveDown,
          new ToolStripSeparator(), _btnPageDelete, _btnPageInsert })
          _toolbar2.Items.Add(btn);
      }
    }

    private ContextMenuStrip BuildThumbnailContextMenu() {
      var ctx = new ContextMenuStrip();
      ctx.Items.Add("▲ 위로 이동", null, OnPageMoveUp);
      ctx.Items.Add("▼ 아래로 이동", null, OnPageMoveDown);
      ctx.Items.Add(new ToolStripSeparator());
      ctx.Items.Add("🗑 페이지 삭제", null, OnPageDelete);
      ctx.Items.Add(new ToolStripSeparator());
      ctx.Items.Add("📄 이 페이지 앞에 PDF 삽입", null, OnPageInsertBefore);
      ctx.Items.Add("📄 이 페이지 뒤에 PDF 삽입", null, OnPageInsertAfter);
      ctx.Items.Add(new ToolStripSeparator());
      ctx.Items.Add("📄 맨 앞에 PDF 삽입", null, OnPageInsertFirst);
      ctx.Items.Add("📄 맨 뒤에 PDF 삽입", null, OnPageInsertLast);
      ctx.Opening += (_, _) => UpdatePageContextMenuState(ctx);
      return ctx;
    }

    private void UpdatePageContextMenuState(ContextMenuStrip ctx) {
      bool canEdit = CanPageManage();
      foreach (ToolStripItem item in ctx.Items) item.Enabled = canEdit;
      if (canEdit) {
        ctx.Items[0].Enabled = _currentPage > 0;                        // 위로 이동
        ctx.Items[1].Enabled = _currentPage < _pdfService.TotalPages - 1; // 아래로 이동
        ctx.Items[3].Enabled = _pdfService.TotalPages > 1;               // 삭제
      }
    }

    private bool CanPageManage() => _isEditMode && _pdfService.TotalPages > 0;

    // ── 페이지 관리 이벤트 핸들러 ────────────────────────────────────

    private void OnPageMoveUp(object? s, EventArgs e) {
      if (!CanPageManage() || _currentPage <= 0) return;
      if (!ConfirmPageOperation()) return;
      try {
        UseWaitCursor = true;
        int newIdx = _currentPage - 1;
        _pdfService.MovePage(_currentPage, newIdx);
        _currentPage = newIdx;
        AfterPageOperation($"페이지 {newIdx + 2} → {newIdx + 1}로 이동 완료");
      } catch (Exception ex) { Msg($"페이지 이동 오류:\n{ex.Message}", err: true); } finally { UseWaitCursor = false; }
    }

    private void OnPageMoveDown(object? s, EventArgs e) {
      if (!CanPageManage() || _currentPage >= _pdfService.TotalPages - 1) return;
      if (!ConfirmPageOperation()) return;
      try {
        UseWaitCursor = true;
        int newIdx = _currentPage + 1;
        _pdfService.MovePage(_currentPage, newIdx);
        _currentPage = newIdx;
        AfterPageOperation($"페이지 {newIdx} → {newIdx + 1}로 이동 완료");
      } catch (Exception ex) { Msg($"페이지 이동 오류:\n{ex.Message}", err: true); } finally { UseWaitCursor = false; }
    }

    private void OnPageDelete(object? s, EventArgs e) {
      if (!CanPageManage()) return;
      if (Msg($"현재 페이지 ({_currentPage + 1}페이지)를 삭제하시겠습니까?\n이 작업은 파일에 즉시 반영됩니다.", ask: true) != DialogResult.Yes) return;
      if (!ConfirmPageOperation()) return;
      try {
        UseWaitCursor = true;
        _pdfService.DeletePage(_currentPage);
        _currentPage = Math.Min(_currentPage, _pdfService.TotalPages - 1);
        AfterPageOperation("페이지 삭제 완료");
      } catch (Exception ex) { Msg($"페이지 삭제 오류:\n{ex.Message}", err: true); } finally { UseWaitCursor = false; }
    }

    private void OnPageInsertMenu(object? s, EventArgs e) {
      // 툴바 "병합" 버튼: 드롭다운 메뉴 표시
      if (_btnPageInsert == null) return;
      var ctx = new ContextMenuStrip();
      ctx.Items.Add("이 페이지 앞에 PDF 삽입", null, OnPageInsertBefore);
      ctx.Items.Add("이 페이지 뒤에 PDF 삽입", null, OnPageInsertAfter);
      ctx.Items.Add(new ToolStripSeparator());
      ctx.Items.Add("맨 앞에 PDF 삽입", null, OnPageInsertFirst);
      ctx.Items.Add("맨 뒤에 PDF 삽입", null, OnPageInsertLast);
      ctx.Show(_toolbar2 ?? _toolbar, _btnPageInsert.Bounds.Location with { Y = (_toolbar2 ?? _toolbar).Height });
    }

    private void OnPageInsertBefore(object? s, EventArgs e) => DoInsertPdf(_currentPage);
    private void OnPageInsertAfter(object? s, EventArgs e) => DoInsertPdf(_currentPage + 1);
    private void OnPageInsertFirst(object? s, EventArgs e) => DoInsertPdf(0);
    private void OnPageInsertLast(object? s, EventArgs e) => DoInsertPdf(_pdfService.TotalPages);

    private void DoInsertPdf(int insertBeforeIndex) {
      if (!CanPageManage()) return;

      using var dlg = new OpenFileDialog {
        Filter = "PDF 파일 (*.pdf)|*.pdf",
        Title = "삽입할 PDF 파일 선택",
        Multiselect = true,
      };
      if (dlg.ShowDialog(this) != DialogResult.OK) return;
      InsertPdfFilesAt(dlg.FileNames, insertBeforeIndex);
    }

    /// <summary>페이지 작업 전: 미저장 편집이 있으면 저장 여부 확인</summary>
    private bool ConfirmPageOperation() {
      if (!HasUnsavedChanges()) return true;
      var r = MessageBox.Show(
          "저장하지 않은 텍스트/주석 편집이 있습니다.\n" +
          "페이지 작업 전에 저장하시겠습니까?\n\n" +
          "예 = 저장 후 진행   아니오 = 편집 버리고 진행   취소 = 중단",
          "미저장 변경 사항",
          MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
      if (r == DialogResult.Cancel) return false;
      if (r == DialogResult.Yes) {
        try { _pdfService.Save(_pdfService.FilePath); _history.Clear(); } catch (Exception ex) { Msg($"저장 오류:\n{ex.Message}", err: true); return false; }
      } else {
        _history.Clear(); // 편집 버림
      }
      return true;
    }

    private void AfterPageOperation(string statusMsg) {
      _history.Clear();
      RefreshThumbnails();        // ← 먼저 썸네일 목록을 갱신해야 SelectedIndex 범위 오류 방지
      UpdatePageLabel();
      RenderCurrentPage(resetScroll: true);
      UpdatePageManageButtons();
      SetStatus(statusMsg);
    }

    private void UpdatePageManageButtons() {
      bool can = CanPageManage();
      if (_btnPageMoveUp != null) _btnPageMoveUp.Enabled = can && _currentPage > 0;
      if (_btnPageMoveDown != null) _btnPageMoveDown.Enabled = can && _currentPage < _pdfService.TotalPages - 1;
      if (_btnPageDelete != null) _btnPageDelete.Enabled = can && _pdfService.TotalPages > 1;
      if (_btnPageInsert != null) _btnPageInsert.Enabled = can;
      if (_miPageMoveUp != null) _miPageMoveUp.Enabled = can && _currentPage > 0;
      if (_miPageMoveDown != null) _miPageMoveDown.Enabled = can && _currentPage < _pdfService.TotalPages - 1;
      if (_miPageDelete != null) _miPageDelete.Enabled = can && _pdfService.TotalPages > 1;
      if (_miPageInsertBefore != null) _miPageInsertBefore.Enabled = can;
      if (_miPageInsertAfter != null) _miPageInsertAfter.Enabled = can;
      if (_miPageInsertFirst != null) _miPageInsertFirst.Enabled = can;
      if (_miPageInsertLast != null) _miPageInsertLast.Enabled = can;
    }

    private void BuildToggleEditButton() {
      _btnToggleEdit = new ToolStripButton {
        Text = "✏️  편집 모드로",
        ForeColor = System.Drawing.Color.FromArgb(80, 220, 120),
        Font = new System.Drawing.Font("Malgun Gothic", 9.5f, System.Drawing.FontStyle.Bold),
        CheckOnClick = true,
        Checked = false,
        Enabled = false,
        Margin = new Padding(8, 0, 4, 0),
        Padding = new Padding(10, 3, 10, 3),
        AutoToolTip = true,
        ToolTipText = "클릭하면 편집 모드로 전환됩니다",
      };
      _btnToggleEdit.Click += OnToggleEdit;

      // ── 툴바2 맨 앞(왼쪽)에 토글 버튼 배치 ──────────────────────
      if (_toolbar2 != null) {
        _toolbar2.Items.Insert(0, new ToolStripSeparator());
        _toolbar2.Items.Insert(0, _btnToggleEdit);
      }
    }

    // ── 썸네일 드로잉 ──────────────────────────────────────────────
    private void ThumbnailList_DrawItem(object? sender, DrawItemEventArgs e) {
      if (e.Index < 0 || _pdfService.TotalPages == 0) return;
      e.DrawBackground();

      var g = e.Graphics;
      var r = e.Bounds;
      bool selected = (e.State & DrawItemState.Selected) != 0;

      // 배경
      var bgColor = selected
          ? (_isEditMode ? System.Drawing.Color.FromArgb(40, 100, 60) : System.Drawing.Color.FromArgb(30, 60, 120))
          : System.Drawing.Color.FromArgb(35, 35, 48);
      using (var bgBrush = new System.Drawing.SolidBrush(bgColor))
        g.FillRectangle(bgBrush, r);

      bool drawInsertTop = _thumbnailDropActive && _thumbnailDropInsertIndex == e.Index;
      bool drawInsertBottom = _thumbnailDropActive &&
                              _thumbnailDropInsertIndex == e.Index + 1 &&
                              e.Index == _pdfService.TotalPages - 1;

      if (drawInsertTop || drawInsertBottom) {
        using (var insertPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 196, 72), 3f)) {
          int y = drawInsertTop ? (r.Top + 1) : (r.Bottom - 2);
          g.DrawLine(insertPen, r.Left + 6, y, r.Right - 6, y);
        }
      }

      // 썸네일 이미지 (캐시된 것 사용)
      var thumb = GetThumbnail(e.Index);
      if (thumb != null) {
        int tw = Math.Min(r.Width - 16, thumb.Width);
        int th = (int)((float)thumb.Height / thumb.Width * tw);
        th = Math.Min(th, r.Height - 26);
        tw = (int)((float)thumb.Width / thumb.Height * th);
        int tx = r.X + (r.Width - tw) / 2;
        int ty = r.Y + 4;
        g.DrawImage(thumb, new System.Drawing.Rectangle(tx, ty, tw, th));

        // 흰 테두리
        using var borderPen = new System.Drawing.Pen(selected
            ? System.Drawing.Color.FromArgb(80, 180, 120)
            : System.Drawing.Color.FromArgb(70, 70, 90), 1.5f);
        g.DrawRectangle(borderPen, new System.Drawing.Rectangle(tx - 1, ty - 1, tw + 1, th + 1));
      }

      // 페이지 번호 레이블
      using var numFont = new System.Drawing.Font("Malgun Gothic", 8f);
      using var numBrush = new System.Drawing.SolidBrush(selected
          ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(160, 160, 170));
      var numStr = $"  {e.Index + 1} / {_pdfService.TotalPages}";
      g.DrawString(numStr, numFont, numBrush, r.X, r.Bottom - 18);
    }

    // 썸네일 캐시
    private readonly System.Collections.Generic.Dictionary<int, System.Drawing.Bitmap> _thumbCache = new();

    private System.Drawing.Bitmap? GetThumbnail(int pageIndex) {
      if (_thumbCache.TryGetValue(pageIndex, out var cached)) return cached;
      try {
        var bmp = _pdfService.RenderPage(pageIndex, 0.18f);   // 18% 축소
        _thumbCache[pageIndex] = bmp;
        return bmp;
      } catch { return null; }
    }

    private void RefreshThumbnails() {
      if (_thumbnailList == null) return;

      // 캐시 초기화
      foreach (var b in _thumbCache.Values) b.Dispose();
      _thumbCache.Clear();

      _thumbnailList.Items.Clear();
      for (int i = 0; i < _pdfService.TotalPages; i++)
        _thumbnailList.Items.Add(i);   // 아이템은 페이지 인덱스(0-based)

      SafeUpdateThumbnailLayout();
      UpdateThumbnailSelection();
    }
    private void SafeUpdateThumbnailLayout() {
      if (_updatingThumbnailLayout || _thumbnailList == null || _thumbnailPanel == null)
        return;

      try {
        _updatingThumbnailLayout = true;

        int clientWidth = _thumbnailList.ClientSize.Width;
        if (clientWidth <= 0)
          return;

        int thumbWidth = Math.Max(48, clientWidth - SystemInformation.VerticalScrollBarWidth - 18);

        float pageRatio = 1.4142f;
        if (_pdfService.TotalPages > 0) {
          float pw = Math.Max(_pdfService.GetPageWidth(0), 1f);
          float ph = Math.Max(_pdfService.GetPageHeight(0), 1f);
          pageRatio = ph / pw;
        }

        int desiredImageHeight = (int)Math.Round(thumbWidth * pageRatio);
        int desiredItemHeight = desiredImageHeight + 26;
        int safeItemHeight = Math.Max(48, Math.Min(255, desiredItemHeight));

        if (_thumbnailList.ItemHeight != safeItemHeight)
          _thumbnailList.ItemHeight = safeItemHeight;

        _thumbnailList.Invalidate();
      } finally {
        _updatingThumbnailLayout = false;
      }
    }


    //private void UpdateThumbnailLayout() {
    //  if (_thumbnailList == null) return;
    //  int clientW = Math.Max(_thumbnailList.ClientSize.Width, 80);
    //  int thumbW = Math.Max(clientW - SystemInformation.VerticalScrollBarWidth - 18, 70);
    //  float pageRatio = 1.414f; // 기본 A계열 비율
    //  if (_pdfService.TotalPages > 0) {
    //    float pw = Math.Max(_pdfService.GetPageWidth(0), 1f);
    //    float ph = Math.Max(_pdfService.GetPageHeight(0), 1f);
    //    pageRatio = ph / pw;
    //  }
    //  int thumbH = Math.Max((int)Math.Round(thumbW * pageRatio), 90);
    //  int itemH = thumbH + 28;
    //  if (_thumbnailList.ItemHeight != itemH)
    //    _thumbnailList.ItemHeight = itemH;
    //  _thumbnailList.Invalidate();
    //}

    private void UpdateThumbnailLayout() => SafeUpdateThumbnailLayout();

    private void UpdateThumbnailSelection() {
      if (_thumbnailList == null || _thumbnailList.Items.Count == 0) return;
      if (_thumbnailList.SelectedIndex != _currentPage) {
        _thumbnailList.SelectedIndex = _currentPage;
        _thumbnailList.Invalidate();
      }
    }

    private void ThumbnailList_SelectedIndexChanged(object? sender, EventArgs e) {
      if (_thumbnailList == null) return;
      int idx = _thumbnailList.SelectedIndex;
      if (idx < 0 || idx == _currentPage) return;
      _pageView.CommitEdit();
      _currentPage = idx;
      UpdatePageLabel();
      RenderCurrentPage(resetScroll: true);
    }

    private void ThumbnailList_DragEnter(object? sender, DragEventArgs e) {
      UpdateThumbnailDragEffect(e);
      UpdateThumbnailDropTargetFromDrag(e);
    }

    private void ThumbnailList_DragOver(object? sender, DragEventArgs e) {
      UpdateThumbnailDragEffect(e);
      UpdateThumbnailDropTargetFromDrag(e);
    }

    private void ThumbnailList_DragLeave(object? sender, EventArgs e) {
      ClearThumbnailDropTarget();
    }

    private void ThumbnailList_DragDrop(object? sender, DragEventArgs e) {
      try {
        if (!CanPageManage()) return;
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
          return;

        int insertIndex = _thumbnailDropInsertIndex;
        if (insertIndex < 0)
          insertIndex = GetThumbnailInsertIndexFromClientPoint(
              _thumbnailList != null
                  ? _thumbnailList.PointToClient(new Point(e.X, e.Y))
                  : Point.Empty);

        InsertPdfFilesAt(files, insertIndex);
      } finally {
        ClearThumbnailDropTarget();
      }
    }

    private void UpdateThumbnailDragEffect(DragEventArgs e) {
      if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true || !CanPageManage()) {
        e.Effect = DragDropEffects.None;
        return;
      }

      if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) {
        e.Effect = DragDropEffects.None;
        return;
      }

      bool hasPdf = files.Any(IsPdfFilePath);
      e.Effect = hasPdf ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void UpdateThumbnailDropTargetFromDrag(DragEventArgs e) {
      if (_thumbnailList == null || e.Effect == DragDropEffects.None) {
        ClearThumbnailDropTarget();
        return;
      }

      Point clientPoint = _thumbnailList.PointToClient(new Point(e.X, e.Y));
      int insertIndex = GetThumbnailInsertIndexFromClientPoint(clientPoint);
      SetThumbnailDropTarget(insertIndex);
    }

    private int GetThumbnailInsertIndexFromClientPoint(Point clientPoint) {
      if (_thumbnailList == null || _pdfService.TotalPages <= 0)
        return 0;

      int idx = _thumbnailList.IndexFromPoint(clientPoint);
      if (idx >= 0) {
        Rectangle itemRect = _thumbnailList.GetItemRectangle(idx);
        bool insertAfter = clientPoint.Y >= itemRect.Top + (itemRect.Height / 2);
        return insertAfter ? idx + 1 : idx;
      }

      if (_thumbnailList.Items.Count == 0)
        return 0;

      Rectangle firstRect = _thumbnailList.GetItemRectangle(0);
      if (clientPoint.Y < firstRect.Top)
        return 0;

      Rectangle lastRect = _thumbnailList.GetItemRectangle(_thumbnailList.Items.Count - 1);
      if (clientPoint.Y > lastRect.Bottom)
        return _thumbnailList.Items.Count;

      int fallback = Math.Max(0, Math.Min(_thumbnailList.TopIndex, _thumbnailList.Items.Count - 1));
      Rectangle fallbackRect = _thumbnailList.GetItemRectangle(fallback);
      return clientPoint.Y < fallbackRect.Top + (fallbackRect.Height / 2) ? fallback : fallback + 1;
    }

    private void SetThumbnailDropTarget(int insertIndex) {
      if (_thumbnailList == null)
        return;

      int clamped = Math.Max(0, Math.Min(insertIndex, _pdfService.TotalPages));
      if (_thumbnailDropActive && _thumbnailDropInsertIndex == clamped)
        return;

      _thumbnailDropInsertIndex = clamped;
      _thumbnailDropActive = true;
      _thumbnailList.Invalidate();
    }

    private void ClearThumbnailDropTarget() {
      if (_thumbnailList == null)
        return;

      bool hadState = _thumbnailDropActive || _thumbnailDropInsertIndex >= 0;
      _thumbnailDropActive = false;
      _thumbnailDropInsertIndex = -1;
      if (hadState)
        _thumbnailList.Invalidate();
    }

    private bool IsPdfFilePath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    private void InsertPdfFilesAt(string[] files, int insertBeforeIndex) {
      if (!CanPageManage()) return;

      var pdfFiles = files
          .Where(IsPdfFilePath)
          .Where(File.Exists)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();

      if (pdfFiles.Count == 0) {
        Msg("끌어 넣은 파일 중 삽입 가능한 PDF가 없습니다.", err: true);
        return;
      }

      if (!ConfirmPageOperation())
        return;

      try {
        UseWaitCursor = true;

        int cursor = Math.Max(0, Math.Min(insertBeforeIndex, _pdfService.TotalPages));
        int totalAdded = 0;
        bool shiftCurrentPage = cursor <= _currentPage;

        foreach (string pdfPath in pdfFiles) {
          int beforePages = _pdfService.TotalPages;
          _pdfService.InsertPdf(pdfPath, cursor);
          int added = _pdfService.TotalPages - beforePages;

          totalAdded += added;
          cursor += added;

          if (shiftCurrentPage)
            _currentPage += added;
        }

        string fileCaption = pdfFiles.Count == 1
            ? Path.GetFileName(pdfFiles[0])
            : string.Format("{0} 외 {1}건", Path.GetFileName(pdfFiles[0]), pdfFiles.Count - 1);

        AfterPageOperation(string.Format(
            "PDF Drag & Drop 병합 완료: {0}페이지 삽입 ({1})",
            totalAdded, fileCaption));
      } catch (Exception ex) {
        Msg(string.Format("PDF Drag & Drop 병합 오류:\n{0}", ex.Message), err: true);
      } finally {
        UseWaitCursor = false;
      }
    }

    private void InitializeZoomUi() {
      if (_cmbZoom != null) {
        _cmbZoom.Items.Clear();
        foreach (int pct in ZoomPercents)
          _cmbZoom.Items.Add($"{pct}%");
        _cmbZoom.Width = 86;
      }

      if (miView != null) {
        miView.DropDownItems.Clear();
        foreach (int pct in ZoomPercents) {
          var item = new ToolStripMenuItem($"{pct}%");
          item.Click += MiZoom_Click;
          miView.DropDownItems.Add(item);
        }
      }
    }

    // ══════════════════════════════════════════════════════════════
    //  아이콘 생성 / 로드
    // ══════════════════════════════════════════════════════════════

    private void SetupIcons() {
      Icon = TryLoadAppIcon() ?? CreateFallbackAppIcon();

      // 메뉴 아이콘
      AssignMenuIcon("파일", CreateFolderMenuIcon());
      AssignMenuIcon("편집", CreateEditMenuIcon());
      AssignMenuIcon("보기", CreateViewIcon());
      AssignMenuIcon("도움말", CreateHelpIcon());

      AssignMenuIcon("열기", CreateOpenIcon());
      AssignMenuIcon("저장(&S)", CreateSaveIcon());
      AssignMenuIcon("다른 이름으로 저장", CreateSaveAsIcon());
      AssignMenuIcon("종료", CreateExitIcon());
      AssignMenuIcon("찾기 / 바꾸기", CreateFindIcon());
      AssignMenuIcon("모든 변경 사항 되돌리기", CreateUndoIcon());
      AssignMenuIcon("사용 방법", CreateBookIcon());
      AssignMenuIcon("정보", CreateInfoIcon());

      // 동적 툴바 버튼 아이콘
      if (_btnUndo != null) ApplyToolbarIcon(_btnUndo, CreateUndoIcon());
      if (_btnRedo != null) ApplyToolbarIcon(_btnRedo, CreateRedoIcon());
      if (_btnModeEdit != null) ApplyToolbarIcon(_btnModeEdit, LoadToolbarIcon("text_20.png") ?? CreateTextModeIcon());
      if (_btnModeSelect != null) ApplyToolbarIcon(_btnModeSelect, LoadToolbarIcon("area_delete_20.png") ?? CreateAreaDeleteModeIcon());
      if (_btnModeAnnot != null) ApplyToolbarIcon(_btnModeAnnot, LoadToolbarIcon("annotation_20.png") ?? CreateAnnotationModeIcon());
    }

    private void ApplyToolbarIcon(ToolStripButton button, Image image) {
      button.Image = image;
      button.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
      button.TextImageRelation = TextImageRelation.ImageBeforeText;
      button.ImageTransparentColor = Color.Magenta;
    }

    private void AssignMenuIcon(string textContains, Image image) {
      foreach (var item in EnumerateMenuItems(_menu.Items)) {
        if ((item.Text ?? string.Empty).IndexOf(textContains, StringComparison.OrdinalIgnoreCase) >= 0) {
          item.Image = image;
          return;
        }
      }
    }

    private static System.Collections.Generic.IEnumerable<ToolStripMenuItem> EnumerateMenuItems(ToolStripItemCollection items) {
      foreach (var item in items.OfType<ToolStripMenuItem>()) {
        yield return item;
        foreach (var child in EnumerateMenuItems(item.DropDownItems))
          yield return child;
      }
    }

    private string GetAssetsDir() => Path.Combine(AppContext.BaseDirectory, "Assets");

    private Icon? TryLoadAppIcon() {
      try {
        string path = Path.Combine(GetAssetsDir(), "app_icon.ico");
        return File.Exists(path) ? new Icon(path) : null;
      } catch { return null; }
    }

    private Image? LoadToolbarIcon(string fileName) {
      try {
        string path = Path.Combine(GetAssetsDir(), fileName);
        if (!File.Exists(path)) return null;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var img = Image.FromStream(fs);
        return new Bitmap(img);
      } catch { return null; }
    }

    private Icon CreateFallbackAppIcon() {
      using var bmp = new Bitmap(64, 64);
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.Clear(Color.Transparent);

      var pageRect = new RectangleF(10, 6, 44, 52);
      using var pageBrush = new SolidBrush(Color.FromArgb(250, 250, 252));
      using var borderPen = new Pen(Color.FromArgb(120, 120, 128), 2f);
      FillRounded(g, pageBrush, pageRect, 8f);
      DrawRounded(g, borderPen, pageRect, 8f);

      var fold = new PointF[] { new(40, 6), new(54, 6), new(54, 20) };
      using var foldBrush = new SolidBrush(Color.FromArgb(232, 232, 238));
      g.FillPolygon(foldBrush, fold);
      g.DrawPolygon(borderPen, fold);

      var banner = new RectangleF(14, 11, 18, 11);
      using var bannerBrush = new SolidBrush(Color.FromArgb(206, 38, 58));
      FillRounded(g, bannerBrush, banner, 4f);

      using var pdfFont = new Font("Arial", 8.5f, FontStyle.Bold, GraphicsUnit.Pixel);
      using var whiteBrush = new SolidBrush(Color.White);
      var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
      g.DrawString("PDF", pdfFont, whiteBrush, banner, sf);

      using var lineBrush = new SolidBrush(Color.FromArgb(184, 188, 196));
      FillRounded(g, lineBrush, new RectangleF(15, 28, 28, 3), 1.5f);
      FillRounded(g, lineBrush, new RectangleF(15, 35, 28, 3), 1.5f);
      FillRounded(g, lineBrush, new RectangleF(15, 42, 24, 3), 1.5f);

      using var pen = new Pen(Color.FromArgb(63, 122, 214), 5f) {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
      };
      g.DrawLine(pen, 26, 48, 45, 29);
      using var tipBrush = new SolidBrush(Color.FromArgb(230, 190, 130));
      g.FillPolygon(tipBrush, new PointF[] { new(22, 52), new(27, 47), new(29, 54) });

      var h = bmp.GetHicon();
      return Icon.FromHandle(h);
    }

    private static Bitmap NewIconBitmap() => new Bitmap(20, 20);

    private static void FillRounded(Graphics g, Brush brush, RectangleF rect, float radius) {
      using var path = RoundedRect(rect, radius);
      g.FillPath(brush, path);
    }

    private static void DrawRounded(Graphics g, Pen pen, RectangleF rect, float radius) {
      using var path = RoundedRect(rect, radius);
      g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius) {
      float d = radius * 2f;
      var path = new GraphicsPath();
      path.AddArc(rect.X, rect.Y, d, d, 180, 90);
      path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
      path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
      path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
      path.CloseFigure();
      return path;
    }

    private Image CreateFolderMenuIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var dark = new SolidBrush(Color.FromArgb(205, 165, 70));
      using var light = new SolidBrush(Color.FromArgb(237, 203, 111));
      using var pen = new Pen(Color.FromArgb(170, 132, 50));
      g.FillRectangle(dark, 3, 6, 6, 3);
      FillRounded(g, light, new RectangleF(2, 7, 16, 10), 2f);
      g.DrawRectangle(pen, 2, 7, 16, 10);
      return bmp;
    }

    private Image CreateOpenIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var folder = new SolidBrush(Color.FromArgb(242, 206, 98));
      using var pen = new Pen(Color.FromArgb(173, 136, 45));
      FillRounded(g, folder, new RectangleF(2, 7, 12, 9), 2f);
      g.DrawRectangle(pen, 2, 7, 12, 9);
      using var arrowPen = new Pen(Color.FromArgb(70, 130, 220), 2f) { EndCap = LineCap.ArrowAnchor };
      g.DrawLine(arrowPen, 9, 4, 16, 10);
      return bmp;
    }

    private Image CreateSaveIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var body = new SolidBrush(Color.FromArgb(78, 167, 106));
      using var slot = new SolidBrush(Color.FromArgb(230, 240, 235));
      using var pen = new Pen(Color.FromArgb(56, 130, 78));
      FillRounded(g, body, new RectangleF(3, 2, 14, 16), 2f);
      g.DrawRectangle(pen, 3, 2, 14, 16);
      g.FillRectangle(slot, 6, 4, 8, 4);
      g.FillRectangle(Brushes.White, 6, 11, 7, 4);
      return bmp;
    }

    private Image CreateSaveAsIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var paper = new SolidBrush(Color.FromArgb(236, 239, 246));
      using var pen = new Pen(Color.FromArgb(135, 145, 168));
      FillRounded(g, paper, new RectangleF(3, 2, 11, 15), 2f);
      g.DrawRectangle(pen, 3, 2, 11, 15);
      using var plusPen = new Pen(Color.FromArgb(75, 150, 92), 2f);
      g.DrawLine(plusPen, 14, 8, 18, 8);
      g.DrawLine(plusPen, 16, 6, 16, 10);
      return bmp;
    }

    private Image CreateExitIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var door = new SolidBrush(Color.FromArgb(176, 120, 76));
      using var pen = new Pen(Color.FromArgb(132, 86, 54));
      g.FillRectangle(door, 4, 3, 8, 14);
      g.DrawRectangle(pen, 4, 3, 8, 14);
      g.FillEllipse(Brushes.Gold, 9, 9, 2, 2);
      using var arrowPen = new Pen(Color.FromArgb(220, 90, 90), 2f) { EndCap = LineCap.ArrowAnchor };
      g.DrawLine(arrowPen, 11, 10, 18, 10);
      return bmp;
    }

    private Image CreateEditMenuIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var pen = new Pen(Color.FromArgb(71, 131, 218), 3f) {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
      };
      g.DrawLine(pen, 5, 15, 14, 6);
      using var tip = new SolidBrush(Color.FromArgb(236, 194, 138));
      g.FillPolygon(tip, new PointF[] { new(4, 16), new(6, 13), new(7, 17) });
      return bmp;
    }

    private Image CreateFindIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var pen = new Pen(Color.FromArgb(132, 89, 195), 2.2f);
      g.DrawEllipse(pen, 3, 3, 9, 9);
      g.DrawLine(pen, 10, 10, 16, 16);
      return bmp;
    }

    private Image CreateUndoIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var pen = new Pen(Color.FromArgb(214, 121, 68), 2.2f);
      g.DrawArc(pen, 4, 4, 10, 10, 110, 220);
      using var brush = new SolidBrush(Color.FromArgb(214, 121, 68));
      g.FillPolygon(brush, new PointF[] { new(4, 8), new(9, 6), new(8, 11) });
      return bmp;
    }

    private Image CreateRedoIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var pen = new Pen(Color.FromArgb(214, 121, 68), 2.2f);
      g.DrawArc(pen, 6, 4, 10, 10, -150, 220);
      using var brush = new SolidBrush(Color.FromArgb(214, 121, 68));
      g.FillPolygon(brush, new PointF[] { new(16, 8), new(11, 6), new(12, 11) });
      return bmp;
    }

    private Image CreateViewIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var pen = new Pen(Color.FromArgb(104, 157, 201), 1.6f);
      g.DrawArc(pen, 2, 5, 16, 10, 0, 180);
      g.DrawArc(pen, 2, 5, 16, 10, 180, 180);
      using var pupil = new SolidBrush(Color.FromArgb(104, 157, 201));
      g.FillEllipse(pupil, 8, 7, 4, 4);
      return bmp;
    }

    private Image CreateHelpIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
      using var font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel);
      using var brush = new SolidBrush(Color.FromArgb(116, 129, 213));
      g.DrawString("?", font, brush, new RectangleF(2, 1, 16, 16),
          new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
      return bmp;
    }

    private Image CreateBookIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var cover = new SolidBrush(Color.FromArgb(104, 142, 201));
      using var page = new SolidBrush(Color.FromArgb(245, 248, 252));
      using var pen = new Pen(Color.FromArgb(78, 108, 164));
      FillRounded(g, cover, new RectangleF(2, 3, 15, 13), 2f);
      g.FillRectangle(page, 5, 5, 9, 9);
      g.DrawRectangle(pen, 2, 3, 15, 13);
      return bmp;
    }

    private Image CreateInfoIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var circle = new SolidBrush(Color.FromArgb(91, 157, 214));
      g.FillEllipse(circle, 3, 3, 14, 14);
      using var font = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel);
      g.DrawString("i", font, Brushes.White, new RectangleF(3, 3, 14, 14),
          new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
      return bmp;
    }

    private Image CreateTextModeIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var back = new SolidBrush(Color.FromArgb(235, 244, 255));
      using var pen = new Pen(Color.FromArgb(118, 152, 220));
      FillRounded(g, back, new RectangleF(1.5f, 1.5f, 17, 17), 4f);
      DrawRounded(g, pen, new RectangleF(1.5f, 1.5f, 17, 17), 4f);
      using var font = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel);
      using var brush = new SolidBrush(Color.FromArgb(52, 92, 186));
      g.DrawString("T", font, brush, new RectangleF(0, 0, 20, 20),
          new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
      return bmp;
    }

    private Image CreateAreaDeleteModeIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var back = new SolidBrush(Color.FromArgb(255, 242, 242));
      using var framePen = new Pen(Color.FromArgb(197, 95, 95));
      FillRounded(g, back, new RectangleF(1.5f, 1.5f, 17, 17), 4f);
      DrawRounded(g, framePen, new RectangleF(1.5f, 1.5f, 17, 17), 4f);

      using var dashPen = new Pen(Color.FromArgb(197, 95, 95), 1.1f) { DashStyle = DashStyle.Dash };
      g.DrawRectangle(dashPen, 4, 4, 12, 12);
      using var xpen = new Pen(Color.FromArgb(191, 70, 70), 2f);
      g.DrawLine(xpen, 6, 6, 14, 14);
      g.DrawLine(xpen, 14, 6, 6, 14);
      return bmp;
    }

    private Image CreateAnnotationModeIcon() {
      var bmp = NewIconBitmap();
      using var g = Graphics.FromImage(bmp);
      g.SmoothingMode = SmoothingMode.AntiAlias;
      using var back = new SolidBrush(Color.FromArgb(250, 242, 163));
      using var fold = new SolidBrush(Color.FromArgb(236, 214, 114));
      using var pen = new Pen(Color.FromArgb(176, 150, 49));
      FillRounded(g, back, new RectangleF(2, 2, 15, 14), 2f);
      g.FillPolygon(fold, new PointF[] { new(13, 2), new(17, 2), new(17, 6) });
      g.DrawRectangle(pen, 2, 2, 15, 14);
      using var lpen = new Pen(Color.FromArgb(135, 110, 38), 1.2f);
      g.DrawLine(lpen, 5, 8, 14, 8);
      g.DrawLine(lpen, 5, 11, 12, 11);
      return bmp;
    }

    // ── 디자이너 호환 이벤트 래퍼 ────────────────────────────────
    private void MiExit_Click(object s, EventArgs e) => Close();
    private void BtnPrev_Click(object s, EventArgs e) => MovePage(-1);
    private void BtnNext_Click(object s, EventArgs e) => MovePage(+1);
    private void BtnCloseFind_Click(object s, EventArgs e) => _findPanel.Visible = false;
    private void MiZoom_Click(object s, EventArgs e) { if (s is ToolStripMenuItem m) SetZoomText(m.Text); }
    private void PageView_TextBlockCommitted(object s, TextBlock e) => UpdateModifiedLabel();
    private void PageView_ContentChanged(object s, EventArgs e) {
      UpdateModifiedLabel();
      // 썸네일의 선택색/상태 표시를 즉시 다시 그림
      _thumbnailList?.Invalidate();
    }
    private void PageContainer_Resize(object s, EventArgs e) => CenterPage();
    private void PageContainer_DragEnter(object s, DragEventArgs e) {
      if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
    }
    private void PageContainer_DragDrop(object s, DragEventArgs e) {
      if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        TryOpenFile(files[0]);
    }

    // ── 주석 이벤트 ───────────────────────────────────────────────
    private void PageView_AnnotationAdded(object s, PdfAnnotation a) {
      a.PageNumber = _currentPage + 1;
      UpdateModifiedLabel();
    }
    private void PageView_AnnotationDeleted(object s, PdfAnnotation a) => UpdateModifiedLabel();
    private void PageView_AnnotationEdited(object s, PdfAnnotation a) => UpdateModifiedLabel();

    // ── 동적 편집 UI 생성 ───────────────────────────────────────
    private void EnsureEditingUi() {
      EnsureUndoRedoMenuItems();
      EnsureToolbarButtons();
    }

    private void EnsureUndoRedoMenuItems() {
      if (_menu == null) return;

      var editMenu = _menu.Items
          .OfType<ToolStripMenuItem>()
          .FirstOrDefault(m => (m.Text ?? string.Empty).Contains("편집"));
      if (editMenu == null) return;

      _miUndo ??= new ToolStripMenuItem("실행 취소(&U)  Ctrl+Z", null, OnUndo);
      _miRedo ??= new ToolStripMenuItem("다시 실행(&R)  Ctrl+Y", null, OnRedo);

      if (!editMenu.DropDownItems.Contains(_miUndo))
        editMenu.DropDownItems.Insert(0, _miUndo);
      if (!editMenu.DropDownItems.Contains(_miRedo))
        editMenu.DropDownItems.Insert(1, _miRedo);

      if (editMenu.DropDownItems.Count < 3 || editMenu.DropDownItems[2] is not ToolStripSeparator)
        editMenu.DropDownItems.Insert(2, new ToolStripSeparator());
    }

    private void EnsureToolbarButtons() {
      if (_toolbar == null) return;

      // ── 툴바1: Undo / Redo ────────────────────────────────────────
      _btnUndo ??= CreateToolbarButton("↶ 취소", OnUndo, checkOnClick: false);
      _btnRedo ??= CreateToolbarButton("↷ 재실행", OnRedo, checkOnClick: false);

      AddToolbar1ItemIfMissing(_btnUndo);
      AddToolbar1ItemIfMissing(_btnRedo);

      // ── 툴바2: 편집 서브모드 버튼 ────────────────────────────────
      if (_toolbar2 == null) return;

      _btnModeEdit ??= CreateToolbarButton("텍스트", BtnModeEdit_Click, checkOnClick: true);
      _btnModeSelect ??= CreateToolbarButton("영역삭제", BtnModeSelect_Click, checkOnClick: true);
      _btnModeAnnot ??= CreateToolbarButton("주석", BtnModeAnnot_Click, checkOnClick: true);

      AddToolbar2ItemIfMissing(_btnModeEdit);
      AddToolbar2ItemIfMissing(_btnModeSelect);
      AddToolbar2ItemIfMissing(_btnModeAnnot);
    }

    private ToolStripButton CreateToolbarButton(string text, EventHandler onClick, bool checkOnClick) {
      var b = new ToolStripButton(text) {
        DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
        TextImageRelation = TextImageRelation.ImageBeforeText,
        ImageTransparentColor = Color.Magenta,
        CheckOnClick = checkOnClick,
        AutoToolTip = true,
        Margin = new Padding(2, 0, 2, 0),
        Padding = new Padding(4, 2, 4, 2),
      };
      b.Click += onClick;
      return b;
    }

    private void AddToolbar1ItemIfMissing(ToolStripItem item) {
      if (_toolbar.Items.OfType<ToolStripItem>().Any(x => ReferenceEquals(x, item))) return;
      _toolbar.Items.Add(item);
    }

    private void AddToolbar2ItemIfMissing(ToolStripItem item) {
      if (_toolbar2 == null) return;
      if (_toolbar2.Items.OfType<ToolStripItem>().Any(x => ReferenceEquals(x, item))) return;
      _toolbar2.Items.Add(item);
    }

    // ══════════════════════════════════════════════════════════════
    //  파일 작업
    // ══════════════════════════════════════════════════════════════
    private void OnOpen(object s, EventArgs e) {
      using var dlg = new OpenFileDialog { Filter = "PDF 파일 (*.pdf)|*.pdf|모든 파일 (*.*)|*.*", Title = "PDF 파일 열기" };
      if (dlg.ShowDialog(this) == DialogResult.OK) TryOpenFile(dlg.FileName);
    }

    private void TryOpenFile(string path) {
      CommitPendingEdits();
      if (HasUnsavedChanges() &&
          Msg("저장하지 않은 변경 사항이 있습니다.\n저장하지 않고 다른 파일을 여시겠습니까?", ask: true) != DialogResult.Yes)
        return;
      if (!File.Exists(path)) { Msg($"파일을 찾을 수 없습니다:\n{path}", err: true); return; }
      if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
          Msg("선택한 파일이 PDF가 아닐 수 있습니다. 계속하시겠습니까?", ask: true) != DialogResult.Yes)
        return;

      UseWaitCursor = true;
      try {
        _pdfService.Load(path);
        _history.Clear();
        _currentPage = 0;
        Text = $"PDF 편집기  [보기]  —  {Path.GetFileName(path)}";
        SetFileOpen(true);
        // 파일 열 때는 항상 보기 모드로 시작
        _isEditMode = false;
        ApplyViewEditMode();
        UpdatePageLabel();
        RenderCurrentPage(resetScroll: true);
        RefreshThumbnails();
        UpdatePageManageButtons();
        string warningSuffix = _pdfService.LoadWarnings.Count > 0
            ? $"   |   주의: {_pdfService.LoadWarnings.Count}개 페이지는 글꼴/CMap 해석 문제로 텍스트 편집 정보 추출이 제한됨"
            : string.Empty;
        SetStatus($"파일 열기 완료: {path}   |   총 {_pdfService.TotalPages}페이지  |  보기 모드 (편집하려면 [편집] 버튼 클릭){warningSuffix}");
      } catch (Exception ex) { Msg($"파일 열기 오류:\n{ex.Message}", err: true); } finally { UseWaitCursor = false; }
    }

    private void OnSave(object s, EventArgs e) {
      if (_pdfService.FilePath == "") return;
      CommitPendingEdits();
      string savePath = _pdfService.FilePath + ".~saving.pdf";
      DoSave(savePath, overwrite: true, finalPath: _pdfService.FilePath);
    }

    private void OnSaveAs(object s, EventArgs e) {
      if (_pdfService.FilePath == "") return;
      CommitPendingEdits();
      using var dlg = new SaveFileDialog {
        Filter = "PDF 파일 (*.pdf)|*.pdf",
        FileName = Path.GetFileNameWithoutExtension(_pdfService.FilePath) + "_edited.pdf",
        InitialDirectory = Path.GetDirectoryName(_pdfService.FilePath),
        Title = "다른 이름으로 저장",
      };
      if (dlg.ShowDialog(this) == DialogResult.OK)
        DoSave(dlg.FileName, overwrite: false, finalPath: dlg.FileName);
    }

    private void DoSave(string outputPath, bool overwrite, string finalPath) {
      UseWaitCursor = true;
      try {
        var (saved, _) = _pdfService.Save(outputPath);

        if (overwrite) {
          File.Delete(finalPath);
          File.Move(outputPath, finalPath);
        }
        _pdfService.AcceptAllEdits();
        _pdfService.SetCurrentFilePath(finalPath);
        _history.Clear();

        Text = _isEditMode
            ? $"PDF 편집기  [편집]  —  {Path.GetFileName(finalPath)}"
            : $"PDF 편집기  [보기]  —  {Path.GetFileName(finalPath)}";
        RenderCurrentPage(resetScroll: false);
        RefreshThumbnails();
        UpdateModifiedLabel();
        SetStatus($"저장 완료  ✓  |  수정된 블록: {saved}개  |  {finalPath}");
        Msg($"저장 완료!\n수정된 텍스트 블록: {saved}개\n경로: {finalPath}");
      } catch (Exception ex) {
        var sb = new System.Text.StringBuilder();
        for (var ex2 = ex; ex2 != null; ex2 = ex2.InnerException)
          sb.AppendLine($"• {ex2.GetType().Name}: {ex2.Message}");
        Msg($"저장 오류:\n{sb}", err: true);
      } finally { UseWaitCursor = false; }
    }

    private void OnRevertAll(object s, EventArgs e) {
      if (!CanEditFeatures()) return;
      CommitPendingEdits();
      if (Msg("현재 페이지의 모든 변경 사항을 되돌리시겠습니까?", ask: true) != DialogResult.Yes) return;
      foreach (var block in _pdfService.AllBlocks[_currentPage])
        block.EditedText = block.OriginalText;
      _history.Clear();
      RenderCurrentPage(resetScroll: false);
      UpdateModifiedLabel();
      SetStatus("현재 페이지의 변경 사항이 되돌려졌습니다.");
    }

    // ══════════════════════════════════════════════════════════════
    //  Undo / Redo
    // ══════════════════════════════════════════════════════════════
    private void OnUndo(object s, EventArgs e) {
      if (!CanEditFeatures()) return;
      CommitPendingEdits();
      _history.Undo();
      RenderCurrentPage(resetScroll: false);
      UpdateModifiedLabel();
      SetStatus($"실행 취소: {_history.RedoDescription}");
    }

    private void OnRedo(object s, EventArgs e) {
      if (!CanEditFeatures()) return;
      CommitPendingEdits();
      _history.Redo();
      RenderCurrentPage(resetScroll: true);
      UpdateModifiedLabel();
      SetStatus($"다시 실행: {_history.UndoDescription}");
    }

    private void UpdateUndoRedoButtons() {
      bool canUndo = _isEditMode && _history.CanUndo;
      bool canRedo = _isEditMode && _history.CanRedo;
      if (_btnUndo != null) {
        _btnUndo.Enabled = canUndo;
        _btnUndo.ToolTipText = _history.CanUndo ? $"실행 취소: {_history.UndoDescription}" : "실행 취소";
      }
      if (_btnRedo != null) {
        _btnRedo.Enabled = canRedo;
        _btnRedo.ToolTipText = _history.CanRedo ? $"다시 실행: {_history.RedoDescription}" : "다시 실행";
      }
      if (_miUndo != null) _miUndo.Enabled = canUndo;
      if (_miRedo != null) _miRedo.Enabled = canRedo;
    }

    // ══════════════════════════════════════════════════════════════
    //  편집 모드 전환
    // ══════════════════════════════════════════════════════════════
    private void SetEditMode(EditMode mode) {
      _pageView.Mode = mode;
      if (_btnModeEdit != null) _btnModeEdit.Checked = mode == EditMode.TextEdit;
      if (_btnModeSelect != null) _btnModeSelect.Checked = mode == EditMode.SelectDelete;
      if (_btnModeAnnot != null) _btnModeAnnot.Checked = mode == EditMode.AddAnnotation;
      _toolbar?.Invalidate();

      SetStatus(mode switch {
        EditMode.SelectDelete => "드래그하여 영역 내 텍스트를 삭제합니다.",
        EditMode.AddAnnotation => "클릭하거나 드래그하여 임의 위치에 주석을 추가합니다.",
        _ => "파란 영역을 클릭하면 텍스트를 수정할 수 있습니다.",
      });
    }

    // ── 보기/편집 토글 ────────────────────────────────────────────────
    private void OnToggleEdit(object s, EventArgs e) {
      _isEditMode = !_isEditMode;
      ApplyViewEditMode();
      SetStatus(_isEditMode
          ? "편집 모드: 파란 영역 클릭으로 텍스트 수정, 모드 버튼으로 기능 전환"
          : "보기 모드: [편집] 버튼을 클릭하면 편집할 수 있습니다.");
    }

    private void ApplyViewEditMode() {
      bool edit = _isEditMode;
      _pageView.IsViewOnly = !edit;

      if (!edit)
        _findPanel.Visible = false;

      // ── 편집 전용 컨트롤 활성화/비활성화 ─────────────────────────
      if (_btnModeEdit != null) _btnModeEdit.Enabled = edit;
      if (_btnModeSelect != null) _btnModeSelect.Enabled = edit;
      if (_btnModeAnnot != null) _btnModeAnnot.Enabled = edit;
      if (_btnUndo != null) _btnUndo.Enabled = edit && _history.CanUndo;
      if (_btnRedo != null) _btnRedo.Enabled = edit && _history.CanRedo;
      if (_miUndo != null) _miUndo.Enabled = edit && _history.CanUndo;
      if (_miRedo != null) _miRedo.Enabled = edit && _history.CanRedo;
      _btnSave.Enabled = edit;
      _btnSaveAs.Enabled = edit;
      _miSave.Enabled = edit;
      _miSaveAs.Enabled = edit;
      _btnRevert.Enabled = edit;
      _miRevert.Enabled = edit;
      _miFindReplace.Enabled = edit;

      // ── 토글 버튼 텍스트/색/상태 업데이트 ────────────────────────
      if (_btnToggleEdit != null) {
        _btnToggleEdit.Text = edit ? "🔒  보기 모드로" : "✏️  편집 모드로";
        _btnToggleEdit.ForeColor = edit
            ? System.Drawing.Color.FromArgb(255, 160, 60)   // 주황 = 편집 중
            : System.Drawing.Color.FromArgb(80, 220, 120);  // 초록 = 보기 중
        _btnToggleEdit.Checked = edit;
        // 툴팁도 상태에 맞게 갱신
        _btnToggleEdit.ToolTipText = edit
            ? "현재 편집 모드입니다. 클릭하면 보기 모드로 전환합니다."
            : "클릭하면 편집 모드로 전환합니다.";
      }

      // ── 편집 모드 진입 시 기본 서브모드(텍스트 편집)로 초기화 ──────
      if (edit) SetEditMode(EditMode.TextEdit);

      // ── 페이지 관리 버튼 상태 갱신 ────────────────────────────────
      UpdatePageManageButtons();

      // ── 썸네일 패널 배경색으로 현재 모드 시각적 표시 ────────────
      if (_thumbnailPanel != null)
        _thumbnailPanel.BackColor = edit
            ? System.Drawing.Color.FromArgb(25, 45, 25)   // 어두운 초록 = 편집 모드
            : System.Drawing.Color.FromArgb(28, 28, 40);  // 어두운 네이비 = 보기 모드

      // ── 타이틀바에 모드 표시 ─────────────────────────────────────
      string fileName = System.IO.Path.GetFileName(_pdfService.FilePath);
      if (!string.IsNullOrEmpty(fileName))
        Text = edit
            ? $"PDF 편집기  [편집]  —  {fileName}"
            : $"PDF 편집기  [보기]  —  {fileName}";
      _thumbnailList?.Invalidate();
    }

    private void BtnModeEdit_Click(object s, EventArgs e) => SetEditMode(EditMode.TextEdit);
    private void BtnModeSelect_Click(object s, EventArgs e) => SetEditMode(EditMode.SelectDelete);
    private void BtnModeAnnot_Click(object s, EventArgs e) => SetEditMode(EditMode.AddAnnotation);

    // ══════════════════════════════════════════════════════════════
    //  페이지 렌더링 / 내비게이션
    // ══════════════════════════════════════════════════════════════
    private void RenderCurrentPage(bool resetScroll = false) {
      if (_pdfService.TotalPages == 0) return;
      UseWaitCursor = true;
      try {
        var bmp = _pdfService.RenderPage(_currentPage, _zoom);
        _pageView.SetPage(
            bmp,
            _pdfService.AllBlocks[_currentPage],
            _pdfService.AllAnnotations[_currentPage],
            _pdfService.GetPageWidth(_currentPage),
            _pdfService.GetPageHeight(_currentPage));
        UpdatePageViewport(resetScroll);
        UpdateModifiedLabel();
        SafeUpdateThumbnailLayout();
        UpdateThumbnailSelection();
      } catch (Exception ex) { Msg($"페이지 렌더링 오류:\n{ex.Message}", err: true); } finally { UseWaitCursor = false; }
    }

    private void CenterPage() => UpdatePageViewport(resetScroll: false);

    private void ResetPageScrollToTop() {
      if (_pageContainer == null) return;
      _pageContainer.AutoScrollPosition = new Point(0, 0);
    }

    private void UpdatePageViewport(bool resetScroll) {
      if (_pageContainer == null || _pageView == null) return;

      const int margin = 20;
      int currentX = Math.Max(-_pageContainer.AutoScrollPosition.X, 0);
      int currentY = Math.Max(-_pageContainer.AutoScrollPosition.Y, 0);

      int contentWidth = Math.Max(_pageView.Width + margin * 2, _pageContainer.ClientSize.Width);
      int contentHeight = Math.Max(_pageView.Height + margin * 2, _pageContainer.ClientSize.Height);

      var minSize = new Size(contentWidth, contentHeight);
      if (_pageContainer.AutoScrollMinSize != minSize)
        _pageContainer.AutoScrollMinSize = minSize;

      int x = (_pageView.Width + margin * 2 < _pageContainer.ClientSize.Width)
          ? Math.Max((_pageContainer.ClientSize.Width - _pageView.Width) / 2, margin)
          : margin;
      _pageView.Location = new Point(x, margin);

      if (resetScroll) {
        _pageContainer.AutoScrollPosition = new Point(0, 0);
        if (IsHandleCreated) {
          BeginInvoke((Action)(() => {
            if (!IsDisposed && _pageContainer != null && _pageView != null)
              UpdatePageViewport(resetScroll: false);
          }));
        }
        return;
      }

      int maxScrollX = Math.Max(contentWidth - _pageContainer.ClientSize.Width, 0);
      int maxScrollY = Math.Max(contentHeight - _pageContainer.ClientSize.Height, 0);

      int scrollX = Math.Max(0, Math.Min(currentX, maxScrollX));
      int scrollY = Math.Max(0, Math.Min(currentY, maxScrollY));

      _pageContainer.AutoScrollPosition = new Point(scrollX, scrollY);
    }

    private void MovePage(int delta) {
      int next = _currentPage + delta;
      if (next < 0 || next >= _pdfService.TotalPages) return;
      _pageView.CommitEdit();
      _currentPage = next;
      UpdatePageLabel();
      RenderCurrentPage(resetScroll: true);
      UpdateThumbnailSelection();
      UpdatePageManageButtons();
    }

    private void UpdatePageLabel() {
      int total = _pdfService.TotalPages;
      _lblPage.Text = $"{_currentPage + 1} / {total}";
      _btnPrev.Enabled = _currentPage > 0;
      _btnNext.Enabled = _currentPage < total - 1;
    }

    private void UpdateModifiedLabel() {
      int nText = _pdfService.AllBlocks.SelectMany(p => p).Count(b => b.IsModified);
      int nAnn = _pdfService.AllAnnotations.SelectMany(p => p).Count();
      var parts = new System.Collections.Generic.List<string>();
      if (nText > 0) parts.Add($"텍스트 {nText}개 수정");
      if (nAnn > 0) parts.Add($"주석 {nAnn}개");
      _lblModified.Text = parts.Count > 0 ? "●  " + string.Join(" / ", parts) : "";
    }

    // ══════════════════════════════════════════════════════════════
    //  확대 / 축소
    // ══════════════════════════════════════════════════════════════
    private void OnZoomChanged(object s, EventArgs e) {
      if (_cmbZoom.SelectedItem is string z) SetZoomText(z);
    }
    private void SetZoomText(string z) {
      int pctInt;
      if (!int.TryParse(z.TrimEnd('%'), out pctInt))
        return;

      pctInt = ZoomPercents.OrderBy(v => Math.Abs(v - pctInt)).First();
      string normalized = $"{pctInt}%";

      if (_cmbZoom.SelectedItem is not string current || current != normalized)
        _cmbZoom.SelectedItem = normalized;

      if (float.TryParse(normalized.TrimEnd('%'), out float pct)) {
        var scrollRatio = CapturePageScrollRatio();
        _zoom = pct / 100f;
        if (_pdfService.TotalPages > 0) {
          RenderCurrentPage(resetScroll: true);
          RestorePageScrollFromRatio(scrollRatio.xRatio, scrollRatio.yRatio);
        }
      }
    }

    private (float xRatio, float yRatio) CapturePageScrollRatio() {
      if (_pageContainer == null)
        return (0f, 0f);

      int maxX = Math.Max(_pageContainer.AutoScrollMinSize.Width - _pageContainer.ClientSize.Width, 0);
      int maxY = Math.Max(_pageContainer.AutoScrollMinSize.Height - _pageContainer.ClientSize.Height, 0);
      int currentX = Math.Max(-_pageContainer.AutoScrollPosition.X, 0);
      int currentY = Math.Max(-_pageContainer.AutoScrollPosition.Y, 0);

      float xRatio = maxX > 0 ? (float)currentX / maxX : 0f;
      float yRatio = maxY > 0 ? (float)currentY / maxY : 0f;
      return (xRatio, yRatio);
    }

    private void RestorePageScrollFromRatio(float xRatio, float yRatio) {
      if (_pageContainer == null)
        return;

      int maxX = Math.Max(_pageContainer.AutoScrollMinSize.Width - _pageContainer.ClientSize.Width, 0);
      int maxY = Math.Max(_pageContainer.AutoScrollMinSize.Height - _pageContainer.ClientSize.Height, 0);

      int targetX = maxX > 0 ? (int)Math.Round(Math.Max(0f, Math.Min(1f, xRatio)) * maxX) : 0;
      int targetY = maxY > 0 ? (int)Math.Round(Math.Max(0f, Math.Min(1f, yRatio)) * maxY) : 0;

      _pageContainer.AutoScrollPosition = new Point(targetX, targetY);
    }

    private void AdjustZoom(int delta) {
      int current = (int)Math.Round(_zoom * 100f);
      int idx = Array.FindIndex(ZoomPercents, z => z >= current);
      if (idx < 0) idx = ZoomPercents.Length - 1;

      if (delta > 0) {
        while (idx < ZoomPercents.Length - 1 && ZoomPercents[idx] <= current)
          idx++;
      } else if (delta < 0) {
        while (idx > 0 && ZoomPercents[idx] >= current)
          idx--;
      }

      SetZoomText($"{ZoomPercents[idx]}%");
    }
    private void OnMouseWheelZoom(object s, MouseEventArgs e) {
      if (ModifierKeys.HasFlag(Keys.Control)) {
        AdjustZoom(e.Delta > 0 ? +1 : -1);

        if (e is HandledMouseEventArgs h1) h1.Handled = true;
        return;
      }

      // 일부 마우스/트랙패드는 좌우 휠을 Shift+세로휠이 아니라
      // 별도의 수평 휠 메시지(WM_MOUSEHWHEEL)로 보내지 못하기도 하므로,
      // 여기서는 Shift+휠만 보조적으로 처리한다.
      if (ModifierKeys.HasFlag(Keys.Shift)) {
        ScrollPageHorizontally(-e.Delta);
        if (e is HandledMouseEventArgs h2) h2.Handled = true;
      }
    }

    private bool HandleHorizontalWheelMessage(int delta) {
      if (_pageContainer == null || !_pageContainer.Visible)
        return false;

      if (!IsMouseOverScrollablePageArea())
        return false;

      ScrollPageHorizontally(delta);
      return true;
    }

    private bool IsMouseOverScrollablePageArea() {
      if (_pageContainer == null)
        return false;

      var mousePos = Control.MousePosition;
      if (_pageContainer.RectangleToScreen(_pageContainer.ClientRectangle).Contains(mousePos))
        return true;

      return _pageView != null && _pageView.Visible && _pageView.RectangleToScreen(_pageView.ClientRectangle).Contains(mousePos);
    }

    private void ScrollPageHorizontally(int wheelDelta) {
      if (_pageContainer == null)
        return;

      int maxX = Math.Max(_pageContainer.AutoScrollMinSize.Width - _pageContainer.ClientSize.Width, 0);
      if (maxX <= 0)
        return;

      int currentX = Math.Max(-_pageContainer.AutoScrollPosition.X, 0);
      int currentY = Math.Max(-_pageContainer.AutoScrollPosition.Y, 0);
      int step = Math.Max(_pageContainer.ClientSize.Width / 8, 48);

      int nextX = wheelDelta > 0 ? currentX - step : currentX + step;
      nextX = Math.Max(0, Math.Min(maxX, nextX));

      _pageContainer.AutoScrollPosition = new Point(nextX, currentY);
    }

    private sealed class HorizontalWheelMessageFilter : IMessageFilter {
      private const int WM_MOUSEHWHEEL = 0x020E;
      private readonly Func<int, bool> _handler;

      public HorizontalWheelMessageFilter(Func<int, bool> handler) {
        _handler = handler;
      }

      public bool PreFilterMessage(ref Message m) {
        if (m.Msg != WM_MOUSEHWHEEL)
          return false;

        int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
        return delta != 0 && _handler(delta);
      }
    }

    // ══════════════════════════════════════════════════════════════
    //  찾기 / 바꾸기
    // ══════════════════════════════════════════════════════════════
    private void ToggleFindPanel(object s, EventArgs e) {
      if (!CanEditFeatures()) {
        SetStatus("보기 모드에서는 찾기/바꾸기를 사용할 수 없습니다. [편집 모드로] 버튼을 눌러 전환하세요.");
        return;
      }
      _findPanel.Visible = !_findPanel.Visible;
      if (_findPanel.Visible) {
        AdjustFindPanelLayout();
        _txtFind.Focus();
      }
    }
    private void OnFindKeyDown(object s, KeyEventArgs e) {
      if (e.KeyCode == Keys.Return) { OnFindAll(s, e); e.Handled = true; }
      if (e.KeyCode == Keys.Escape) { _findPanel.Visible = false; e.Handled = true; }
    }
    private void OnFindAll(object s, EventArgs e) {
      string needle = _txtFind.Text;
      if (string.IsNullOrEmpty(needle)) { _lblFindResult.Text = "검색어를 입력하세요."; return; }
      var cmp = _chkMatchCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
      //int count = _pdfService.AllBlocks.SelectMany(p => p).Count(b => b.EditedText.Contains(needle, cmp));
      int count = _pdfService.AllBlocks.SelectMany(p => p).Count(b => (b.EditedText ?? string.Empty).IndexOf(needle, cmp) >= 0);
      _lblFindResult.Text = count > 0 ? $"  {count}개 발견" : "  결과 없음";
      AdjustFindPanelLayout();
    }
    private void OnReplaceAll(object s, EventArgs e) {
      if (!CanEditFeatures()) return;
      CommitPendingEdits();
      string needle = _txtFind.Text;
      string replace = _txtReplace.Text;
      if (string.IsNullOrEmpty(needle)) { _lblFindResult.Text = "검색어를 입력하세요."; return; }
      var cmp = _chkMatchCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
      int blockCount = 0, replaceCount = 0;
      foreach (var block in _pdfService.AllBlocks.SelectMany(p => p)) {
        //if (!block.EditedText.Contains(needle, cmp)) continue;
        if ((block.EditedText ?? string.Empty).IndexOf(needle, cmp) < 0) continue;
        string before = block.EditedText;
        int replaced;
        string after = _chkMatchCase.Checked
            ? ReplaceSensitive(before, needle, replace, out replaced)
            : ReplaceInsensitive(before, needle, replace, out replaced);
        if (replaced <= 0) continue;
        _history.Execute(new TextEditCommand(block, before, after));
        blockCount++; replaceCount += replaced;
      }
      _lblFindResult.Text = $"  블록 {blockCount}개 / 치환 {replaceCount}건";
      AdjustFindPanelLayout();
      RenderCurrentPage();
      UpdateModifiedLabel();
    }

    private static string ReplaceSensitive(string src, string old, string @new, out int n) {
      n = 0; if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(old)) return src;
      int i = 0; while ((i = src.IndexOf(old, i, StringComparison.Ordinal)) >= 0) { n++; i += Math.Max(@new.Length, 1); }
      return n > 0 ? src.Replace(old, @new) : src;
    }
    private static string ReplaceInsensitive(string src, string old, string @new, out int n) {
      n = 0; if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(old)) return src;
      var sb = new System.Text.StringBuilder(src.Length); int pos = 0;
      while (true) {
        int idx = src.IndexOf(old, pos, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) { sb.Append(src, pos, src.Length - pos); break; }
        sb.Append(src, pos, idx - pos); sb.Append(@new); pos = idx + old.Length; n++;
      }
      return n > 0 ? sb.ToString() : src;
    }

    // ══════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════
    private void SetFileOpen(bool open) {
      // 내비게이션은 파일 열림 여부만으로 제어
      _btnPrev.Enabled = open; _btnNext.Enabled = open;
      if (_btnToggleEdit != null) _btnToggleEdit.Enabled = open;

      // 저장/편집 버튼은 편집 모드일 때만 활성화 (ApplyViewEditMode 에서 관리)
      // 단, 파일이 닫히면 모두 비활성화
      if (!open) {
        _btnSave.Enabled = false; _btnSaveAs.Enabled = false;
        _btnRevert.Enabled = false; _miSave.Enabled = false;
        _miSaveAs.Enabled = false; _miRevert.Enabled = false;
        _miFindReplace.Enabled = false;
        if (_btnUndo != null) _btnUndo.Enabled = false;
        if (_btnRedo != null) _btnRedo.Enabled = false;
        if (_miUndo != null) _miUndo.Enabled = false;
        if (_miRedo != null) _miRedo.Enabled = false;
        if (_btnModeEdit != null) _btnModeEdit.Enabled = false;
        if (_btnModeSelect != null) _btnModeSelect.Enabled = false;
        if (_btnModeAnnot != null) _btnModeAnnot.Enabled = false;
        if (_btnPageMoveUp != null) _btnPageMoveUp.Enabled = false;
        if (_btnPageMoveDown != null) _btnPageMoveDown.Enabled = false;
        if (_btnPageDelete != null) _btnPageDelete.Enabled = false;
        if (_btnPageInsert != null) _btnPageInsert.Enabled = false;
        if (_miPage != null) _miPage.Enabled = false;
      }
    }

    private void CommitPendingEdits() => _pageView?.CommitEdit();
    private bool CanEditFeatures() => _isEditMode && _pdfService.TotalPages > 0;
    private bool HasUnsavedChanges() =>
          _pdfService.AllBlocks.Any(p => p.Any(b => b.IsModified)) ||
          _pdfService.AllAnnotations.Any(p => p.Any(a => a.IsNew));

    private void SetStatus(string msg) => _lblStatus.Text = msg;

    private DialogResult Msg(string text, bool err = false, bool ask = false) {
      if (ask) return MessageBox.Show(text, "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
      MessageBox.Show(text, err ? "오류" : "알림", MessageBoxButtons.OK,
          err ? MessageBoxIcon.Error : MessageBoxIcon.Information);
      return DialogResult.OK;
    }

    private void OnAbout(object s, EventArgs e) {
      using (Form aboutForm = new Form()) {
        aboutForm.Text = "프로그램 정보";
        aboutForm.Size = new Size(560, 340);
        aboutForm.StartPosition = FormStartPosition.CenterParent;
        aboutForm.FormBorderStyle = FormBorderStyle.FixedDialog;
        aboutForm.MaximizeBox = false;
        aboutForm.MinimizeBox = false;

        // 1. 로고 이미지 (리소스에서 가져오기)
        PictureBox logo = new PictureBox {
          Image = Resources.EDSCorp_Logo, // 리소스에 등록된 이름
          SizeMode = PictureBoxSizeMode.Zoom,
          Location = new Point(20, 20),
          Size = new Size(168, 47) /* 672 X 185 */
        };

        // 2. 텍스트 정보
        var asm = Assembly.GetExecutingAssembly();
        string infoText = $"경량 PDF 편집 및 주석 도구\n" +
                          $"버전: {asm.GetName().Version}\n\n" +
                          $"{asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright}";

        Label infoLabel = new Label {
          Text = infoText,
          Location = new Point(20, 100),
          Size = new Size(500, 120),
          TextAlign = ContentAlignment.MiddleLeft
        };

        // 3. 확인 버튼
        System.Windows.Forms.Button okBtn = new System.Windows.Forms.Button {
          Text = "확인",
          DialogResult = DialogResult.OK,
          Location = new Point(380, 220),
          Size = new Size(150, 40)
        };

        aboutForm.Controls.Add(logo);
        aboutForm.Controls.Add(infoLabel);
        aboutForm.Controls.Add(okBtn);
        aboutForm.ShowDialog();
      }
    }

    private void OnHelp(object s, EventArgs e) {
      MessageBox.Show(
          "■ QPDF 편집기 사용 방법\n\n" +
          "【텍스트 편집 모드】 (기본)\n" +
          "  - 파란 영역 클릭 → 인라인 편집 → Enter 확정 / Esc 취소\n\n" +
          "【영역 선택 삭제 모드】\n" +
          "  - 마우스 드래그로 영역 선택 → 범위 내 텍스트 모두 삭제\n\n" +
          "【주석 추가 모드】\n" +
          "  - 원하는 위치 클릭 또는 드래그 → 주석 입력 창 → 확인\n" +
          "  - 주석 클릭: 편집, 우클릭: 컨텍스트 메뉴, [×]: 삭제\n\n" +
          "■ 단축키\n" +
          "  Ctrl+Z: 실행 취소    Ctrl+Y: 다시 실행\n" +
          "  Ctrl+O: 열기         Ctrl+S: 저장\n" +
          "  Ctrl+H: 찾기/바꾸기  Ctrl+±: 확대/축소\n" +
          "  Ctrl+Shift+↑: 현재 페이지 위로 이동\n" +
          "  Ctrl+Shift+↓: 현재 페이지 아래로 이동\n\n" +
          "■ 페이지 관리 (편집 모드)\n" +
          "  썸네일 우클릭 또는 상단 [페이지] 메뉴\n" +
          "  - 페이지 위/아래 이동, 삭제, 다른 PDF 병합",
          "사용 방법", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnGlobalKeyDown(object s, KeyEventArgs e) {
      if (!e.Control && IsTextInputFocused()) {
        switch (e.KeyCode) {
          case Keys.Left:
          case Keys.Right:
          case Keys.PageUp:
          case Keys.PageDown:
          case Keys.Home:
          case Keys.End:
            return;
        }
      }

      if (e.Control) {
        switch (e.KeyCode) {
          case Keys.O:
            OnOpen(s, e); e.Handled = true; e.SuppressKeyPress = true; break;

          case Keys.S when !e.Shift:
            if (CanEditFeatures()) { OnSave(s, e); e.Handled = true; e.SuppressKeyPress = true; }
            break;

          case Keys.S when e.Shift:
            if (CanEditFeatures()) { OnSaveAs(s, e); e.Handled = true; e.SuppressKeyPress = true; }
            break;

          case Keys.H:
            if (CanEditFeatures()) { ToggleFindPanel(s, e); e.Handled = true; e.SuppressKeyPress = true; }
            break;

          case Keys.Z:
            if (CanEditFeatures()) { OnUndo(s, e); e.Handled = true; e.SuppressKeyPress = true; }
            break;

          case Keys.Y:
            if (CanEditFeatures()) { OnRedo(s, e); e.Handled = true; e.SuppressKeyPress = true; }
            break;

          case Keys.Up when e.Shift:
            if (CanPageManage()) { OnPageMoveUp(s, e); e.Handled = true; e.SuppressKeyPress = true; }
            break;

          case Keys.Down when e.Shift:
            if (CanPageManage()) { OnPageMoveDown(s, e); e.Handled = true; e.SuppressKeyPress = true; }
            break;

          case Keys.OemMinus:
          case Keys.Subtract:
            AdjustZoom(-1); e.Handled = true; e.SuppressKeyPress = true; break;

          case Keys.Oemplus:
          case Keys.Add:
            AdjustZoom(+1); e.Handled = true; e.SuppressKeyPress = true; break;

          case Keys.D0:
          case Keys.NumPad0:
            SetZoomText("100%"); e.Handled = true; e.SuppressKeyPress = true; break;
        }
      } else {
        switch (e.KeyCode) {
          case Keys.Left:
          case Keys.PageUp:
            MovePage(-1); e.Handled = true; break;

          case Keys.Right:
          case Keys.PageDown:
            MovePage(+1); e.Handled = true; break;
        }
      }
    }

    private bool IsTextInputFocused() {
      Control? focused = GetDeepActiveControl(this);
      if (focused == null)
        return false;

      if (focused is TextBoxBase)
        return true;

      if (focused is ComboBox combo && combo.DroppedDown)
        return true;

      return false;
    }

    private static Control? GetDeepActiveControl(Control root) {
      Control? current = root;

      while (current is ContainerControl container && container.ActiveControl != null) {
        current = container.ActiveControl;
      }

      return current;
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
      CommitPendingEdits();
      if (HasUnsavedChanges() &&
          Msg("저장하지 않은 변경 사항이 있습니다.\n종료하시겠습니까?", ask: true) != DialogResult.Yes)
        e.Cancel = true;
      base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        if (_horizontalWheelFilter != null) {
          Application.RemoveMessageFilter(_horizontalWheelFilter);
          _horizontalWheelFilter = null;
        }
        _pdfService.Dispose();
        components?.Dispose();
        foreach (var b in _thumbCache.Values) b.Dispose();
        _thumbCache.Clear();
      }
      base.Dispose(disposing);
    }
  }

  // ── 다크 테마 렌더러 ──────────────────────────────────────────────
  internal sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer {
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) {
      using var br = new SolidBrush(SystemColors.MenuBar);
      e.Graphics.FillRectangle(br, e.AffectedBounds);
    }
    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) {
      bool isChecked = e.Item is ToolStripButton { Checked: true };
      if (!e.Item.Selected && !e.Item.Pressed && !isChecked) return;

      Color bg = isChecked
                ? Color.FromArgb(46, 118, 214)
                : (e.Item.Pressed ? Color.FromArgb(60, 65, 90) : Color.FromArgb(50, 52, 72));

      using var br = new SolidBrush(bg);
      var rect = new Rectangle(1, 1, e.Item.Width - 2, e.Item.Height - 2);
      e.Graphics.FillRectangle(br, rect);

      if (isChecked) {
        using var pen = new Pen(Color.FromArgb(24, 78, 151));
        e.Graphics.DrawRectangle(pen, rect);
      }
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) {
      int cx = e.Item.Width / 2;
      using var pen = new Pen(SystemColors.ControlText);
      e.Graphics.DrawLine(pen, cx, 4, cx, e.Item.Height - 4);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
      if (e.Item is ToolStripButton { Checked: true })
        e.TextColor = Color.White;
      base.OnRenderItemText(e);
    }

  }
}
