using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TextGenTest
{
    partial class Form1
    {
        private IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            ComponentResourceManager resources = new ComponentResourceManager(typeof(Form1));
            tableLayoutRoot = new TableLayoutPanel();
            panelHeader = new Panel();
            flowHeader = new FlowLayoutPanel();
            lblTitle = new Label();
            lblHint = new Label();
            lblStatus = new Label();
            tableMain = new TableLayoutPanel();
            tableLeft = new TableLayoutPanel();
            lblCorpus = new Label();
            richTextBox1 = new RichTextBox();
            tableRight = new TableLayoutPanel();
            flowSeed = new FlowLayoutPanel();
            lblSeed = new Label();
            txtSeed = new TextBox();
            btnGenerate = new Button();
            lblOutput = new Label();
            richTextBox2 = new RichTextBox();
            panelBottom = new Panel();
            flowBottom = new FlowLayoutPanel();
            lblNGram = new Label();
            numNGram = new NumericUpDown();
            btnTrain = new Button();
            tableLayoutRoot.SuspendLayout();
            panelHeader.SuspendLayout();
            flowHeader.SuspendLayout();
            tableMain.SuspendLayout();
            tableLeft.SuspendLayout();
            tableRight.SuspendLayout();
            flowSeed.SuspendLayout();
            panelBottom.SuspendLayout();
            flowBottom.SuspendLayout();
            ((ISupportInitialize)numNGram).BeginInit();
            SuspendLayout();
            // 
            // tableLayoutRoot
            // 
            tableLayoutRoot.ColumnCount = 1;
            tableLayoutRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutRoot.Controls.Add(panelHeader, 0, 0);
            tableLayoutRoot.Controls.Add(tableMain, 0, 1);
            tableLayoutRoot.Controls.Add(panelBottom, 0, 2);
            tableLayoutRoot.Dock = DockStyle.Fill;
            tableLayoutRoot.Location = new Point(0, 0);
            tableLayoutRoot.Margin = new Padding(0);
            tableLayoutRoot.Name = "tableLayoutRoot";
            tableLayoutRoot.RowCount = 3;
            tableLayoutRoot.RowStyles.Add(new RowStyle());
            tableLayoutRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutRoot.RowStyles.Add(new RowStyle());
            tableLayoutRoot.Size = new Size(960, 640);
            tableLayoutRoot.TabIndex = 0;
            // 
            // panelHeader
            // 
            panelHeader.BackColor = Color.White;
            panelHeader.Controls.Add(flowHeader);
            panelHeader.Dock = DockStyle.Fill;
            panelHeader.Location = new Point(0, 0);
            panelHeader.Margin = new Padding(0);
            panelHeader.Name = "panelHeader";
            panelHeader.Padding = new Padding(20, 14, 20, 12);
            panelHeader.Size = new Size(960, 108);
            panelHeader.TabIndex = 0;
            panelHeader.Paint += panelHeader_Paint;
            // 
            // flowHeader
            // 
            flowHeader.AutoSize = true;
            flowHeader.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowHeader.Controls.Add(lblTitle);
            flowHeader.Controls.Add(lblHint);
            flowHeader.Controls.Add(lblStatus);
            flowHeader.Dock = DockStyle.Fill;
            flowHeader.FlowDirection = FlowDirection.TopDown;
            flowHeader.Location = new Point(20, 14);
            flowHeader.Margin = new Padding(0);
            flowHeader.Name = "flowHeader";
            flowHeader.Size = new Size(920, 82);
            flowHeader.TabIndex = 0;
            flowHeader.WrapContents = false;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 12.75F, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(15, 23, 42);
            lblTitle.Location = new Point(0, 0);
            lblTitle.Margin = new Padding(0, 0, 0, 6);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(420, 23);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Генерация текста (n-граммы + марковская цепь)";
            // 
            // lblHint
            // 
            lblHint.AutoSize = true;
            lblHint.Font = new Font("Segoe UI", 9F);
            lblHint.ForeColor = Color.FromArgb(100, 116, 139);
            lblHint.Location = new Point(0, 29);
            lblHint.Margin = new Padding(0, 0, 0, 6);
            lblHint.MaximumSize = new Size(900, 0);
            lblHint.Name = "lblHint";
            lblHint.Size = new Size(890, 30);
            lblHint.TabIndex = 1;
            lblHint.Text = "Сначала обучите модель на опорном тексте, затем задайте затравку и нажмите «Генерировать».";
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 9F);
            lblStatus.ForeColor = Color.FromArgb(100, 116, 139);
            lblStatus.Location = new Point(0, 65);
            lblStatus.Margin = new Padding(0, 0, 0, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(52, 15);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Статус";
            // 
            // tableMain
            // 
            tableMain.ColumnCount = 2;
            tableMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            tableMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            tableMain.Controls.Add(tableLeft, 0, 0);
            tableMain.Controls.Add(tableRight, 1, 0);
            tableMain.Dock = DockStyle.Fill;
            tableMain.Location = new Point(0, 108);
            tableMain.Margin = new Padding(0);
            tableMain.Name = "tableMain";
            tableMain.Padding = new Padding(10, 8, 10, 4);
            tableMain.RowCount = 1;
            tableMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableMain.Size = new Size(960, 508);
            tableMain.TabIndex = 1;
            // 
            // tableLeft
            // 
            tableLeft.ColumnCount = 1;
            tableLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLeft.Controls.Add(lblCorpus, 0, 0);
            tableLeft.Controls.Add(richTextBox1, 0, 1);
            tableLeft.Dock = DockStyle.Fill;
            tableLeft.Location = new Point(13, 11);
            tableLeft.Margin = new Padding(3);
            tableLeft.Name = "tableLeft";
            tableLeft.RowCount = 2;
            tableLeft.RowStyles.Add(new RowStyle());
            tableLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLeft.Size = new Size(484, 486);
            tableLeft.TabIndex = 0;
            // 
            // lblCorpus
            // 
            lblCorpus.AutoSize = true;
            lblCorpus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCorpus.ForeColor = Color.FromArgb(51, 65, 85);
            lblCorpus.Location = new Point(0, 0);
            lblCorpus.Margin = new Padding(0, 0, 0, 6);
            lblCorpus.Name = "lblCorpus";
            lblCorpus.Size = new Size(98, 15);
            lblCorpus.TabIndex = 0;
            lblCorpus.Text = "Опорный текст";
            // 
            // richTextBox1
            // 
            richTextBox1.BorderStyle = BorderStyle.FixedSingle;
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Font = new Font("Segoe UI", 9F);
            richTextBox1.Location = new Point(0, 21);
            richTextBox1.Margin = new Padding(0);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(484, 465);
            richTextBox1.TabIndex = 1;
            richTextBox1.Text = resources.GetString("richTextBox1.Text");
            // 
            // tableRight
            // 
            tableRight.ColumnCount = 1;
            tableRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableRight.Controls.Add(flowSeed, 0, 0);
            tableRight.Controls.Add(lblOutput, 0, 1);
            tableRight.Controls.Add(richTextBox2, 0, 2);
            tableRight.Dock = DockStyle.Fill;
            tableRight.Location = new Point(503, 11);
            tableRight.Margin = new Padding(3);
            tableRight.Name = "tableRight";
            tableRight.RowCount = 3;
            tableRight.RowStyles.Add(new RowStyle());
            tableRight.RowStyles.Add(new RowStyle());
            tableRight.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableRight.Size = new Size(444, 486);
            tableRight.TabIndex = 1;
            // 
            // flowSeed
            // 
            flowSeed.AutoSize = true;
            flowSeed.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowSeed.Controls.Add(lblSeed);
            flowSeed.Controls.Add(txtSeed);
            flowSeed.Controls.Add(btnGenerate);
            flowSeed.Dock = DockStyle.Fill;
            flowSeed.Location = new Point(0, 0);
            flowSeed.Margin = new Padding(0);
            flowSeed.Name = "flowSeed";
            flowSeed.Size = new Size(444, 42);
            flowSeed.TabIndex = 0;
            flowSeed.WrapContents = false;
            // 
            // lblSeed
            // 
            lblSeed.AutoSize = true;
            lblSeed.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSeed.ForeColor = Color.FromArgb(51, 65, 85);
            lblSeed.Location = new Point(0, 12);
            lblSeed.Margin = new Padding(0, 12, 8, 0);
            lblSeed.Name = "lblSeed";
            lblSeed.Size = new Size(60, 15);
            lblSeed.TabIndex = 0;
            lblSeed.Text = "Затравка";
            // 
            // txtSeed
            // 
            txtSeed.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtSeed.Font = new Font("Segoe UI", 9F);
            txtSeed.Location = new Point(68, 8);
            txtSeed.Margin = new Padding(0, 8, 10, 0);
            txtSeed.MinimumSize = new Size(200, 0);
            txtSeed.Name = "txtSeed";
            txtSeed.Size = new Size(240, 23);
            txtSeed.TabIndex = 1;
            // 
            // btnGenerate
            // 
            btnGenerate.AutoSize = false;
            btnGenerate.BackColor = Color.FromArgb(37, 99, 235);
            btnGenerate.Cursor = Cursors.Hand;
            btnGenerate.FlatAppearance.BorderSize = 0;
            btnGenerate.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 64, 175);
            btnGenerate.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            btnGenerate.FlatStyle = FlatStyle.Flat;
            btnGenerate.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            btnGenerate.ForeColor = Color.White;
            btnGenerate.Location = new Point(318, 2);
            btnGenerate.Margin = new Padding(0);
            btnGenerate.Name = "btnGenerate";
            btnGenerate.Size = new Size(120, 36);
            btnGenerate.TabIndex = 2;
            btnGenerate.Text = "Генерировать";
            btnGenerate.UseVisualStyleBackColor = false;
            btnGenerate.Click += btnGenerate_Click;
            // 
            // lblOutput
            // 
            lblOutput.AutoSize = true;
            lblOutput.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblOutput.ForeColor = Color.FromArgb(51, 65, 85);
            lblOutput.Location = new Point(0, 48);
            lblOutput.Margin = new Padding(0, 6, 0, 6);
            lblOutput.Name = "lblOutput";
            lblOutput.Size = new Size(66, 15);
            lblOutput.TabIndex = 1;
            lblOutput.Text = "Результат";
            // 
            // richTextBox2
            // 
            richTextBox2.BorderStyle = BorderStyle.FixedSingle;
            richTextBox2.Dock = DockStyle.Fill;
            richTextBox2.Font = new Font("Segoe UI", 9F);
            richTextBox2.Location = new Point(0, 69);
            richTextBox2.Margin = new Padding(0);
            richTextBox2.Name = "richTextBox2";
            richTextBox2.ReadOnly = true;
            richTextBox2.Size = new Size(444, 417);
            richTextBox2.TabIndex = 2;
            richTextBox2.Text = "";
            // 
            // panelBottom
            // 
            panelBottom.BackColor = Color.FromArgb(245, 247, 250);
            panelBottom.Controls.Add(flowBottom);
            panelBottom.Dock = DockStyle.Fill;
            panelBottom.Location = new Point(0, 616);
            panelBottom.Margin = new Padding(0);
            panelBottom.Name = "panelBottom";
            panelBottom.Padding = new Padding(16, 10, 16, 10);
            panelBottom.Size = new Size(960, 56);
            panelBottom.TabIndex = 2;
            // 
            // flowBottom
            // 
            flowBottom.AutoSize = true;
            flowBottom.Controls.Add(lblNGram);
            flowBottom.Controls.Add(numNGram);
            flowBottom.Controls.Add(btnTrain);
            flowBottom.Dock = DockStyle.Fill;
            flowBottom.Location = new Point(16, 10);
            flowBottom.Margin = new Padding(0);
            flowBottom.Name = "flowBottom";
            flowBottom.Size = new Size(320, 38);
            flowBottom.TabIndex = 0;
            flowBottom.WrapContents = false;
            // 
            // lblNGram
            // 
            lblNGram.AutoSize = true;
            lblNGram.Font = new Font("Segoe UI", 9F);
            lblNGram.ForeColor = Color.FromArgb(51, 65, 85);
            lblNGram.Location = new Point(0, 10);
            lblNGram.Margin = new Padding(0, 10, 8, 0);
            lblNGram.Name = "lblNGram";
            lblNGram.Size = new Size(75, 15);
            lblNGram.TabIndex = 0;
            lblNGram.Text = "N-грамма (n)";
            // 
            // numNGram
            // 
            numNGram.Font = new Font("Segoe UI", 9F);
            numNGram.Location = new Point(83, 6);
            numNGram.Margin = new Padding(0, 6, 16, 0);
            numNGram.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            numNGram.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numNGram.Name = "numNGram";
            numNGram.Size = new Size(56, 23);
            numNGram.TabIndex = 1;
            numNGram.Value = new decimal(new int[] { 3, 0, 0, 0 });
            numNGram.ValueChanged += numNGram_ValueChanged;
            // 
            // btnTrain
            // 
            btnTrain.AutoSize = false;
            btnTrain.BackColor = Color.White;
            btnTrain.Cursor = Cursors.Hand;
            btnTrain.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            btnTrain.FlatAppearance.MouseDownBackColor = Color.FromArgb(241, 245, 249);
            btnTrain.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 250, 252);
            btnTrain.FlatStyle = FlatStyle.Flat;
            btnTrain.Font = new Font("Segoe UI", 9.75F);
            btnTrain.ForeColor = Color.FromArgb(51, 65, 85);
            btnTrain.Location = new Point(155, 0);
            btnTrain.Margin = new Padding(0);
            btnTrain.Name = "btnTrain";
            btnTrain.Size = new Size(140, 36);
            btnTrain.TabIndex = 2;
            btnTrain.Text = "Обучить модель";
            btnTrain.UseVisualStyleBackColor = false;
            btnTrain.Click += btnTrain_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(237, 241, 245);
            ClientSize = new Size(960, 640);
            Controls.Add(tableLayoutRoot);
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(800, 520);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "TextGenTest — n-граммы и марковская цепь";
            tableLayoutRoot.ResumeLayout(false);
            panelHeader.ResumeLayout(false);
            panelHeader.PerformLayout();
            flowHeader.ResumeLayout(false);
            flowHeader.PerformLayout();
            tableMain.ResumeLayout(false);
            tableLeft.ResumeLayout(false);
            tableLeft.PerformLayout();
            tableRight.ResumeLayout(false);
            tableRight.PerformLayout();
            flowSeed.ResumeLayout(false);
            flowSeed.PerformLayout();
            panelBottom.ResumeLayout(false);
            panelBottom.PerformLayout();
            flowBottom.ResumeLayout(false);
            flowBottom.PerformLayout();
            ((ISupportInitialize)numNGram).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel tableLayoutRoot;
        private Panel panelHeader;
        private FlowLayoutPanel flowHeader;
        private Label lblTitle;
        private Label lblHint;
        private Label lblStatus;
        private TableLayoutPanel tableMain;
        private TableLayoutPanel tableLeft;
        private Label lblCorpus;
        private RichTextBox richTextBox1;
        private TableLayoutPanel tableRight;
        private FlowLayoutPanel flowSeed;
        private Label lblSeed;
        private TextBox txtSeed;
        private Button btnGenerate;
        private Label lblOutput;
        private RichTextBox richTextBox2;
        private Panel panelBottom;
        private FlowLayoutPanel flowBottom;
        private Label lblNGram;
        private NumericUpDown numNGram;
        private Button btnTrain;
    }
}
