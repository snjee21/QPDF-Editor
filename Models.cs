using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace QPDFEditor {
  // ─────────────────────────────────────────────────────────────────
  // TextBlock: PDF 에서 추출된 하나의 텍스트 스팬
  // ─────────────────────────────────────────────────────────────────
  public class TextBlock {
    public string OriginalText { get; set; } = "";
    public string EditedText { get; set; } = "";

    public RectangleF PdfBounds { get; set; }
    public float BaselineY { get; set; }
    public System.Drawing.Rectangle ScreenBounds { get; set; }

    public float FontSize { get; set; }
    public string FontName { get; set; } = "Helvetica";
    public int PageNumber { get; set; }   // 1-based

    // ── 텍스트 방향 기하 정보 (CAD PDF 대응) ──────────────────────
    public PointF BaselineStart { get; set; }
    public PointF BaselineEnd { get; set; }
    public PointF AscentStart { get; set; }
    public PointF DescentStart { get; set; }

    // ── 원본 폰트 복원용 PDF 객체 번호 ────────────────────────────
    public int FontObjectNumber { get; set; } = -1;

    public bool IsModified => EditedText != OriginalText;
    public bool IsDeleted => IsModified && string.IsNullOrWhiteSpace(EditedText);
  }

  // ─────────────────────────────────────────────────────────────────
  // PdfAnnotation: 사용자가 추가하는 PDF 주석
  // ─────────────────────────────────────────────────────────────────
  public class PdfAnnotation {
    public Guid Id { get; } = Guid.NewGuid();
    public int PageNumber { get; set; }    // 1-based

    /// <summary>페이지 좌표계 위치 (PDF pt, Y-up)</summary>
    public RectangleF PdfBounds { get; set; }

    /// <summary>화면 픽셀 위치 (렌더링마다 갱신)</summary>
    public System.Drawing.Rectangle ScreenBounds { get; set; }

    public string Text { get; set; } = "주석을 입력하세요.";
    public Color Color { get; set; } = Color.FromArgb(255, 255, 180);   // 연한 노랑
    public string Author { get; set; } = Environment.UserName;

    public bool IsNew { get; set; } = true;   // 이번 세션에서 추가됨
    public int SourceObjectNumber { get; set; } = -1;   // 기존 PDF annotation obj num
  }

  // ─────────────────────────────────────────────────────────────────
  // PdfTextListener: iText 텍스트 추출 리스너
  // ─────────────────────────────────────────────────────────────────
  public class PdfTextListener : IEventListener {
    private readonly List<TextBlock> _blocks = new();
    private readonly int _pageNumber;

    public PdfTextListener(int pageNumber) { _pageNumber = pageNumber; }
    public List<TextBlock> Blocks => _blocks;

    private static float Min2(float a, float b) { return (float)Math.Min(a, b); }
    private static float Max2(float a, float b) { return (float)Math.Max(a, b); }
    private static float Min3(float a, float b, float c) { return Min2(Min2(a, b), c); }
    private static float Max3(float a, float b, float c) { return Max2(Max2(a, b), c); }

    private static string ResolveFontName(TextRenderInfo ri) {
      try {
        var font = ri.GetFont();
        var pdfObject = font?.GetPdfObject();
        if (pdfObject is PdfDictionary dict) {
          string rawName = dict.GetAsName(PdfName.BaseFont)?.GetValue();
          if (string.IsNullOrWhiteSpace(rawName)) {
            var descriptor = dict.GetAsDictionary(PdfName.FontDescriptor);
            rawName = descriptor?.GetAsName(PdfName.FontName)?.GetValue();
          }

          if (!string.IsNullOrWhiteSpace(rawName))
            return rawName.TrimStart('/');
        }
      } catch { }

      try { return ri.GetFont()?.GetFontProgram()?.GetFontNames()?.GetFontName() ?? "Helvetica"; } catch { return "Helvetica"; }
    }

    public void EventOccurred(IEventData data, EventType type) {
      if (data is not TextRenderInfo ri) return;
      string text = ri.GetText();
      if (string.IsNullOrWhiteSpace(text)) return;

      var baseline = ri.GetBaseline();
      var ascent = ri.GetAscentLine();
      var descent = ri.GetDescentLine();

      var bStart = baseline.GetStartPoint();
      var bEnd = baseline.GetEndPoint();
      var aStart = ascent.GetStartPoint();
      var aEnd = ascent.GetEndPoint();
      var dStart = descent.GetStartPoint();
      var dEnd = descent.GetEndPoint();

      float minX = Min3(Min2(bStart.Get(Vector.I1), bEnd.Get(Vector.I1)),
                   Min2(aStart.Get(Vector.I1), aEnd.Get(Vector.I1)),
                   Min2(dStart.Get(Vector.I1), dEnd.Get(Vector.I1)));
      float maxX = Max3(Max2(bStart.Get(Vector.I1), bEnd.Get(Vector.I1)),
                   Max2(aStart.Get(Vector.I1), aEnd.Get(Vector.I1)),
                   Max2(dStart.Get(Vector.I1), dEnd.Get(Vector.I1)));
      float minY = Min3(Min2(bStart.Get(Vector.I2), bEnd.Get(Vector.I2)),
                   Min2(aStart.Get(Vector.I2), aEnd.Get(Vector.I2)),
                   Min2(dStart.Get(Vector.I2), dEnd.Get(Vector.I2)));
      float maxY = Max3(Max2(bStart.Get(Vector.I2), bEnd.Get(Vector.I2)),
                   Max2(aStart.Get(Vector.I2), aEnd.Get(Vector.I2)),
                   Max2(dStart.Get(Vector.I2), dEnd.Get(Vector.I2)));

      float x = minX, y = minY;
      float w = maxX - minX, h = maxY - minY;
      float bl = bStart.Get(Vector.I2);
      float fs = ri.GetFontSize();

      if (w < 0.5f) w = Math.Max(fs * 0.6f * text.Length, 1f);
      if (h < 1.0f) h = Math.Max(fs * 1.2f, 1f);

      string fontName = ResolveFontName(ri);

      int fontObjNum = -1;
      try {
        var indRef = ri.GetFont()?.GetPdfObject()?.GetIndirectReference();
        if (indRef != null) fontObjNum = indRef.GetObjNumber();
      } catch { }

      _blocks.Add(new TextBlock {
        OriginalText = text,
        EditedText = text,
        PdfBounds = new RectangleF(x, y, w, h),
        BaselineY = bl,
        FontSize = fs > 0 ? fs : 12f,
        FontName = fontName,
        FontObjectNumber = fontObjNum,
        PageNumber = _pageNumber,
        BaselineStart = new PointF(bStart.Get(Vector.I1), bStart.Get(Vector.I2)),
        BaselineEnd = new PointF(bEnd.Get(Vector.I1), bEnd.Get(Vector.I2)),
        AscentStart = new PointF(aStart.Get(Vector.I1), aStart.Get(Vector.I2)),
        DescentStart = new PointF(dStart.Get(Vector.I1), dStart.Get(Vector.I2)),
      });
    }

    public ICollection<EventType> GetSupportedEvents() =>
        new HashSet<EventType> { EventType.RENDER_TEXT };
  }
}