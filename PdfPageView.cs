using iText.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ScrollBar;

namespace QPDFEditor {
  // ── 편집 모드 ─────────────────────────────────────────────────────
  public enum EditMode {
    TextEdit,       // 클릭하여 텍스트 편집 (기본)
    SelectDelete,   // 드래그하여 영역 내 블록 삭제
    AddAnnotation,  // 클릭하여 주석 추가
  }

  public sealed class PdfPageView : Panel {
    // ── 상태 ───────────────────────────────────────────────────────
    private Bitmap? _pageImage;
    private List<TextBlock> _blocks = new();
    private List<PdfAnnotation> _annotations = new();
    private float _pdfW, _pdfH;
    private TextBlock? _activeBlock;
    private TextBox? _editor;
    private TextBlock? _hoveredBlock;
    private PdfAnnotation? _hoveredAnnotation;
    private EditHistory? _history;

    // ── 드래그 선택 ────────────────────────────────────────────────
    private bool _dragging;
    private Point _dragStart;
    private Point _dragCurrent;
    private bool _annotationPlacing;
    private Point _annotationStart;
    private Point _annotationCurrent;

    private bool _panning;
    private Point _panStartScreen;
    private Point _panStartScroll;
    private Point _editHostScroll;
    private bool _editHostScrollCaptured;
    private const int DefaultAnnotationWidth = 150;
    private const int DefaultAnnotationHeight = 80;
    private const int AnnotationClickThreshold = 12;

    // ── 모드 ───────────────────────────────────────────────────────
    private EditMode _mode = EditMode.TextEdit;
    public EditMode Mode {
      get => _mode;
      set {
        if (_mode == value) return;
        CommitEdit(save: true);
        _mode = value;
        Cursor = value switch {
          EditMode.SelectDelete => Cursors.Cross,
          EditMode.AddAnnotation => Cursors.Hand,
          _ => Cursors.Default,
        };
        Invalidate();
      }
    }

    // ── 보기 전용 모드 ────────────────────────────────────────────
    private bool _isViewOnly = false;
    public bool IsViewOnly {
      get => _isViewOnly;
      set {
        _isViewOnly = value;
        if (value) {
          CommitEdit(save: true);
          _dragging = false;
          _annotationPlacing = false;
          _panning = false;
          _hoveredBlock = null;
          _hoveredAnnotation = null;
        }
        Cursor = value ? Cursors.Hand : (_mode switch {
          EditMode.SelectDelete => Cursors.Cross,
          EditMode.AddAnnotation => Cursors.Hand,
          _ => Cursors.Default,
        });
        Invalidate();
      }
    }

    // ── 이벤트 ────────────────────────────────────────────────────
    public event EventHandler<TextBlock>? TextBlockCommitted;
    public event EventHandler? ContentChanged;
    public event EventHandler<PdfAnnotation>? AnnotationAdded;
    public event EventHandler<PdfAnnotation>? AnnotationDeleted;
    public event EventHandler<PdfAnnotation>? AnnotationEdited;

    // ── 공개 속성 ─────────────────────────────────────────────────
    public EditHistory? History { get => _history; set => _history = value; }

    // ── 생성자 ────────────────────────────────────────────────────
    public PdfPageView() {
      DoubleBuffered = true;
      BackColor = Color.White;
      Cursor = Cursors.Default;
      TabStop = true;
    }

    // ── Public API ────────────────────────────────────────────────

    public void SetPage(Bitmap image, List<TextBlock> blocks,
                        List<PdfAnnotation> annotations,
                        float pdfW, float pdfH) {
      CommitEdit(save: true);
      _pageImage?.Dispose();
      _pageImage = image;
      _blocks = blocks;
      _annotations = annotations;
      _pdfW = pdfW;
      _pdfH = pdfH;
      Size = image.Size;
      RecalcScreenBounds();
      Invalidate();
    }

    public void CommitEdit(bool save = true) {
      if (_editor == null || _activeBlock == null) return;

      var editor = _editor;
      var block = _activeBlock;
      var host = GetScrollHost();
      Point restoreScroll = _editHostScrollCaptured
                ? _editHostScroll
                : (host != null ? GetScrollOffset(host) : Point.Empty);
      _editor = null;
      _activeBlock = null;
      editor.LostFocus -= EditorLostFocus;

      if (save && editor.Text != block.EditedText) {
        var cmd = new TextEditCommand(block, block.EditedText, editor.Text);
        _history?.Execute(cmd);

        TextBlockCommitted?.Invoke(this, block);
        ContentChanged?.Invoke(this, EventArgs.Empty);
      }

      Controls.Remove(editor);
      editor.Dispose();

      if (host != null)
        RestoreHostScrollAsync(restoreScroll);
      _editHostScrollCaptured = false;

      Invalidate(Inflate(block.ScreenBounds, 6));
    }

    // ── 좌표 변환 ─────────────────────────────────────────────────

