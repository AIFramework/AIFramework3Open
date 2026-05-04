namespace SpectrumAnalyzer
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

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
            tableLayoutRoot = new System.Windows.Forms.TableLayoutPanel();
            panelHeader = new System.Windows.Forms.Panel();
            tableHeader = new System.Windows.Forms.TableLayoutPanel();
            flowHeader = new System.Windows.Forms.FlowLayoutPanel();
            lblTitle = new System.Windows.Forms.Label();
            lblParams = new System.Windows.Forms.Label();
            lblStatus = new System.Windows.Forms.Label();
            btnAnalyze = new System.Windows.Forms.Button();
            panelContent = new System.Windows.Forms.Panel();
            spectrumWelchAnalyzer1 = new AI.Charts.WinForms.SpectrumWelchAnalyzer();
            tableLayoutRoot.SuspendLayout();
            panelHeader.SuspendLayout();
            tableHeader.SuspendLayout();
            flowHeader.SuspendLayout();
            panelContent.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutRoot
            // 
            tableLayoutRoot.ColumnCount = 1;
            tableLayoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutRoot.Controls.Add(panelHeader, 0, 0);
            tableLayoutRoot.Controls.Add(panelContent, 0, 1);
            tableLayoutRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutRoot.Location = new System.Drawing.Point(0, 0);
            tableLayoutRoot.Margin = new System.Windows.Forms.Padding(0);
            tableLayoutRoot.Name = "tableLayoutRoot";
            tableLayoutRoot.RowCount = 2;
            tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutRoot.Size = new System.Drawing.Size(1000, 580);
            tableLayoutRoot.TabIndex = 0;
            // 
            // panelHeader
            // 
            panelHeader.BackColor = System.Drawing.Color.White;
            panelHeader.Controls.Add(tableHeader);
            panelHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            panelHeader.Location = new System.Drawing.Point(0, 0);
            panelHeader.Margin = new System.Windows.Forms.Padding(0);
            panelHeader.Name = "panelHeader";
            panelHeader.Padding = new System.Windows.Forms.Padding(20, 18, 20, 16);
            panelHeader.Size = new System.Drawing.Size(1000, 128);
            panelHeader.TabIndex = 0;
            panelHeader.Paint += panelHeader_Paint;
            // 
            // tableHeader
            // 
            tableHeader.ColumnCount = 2;
            tableHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            tableHeader.Controls.Add(flowHeader, 0, 0);
            tableHeader.Controls.Add(btnAnalyze, 1, 0);
            tableHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            tableHeader.Location = new System.Drawing.Point(20, 18);
            tableHeader.Margin = new System.Windows.Forms.Padding(0);
            tableHeader.Name = "tableHeader";
            tableHeader.RowCount = 1;
            tableHeader.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableHeader.Size = new System.Drawing.Size(960, 94);
            tableHeader.TabIndex = 0;
            // 
            // flowHeader
            // 
            flowHeader.AutoSize = true;
            flowHeader.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowHeader.Controls.Add(lblTitle);
            flowHeader.Controls.Add(lblParams);
            flowHeader.Controls.Add(lblStatus);
            flowHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            flowHeader.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            flowHeader.Location = new System.Drawing.Point(0, 0);
            flowHeader.Margin = new System.Windows.Forms.Padding(0);
            flowHeader.Name = "flowHeader";
            flowHeader.Size = new System.Drawing.Size(727, 94);
            flowHeader.TabIndex = 0;
            flowHeader.WrapContents = false;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new System.Drawing.Font("Segoe UI", 12.75F, System.Drawing.FontStyle.Bold);
            lblTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblTitle.Location = new System.Drawing.Point(0, 0);
            lblTitle.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new System.Drawing.Size(360, 23);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Спектральный анализ (метод Уэлча)";
            // 
            // lblParams
            // 
            lblParams.AutoSize = true;
            lblParams.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblParams.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblParams.Location = new System.Drawing.Point(0, 31);
            lblParams.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            lblParams.MaximumSize = new System.Drawing.Size(720, 0);
            lblParams.Name = "lblParams";
            lblParams.Size = new System.Drawing.Size(0, 15);
            lblParams.TabIndex = 1;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblStatus.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblStatus.Location = new System.Drawing.Point(0, 52);
            lblStatus.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(64, 15);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Загрузка…";
            // 
            // btnAnalyze
            // 
            btnAnalyze.Anchor = System.Windows.Forms.AnchorStyles.Right;
            btnAnalyze.AutoSize = false;
            btnAnalyze.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            btnAnalyze.Cursor = System.Windows.Forms.Cursors.Hand;
            btnAnalyze.FlatAppearance.BorderSize = 0;
            btnAnalyze.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(30, 64, 175);
            btnAnalyze.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(29, 78, 216);
            btnAnalyze.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnAnalyze.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnAnalyze.ForeColor = System.Drawing.Color.White;
            btnAnalyze.Location = new System.Drawing.Point(743, 28);
            btnAnalyze.Margin = new System.Windows.Forms.Padding(16, 0, 0, 0);
            btnAnalyze.MinimumSize = new System.Drawing.Size(200, 38);
            btnAnalyze.Name = "btnAnalyze";
            btnAnalyze.Size = new System.Drawing.Size(217, 38);
            btnAnalyze.TabIndex = 3;
            btnAnalyze.Text = "Пересчитать спектр";
            btnAnalyze.UseVisualStyleBackColor = false;
            // 
            // panelContent
            // 
            panelContent.BackColor = System.Drawing.Color.FromArgb(237, 241, 245);
            panelContent.Controls.Add(spectrumWelchAnalyzer1);
            panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent.Location = new System.Drawing.Point(0, 128);
            panelContent.Margin = new System.Windows.Forms.Padding(0);
            panelContent.Name = "panelContent";
            panelContent.Padding = new System.Windows.Forms.Padding(12);
            panelContent.Size = new System.Drawing.Size(1000, 452);
            panelContent.TabIndex = 1;
            // 
            // spectrumWelchAnalyzer1
            // 
            spectrumWelchAnalyzer1.Dock = System.Windows.Forms.DockStyle.Fill;
            spectrumWelchAnalyzer1.FFTBlock = 4096;
            spectrumWelchAnalyzer1.FreqOffset = 0D;
            spectrumWelchAnalyzer1.Location = new System.Drawing.Point(12, 12);
            spectrumWelchAnalyzer1.Margin = new System.Windows.Forms.Padding(0);
            spectrumWelchAnalyzer1.Name = "spectrumWelchAnalyzer1";
            spectrumWelchAnalyzer1.Size = new System.Drawing.Size(976, 428);
            spectrumWelchAnalyzer1.SR = 4096;
            spectrumWelchAnalyzer1.TabIndex = 0;
            spectrumWelchAnalyzer1.WelchPSDTypeData = AI.DSP.Analyse.WelchPSDType.Db;
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(237, 241, 245);
            ClientSize = new System.Drawing.Size(1000, 580);
            Controls.Add(tableLayoutRoot);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            MinimumSize = new System.Drawing.Size(720, 420);
            Name = "Form1";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "SpectrumAnalyzer — спектр Уэлча";
            tableLayoutRoot.ResumeLayout(false);
            panelHeader.ResumeLayout(false);
            tableHeader.ResumeLayout(false);
            tableHeader.PerformLayout();
            flowHeader.ResumeLayout(false);
            flowHeader.PerformLayout();
            panelContent.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutRoot;
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.TableLayoutPanel tableHeader;
        private System.Windows.Forms.FlowLayoutPanel flowHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblParams;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnAnalyze;
        private System.Windows.Forms.Panel panelContent;
        private AI.Charts.WinForms.SpectrumWelchAnalyzer spectrumWelchAnalyzer1;
    }
}
