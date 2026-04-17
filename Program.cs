using System;
using System.IO;
using System.Windows.Forms;

namespace QPDFEditor {
  static class Program {
    [STAThread]
    static void Main(string[] args) {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      // 탐색기 컨텍스트 메뉴 / 파일 연결로 실행 시 args[0] 에 경로 전달됨
      string? startupFile = null;
      if (args.Length > 0) {
        string candidate = args[0].Trim('"');
        if (File.Exists(candidate) &&
            candidate.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
          startupFile = candidate;
      }

      Application.Run(new MainForm(startupFile));
    }
  }
}