    private void RecalcScreenBounds() {
      if (_pageImage == null || _pdfW <= 0) return;
      float sx = _pageImage.Width / _pdfW;
      float sy = _pageImage.Height / _pdfH;

      foreach (var b in _blocks) {
        int x = (int)(b.PdfBounds.X * sx);
        int w = (int)Math.Max(b.PdfBounds.Width * sx, 4);
        int h = (int)Math.Max(b.PdfBounds.Height * sy, 4);
        int y = (int)((_pdfH - b.PdfBounds.Y - b.PdfBounds.Height) * sy);
        b.ScreenBounds = new Rectangle(x, y, w, h);
      }

      foreach (var a in _annotations)
        a.ScreenBounds = PdfToScreen(a.PdfBounds, sx, sy);
    }

    private Rectangle PdfToScreen(RectangleF r, float sx, float sy) =>
        new((int)(r.X * sx),
            (int)((_pdfH - r.Y - r.Height) * sy),
            (int)Math.Max(r.Width * sx, 16),
            (int)Math.Max(r.Height * sy, 16));

    private RectangleF ScreenToPdf(Rectangle r) {
      if (_pageImage == null) return RectangleF.Empty;
      float sx = _pdfW / _pageImage.Width;
      float sy = _pdfH / _pageImage.Height;
      float px = r.X * sx;
      float ph = r.Height * sy;
      float py = _pdfH - r.Y * sy - ph;
      return new RectangleF(px, py, r.Width * sx, ph);
    }



    private Rectangle NormalizeRect(Point p1, Point p2) {
      int x = Math.Min(p1.X, p2.X);
      int y = Math.Min(p1.Y, p2.Y);
      int w = Math.Abs(p2.X - p1.X);
      int h = Math.Abs(p2.Y - p1.Y);
      return new Rectangle(x, y, w, h);
    }

    private Rectangle GetAnnotationDraftRect() => NormalizeRect(_annotationStart, _annotationCurrent);

    private Rectangle CreateDefaultAnnotationRect(Point screenPt) =>
        ClampToPage(new Rectangle(screenPt.X, screenPt.Y, DefaultAnnotationWidth, DefaultAnnotationHeight));

    private Rectangle ClampToPage(Rectangle r) {
      if (_pageImage == null) return r;
      var page = new Rectangle(0, 0, _pageImage.Width, _pageImage.Height);

      int minW = Math.Min(48, page.Width);
      int minH = Math.Min(28, page.Height);
      int w = Math.Max(r.Width, minW);
      int h = Math.Max(r.Height, minH);
      w = Math.Min(w, page.Width);
      h = Math.Min(h, page.Height);

      int x = r.X;
      int y = r.Y;
      if (x < page.Left) x = page.Left;
      if (y < page.Top) y = page.Top;
      if (x + w > page.Right) x = page.Right - w;
      if (y + h > page.Bottom) y = page.Bottom - h;
      return new Rectangle(x, y, w, h);
    }

    private void UpdateAnnotationScreenBounds(PdfAnnotation ann) {
      if (_pageImage == null || _pdfW <= 0 || _pdfH <= 0) return;
      float sx = _pageImage.Width / _pdfW;
      float sy = _pageImage.Height / _pdfH;
      ann.ScreenBounds = PdfToScreen(ann.PdfBounds, sx, sy);
    }

    private ScrollableControl? GetScrollHost() => Parent as ScrollableControl;

    private void BeginPan(Point screenPoint) {
      var host = GetScrollHost();
      if (host == null) return;
      _panning = true;
      _panStartScreen = screenPoint;
      _panStartScroll = new Point(-host.AutoScrollPosition.X, -host.AutoScrollPosition.Y);
      Cursor = Cursors.Hand;
    }

    private void UpdatePan(Point screenPoint) {
      if (!_panning) return;
      var host = GetScrollHost();
      if (host == null) return;

      int dx = screenPoint.X - _panStartScreen.X;
      int dy = screenPoint.Y - _panStartScreen.Y;

      int newX = Math.Max(_panStartScroll.X - dx, 0);
      int newY = Math.Max(_panStartScroll.Y - dy, 0);
      host.AutoScrollPosition = new Point(newX, newY);
    }

    private void EndPan() {
      _panning = false;
      Cursor = _isViewOnly ? Cursors.Hand : (_mode switch {
        EditMode.SelectDelete => Cursors.Cross,
        EditMode.AddAnnotation => Cursors.Hand,
        _ => Cursors.Default,
      });
    }
    private static Rectangle Inflate(Rectangle r, int d) =>
        new(r.X - d, r.Y - d, r.Width + d * 2, r.Height + d * 2);

    private static float FontHeightPx(Rectangle r) =>
        Math.Max(r.Height * 0.72f, 4f);

    private float GetBlockFontHeightPx(TextBlock block) {
      float rectBased = FontHeightPx(block.ScreenBounds);

      if (_pageImage == null || _pdfH <= 0f)
        return rectBased;

      float sy = _pageImage.Height / _pdfH;
      float fontPx = block.FontSize > 0f ? block.FontSize * sy : 0f;
      if (fontPx <= 0f)
        return rectBased;

      float maxReasonable = Math.Max(block.ScreenBounds.Height - 2f, 4f);
      return Math.Max(4f, Math.Min(fontPx, maxReasonable));
    }

