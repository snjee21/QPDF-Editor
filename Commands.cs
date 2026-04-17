using System.Drawing;
using System;
using System.Collections.Generic;

namespace QPDFEditor {
  // ═══════════════════════════════════════════════════════════════════
  //  Undo / Redo 커맨드 패턴
  // ═══════════════════════════════════════════════════════════════════

  public interface IEditCommand {
    string Description { get; }
    void Execute();
    void Undo();
  }

  // ── 텍스트 블록 단일 편집 ──────────────────────────────────────────
  public sealed class TextEditCommand : IEditCommand {
    private readonly TextBlock _block;
    private readonly string _before;
    private readonly string _after;

    public TextEditCommand(TextBlock block, string before, string after) {
      _block = block;
      _before = before;
      _after = after;
    }

    public string Description => $"텍스트 수정: \"{_before}\" → \"{_after}\"";

    public void Execute() => _block.EditedText = _after;
    public void Undo() => _block.EditedText = _before;
  }

  // ── 드래그 선택 삭제 (여러 블록) ──────────────────────────────────
  public sealed class BatchDeleteCommand : IEditCommand {
    private readonly List<(TextBlock Block, string Before)> _items;

    public BatchDeleteCommand(IEnumerable<TextBlock> blocks) {
      _items = new();
      foreach (var b in blocks)
        _items.Add((b, b.EditedText));
    }

    public string Description => $"영역 삭제: {_items.Count}개 블록";

    public void Execute() {
      foreach (var (b, _) in _items)
        b.EditedText = "";
    }

    public void Undo() {
      foreach (var (b, before) in _items)
        b.EditedText = before;
    }
  }

  // ── 주석 추가 ──────────────────────────────────────────────────────
  public sealed class AnnotationAddCommand : IEditCommand {
    private readonly List<PdfAnnotation> _store;
    private readonly PdfAnnotation _item;

    public AnnotationAddCommand(List<PdfAnnotation> store, PdfAnnotation item) { _store = store; _item = item; }

    public string Description => $"주석 추가: \"{_item.Text}\"";
    public void Execute() => _store.Add(_item);
    public void Undo() => _store.Remove(_item);
  }

  // ── 주석 삭제 ──────────────────────────────────────────────────────
  public sealed class AnnotationDeleteCommand : IEditCommand {
    private readonly List<PdfAnnotation> _store;
    private readonly PdfAnnotation _item;

    public AnnotationDeleteCommand(List<PdfAnnotation> store, PdfAnnotation item) { _store = store; _item = item; }

    public string Description => $"주석 삭제: \"{_item.Text}\"";
    public void Execute() => _store.Remove(_item);
    public void Undo() => _store.Add(_item);
  }

  // ── 주석 편집 / 위치 / 색상 변경 ────────────────────────────────────
  public sealed class AnnotationEditCommand : IEditCommand {
    private readonly PdfAnnotation _item;
    private readonly string _beforeText;
    private readonly string _afterText;
    private readonly Color _beforeColor;
    private readonly Color _afterColor;
    private readonly System.Drawing.RectangleF _beforeBounds;
    private readonly System.Drawing.RectangleF _afterBounds;

    public AnnotationEditCommand(PdfAnnotation item, string before, string after)
        : this(item, before, after, item.Color, item.Color, item.PdfBounds, item.PdfBounds) { }

    public AnnotationEditCommand(PdfAnnotation item, string beforeText, string afterText,
                                 Color beforeColor, Color afterColor,
                                 System.Drawing.RectangleF beforeBounds, System.Drawing.RectangleF afterBounds) {
      _item = item;
      _beforeText = beforeText;
      _afterText = afterText;
      _beforeColor = beforeColor;
      _afterColor = afterColor;
      _beforeBounds = beforeBounds;
      _afterBounds = afterBounds;
    }

    public string Description => $"주석 편집: \"{_beforeText}\" → \"{_afterText}\"";

    public void Execute() {
      _item.Text = _afterText;
      _item.Color = _afterColor;
      _item.PdfBounds = _afterBounds;
    }

    public void Undo() {
      _item.Text = _beforeText;
      _item.Color = _beforeColor;
      _item.PdfBounds = _beforeBounds;
    }
  }

  // ═══════════════════════════════════════════════════════════════════
  //  Undo / Redo 스택 매니저
  // ═══════════════════════════════════════════════════════════════════
  public sealed class EditHistory {
    private readonly Stack<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();
    private const int MaxDepth = 100;

    public event EventHandler? Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string UndoDescription => CanUndo ? _undo.Peek().Description : "";
    public string RedoDescription => CanRedo ? _redo.Peek().Description : "";

    /// <summary>커맨드를 실행하고 Undo 스택에 추가합니다.</summary>
    public void Execute(IEditCommand cmd) {
      cmd.Execute();
      _undo.Push(cmd);
      _redo.Clear();                     // 새 명령 → Redo 무효화

      // 스택 깊이 제한
      if (_undo.Count > MaxDepth) {
        var arr = _undo.ToArray();
        _undo.Clear();
        for (int i = MaxDepth - 1; i >= 0; i--)
          _undo.Push(arr[i]);
      }

      Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo() {
      if (!CanUndo) return;
      var cmd = _undo.Pop();
      cmd.Undo();
      _redo.Push(cmd);
      Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo() {
      if (!CanRedo) return;
      var cmd = _redo.Pop();
      cmd.Execute();
      _undo.Push(cmd);
      Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear() {
      _undo.Clear();
      _redo.Clear();
      Changed?.Invoke(this, EventArgs.Empty);
    }
  }
}
