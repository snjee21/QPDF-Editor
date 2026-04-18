using Docnet.Core;
using Docnet.Core.Models;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using NativePdfAnnotation = iText.Kernel.Pdf.Annot.PdfAnnotation;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Path = System.IO.Path;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace QPDFEditor {
  public sealed class PdfService : IDisposable {
    private const string DeletedMarkerSubject = "PdfEditorDeleted";
    private const string UserAnnotationSubject = "PdfEditorAnnotation";

    public string FilePath { get; private set; } = "";
    public int TotalPages { get; private set; }
    public List<List<TextBlock>> AllBlocks { get; } = new();
    public List<List<PdfAnnotation>> AllAnnotations { get; } = new();
    public List<string> LoadWarnings { get; } = new();

    private float[] _pdfW = Array.Empty<float>();
    private float[] _pdfH = Array.Empty<float>();

    // ── Load ───────────────────────────────────────────────────────

    public void Load(string path) {
      FilePath = path;
      AllBlocks.Clear();
      AllAnnotations.Clear();
      LoadWarnings.Clear();

      byte[] bytes = File.ReadAllBytes(path);
      using var ms = new MemoryStream(bytes);
      using var reader = new PdfReader(ms);
      using var pdfDoc = new PdfDocument(reader);

      TotalPages = pdfDoc.GetNumberOfPages();
      _pdfW = new float[TotalPages];
      _pdfH = new float[TotalPages];

      for (int i = 0; i < TotalPages; i++) {
        var page = pdfDoc.GetPage(i + 1);
        var rect = page.GetPageSize();
        _pdfW[i] = rect.GetWidth();
        _pdfH[i] = rect.GetHeight();

        var deletedRects = GetDeletedMarkerRects(page);

        List<TextBlock> blocks;
        try {
          var listener = new PdfTextListener(i + 1);
          new PdfDocumentContentParser(pdfDoc).ProcessContent(i + 1, listener);
          blocks = listener.Blocks;
        } catch (Exception ex) when (IsRecoverableTextExtractionException(ex)) {
          blocks = new List<TextBlock>();
          LoadWarnings.Add($"페이지 {i + 1}: 일부 글꼴/CMap 정보를 해석하지 못해 텍스트 편집 정보 추출을 건너뛰었습니다.");
        }

        blocks = MergeAdjacentTextBlocks(blocks);
        AllBlocks.Add(FilterOverlappingBlocks(blocks, deletedRects));
        AllAnnotations.Add(LoadPageAnnotations(page, i + 1));
      }
    }

    private static bool IsRecoverableTextExtractionException(Exception ex) {
      for (Exception? cur = ex; cur != null; cur = cur.InnerException) {
        string msg = cur.Message ?? string.Empty;
        if (msg.IndexOf("Cannot recognise document font", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("CMap", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("CIDSystemInfo", StringComparison.OrdinalIgnoreCase) >= 0 ||
            msg.IndexOf("WinCharSet", StringComparison.OrdinalIgnoreCase) >= 0)
          return true;
      }
      return false;
    }

    private static List<TextBlock> MergeAdjacentTextBlocks(List<TextBlock> blocks) {
      if (blocks == null || blocks.Count <= 1)
        return blocks?.ToList() ?? new List<TextBlock>();

      var merged = new List<TextBlock>(blocks.Count);
      foreach (var block in blocks) {
        if (string.IsNullOrEmpty(block.OriginalText))
          continue;

        //if (merged.Count > 0 && CanMergeBlocks(merged[^1], block, out string separator)) {
        //MergeBlockInto(merged[^1], block, separator);
        if (merged.Count > 0 && CanMergeBlocks(merged[merged.Count - 1], block, out string separator)) {
          MergeBlockInto(merged[merged.Count - 1], block, separator);
        } else {
          merged.Add(CloneBlock(block));
        }
      }
      return merged;
    }

    private static TextBlock CloneBlock(TextBlock src) => new TextBlock {
      OriginalText = src.OriginalText,
      EditedText = src.EditedText,
      PdfBounds = src.PdfBounds,
      BaselineY = src.BaselineY,
      ScreenBounds = src.ScreenBounds,
      FontSize = src.FontSize,
      FontName = src.FontName,
      PageNumber = src.PageNumber,
      BaselineStart = src.BaselineStart,
      BaselineEnd = src.BaselineEnd,
      AscentStart = src.AscentStart,
      DescentStart = src.DescentStart,
      FontObjectNumber = src.FontObjectNumber,
    };

    private static void MergeBlockInto(TextBlock head, TextBlock tail, string separator) {
      head.OriginalText += separator + tail.OriginalText;
      head.EditedText += separator + tail.EditedText;
      head.PdfBounds = RectangleF.Union(head.PdfBounds, tail.PdfBounds);
      head.BaselineEnd = tail.BaselineEnd;
      head.BaselineY = (head.BaselineY + tail.BaselineY) * 0.5f;

      if (tail.FontSize > 0f)
        head.FontSize = Math.Max(head.FontSize, tail.FontSize);
    }

    private static bool CanMergeBlocks(TextBlock left, TextBlock right, out string separator) {
      separator = string.Empty;

      if (left.PageNumber != right.PageNumber)
        return false;
      if (left.OriginalText == "[DEL]" || right.OriginalText == "[DEL]")
        return false;

      if (!HaveCompatibleFonts(left, right))
        return false;

      float avgFont = Math.Max((left.FontSize + right.FontSize) * 0.5f, 1f);
      if (Math.Abs(left.FontSize - right.FontSize) > Math.Max(0.75f, avgFont * 0.18f))
        return false;

      var u1 = GetUnitDirection(left);
      var u2 = GetUnitDirection(right);
      float cosine = Dot(u1, u2);
      if (cosine < 0.995f)
        return false;

      var perp = new PointF(-u1.Y, u1.X);
      var startDelta = Subtract(right.BaselineStart, left.BaselineStart);
      float lineOffset = Math.Abs(Dot(startDelta, perp));
      float maxLineOffset = Math.Max(1.2f, Math.Max(left.PdfBounds.Height, right.PdfBounds.Height) * 0.35f);
      if (lineOffset > maxLineOffset)
        return false;

      float gap = Dot(Subtract(right.BaselineStart, left.BaselineEnd), u1);
      float minGap = -Math.Max(1.0f, avgFont * 0.20f);
      float maxGap = Math.Max(3.0f, avgFont * 1.25f);
      if (gap < minGap || gap > maxGap)
        return false;

      bool alreadySpaced = left.OriginalText.EndsWith(" ", StringComparison.Ordinal) ||
                           right.OriginalText.StartsWith(" ", StringComparison.Ordinal);
      bool insertSyntheticSpace = !alreadySpaced && gap > Math.Max(1.5f, avgFont * 0.45f);
      separator = insertSyntheticSpace ? " " : string.Empty;
      return true;
    }

    private static bool HaveCompatibleFonts(TextBlock left, TextBlock right) {
      if (left.FontObjectNumber >= 0 && right.FontObjectNumber >= 0)
        return left.FontObjectNumber == right.FontObjectNumber;

      string l = NormalizeFontName(left.FontName);
      string r = NormalizeFontName(right.FontName);
      return string.Equals(l, r, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFontName(string? name) {
      string s = (name ?? string.Empty).Trim().TrimStart('/');
      int plus = s.IndexOf('+');
      if (plus >= 0 && plus + 1 < s.Length)
        s = s.Substring(plus + 1);
      int comma = s.IndexOf(',');
      if (comma >= 0)
        s = s.Substring(0, comma);
      return s.Trim();
    }

    private static PointF GetUnitDirection(TextBlock block) {
      float dx = block.BaselineEnd.X - block.BaselineStart.X;
      float dy = block.BaselineEnd.Y - block.BaselineStart.Y;
      float len = (float)Math.Sqrt(dx * dx + dy * dy);
      if (len < 0.001f)
        return new PointF(1f, 0f);
      return new PointF(dx / len, dy / len);
    }

    private static PointF Subtract(PointF a, PointF b) => new PointF(a.X - b.X, a.Y - b.Y);
    private static float Dot(PointF a, PointF b) => a.X * b.X + a.Y * b.Y;

    // ── Dimensions ─────────────────────────────────────────────────
    public float GetPageWidth(int pi) => _pdfW[pi];
    public float GetPageHeight(int pi) => _pdfH[pi];

    // ── Render ─────────────────────────────────────────────────────

    public Bitmap RenderPage(int pageIndex, float zoom) {
      try {
        double scale = (96.0 / 72.0) * zoom;
        using var lib = DocLib.Instance;
        using var doc = lib.GetDocReader(FilePath, new PageDimensions(scale));
        using var page = doc.GetPageReader(pageIndex);

        int w = page.GetPageWidth(), h = page.GetPageHeight();
        var raw = page.GetImage();

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h),
                                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(raw, 0, data.Scan0, raw.Length);
        bmp.UnlockBits(data);
        return bmp;
      } catch (BadImageFormatException ex) {
        throw new InvalidOperationException(
            "PDF 렌더링 엔진(PDFium) 아키텍처가 현재 실행 파일과 맞지 않습니다. " +
            "QPDFEditor를 x64로 빌드하고, 출력 폴더의 PDFium/native DLL도 x64 기준으로 다시 복사해야 합니다.", ex);
      }
    }

    // ── Save ───────────────────────────────────────────────────────

    public (int saved, int skipped) Save(string outputPath) {
      if (string.IsNullOrWhiteSpace(FilePath))
        throw new InvalidOperationException("먼저 PDF 파일을 열어야 합니다.");

      int saved = 0, skipped = 0;

      byte[] srcBytes = File.ReadAllBytes(FilePath);
      using var inMs = new MemoryStream(srcBytes);
      using var outMs = new MemoryStream();

      using var reader = new PdfReader(inMs);
      reader.SetMemorySavingMode(false);

      using var writer = new PdfWriter(outMs,
          new WriterProperties().SetFullCompressionMode(false));

      using var pdfDoc = new PdfDocument(reader, writer);

      PdfFont fallbackFont = CreateBestFont(pdfDoc);

      // ── 텍스트 편집 적용 ──────────────────────────────────────
      foreach (var pageBlocks in AllBlocks) {
        foreach (var block in pageBlocks) {
          if (!block.IsModified) { skipped++; continue; }
          try {
            PdfFont useFont = SelectFontForEditedBlock(pdfDoc, block, fallbackFont);
            ApplyEdit(pdfDoc, block, useFont);
            saved++;
          } catch (Exception ex) {
            var chain = new System.Text.StringBuilder();
            for (var e = ex; e != null; e = e.InnerException)
              chain.AppendLine($"• {e.GetType().Name}: {e.Message}");
            throw new Exception($"페이지 {block.PageNumber} 저장 중 오류:\n{chain}", ex);
          }
        }
      }

      // ── 주석 저장 ─────────────────────────────────────────────
      for (int pi = 0; pi < AllAnnotations.Count; pi++) {
        var page = pdfDoc.GetPage(pi + 1);
        var current = AllAnnotations[pi];

        foreach (var existing in page.GetAnnotations().ToList()) {
          var subtype = existing.GetSubtype();
          var subj = existing.GetPdfObject()?.GetAsString(PdfName.Subj)?.ToUnicodeString() ?? string.Empty;
          if (PdfName.FreeText.Equals(subtype) || PdfName.Text.Equals(subtype) || string.Equals(subj, DeletedMarkerSubject, StringComparison.Ordinal))
            page.RemoveAnnotation(existing);
        }

        foreach (var ann in current) {
          var r = ann.PdfBounds;
          var rect = new iText.Kernel.Geom.Rectangle(r.X, r.Y, Math.Max(r.Width, 12f), Math.Max(r.Height, 12f));

          var pdfAnn = new PdfFreeTextAnnotation(rect, new PdfString(ann.Text ?? string.Empty));
          pdfAnn.SetContents(ann.Text ?? string.Empty);
          pdfAnn.SetColor(ToITextColor(ann.Color));
          pdfAnn.Put(PdfName.T, new PdfString(ann.Author ?? string.Empty));
          pdfAnn.Put(PdfName.Subj, new PdfString(UserAnnotationSubject));
          pdfAnn.Put(PdfName.DA, new PdfString("0 0 0 rg /Helv 10 Tf"));
          page.AddAnnotation(pdfAnn);

          ann.IsNew = false;
          ann.SourceObjectNumber = pdfAnn.GetPdfObject()?.GetIndirectReference()?.GetObjNumber() ?? -1;
        }

        foreach (var deleted in AllBlocks[pi].Where(b => b.IsDeleted)) {
          AddDeletedMarkerAnnotation(page, deleted.PdfBounds);
        }
      }

      pdfDoc.Close();

      string guid8 = Guid.NewGuid().ToString("N").Substring(0, 8);
      string tmp = outputPath + ".~tmp" + guid8;
      try {
        File.WriteAllBytes(tmp, outMs.ToArray());
        if (File.Exists(outputPath)) File.Delete(outputPath);
        File.Move(tmp, outputPath);
      } catch { try { File.Delete(tmp); } catch { } throw; }

      return (saved, skipped);
    }


    private static List<PdfAnnotation> LoadPageAnnotations(PdfPage page, int pageNumber) {
      var result = new List<PdfAnnotation>();
      foreach (var native in page.GetAnnotations()) {
        try {
          var subj = native.GetPdfObject()?.GetAsString(PdfName.Subj)?.ToUnicodeString() ?? string.Empty;
          if (string.Equals(subj, DeletedMarkerSubject, StringComparison.Ordinal))
            continue;

          var subtype = native.GetSubtype();
          if (!PdfName.FreeText.Equals(subtype) && !PdfName.Text.Equals(subtype))
            continue;

          var rect = native.GetRectangle();
          if (rect == null) continue;
          var r = rect.ToRectangle();
          var contents = native.GetContents()?.ToUnicodeString() ?? string.Empty;
          var author = native.GetPdfObject()?.GetAsString(PdfName.T)?.ToUnicodeString() ?? Environment.UserName;

          result.Add(new PdfAnnotation {
            PageNumber = pageNumber,
            PdfBounds = new RectangleF(r.GetX(), r.GetY(), r.GetWidth(), r.GetHeight()),
            Text = contents,
            Author = author,
            Color = FromITextColor(native),
            IsNew = false,
            SourceObjectNumber = native.GetPdfObject()?.GetIndirectReference()?.GetObjNumber() ?? -1,
          });
        } catch { }
      }
      return result;
    }


    private static List<RectangleF> GetDeletedMarkerRects(PdfPage page) {
      var rects = new List<RectangleF>();
      foreach (var native in page.GetAnnotations()) {
        try {
          var subj = native.GetPdfObject()?.GetAsString(PdfName.Subj)?.ToUnicodeString() ?? string.Empty;
          if (!string.Equals(subj, DeletedMarkerSubject, StringComparison.Ordinal))
            continue;

          var rectObj = native.GetRectangle();
          if (rectObj == null) continue;
          var r = rectObj.ToRectangle();
          rects.Add(new RectangleF(r.GetX(), r.GetY(), r.GetWidth(), r.GetHeight()));
        } catch { }
      }
      return rects;
    }

    private static void AddDeletedMarkerAnnotation(PdfPage page, RectangleF r) {
      var rect = new iText.Kernel.Geom.Rectangle(r.X, r.Y, Math.Max(r.Width, 6f), Math.Max(r.Height, 6f));
      var ann = new PdfFreeTextAnnotation(rect, new PdfString(string.Empty));
      ann.SetContents(string.Empty);
      ann.Put(PdfName.Subj, new PdfString(DeletedMarkerSubject));
      ann.Put(PdfName.T, new PdfString("QPDFEditor"));
      ann.Put(PdfName.DA, new PdfString("0 0 0 rg /Helv 1 Tf"));
      ann.Put(PdfName.F, new PdfNumber(34)); // Hidden + NoView
      ann.SetColor(ColorConstants.WHITE);
      page.AddAnnotation(ann);
    }

    // ── 단일 텍스트 블록 편집 적용 ─────────────────────────────────

    private static void ApplyEdit(PdfDocument pdfDoc, TextBlock block, PdfFont font) {
      var pdfPage = pdfDoc.GetPage(block.PageNumber);

      float baseDx = block.BaselineEnd.X - block.BaselineStart.X;
      float baseDy = block.BaselineEnd.Y - block.BaselineStart.Y;
      float baseLen = (float)Math.Sqrt(baseDx * baseDx + baseDy * baseDy);
      if (baseLen < 0.001f) { baseDx = Math.Max(block.PdfBounds.Width, 1f); baseDy = 0f; baseLen = baseDx; }
      float ux = baseDx / baseLen, uy = baseDy / baseLen;

      float upDx = block.AscentStart.X - block.DescentStart.X;
      float upDy = block.AscentStart.Y - block.DescentStart.Y;
      float upLen = (float)Math.Sqrt(upDx * upDx + upDy * upDy);
      if (upLen < 0.001f) { upDx = -uy; upDy = ux; upLen = 1f; }
      float vx = upDx / upLen, vy = upDy / upLen;

      float targetH = upLen > 0.1f ? upLen : Math.Max(block.PdfBounds.Height, 1f);
      float fs = block.FontSize > 0f
        ? Math.Max(Math.Min(block.FontSize, targetH * 1.35f), 3f)
        : Math.Max(targetH * 0.85f, 3f);

      string text = (block.EditedText ?? "")
          .Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

      var pdfCanvas = new PdfCanvas(pdfPage, true);
      try {
        // 1. 원본 흰색으로 덮기
        float pad = Math.Max(targetH * 0.08f, 0.6f);
        float qx1 = block.DescentStart.X - vx * pad, qy1 = block.DescentStart.Y - vy * pad;
        float qx2 = qx1 + baseDx, qy2 = qy1 + baseDy;
        float qx4 = block.AscentStart.X + vx * pad, qy4 = block.AscentStart.Y + vy * pad;
        float qx3 = qx4 + baseDx, qy3 = qy4 + baseDy;

        pdfCanvas.SaveState()
                 .SetFillColor(ColorConstants.WHITE)
                 .MoveTo(qx1, qy1).LineTo(qx2, qy2).LineTo(qx3, qy3).LineTo(qx4, qy4)
                 .ClosePath().Fill()
                 .RestoreState();

        if (string.IsNullOrWhiteSpace(text)) {
          // 삭제: 흰색으로만 덮고, 재로드 필터링은 숨김 annotation으로 처리
        } else {
          // 수정: 새 텍스트 (원본 폭에 맞게 scaleX 조정)
          float tw = font.GetWidth(text, fs);
          float scaleX = (tw > 0.1f && baseLen > 0.1f) ? baseLen / tw : 1f;

          pdfCanvas.SaveState()
                   .BeginText()
                   .SetFillColor(ColorConstants.BLACK)
                   .SetFontAndSize(font, fs)
                   .SetTextMatrix(ux * scaleX, uy * scaleX, vx, vy,
                                  block.BaselineStart.X, block.BaselineStart.Y)
                   .ShowText(text)
                   .EndText()
                   .RestoreState();
        }
      } finally {
        pdfCanvas.Release();   // ← 반드시 호출: 미호출 시 스트림 미flush
      }
    }

    // ── 원본 폰트 복원 ─────────────────────────────────────────────

    private static PdfFont SelectFontForEditedBlock(PdfDocument pdfDoc, TextBlock block, PdfFont fallbackFont) {
      string edited = NormalizeTextForPdf(block.EditedText);
      if (string.IsNullOrWhiteSpace(edited))
        return fallbackFont;

      if (!IntroducesNewGlyphs(block.OriginalText, edited)) {
        var originalFont = TryGetOriginalFont(pdfDoc, block.FontObjectNumber, edited);
        if (originalFont != null)
          return originalFont;
      }

      var matchedFont = CreateMatchedSystemFont(pdfDoc, block.FontName, edited);
      if (matchedFont != null)
        return matchedFont;

      return fallbackFont;
    }

    private static bool IntroducesNewGlyphs(string originalText, string editedText) {
      var originalChars = new HashSet<char>(NormalizeTextForPdf(originalText)
          .Where(c => !char.IsWhiteSpace(c)));

      foreach (char ch in editedText) {
        if (char.IsWhiteSpace(ch))
          continue;
        if (!originalChars.Contains(ch))
          return true;
      }
      return false;
    }

    private static string NormalizeTextForPdf(string? text) => (text ?? string.Empty)
        .Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

    private static PdfFont? TryGetOriginalFont(PdfDocument pdfDoc, int objNum, string text) {
      if (objNum <= 0) return null;
      try {
        var pdfObj = pdfDoc.GetPdfObject(objNum);
        if (pdfObj is not PdfDictionary fontDict) return null;
        var font = PdfFontFactory.CreateFont(fontDict);
        if (!string.IsNullOrEmpty(text)) {
          byte[] enc = font.ConvertToBytes(text);
          if (enc == null || enc.Length == 0) return null;
        }
        return font;
      } catch { return null; }
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

    private static bool IsBoldFont(string? fontName) {
      string n = NormalizePdfFontName(fontName).ToLowerInvariant();
      return n.Contains("bold") || n.Contains("black") || n.Contains("heavy") || n.Contains("demi");
    }

    private static IEnumerable<string> GetCandidateFontFiles(string? pdfFontName) {
      bool bold = IsBoldFont(pdfFontName);
      string n = NormalizePdfFontName(pdfFontName).Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();

      if (n.Contains("courier") || n.Contains("mono"))
        return bold ? new[] { "courbd.ttf", "consolab.ttf", "consola.ttf" } : new[] { "cour.ttf", "consola.ttf", "courbd.ttf" };

      if (n.Contains("times") || n.Contains("mincho") || n.Contains("batang") || n.Contains("serif"))
        return bold
            ? new[] { "timesbd.ttf", "batang.ttc", "malgunbd.ttf", "arialbd.ttf" }
            : new[] { "times.ttf", "batang.ttc", "malgun.ttf", "arial.ttf" };

      if (n.Contains("dotum") || n.Contains("gulim") || n.Contains("gothic") || n.Contains("malgun") || n.Contains("helvetica") || n.Contains("arial") || n.Contains("sans"))
        return bold
            ? new[] { "malgunbd.ttf", "arialbd.ttf", "seguisb.ttf", "malgun.ttf", "arial.ttf" }
            : new[] { "malgun.ttf", "arial.ttf", "segoeui.ttf", "malgunbd.ttf", "arialbd.ttf" };

      return bold
          ? new[] { "malgunbd.ttf", "arialbd.ttf", "segoeuib.ttf", "malgun.ttf", "arial.ttf" }
          : new[] { "malgun.ttf", "arial.ttf", "segoeui.ttf", "malgunbd.ttf", "arialbd.ttf" };
    }

    private static PdfFont? CreateMatchedSystemFont(PdfDocument pdfDoc, string? pdfFontName, string text) {
      string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

      foreach (string fileName in GetCandidateFontFiles(pdfFontName).Distinct(StringComparer.OrdinalIgnoreCase)) {
        string path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
          continue;

        try {
          var font = PdfFontFactory.CreateFont(path, PdfEncodings.IDENTITY_H,
              PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED, pdfDoc);
          byte[] enc = font.ConvertToBytes(text);
          if (enc != null && enc.Length > 0)
            return font;
        } catch { }
      }

      return null;
    }

    private static PdfFont CreateBestFont(PdfDocument pdfDoc) {
      string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
      foreach (string name in new[] { "malgun.ttf", "malgunbd.ttf", "arialuni.ttf", "arial.ttf", "times.ttf" }) {
        string path = Path.Combine(dir, name);
        if (!File.Exists(path)) continue;
        try {
          return PdfFontFactory.CreateFont(path, PdfEncodings.IDENTITY_H,
              PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED, pdfDoc);
        } catch { }
      }
      return PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
    }

    // ── 주석 색상 변환 ──────────────────────────────────────────────

    private static iText.Kernel.Colors.Color ToITextColor(System.Drawing.Color c) =>
        new DeviceRgb(c.R / 255f, c.G / 255f, c.B / 255f);

    private static System.Drawing.Color FromITextColor(NativePdfAnnotation ann) {
      try {
        var arr = ann.GetColorObject();
        if (arr != null && arr.Size() >= 3) {
          int r = (int)Math.Round(arr.GetAsNumber(0).FloatValue() * 255f);
          int g = (int)Math.Round(arr.GetAsNumber(1).FloatValue() * 255f);
          int b = (int)Math.Round(arr.GetAsNumber(2).FloatValue() * 255f);
          return System.Drawing.Color.FromArgb(Clamp255(r), Clamp255(g), Clamp255(b));
        }
      } catch { }
      return System.Drawing.Color.FromArgb(255, 255, 180);
    }

    private static int Clamp255(int v) => Math.Max(0, Math.Min(255, v));

    // ── 겹치는 블록 필터링 (기존 로직 유지) ────────────────────────

    private static List<TextBlock> FilterOverlappingBlocks(List<TextBlock> blocks, List<RectangleF>? deletedRects = null) {
      var result = new List<TextBlock>();
      var markerRects = blocks.Where(b => b.OriginalText == "[DEL]").Select(b => b.PdfBounds).ToList();
      if (deletedRects != null && deletedRects.Count > 0)
        markerRects.AddRange(deletedRects);

      for (int i = 0; i < blocks.Count; i++) {
        var b = blocks[i];
        if (b.OriginalText == "[DEL]" || string.IsNullOrWhiteSpace(b.OriginalText)) continue;

        bool coveredByDelete = markerRects.Any(d => OverlapRatio(b.PdfBounds, d) > 0.5f);
        bool coveredByLaterText = blocks.Skip(i + 1)
            .Where(b2 => b2.OriginalText != "[DEL]" && !string.IsNullOrWhiteSpace(b2.OriginalText))
            .Any(b2 => OverlapRatio(b.PdfBounds, b2.PdfBounds) > 0.5f);

        if (!coveredByDelete && !coveredByLaterText)
          result.Add(b);
      }
      return result;
    }

    private static float OverlapRatio(RectangleF a, RectangleF b) {
      var inter = RectangleF.Intersect(a, b);
      float area = a.Width * a.Height;
      return area > 0 ? (inter.Width * inter.Height) / area : 0f;
    }

    // ── Helpers ────────────────────────────────────────────────────

    public void AcceptAllEdits() {
      foreach (var block in AllBlocks.SelectMany(p => p))
        block.OriginalText = block.EditedText;
    }

    public void SetCurrentFilePath(string path) => FilePath = path;

    // ── Page Management ────────────────────────────────────────────

    /// <summary>페이지 삭제 (0-based). 파일을 재작성하고 리로드합니다.</summary>
    public void DeletePage(int pageIndex) {
      if (pageIndex < 0 || pageIndex >= TotalPages)
        throw new ArgumentOutOfRangeException(nameof(pageIndex));
      if (TotalPages <= 1)
        throw new InvalidOperationException("마지막 남은 페이지는 삭제할 수 없습니다.");

      string tmp = FilePath + ".~pagedel.pdf";
      try {
        byte[] srcBytes = File.ReadAllBytes(FilePath);
        using var inMs = new MemoryStream(srcBytes);
        using var outMs = new MemoryStream();
        using var reader = new PdfReader(inMs);
        using var writer = new PdfWriter(outMs, new WriterProperties().SetFullCompressionMode(false));
        using var pdfDoc = new PdfDocument(reader, writer);
        pdfDoc.RemovePage(pageIndex + 1);   // iText7: 1-based
        pdfDoc.Close();
        File.WriteAllBytes(tmp, outMs.ToArray());
        File.Delete(FilePath);
        File.Move(tmp, FilePath);
      } catch { try { File.Delete(tmp); } catch { } throw; }

      Load(FilePath);
    }

    /// <summary>페이지 순서 변경 (0-based). 파일을 재작성하고 리로드합니다.</summary>
    public void MovePage(int fromIndex, int toIndex) {
      if (fromIndex < 0 || fromIndex >= TotalPages) throw new ArgumentOutOfRangeException(nameof(fromIndex));
      if (toIndex < 0 || toIndex >= TotalPages) throw new ArgumentOutOfRangeException(nameof(toIndex));
      if (fromIndex == toIndex) return;

      string tmp = FilePath + ".~pagemove.pdf";
      try {
        byte[] srcBytes = File.ReadAllBytes(FilePath);
        using var inMs = new MemoryStream(srcBytes);
        using var outMs = new MemoryStream();
        using var reader = new PdfReader(inMs);
        using var src = new PdfDocument(reader);
        using var writer = new PdfWriter(outMs, new WriterProperties().SetFullCompressionMode(false));
        using var dst = new PdfDocument(writer);

        // 1-based 페이지 순서 재구성
        var order = Enumerable.Range(1, TotalPages).ToList();
        int movedPage = order[fromIndex];
        order.RemoveAt(fromIndex);
        order.Insert(toIndex, movedPage);

        src.CopyPagesTo(order, dst);
        dst.Close();
        src.Close();

        File.WriteAllBytes(tmp, outMs.ToArray());
        File.Delete(FilePath);
        File.Move(tmp, FilePath);
      } catch { try { File.Delete(tmp); } catch { } throw; }

      Load(FilePath);
    }

    /// <summary>
    /// 다른 PDF 파일을 현재 파일에 삽입합니다.
    /// insertBeforeIndex: 삽입할 위치 (0-based). TotalPages이면 맨 뒤에 추가.
    /// </summary>
    public void InsertPdf(string otherPath, int insertBeforeIndex) {
      if (!File.Exists(otherPath))
        throw new FileNotFoundException("삽입할 PDF 파일을 찾을 수 없습니다.", otherPath);
      if (insertBeforeIndex < 0 || insertBeforeIndex > TotalPages)
        throw new ArgumentOutOfRangeException(nameof(insertBeforeIndex));

      string tmp = FilePath + ".~pagemerge.pdf";
      try {
        byte[] srcBytes = File.ReadAllBytes(FilePath);
        byte[] otherBytes = File.ReadAllBytes(otherPath);

        using var inMs1 = new MemoryStream(srcBytes);
        using var inMs2 = new MemoryStream(otherBytes);
        using var outMs = new MemoryStream();

        using var reader1 = new PdfReader(inMs1);
        using var src1 = new PdfDocument(reader1);
        using var reader2 = new PdfReader(inMs2);
        using var src2 = new PdfDocument(reader2);
        using var writer = new PdfWriter(outMs, new WriterProperties().SetFullCompressionMode(false));
        using var dst = new PdfDocument(writer);

        int otherTotal = src2.GetNumberOfPages();

        // 삽입 위치 앞 페이지 복사
        if (insertBeforeIndex > 0)
          src1.CopyPagesTo(1, insertBeforeIndex, dst);

        // 삽입할 PDF 전체 복사
        src2.CopyPagesTo(1, otherTotal, dst);

        // 삽입 위치 뒤 나머지 페이지 복사
        if (insertBeforeIndex < TotalPages)
          src1.CopyPagesTo(insertBeforeIndex + 1, TotalPages, dst);

        dst.Close();
        src2.Close();
        src1.Close();

        File.WriteAllBytes(tmp, outMs.ToArray());
        File.Delete(FilePath);
        File.Move(tmp, FilePath);
      } catch { try { File.Delete(tmp); } catch { } throw; }

      Load(FilePath);
    }

    public void Dispose() { }
  }
}