    private static FontStyle InferFontStyle(string? fontName) {
      string n = (fontName ?? string.Empty).ToLowerInvariant();
      FontStyle style = FontStyle.Regular;
      if (n.Contains("bold") || n.Contains("black") || n.Contains("heavy") || n.Contains("demi"))
        style |= FontStyle.Bold;
      if (n.Contains("italic") || n.Contains("oblique"))
        style |= FontStyle.Italic;
      return style;
    }

    private static string NormalizePdfFontName(string? fontName) {
      string n = (fontName ?? string.Empty).Trim();
      int plus = n.IndexOf('+');
      if (plus >= 0 && plus + 1 < n.Length)
        n = n.Substring(plus + 1);
      int comma = n.IndexOf(',');
      if (comma >= 0)
        n = n.Substring(0, comma);
      return n.Trim();
    }

    private static FontFamily? ResolveInstalledFontFamily(string? pdfFontName) {
      string raw = NormalizePdfFontName(pdfFontName);
      if (string.IsNullOrWhiteSpace(raw))
        return null;

      string compact = raw.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);

      static string Compact(string value) =>
        value.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);

      static bool Match(FontFamily fam, string value) =>
        string.Equals(Compact(fam.Name), value, StringComparison.OrdinalIgnoreCase);

      foreach (var fam in FontFamily.Families)
        if (Match(fam, compact))
          return fam;

      string lowered = compact.ToLowerInvariant();
      string[] aliases = lowered switch {
        var s when s.Contains("malgun") || s.Contains("gothic") => new[] { "Malgun Gothic", "맑은 고딕", "Dotum", "돋움", "Arial", "Segoe UI" },
        var s when s.Contains("dotum") || s.Contains("돋움") => new[] { "Dotum", "돋움", "Malgun Gothic", "Arial" },
        var s when s.Contains("gulim") || s.Contains("굴림") => new[] { "Gulim", "굴림", "Malgun Gothic", "Arial" },
        var s when s.Contains("batang") || s.Contains("바탕") => new[] { "Batang", "바탕", "Malgun Gothic", "Times New Roman" },
        var s when s.Contains("courier") || s.Contains("mono") => new[] { "Consolas", "Courier New" },
        var s when s.Contains("times") || s.Contains("mincho") => new[] { "Times New Roman", "Batang", "Malgun Gothic" },
        var s when s.Contains("helvetica") || s.Contains("arial") || s.Contains("sans") => new[] { "Arial", "Segoe UI", "Malgun Gothic" },
        _ => new[] { "Malgun Gothic", "Arial", "Segoe UI" }
      };

      foreach (string alias in aliases) {
        var fam = FontFamily.Families.FirstOrDefault(f => string.Equals(f.Name, alias, StringComparison.OrdinalIgnoreCase));
        if (fam != null)
          return fam;
      }

      string[] tokens = raw.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (string token in tokens) {
        if (token.Length < 3)
          continue;

        var fam = FontFamily.Families.FirstOrDefault(f =>
        f.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        if (fam != null)
          return fam;
      }

      return null;
    }

    private static FontStyle EnsureSupportedFontStyle(FontFamily family, FontStyle style) {
      if (family.IsStyleAvailable(style))
        return style;

      if ((style & FontStyle.Bold) != 0 && family.IsStyleAvailable(FontStyle.Bold))
        return FontStyle.Bold;

      if ((style & FontStyle.Italic) != 0 && family.IsStyleAvailable(FontStyle.Italic))
        return FontStyle.Italic;

      return family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : style;
    }

    private Font CreateOverlayFont(TextBlock block, float sizePx) {
      FontFamily family = ResolveInstalledFontFamily(block.FontName)
                          ?? FontFamily.Families.FirstOrDefault(f => string.Equals(f.Name, "Malgun Gothic", StringComparison.OrdinalIgnoreCase))
                          ?? FontFamily.GenericSansSerif;

      FontStyle style = EnsureSupportedFontStyle(family, InferFontStyle(block.FontName));
      return new Font(family, sizePx, style, GraphicsUnit.Pixel);
    }

    // 인라인 편집기는 원본 PDF 글자가 매우 작더라도
    // 사용자가 내용을 읽고 수정할 수 있도록 최소 표시 크기를 보장한다.
    private const float MinInlineEditorFontPx = 14f;
    private const int MinInlineEditorWidth = 160;
    private const int MaxInlineEditorWidth = 520;

    private Font CreateEditingFont(TextBlock block, float overlaySizePx) {
      float editorSizePx = Math.Max(overlaySizePx, MinInlineEditorFontPx);
      return CreateOverlayFont(block, editorSizePx);
    }

