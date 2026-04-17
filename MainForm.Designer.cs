using Org.BouncyCastle.Asn1.Crmf;
using QPDFEditor;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace QPDFEditor {
  partial class MainForm {
    private System.ComponentModel.IContainer components = null;

    private void InitializeComponent() {
      this._menu = new System.Windows.Forms.MenuStrip();
      this.miFile = new System.Windows.Forms.ToolStripMenuItem();
      this.miOpen = new System.Windows.Forms.ToolStripMenuItem();
      this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
      this._miSave = new System.Windows.Forms.ToolStripMenuItem();
      this._miSaveAs = new System.Windows.Forms.ToolStripMenuItem();
      this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
      this.miExit = new System.Windows.Forms.ToolStripMenuItem();
      this.miEdit = new System.Windows.Forms.ToolStripMenuItem();
      this._miFindReplace = new System.Windows.Forms.ToolStripMenuItem();
      this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
      this._miRevert = new System.Windows.Forms.ToolStripMenuItem();
      this.miView = new System.Windows.Forms.ToolStripMenuItem();
      this.miZoom50 = new System.Windows.Forms.ToolStripMenuItem();
      this.miZoom75 = new System.Windows.Forms.ToolStripMenuItem();
      this.miZoom100 = new System.Windows.Forms.ToolStripMenuItem();
      this.miZoom125 = new System.Windows.Forms.ToolStripMenuItem();
      this.miZoom150 = new System.Windows.Forms.ToolStripMenuItem();
      this.miZoom200 = new System.Windows.Forms.ToolStripMenuItem();
      this.miHelp = new System.Windows.Forms.ToolStripMenuItem();
      this.miHowToUse = new System.Windows.Forms.ToolStripMenuItem();
      this.miAbout = new System.Windows.Forms.ToolStripMenuItem();
      this._toolbar = new System.Windows.Forms.ToolStrip();
      this.btnOpen = new System.Windows.Forms.ToolStripButton();
      this._btnSave = new System.Windows.Forms.ToolStripButton();
      this._btnSaveAs = new System.Windows.Forms.ToolStripButton();
      this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
      this._btnPrev = new System.Windows.Forms.ToolStripButton();
      this._lblPage = new System.Windows.Forms.ToolStripLabel();
      this._btnNext = new System.Windows.Forms.ToolStripButton();
      this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
      this.lblZoom = new System.Windows.Forms.ToolStripLabel();
      this._cmbZoom = new System.Windows.Forms.ToolStripComboBox();
      this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
      this._btnRevert = new System.Windows.Forms.ToolStripButton();
      this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
      this.btnFind = new System.Windows.Forms.ToolStripButton();
      this._findPanel = new System.Windows.Forms.Panel();
      this.layoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
      this.lblFind = new System.Windows.Forms.Label();
      this._txtFind = new System.Windows.Forms.TextBox();
      this.lblReplace = new System.Windows.Forms.Label();
      this._txtReplace = new System.Windows.Forms.TextBox();
      this._chkMatchCase = new System.Windows.Forms.CheckBox();
      this.btnFindAll = new System.Windows.Forms.Button();
      this.btnReplaceAll = new System.Windows.Forms.Button();
      this.btnCloseFind = new System.Windows.Forms.Button();
      this._lblFindResult = new System.Windows.Forms.Label();
      this._status = new System.Windows.Forms.StatusStrip();
      this._lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
      this._lblModified = new System.Windows.Forms.ToolStripStatusLabel();
      this._pageContainer = new System.Windows.Forms.Panel();
      this._pageView = new QPDFEditor.PdfPageView();
      this._menu.SuspendLayout();
      this._toolbar.SuspendLayout();
      this._findPanel.SuspendLayout();
      this.layoutPanel1.SuspendLayout();
      this._status.SuspendLayout();
      this._pageContainer.SuspendLayout();
      this.SuspendLayout();
      // 
      // _menu
      // 
      this._menu.BackColor = System.Drawing.SystemColors.MenuBar;
      this._menu.ForeColor = System.Drawing.Color.White;
      this._menu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miFile,
            this.miEdit,
            this.miView,
            this.miHelp});
      this._menu.Location = new System.Drawing.Point(0, 0);
      this._menu.Name = "_menu";
      this._menu.Size = new System.Drawing.Size(1304, 24);
      this._menu.TabIndex = 0;
      // 
      // miFile
      // 
      this.miFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miOpen,
            this.toolStripSeparator1,
            this._miSave,
            this._miSaveAs,
            this.toolStripSeparator2,
            this.miExit});
      this.miFile.ForeColor = System.Drawing.SystemColors.ControlText;
      this.miFile.Name = "miFile";
      this.miFile.Size = new System.Drawing.Size(57, 20);
      this.miFile.Text = "파일(&F)";
      // 
      // miOpen
      // 
      this.miOpen.Name = "miOpen";
      this.miOpen.Size = new System.Drawing.Size(269, 22);
      this.miOpen.Text = "열기(&O)  Ctrl+O";
      this.miOpen.Click += new System.EventHandler(this.OnOpen);
      // 
      // toolStripSeparator1
      // 
      this.toolStripSeparator1.Name = "toolStripSeparator1";
      this.toolStripSeparator1.Size = new System.Drawing.Size(266, 6);
      // 
      // _miSave
      // 
      this._miSave.Name = "_miSave";
      this._miSave.Size = new System.Drawing.Size(269, 22);
      this._miSave.Text = "저장(&S)  Ctrl+S";
      this._miSave.Click += new System.EventHandler(this.OnSave);
      // 
      // _miSaveAs
      // 
      this._miSaveAs.Name = "_miSaveAs";
      this._miSaveAs.Size = new System.Drawing.Size(269, 22);
      this._miSaveAs.Text = "다른 이름으로 저장(&A)  Ctrl+Shift+S";
      this._miSaveAs.Click += new System.EventHandler(this.OnSaveAs);
      // 
      // toolStripSeparator2
      // 
      this.toolStripSeparator2.Name = "toolStripSeparator2";
      this.toolStripSeparator2.Size = new System.Drawing.Size(266, 6);
      // 
      // miExit
      // 
      this.miExit.Name = "miExit";
      this.miExit.Size = new System.Drawing.Size(269, 22);
      this.miExit.Text = "종료(&X)";
      this.miExit.Click += new System.EventHandler(this.MiExit_Click);
      // 
      // miEdit
      // 
      this.miEdit.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._miFindReplace,
            this.toolStripSeparator3,
            this._miRevert});
      this.miEdit.ForeColor = System.Drawing.SystemColors.ControlText;
      this.miEdit.Name = "miEdit";
      this.miEdit.Size = new System.Drawing.Size(57, 20);
      this.miEdit.Text = "편집(&E)";
      // 
      // _miFindReplace
      // 
      this._miFindReplace.Name = "_miFindReplace";
      this._miFindReplace.Size = new System.Drawing.Size(221, 22);
      this._miFindReplace.Text = "찾기 / 바꾸기(&H)  Ctrl+H";
      this._miFindReplace.Click += new System.EventHandler(this.ToggleFindPanel);
      // 
      // toolStripSeparator3
      // 
      this.toolStripSeparator3.Name = "toolStripSeparator3";
      this.toolStripSeparator3.Size = new System.Drawing.Size(218, 6);
      // 
      // _miRevert
      // 
      this._miRevert.Name = "_miRevert";
      this._miRevert.Size = new System.Drawing.Size(221, 22);
      this._miRevert.Text = "모든 변경 사항 되돌리기(&Z)";
      this._miRevert.Click += new System.EventHandler(this.OnRevertAll);
      // 
      // miView
      // 
      this.miView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miZoom50,
            this.miZoom75,
            this.miZoom100,
            this.miZoom125,
            this.miZoom150,
            this.miZoom200});
      this.miView.ForeColor = System.Drawing.SystemColors.ControlText;
      this.miView.Name = "miView";
      this.miView.Size = new System.Drawing.Size(59, 20);
      this.miView.Text = "보기(&V)";
      // 
      // miZoom50
      // 
      this.miZoom50.Name = "miZoom50";
      this.miZoom50.Size = new System.Drawing.Size(105, 22);
      this.miZoom50.Text = "50%";
      this.miZoom50.Click += new System.EventHandler(this.MiZoom_Click);
      // 
      // miZoom75
      // 
      this.miZoom75.Name = "miZoom75";
      this.miZoom75.Size = new System.Drawing.Size(105, 22);
      this.miZoom75.Text = "75%";
      this.miZoom75.Click += new System.EventHandler(this.MiZoom_Click);
      // 
      // miZoom100
      // 
      this.miZoom100.Name = "miZoom100";
      this.miZoom100.Size = new System.Drawing.Size(105, 22);
      this.miZoom100.Text = "100%";
      this.miZoom100.Click += new System.EventHandler(this.MiZoom_Click);
      // 
      // miZoom125
      // 
      this.miZoom125.Name = "miZoom125";
      this.miZoom125.Size = new System.Drawing.Size(105, 22);
      this.miZoom125.Text = "125%";
      this.miZoom125.Click += new System.EventHandler(this.MiZoom_Click);
      // 
      // miZoom150
      // 
      this.miZoom150.Name = "miZoom150";
      this.miZoom150.Size = new System.Drawing.Size(105, 22);
      this.miZoom150.Text = "150%";
      this.miZoom150.Click += new System.EventHandler(this.MiZoom_Click);
      // 
      // miZoom200
      // 
      this.miZoom200.Name = "miZoom200";
      this.miZoom200.Size = new System.Drawing.Size(105, 22);
      this.miZoom200.Text = "200%";
      this.miZoom200.Click += new System.EventHandler(this.MiZoom_Click);
      // 
      // miHelp
      // 
      this.miHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miHowToUse,
            this.miAbout});
      this.miHelp.ForeColor = System.Drawing.SystemColors.ControlText;
      this.miHelp.Name = "miHelp";
      this.miHelp.Size = new System.Drawing.Size(69, 20);
      this.miHelp.Text = "도움말(&?)";
      // 
      // miHowToUse
      // 
      this.miHowToUse.Name = "miHowToUse";
      this.miHowToUse.Size = new System.Drawing.Size(143, 22);
      this.miHowToUse.Text = "사용 방법(&H)";
      this.miHowToUse.Click += new System.EventHandler(this.OnHelp);
      // 
      // miAbout
      // 
      this.miAbout.Name = "miAbout";
      this.miAbout.Size = new System.Drawing.Size(143, 22);
      this.miAbout.Text = "정보(&A)";
      this.miAbout.Click += new System.EventHandler(this.OnAbout);
      // 
      // _toolbar
      // 
      this._toolbar.BackColor = System.Drawing.SystemColors.MenuBar;
      this._toolbar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
      this._toolbar.ImageScalingSize = new System.Drawing.Size(22, 22);
      this._toolbar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnOpen,
            this._btnSave,
            this._btnSaveAs,
            this.toolStripSeparator4,
            this._btnPrev,
            this._lblPage,
            this._btnNext,
            this.toolStripSeparator5,
            this.lblZoom,
            this._cmbZoom,
            this.toolStripSeparator6,
            this._btnRevert,
            this.toolStripSeparator7,
            this.btnFind});
      this._toolbar.Location = new System.Drawing.Point(0, 24);
      this._toolbar.Name = "_toolbar";
      this._toolbar.Padding = new System.Windows.Forms.Padding(4, 2, 4, 2);
      this._toolbar.Size = new System.Drawing.Size(1304, 27);
      this._toolbar.TabIndex = 1;
      // 
      // btnOpen
      // 
      this.btnOpen.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(160)))), ((int)(((byte)(240)))));
      this.btnOpen.Name = "btnOpen";
      this.btnOpen.Size = new System.Drawing.Size(51, 20);
      this.btnOpen.Text = "📂 열기";
      this.btnOpen.Click += new System.EventHandler(this.OnOpen);
      // 
      // _btnSave
      // 
      this._btnSave.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(200)))), ((int)(((byte)(120)))));
      this._btnSave.Name = "_btnSave";
      this._btnSave.Size = new System.Drawing.Size(51, 20);
      this._btnSave.Text = "💾 저장";
      this._btnSave.Click += new System.EventHandler(this.OnSave);
      // 
      // _btnSaveAs
      // 
      this._btnSaveAs.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(190)))), ((int)(((byte)(100)))));
      this._btnSaveAs.Name = "_btnSaveAs";
      this._btnSaveAs.Size = new System.Drawing.Size(103, 20);
      this._btnSaveAs.Text = "📋 다른 이름으로";
      this._btnSaveAs.Click += new System.EventHandler(this.OnSaveAs);
      // 
      // toolStripSeparator4
      // 
      this.toolStripSeparator4.Name = "toolStripSeparator4";
      this.toolStripSeparator4.Size = new System.Drawing.Size(6, 23);
      // 
      // _btnPrev
      // 
      this._btnPrev.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
      this._btnPrev.Name = "_btnPrev";
      this._btnPrev.Size = new System.Drawing.Size(23, 20);
      this._btnPrev.Text = "◀";
      this._btnPrev.Click += new System.EventHandler(this.BtnPrev_Click);
      // 
      // _lblPage
      // 
      this._lblPage.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
      this._lblPage.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
      this._lblPage.Name = "_lblPage";
      this._lblPage.Size = new System.Drawing.Size(32, 23);
      this._lblPage.Text = "– / –";
      // 
      // _btnNext
      // 
      this._btnNext.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
      this._btnNext.Name = "_btnNext";
      this._btnNext.Size = new System.Drawing.Size(23, 20);
      this._btnNext.Text = "▶";
      this._btnNext.Click += new System.EventHandler(this.BtnNext_Click);
      // 
      // toolStripSeparator5
      // 
      this.toolStripSeparator5.Name = "toolStripSeparator5";
      this.toolStripSeparator5.Size = new System.Drawing.Size(6, 23);
      // 
      // lblZoom
      // 
      this.lblZoom.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
      this.lblZoom.Name = "lblZoom";
      this.lblZoom.Size = new System.Drawing.Size(38, 20);
      this.lblZoom.Text = "확대: ";
      // 
      // _cmbZoom
      // 
      this._cmbZoom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
      this._cmbZoom.Items.AddRange(new object[] {
            "50%",
            "75%",
            "100%",
            "125%",
            "150%",
            "200%"});
      this._cmbZoom.Name = "_cmbZoom";
      this._cmbZoom.Size = new System.Drawing.Size(121, 23);
      this._cmbZoom.SelectedIndexChanged += new System.EventHandler(this.OnZoomChanged);
      // 
      // toolStripSeparator6
      // 
      this.toolStripSeparator6.Name = "toolStripSeparator6";
      this.toolStripSeparator6.Size = new System.Drawing.Size(6, 23);
      // 
      // _btnRevert
      // 
      this._btnRevert.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(120)))), ((int)(((byte)(60)))));
      this._btnRevert.Name = "_btnRevert";
      this._btnRevert.Size = new System.Drawing.Size(73, 20);
      this._btnRevert.Text = "↺ 되돌리기";
      this._btnRevert.Click += new System.EventHandler(this.OnRevertAll);
      // 
      // toolStripSeparator7
      // 
      this.toolStripSeparator7.Name = "toolStripSeparator7";
      this.toolStripSeparator7.Size = new System.Drawing.Size(6, 23);
      // 
      // btnFind
      // 
      this.btnFind.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(160)))), ((int)(((byte)(100)))), ((int)(((byte)(220)))));
      this.btnFind.Name = "btnFind";
      this.btnFind.Size = new System.Drawing.Size(92, 20);
      this.btnFind.Text = "🔍 찾기/바꾸기";
      this.btnFind.Click += new System.EventHandler(this.ToggleFindPanel);
      // 
      // _findPanel
      // 
      this._findPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(42)))), ((int)(((byte)(58)))));
      this._findPanel.Controls.Add(this.layoutPanel1);
      this._findPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
      this._findPanel.Location = new System.Drawing.Point(0, 853);
      this._findPanel.MinimumSize = new System.Drawing.Size(0, 58);
      this._findPanel.Name = "_findPanel";
      this._findPanel.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
      this._findPanel.Size = new System.Drawing.Size(1304, 58);
      this._findPanel.TabIndex = 2;
      this._findPanel.Visible = false;
      // 
      // layoutPanel1
      // 
      this.layoutPanel1.Controls.Add(this._lblFindResult);
      this.layoutPanel1.Controls.Add(this.lblFind);
      this.layoutPanel1.Controls.Add(this._txtFind);
      this.layoutPanel1.Controls.Add(this.lblReplace);
      this.layoutPanel1.Controls.Add(this._txtReplace);
      this.layoutPanel1.Controls.Add(this._chkMatchCase);
      this.layoutPanel1.Controls.Add(this.btnFindAll);
      this.layoutPanel1.Controls.Add(this.btnReplaceAll);
      this.layoutPanel1.Controls.Add(this.btnCloseFind);
      this.layoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
      this.layoutPanel1.Location = new System.Drawing.Point(8, 5);
      this.layoutPanel1.Name = "layoutPanel1";
      this.layoutPanel1.Size = new System.Drawing.Size(1288, 44);
      this.layoutPanel1.TabIndex = 0;
      this.layoutPanel1.WrapContents = false;
      // 
      // lblFind
      // 
      this.lblFind.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this.lblFind.AutoSize = true;
      this.lblFind.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(160)))), ((int)(((byte)(200)))), ((int)(((byte)(255)))));
      this.lblFind.Location = new System.Drawing.Point(14, 6);
      this.lblFind.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
      this.lblFind.Name = "lblFind";
      this.lblFind.Size = new System.Drawing.Size(37, 17);
      this.lblFind.TabIndex = 0;
      this.lblFind.Text = "찾기:";
      // 
      // _txtFind
      // 
      this._txtFind.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this._txtFind.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(58)))), ((int)(((byte)(80)))));
      this._txtFind.ForeColor = System.Drawing.Color.White;
      this._txtFind.Location = new System.Drawing.Point(58, 3);
      this._txtFind.Margin = new System.Windows.Forms.Padding(3, 3, 6, 3);
      this._txtFind.Name = "_txtFind";
      this._txtFind.MinimumSize = new System.Drawing.Size(150, 32);
      this._txtFind.Size = new System.Drawing.Size(191, 32);
      this._txtFind.TabIndex = 1;
      this._txtFind.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnFindKeyDown);
      // 
      // lblReplace
      // 
      this.lblReplace.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this.lblReplace.AutoSize = true;
      this.lblReplace.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(160)))), ((int)(((byte)(200)))), ((int)(((byte)(255)))));
      this.lblReplace.Location = new System.Drawing.Point(258, 6);
      this.lblReplace.Margin = new System.Windows.Forms.Padding(3, 0, 4, 0);
      this.lblReplace.Name = "lblReplace";
      this.lblReplace.Size = new System.Drawing.Size(50, 17);
      this.lblReplace.TabIndex = 2;
      this.lblReplace.Text = "바꾸기:";
      // 
      // _txtReplace
      // 
      this._txtReplace.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this._txtReplace.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(58)))), ((int)(((byte)(80)))));
      this._txtReplace.ForeColor = System.Drawing.Color.White;
      this._txtReplace.Location = new System.Drawing.Point(315, 3);
      this._txtReplace.Margin = new System.Windows.Forms.Padding(3, 3, 6, 3);
      this._txtReplace.Name = "_txtReplace";
      this._txtReplace.MinimumSize = new System.Drawing.Size(150, 32);
      this._txtReplace.Size = new System.Drawing.Size(191, 32);
      this._txtReplace.TabIndex = 3;
      // 
      // _chkMatchCase
      // 
      this._chkMatchCase.Anchor = System.Windows.Forms.AnchorStyles.Left;
      this._chkMatchCase.AutoSize = true;
      this._chkMatchCase.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
      this._chkMatchCase.Location = new System.Drawing.Point(516, 7);
      this._chkMatchCase.Margin = new System.Windows.Forms.Padding(4, 6, 4, 0);
      this._chkMatchCase.Name = "_chkMatchCase";
      this._chkMatchCase.Size = new System.Drawing.Size(110, 21);
      this._chkMatchCase.TabIndex = 4;
      this._chkMatchCase.Text = "대소문자 구분";
      // 
      // btnFindAll
      // 
      this.btnFindAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this.btnFindAll.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(53)))), ((int)(((byte)(72)))));
      this.btnFindAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this.btnFindAll.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(160)))), ((int)(((byte)(240)))));
      this.btnFindAll.Location = new System.Drawing.Point(632, 3);
      this.btnFindAll.Margin = new System.Windows.Forms.Padding(2);
      this.btnFindAll.MinimumSize = new System.Drawing.Size(96, 32);
      this.btnFindAll.Name = "btnFindAll";
      this.btnFindAll.Size = new System.Drawing.Size(152, 32);
      this.btnFindAll.TabIndex = 5;
      this.btnFindAll.Text = "모두 찾기";
      this.btnFindAll.UseVisualStyleBackColor = false;
      this.btnFindAll.Click += new System.EventHandler(this.OnFindAll);
      // 
      // btnReplaceAll
      // 
      this.btnReplaceAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this.btnReplaceAll.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(53)))), ((int)(((byte)(72)))));
      this.btnReplaceAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this.btnReplaceAll.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(200)))), ((int)(((byte)(120)))));
      this.btnReplaceAll.Location = new System.Drawing.Point(788, 3);
      this.btnReplaceAll.Margin = new System.Windows.Forms.Padding(2);
      this.btnReplaceAll.MinimumSize = new System.Drawing.Size(96, 32);
      this.btnReplaceAll.Name = "btnReplaceAll";
      this.btnReplaceAll.Size = new System.Drawing.Size(152, 32);
      this.btnReplaceAll.TabIndex = 6;
      this.btnReplaceAll.Text = "모두 바꾸기";
      this.btnReplaceAll.UseVisualStyleBackColor = false;
      this.btnReplaceAll.Click += new System.EventHandler(this.OnReplaceAll);
      // 
      // btnCloseFind
      // 
      this.btnCloseFind.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
      this.btnCloseFind.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(53)))), ((int)(((byte)(72)))));
      this.btnCloseFind.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
      this.btnCloseFind.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
      this.btnCloseFind.Location = new System.Drawing.Point(944, 3);
      this.btnCloseFind.Margin = new System.Windows.Forms.Padding(2);
      this.btnCloseFind.MinimumSize = new System.Drawing.Size(56, 32);
      this.btnCloseFind.Name = "btnCloseFind";
      this.btnCloseFind.Size = new System.Drawing.Size(80, 32);
      this.btnCloseFind.TabIndex = 8;
      this.btnCloseFind.Text = "✕";
      this.btnCloseFind.UseVisualStyleBackColor = false;
      this.btnCloseFind.Click += new System.EventHandler(this.BtnCloseFind_Click);
      // 
      // _lblFindResult
      // 
      this._lblFindResult.AutoSize = true;
      this._lblFindResult.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(220)))), ((int)(((byte)(120)))));
      this._lblFindResult.Location = new System.Drawing.Point(6, 9);
      this._lblFindResult.Margin = new System.Windows.Forms.Padding(6, 9, 4, 0);
      this._lblFindResult.Name = "_lblFindResult";
      this._lblFindResult.Size = new System.Drawing.Size(0, 17);
      this._lblFindResult.TabIndex = 7;
      // 
      // _status
      // 
      this._status.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(40)))));
      this._status.ForeColor = System.Drawing.Color.White;
      this._status.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._lblStatus,
            this._lblModified});
      this._status.Location = new System.Drawing.Point(0, 899);
      this._status.Name = "_status";
      this._status.Size = new System.Drawing.Size(1304, 22);
      this._status.SizingGrip = false;
      this._status.TabIndex = 3;
      // 
      // _lblStatus
      // 
      this._lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
      this._lblStatus.Name = "_lblStatus";
      this._lblStatus.Size = new System.Drawing.Size(1289, 17);
      this._lblStatus.Spring = true;
      this._lblStatus.Text = "PDF 파일을 열어주세요. 파란 영역을 클릭하면 텍스트를 수정할 수 있습니다.";
      this._lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
      // 
      // _lblModified
      // 
      this._lblModified.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(230)))), ((int)(((byte)(120)))));
      this._lblModified.Name = "_lblModified";
      this._lblModified.Size = new System.Drawing.Size(0, 17);
      // 
      // _pageContainer
      // 
      this._pageContainer.AllowDrop = true;
      this._pageContainer.AutoScroll = true;
      this._pageContainer.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(82)))), ((int)(((byte)(96)))));
      this._pageContainer.Controls.Add(this._pageView);
      this._pageContainer.Dock = System.Windows.Forms.DockStyle.Fill;
      this._pageContainer.Location = new System.Drawing.Point(0, 51);
      this._pageContainer.Name = "_pageContainer";
      this._pageContainer.Padding = new System.Windows.Forms.Padding(20);
      this._pageContainer.Size = new System.Drawing.Size(1304, 802);
      this._pageContainer.TabIndex = 4;
      // 
      // _pageView
      // 
      this._pageView.BackColor = System.Drawing.Color.White;
      this._pageView.Cursor = System.Windows.Forms.Cursors.Default;
      this._pageView.History = null;
      this._pageView.IsViewOnly = false;
      this._pageView.Location = new System.Drawing.Point(20, 20);
      this._pageView.Mode = QPDFEditor.EditMode.TextEdit;
      this._pageView.Name = "_pageView";
      this._pageView.Size = new System.Drawing.Size(1, 1);
      this._pageView.TabIndex = 0;
      this._pageView.TabStop = true;
      // 
      // MainForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(248)))));
      this.ClientSize = new System.Drawing.Size(1304, 921);
      this.Controls.Add(this._pageContainer);
      this.Controls.Add(this._findPanel);
      this.Controls.Add(this._toolbar);
      this.Controls.Add(this._menu);
      this.Controls.Add(this._status);
      this.Font = new System.Drawing.Font("맑은 고딕", 9.25F);
      this.MainMenuStrip = this._menu;
      this.MinimumSize = new System.Drawing.Size(900, 640);
      this.Name = "MainForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
      this.Text = "PDF 편집기";
      this._menu.ResumeLayout(false);
      this._menu.PerformLayout();
      this._toolbar.ResumeLayout(false);
      this._toolbar.PerformLayout();
      this._findPanel.ResumeLayout(false);
      this.layoutPanel1.ResumeLayout(false);
      this.layoutPanel1.PerformLayout();
      this._status.ResumeLayout(false);
      this._status.PerformLayout();
      this._pageContainer.ResumeLayout(false);
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    private System.Windows.Forms.MenuStrip _menu;
    private System.Windows.Forms.ToolStripMenuItem miFile, miOpen, _miSave, _miSaveAs, miExit;
    private System.Windows.Forms.ToolStripMenuItem miEdit, _miFindReplace, _miRevert;
    private System.Windows.Forms.ToolStripMenuItem miView, miZoom50, miZoom75, miZoom100, miZoom125, miZoom150, miZoom200;
    private System.Windows.Forms.ToolStripMenuItem miHelp, miHowToUse;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator1, toolStripSeparator2, toolStripSeparator3;

    private System.Windows.Forms.ToolStrip _toolbar;
    private System.Windows.Forms.ToolStrip _toolbar2;
    private System.Windows.Forms.ToolStripButton btnOpen, _btnSave, _btnSaveAs, _btnPrev, _btnNext, _btnRevert, btnFind;
    private System.Windows.Forms.ToolStripLabel _lblPage, lblZoom;
    private System.Windows.Forms.ToolStripComboBox _cmbZoom;
    private System.Windows.Forms.ToolStripSeparator toolStripSeparator4, toolStripSeparator5, toolStripSeparator6, toolStripSeparator7;

    private System.Windows.Forms.Panel _findPanel;
    private System.Windows.Forms.FlowLayoutPanel layoutPanel1;
    private System.Windows.Forms.Label lblFind, lblReplace, _lblFindResult;
    private System.Windows.Forms.TextBox _txtFind, _txtReplace;
    private System.Windows.Forms.CheckBox _chkMatchCase;
    private System.Windows.Forms.Button btnFindAll, btnReplaceAll, btnCloseFind;

    private System.Windows.Forms.StatusStrip _status;
    private System.Windows.Forms.ToolStripStatusLabel _lblStatus, _lblModified;

    private System.Windows.Forms.Panel _pageContainer;
    private QPDFEditor.PdfPageView _pageView;
    private ToolStripMenuItem miAbout;
  }
}