    private Size MeasureInlineEditorSize(TextBlock block, Font font, int pad) {
      string sample = string.IsNullOrEmpty(block.EditedText)
          ? "W"
          : block.EditedText + "  ";

      var measured = TextRenderer.MeasureText(
          sample,
          font,
          new Size(4096, 4096),
          TextFormatFlags.NoPadding |
          TextFormatFlags.NoPrefix |
          TextFormatFlags.SingleLine);

      int width = Math.Max(
          Math.Max(block.ScreenBounds.Width + pad * 2, measured.Width + pad * 4),
          MinInlineEditorWidth);
      width = Math.Min(width, MaxInlineEditorWidth);

      int height = Math.Max(
          Math.Max(block.ScreenBounds.Height + pad * 2, measured.Height + pad * 4),
          30);

      return new Size(width, height);
    }

    private static Point GetScrollOffset(ScrollableControl host) =>
        new(Math.Max(-host.AutoScrollPosition.X, 0), Math.Max(-host.AutoScrollPosition.Y, 0));

    private void RestoreHostScrollAsync(Point scroll) {
      var host = GetScrollHost();
      if (host == null || !IsHandleCreated)
        return;

      void ApplyScroll() {
        if (host.IsDisposed)
          return;
        host.AutoScrollPosition = scroll;
      }

      BeginInvoke((Action)(() => {
        ApplyScroll();
        if (!host.IsDisposed && host.IsHandleCreated) {
          host.BeginInvoke((Action)(() => {
            if (!host.IsDisposed)
              ApplyScroll();
          }));
        }
      }));
    }


    // ── 페인팅 ────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics;
      g.InterpolationMode = InterpolationMode.HighQualityBicubic;
      g.PixelOffsetMode = PixelOffsetMode.HighQuality;
      g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

      if (_pageImage == null) { base.OnPaint(e); return; }
      g.DrawImage(_pageImage, 0, 0);

      // ── 텍스트 블록 오버레이 ───────────────────────────────────
      foreach (var b in _blocks) {
        if (b == _activeBlock) continue;
        var rect = b.ScreenBounds;
        bool hovered = b == _hoveredBlock;
        bool modified = b.IsModified;
        bool deleted = b.IsDeleted;

        if (!e.ClipRectangle.IntersectsWith(rect)) continue;

        if (deleted) {
          // 삭제된 블록: 빨간 취소선
          using var wb = new SolidBrush(Color.FromArgb(40, 255, 0, 0));
          g.FillRectangle(wb, rect);
          using var pen = new Pen(Color.FromArgb(180, 200, 30, 30), 1.5f);
          pen.DashStyle = DashStyle.Dot;
          g.DrawRectangle(pen, rect);
          // 취소선
          int cy = rect.Y + rect.Height / 2;
          using var spen = new Pen(Color.FromArgb(220, 200, 30, 30), 1.5f);
          g.DrawLine(spen, rect.X, cy, rect.Right, cy);
        } else if (modified) {
          using var wb = new SolidBrush(Color.White);
          g.FillRectangle(wb, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2));

          float fsPx = GetBlockFontHeightPx(b);
          using var font = CreateOverlayFont(b, fsPx);
          var sf = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.None, Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
          using var tb = new SolidBrush(Color.Black);
          g.DrawString(b.EditedText, font, tb,
              new RectangleF(rect.X, rect.Y, rect.Width + fsPx * 20, rect.Height), sf);

          using var pen = new Pen(hovered ? Color.FromArgb(220, 0, 185, 50) : Color.FromArgb(180, 0, 155, 40), hovered ? 2f : 1.3f);
          g.DrawRectangle(pen, rect);
        } else {
          using var br = new SolidBrush(hovered ? Color.FromArgb(60, 30, 120, 255) : Color.FromArgb(28, 30, 100, 220));
          g.FillRectangle(br, rect);
          using var pen = new Pen(Color.FromArgb(hovered ? 140 : 80, 20, 80, 200), hovered ? 1.5f : 0.8f);
          g.DrawRectangle(pen, rect);
        }
      }

      // ── 주석 렌더링 ───────────────────────────────────────────
      DrawAnnotations(g);

      // ── 드래그 선택 사각형 ────────────────────────────────────
      if (_dragging && _mode == EditMode.SelectDelete) {
        var sel = GetDragRect();
        using var fill = new SolidBrush(Color.FromArgb(50, 255, 80, 80));
        g.FillRectangle(fill, sel);
        using var border = new Pen(Color.FromArgb(200, 220, 40, 40), 1.5f) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(border, sel);
      }

      // ── 주석 배치 드래프트 ─────────────────────────────────────
      if (_annotationPlacing && _mode == EditMode.AddAnnotation) {
        var draft = ClampToPage(GetAnnotationDraftRect());
        using var fill = new SolidBrush(Color.FromArgb(70, 255, 230, 120));
        g.FillRectangle(fill, draft);
        using var border = new Pen(Color.FromArgb(220, 180, 120, 0), 1.5f) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(border, draft);
      }
    }

    private void DrawAnnotations(Graphics g) {
      foreach (var a in _annotations) {
        var r = a.ScreenBounds;
        if (!ClientRectangle.IntersectsWith(r)) continue;

        bool hovered = a == _hoveredAnnotation;

        // 주석 몸통
        using (var bg = new SolidBrush(a.Color))
          g.FillRectangle(bg, r);

        // 그림자
        using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
          g.FillRectangle(shadow, new Rectangle(r.X + 3, r.Y + 3, r.Width, r.Height));
        using (var bg2 = new SolidBrush(a.Color))
          g.FillRectangle(bg2, r);

        // 테두리
        using var border = new Pen(hovered ? Color.FromArgb(200, 180, 120, 0) : Color.FromArgb(150, 160, 110, 0), hovered ? 2f : 1f);
        g.DrawRectangle(border, r);

        // 주석 아이콘(접힌 모서리)
        var corner = new Point[] {
                    new(r.Right - 10, r.Top),
                    new(r.Right,      r.Top + 10),
                    new(r.Right - 10, r.Top + 10),
                };
        using var foldBr = new SolidBrush(Color.FromArgb(180, 200, 160, 0));
        g.FillPolygon(foldBr, corner);

        // 텍스트
        var sf = new StringFormat {
          Trimming = StringTrimming.EllipsisCharacter,
          FormatFlags = StringFormatFlags.LineLimit,
          Alignment = StringAlignment.Near,
          LineAlignment = StringAlignment.Near,
        };
        using var annFont = new Font("Malgun Gothic", Math.Max(r.Height * 0.18f, 7f), FontStyle.Regular, GraphicsUnit.Pixel);
        var textRect = new RectangleF(r.X + 3, r.Y + 3, r.Width - 14, r.Height - 6);
        using var annBrush = new SolidBrush(Color.FromArgb(40, 30, 0));
        g.DrawString(a.Text, annFont, annBrush, textRect, sf);

        // 호버 시 삭제 [×] 버튼
        if (hovered) {
          var del = GetAnnotDeleteRect(r);
          using var delBg = new SolidBrush(Color.FromArgb(200, 220, 60, 60));
          g.FillEllipse(delBg, del);
          using var delF = new Font("Arial", 7f, FontStyle.Bold, GraphicsUnit.Point);
          using var delTxt = new SolidBrush(Color.White);
          var delSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
          g.DrawString("×", delF, delTxt, del, delSf);
        }
      }
    }

    private static Rectangle GetAnnotDeleteRect(Rectangle annRect) =>
        new(annRect.Right - 10, annRect.Top - 6, 14, 14);

    // ── 마우스 ────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e) {
      base.OnMouseMove(e);
      if (_isViewOnly) {
        if (_panning)
          UpdatePan(e.Location);
        else
          Cursor = Cursors.Hand;
        return;
      }

      if (_dragging && _mode == EditMode.SelectDelete) {
        _dragCurrent = e.Location;
        Invalidate();
        return;
      }

      if (_annotationPlacing && _mode == EditMode.AddAnnotation) {
        _annotationCurrent = e.Location;
        Invalidate();
        return;
      }

      // 주석 호버
      var annHit = HitTestAnnotation(e.Location);
      if (annHit != _hoveredAnnotation) {
        _hoveredAnnotation = annHit;
        Invalidate();
      }

      if (_mode == EditMode.TextEdit) {
        var hit = HitTest(e.Location);
        if (hit != _hoveredBlock) {
          _hoveredBlock = hit;
          Cursor = (hit != null) ? Cursors.IBeam : Cursors.Default;
          Invalidate();
        }
      }
    }

    protected override void OnMouseLeave(EventArgs e) {
      base.OnMouseLeave(e);
      if (_hoveredBlock != null) { _hoveredBlock = null; Invalidate(); }
      if (_hoveredAnnotation != null) { _hoveredAnnotation = null; Invalidate(); }
      if (_isViewOnly) {
        Cursor = Cursors.Hand;
        return;
      }
      Cursor = _mode switch {
        EditMode.SelectDelete => Cursors.Cross,
        EditMode.AddAnnotation => Cursors.Hand,
        _ => Cursors.Default,
      };
    }

    protected override void OnMouseDown(MouseEventArgs e) {
      base.OnMouseDown(e);
      if (_isViewOnly) {
        if (e.Button == MouseButtons.Left)
          BeginPan(e.Location);
        return;
      }

      if (e.Button != MouseButtons.Left) return;

      if (_mode == EditMode.SelectDelete) {
        CommitEdit(save: true);
        _dragging = true;
        _dragStart = e.Location;
        _dragCurrent = e.Location;
        return;
      }

      if (_mode == EditMode.AddAnnotation) {
        CommitEdit(save: true);
        var annHit = HitTestAnnotation(e.Location);
        if (annHit == null) {
          _annotationPlacing = true;
          _annotationStart = e.Location;
          _annotationCurrent = e.Location;
        }
        return;
      }
    }

    protected override void OnMouseUp(MouseEventArgs e) {
      base.OnMouseUp(e);
      if (_isViewOnly) {
        if (e.Button == MouseButtons.Left && _panning)
          EndPan();
        return;
      }

      if (_dragging && _mode == EditMode.SelectDelete && e.Button == MouseButtons.Left) {
        _dragging = false;
        ApplyDragDelete();
        return;
      }

      if (_annotationPlacing && _mode == EditMode.AddAnnotation && e.Button == MouseButtons.Left) {
        _annotationPlacing = false;
        _annotationCurrent = e.Location;

        var rawRect = GetAnnotationDraftRect();
        Rectangle rect;

        // 손 모양 주석 추가 모드에서 "클릭"에 가까운 입력은
        // 우클릭 "여기에 주석 추가"와 동일한 기본 크기(150x80)로 생성한다.
        // 아주 미세한 마우스 흔들림으로 48x28 같은 작은 주석이 생기는 것을 방지한다.
        if (rawRect.Width <= AnnotationClickThreshold || rawRect.Height <= AnnotationClickThreshold)
          rect = CreateDefaultAnnotationRect(e.Location);
        else
          rect = ClampToPage(rawRect);

        AddAnnotationAt(rect);
        return;
      }
    }

    protected override void OnMouseClick(MouseEventArgs e) {
      base.OnMouseClick(e);
      if (_isViewOnly) return;

      // 주석 삭제 버튼
      if (e.Button == MouseButtons.Left && _hoveredAnnotation != null) {
        var del = GetAnnotDeleteRect(_hoveredAnnotation.ScreenBounds);
        if (del.Contains(e.Location)) {
          DeleteAnnotation(_hoveredAnnotation);
          return;
        }
        // 주석 클릭 → 편집
        EditAnnotation(_hoveredAnnotation);
        return;
      }

      // 우클릭 → 주석 컨텍스트 메뉴 / 빈 곳 주석 추가
      if (e.Button == MouseButtons.Right) {
        var annHit = HitTestAnnotation(e.Location);
        if (annHit != null) {
          ShowAnnotationContextMenu(annHit, e.Location);
          return;
        }

        ShowEmptyContextMenu(e.Location);
        return;
      }

      if (_mode == EditMode.AddAnnotation && e.Button == MouseButtons.Left) {
        var annHit = HitTestAnnotation(e.Location);
        if (annHit != null) {
          EditAnnotation(annHit);
          return;
        }
      }

      if (_mode != EditMode.TextEdit) return;

      var hit = HitTest(e.Location);
      if (e.Button == MouseButtons.Left) {
        if (hit != null && hit != _activeBlock) BeginEdit(hit);
        else if (hit == null) CommitEdit(save: true);
      }
    }

    // ── 드래그 선택 삭제 ──────────────────────────────────────────

    private Rectangle GetDragRect() {
      int x = Math.Min(_dragStart.X, _dragCurrent.X);
      int y = Math.Min(_dragStart.Y, _dragCurrent.Y);
      int w = Math.Abs(_dragCurrent.X - _dragStart.X);
      int h = Math.Abs(_dragCurrent.Y - _dragStart.Y);
      return new Rectangle(x, y, w, h);
    }

    private void ApplyDragDelete() {
      var sel = GetDragRect();
      if (sel.Width < 3 && sel.Height < 3) { Invalidate(); return; }

      var targets = _blocks
          .Where(b => !b.IsDeleted && sel.IntersectsWith(b.ScreenBounds))
          .ToList();

      if (targets.Count > 0) {
        var cmd = new BatchDeleteCommand(targets);
        _history?.Execute(cmd);
        ContentChanged?.Invoke(this, EventArgs.Empty);
      }

      Invalidate();
    }

    // ── 주석 ──────────────────────────────────────────────────────

    private void AddAnnotationAt(Rectangle screenRect) {
      var rect = ClampToPage(screenRect);
      var pdfRect = ScreenToPdf(rect);

      var ann = new PdfAnnotation {
        PageNumber = 0,
        PdfBounds = pdfRect,
        ScreenBounds = rect,
        Text = "주석",
      };

      var cmd = new AnnotationAddCommand(_annotations, ann);
      _history?.Execute(cmd);
      AnnotationAdded?.Invoke(this, ann);

      ContentChanged?.Invoke(this, EventArgs.Empty);
      Invalidate();
      EditAnnotation(ann);
    }

    private void AddAnnotationAt(Point screenPt) => AddAnnotationAt(CreateDefaultAnnotationRect(screenPt));

    private void DeleteAnnotation(PdfAnnotation ann) {
      var cmd = new AnnotationDeleteCommand(_annotations, ann);
      _history?.Execute(cmd);
      AnnotationDeleted?.Invoke(this, ann);
      _hoveredAnnotation = null;
      ContentChanged?.Invoke(this, EventArgs.Empty);
      Invalidate();
    }

    private void EditAnnotation(PdfAnnotation ann) {
      using var dlg = new AnnotationEditDialog(ann.Text, ann.Color, ann.PdfBounds);
      if (dlg.ShowDialog() != DialogResult.OK) return;

      if (dlg.DeleteRequested) {
        DeleteAnnotation(ann);
        return;
      }

      bool changed = dlg.ResultText != ann.Text
                  || dlg.ResultColor != ann.Color
                  || dlg.ResultBounds != ann.PdfBounds;

      if (changed) {
        var cmd = new AnnotationEditCommand(ann, ann.Text, dlg.ResultText,
            ann.Color, dlg.ResultColor, ann.PdfBounds, dlg.ResultBounds);
        _history?.Execute(cmd);
        UpdateAnnotationScreenBounds(ann);
        AnnotationEdited?.Invoke(this, ann);
      }

      ContentChanged?.Invoke(this, EventArgs.Empty);
      Invalidate();
    }

    private void ShowAnnotationContextMenu(PdfAnnotation ann, Point pt) {
      var menu = new ContextMenuStrip();
      menu.Items.Add("✏️  편집", null, (_, _) => EditAnnotation(ann));
      menu.Items.Add("🗑️  삭제", null, (_, _) => DeleteAnnotation(ann));
      menu.Show(this, pt);
    }

    private void ShowEmptyContextMenu(Point pt) {
      var menu = new ContextMenuStrip();
      menu.Items.Add("📝  여기에 주석 추가", null, (_, _) => AddAnnotationAt(pt));
      menu.Show(this, pt);
    }

    // ── 히트 테스트 ───────────────────────────────────────────────

    private TextBlock? HitTest(Point p) =>
        _blocks.Where(b => b.ScreenBounds.Contains(p))
               .OrderBy(b => b.ScreenBounds.Width * b.ScreenBounds.Height)
               .FirstOrDefault();

    private PdfAnnotation? HitTestAnnotation(Point p) =>
        _annotations.FirstOrDefault(a => a.ScreenBounds.Contains(p));

    // ── 인라인 텍스트 편집기 ──────────────────────────────────────

    private void BeginEdit(TextBlock block) {
      CommitEdit(save: true);
      _activeBlock = block;

      var r = block.ScreenBounds;
      float fsPx = GetBlockFontHeightPx(block);
      const int pad = 2;
      Font editFont = CreateEditingFont(block, fsPx);
      Size editorSize = MeasureInlineEditorSize(block, editFont, pad);

      var host = GetScrollHost();
      if (host != null) {
        _editHostScroll = GetScrollOffset(host);
        _editHostScrollCaptured = true;
      }

      int editorX = Math.Max(r.X - pad, 0);
      int editorY = Math.Max(r.Y - Math.Max((editorSize.Height - r.Height) / 2, pad), 0);

      if (_pageImage != null) {
        int maxX = Math.Max(0, _pageImage.Width - editorSize.Width - 2);
        int maxY = Math.Max(0, _pageImage.Height - editorSize.Height - 2);
        editorX = Math.Min(editorX, maxX);
        editorY = Math.Min(editorY, maxY);
      }

      _editor = new TextBox {
        Location = new Point(editorX, editorY),
        Size = editorSize,
        Text = block.EditedText,
        Font = editFont,
        BackColor = Color.FromArgb(255, 253, 215),
        ForeColor = Color.FromArgb(10, 10, 40),
        BorderStyle = BorderStyle.FixedSingle,
        Multiline = false,
        HideSelection = false,
      };

      _editor.KeyDown += EditorKeyDown;
      _editor.LostFocus += EditorLostFocus;

      Controls.Add(_editor);
      _editor.BringToFront();
      _editor.Focus();
      _editor.SelectAll();
      if (_editHostScrollCaptured)
        RestoreHostScrollAsync(_editHostScroll);
      Invalidate(Inflate(r, 6));
    }

    private void EditorKeyDown(object? sender, KeyEventArgs e) {
      switch (e.KeyCode) {
        case Keys.Return when !e.Shift:
          CommitEdit(save: true);
          e.Handled = true;
          e.SuppressKeyPress = true;
          break;
        case Keys.Escape:
          CommitEdit(save: false);
          e.Handled = true;
          e.SuppressKeyPress = true;
          break;
      }
    }

    private void EditorLostFocus(object? sender, EventArgs e) {
      BeginInvoke((Action)(() => {
        if (!IsDisposed && _editor != null && !_editor.Focused)
          CommitEdit(save: true);
      }));
    }

    // ── Dispose ───────────────────────────────────────────────────

    protected override void Dispose(bool disposing) {
      if (disposing) { _pageImage?.Dispose(); _editor?.Dispose(); }
      base.Dispose(disposing);
    }
  }

  // ═══════════════════════════════════════════════════════════════════
  //  주석 편집 다이얼로그
  // ═══════════════════════════════════════════════════════════════════
  internal sealed class AnnotationEditDialog : Form {
    private readonly TextBox _txtContent;
    private readonly Button _btnOk;
    private readonly Button _btnDelete;
    private readonly Button _btnColor;
    private readonly NumericUpDown _numX;
    private readonly NumericUpDown _numY;
    private readonly NumericUpDown _numW;
    private readonly NumericUpDown _numH;
    private Color _color;

    public string ResultText { get; private set; }
    public Color ResultColor { get; private set; }
    public RectangleF ResultBounds { get; private set; }
    public bool DeleteRequested { get; private set; }

    public AnnotationEditDialog(string currentText, Color currentColor, RectangleF currentBounds) {
      ResultText = currentText;
      ResultColor = currentColor;
      ResultBounds = currentBounds;
      _color = currentColor;

      Text = "주석 편집";
      Size = new Size(430, 300);
      MinimumSize = new Size(360, 260);
      StartPosition = FormStartPosition.CenterParent;
      BackColor = Color.FromArgb(45, 45, 60);
      ForeColor = Color.White;
      Font = new Font("Malgun Gothic", 9.5f);

      _txtContent = new TextBox {
        Multiline = true,
        Text = currentText,
        BackColor = Color.FromArgb(55, 58, 80),
        ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical,
        Font = new Font("Malgun Gothic", 10f),
      };

      _numX = MakeNum(currentBounds.X);
      _numY = MakeNum(currentBounds.Y);
      _numW = MakeNum(Math.Max(currentBounds.Width, 1));
      _numH = MakeNum(Math.Max(currentBounds.Height, 1));

      var posPanel = new TableLayoutPanel {
        Dock = DockStyle.Top,
        Height = 64,
        ColumnCount = 4,
        RowCount = 2,
        BackColor = Color.FromArgb(35, 35, 50),
        Padding = new Padding(6),
      };
      posPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
      posPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
      posPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
      posPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
      posPanel.Controls.Add(MakeLabel("X"), 0, 0);
      posPanel.Controls.Add(MakeLabel("Y"), 1, 0);
      posPanel.Controls.Add(MakeLabel("W"), 2, 0);
      posPanel.Controls.Add(MakeLabel("H"), 3, 0);
      posPanel.Controls.Add(_numX, 0, 1);
      posPanel.Controls.Add(_numY, 1, 1);
      posPanel.Controls.Add(_numW, 2, 1);
      posPanel.Controls.Add(_numH, 3, 1);

      _btnColor = new Button {
        Text = "🎨 색상",
        BackColor = currentColor,
        ForeColor = Color.FromArgb(40, 30, 0),
        FlatStyle = FlatStyle.Flat,
        Width = 80,
        Height = 28,
      };
      _btnColor.Click += (_, _) => {
        using var cd = new ColorDialog { Color = _color, FullOpen = false };
        if (cd.ShowDialog() == DialogResult.OK) {
          _color = cd.Color;
          _btnColor.BackColor = _color;
        }
      };

      _btnOk = new Button {
        Text = "✔ 확인",
        DialogResult = DialogResult.OK,
        BackColor = Color.FromArgb(50, 160, 80),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Width = 80,
        Height = 28,
      };
      _btnOk.Click += (_, _) => {
        ResultText = _txtContent.Text;
        ResultColor = _color;
        ResultBounds = new RectangleF((float)_numX.Value, (float)_numY.Value,
                                      Math.Max(1f, (float)_numW.Value), Math.Max(1f, (float)_numH.Value));
      };

      _btnDelete = new Button {
        Text = "🗑 삭제",
        BackColor = Color.FromArgb(180, 50, 50),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Width = 80,
        Height = 28,
      };
      _btnDelete.Click += (_, _) => {
        DeleteRequested = true;
        DialogResult = DialogResult.OK;
        Close();
      };

      var btnPanel = new FlowLayoutPanel {
        Dock = DockStyle.Bottom,
        Height = 38,
        FlowDirection = FlowDirection.RightToLeft,
        BackColor = Color.FromArgb(35, 35, 50),
        Padding = new Padding(4),
      };
      btnPanel.Controls.AddRange(new Control[] { _btnOk, _btnColor, _btnDelete });

      Controls.Add(_txtContent);
      Controls.Add(posPanel);
      Controls.Add(btnPanel);

      AcceptButton = _btnOk;
      _txtContent.SelectAll();
    }

    private static Label MakeLabel(string text) => new() {
      Text = text,
      Dock = DockStyle.Fill,
      TextAlign = ContentAlignment.MiddleLeft,
      ForeColor = Color.Gainsboro,
      Margin = new Padding(0, 0, 6, 0),
    };

    private static NumericUpDown MakeNum(float value) => new() {
      DecimalPlaces = 1,
      Minimum = 0,
      Maximum = 100000,
      Value = (decimal)Math.Max(0, value),
      Dock = DockStyle.Fill,
      BackColor = Color.FromArgb(55, 58, 80),
      ForeColor = Color.White,
      BorderStyle = BorderStyle.FixedSingle,
      Margin = new Padding(0, 0, 6, 0),
    };
  }
}
