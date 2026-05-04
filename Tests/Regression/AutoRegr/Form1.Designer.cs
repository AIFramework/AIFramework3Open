namespace AutoRegr
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
            this.tableLayoutRoot = new System.Windows.Forms.TableLayoutPanel();
            this.panelHeader = new System.Windows.Forms.Panel();
            this.flowHeader = new System.Windows.Forms.FlowLayoutPanel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblMetrics = new System.Windows.Forms.Label();
            this.btnRepeat = new System.Windows.Forms.Button();
            this.chartVisual1 = new AI.Charts.WinForms.ChartVisual();
            this.tableLayoutRoot.SuspendLayout();
            this.panelHeader.SuspendLayout();
            this.flowHeader.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutRoot
            // 
            this.tableLayoutRoot.ColumnCount = 1;
            this.tableLayoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutRoot.Controls.Add(this.panelHeader, 0, 0);
            this.tableLayoutRoot.Controls.Add(this.chartVisual1, 0, 1);
            this.tableLayoutRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutRoot.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutRoot.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutRoot.Name = "tableLayoutRoot";
            this.tableLayoutRoot.RowCount = 2;
            this.tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutRoot.Size = new System.Drawing.Size(1004, 641);
            this.tableLayoutRoot.TabIndex = 0;
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.Color.FromArgb(250, 251, 252);
            this.panelHeader.Controls.Add(this.flowHeader);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Margin = new System.Windows.Forms.Padding(0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Padding = new System.Windows.Forms.Padding(16, 14, 16, 10);
            this.panelHeader.Size = new System.Drawing.Size(1004, 120);
            this.panelHeader.TabIndex = 0;
            // 
            // flowHeader
            // 
            this.flowHeader.AutoSize = true;
            this.flowHeader.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowHeader.Controls.Add(this.lblTitle);
            this.flowHeader.Controls.Add(this.lblStatus);
            this.flowHeader.Controls.Add(this.lblMetrics);
            this.flowHeader.Controls.Add(this.btnRepeat);
            this.flowHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowHeader.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowHeader.Location = new System.Drawing.Point(16, 14);
            this.flowHeader.Margin = new System.Windows.Forms.Padding(0);
            this.flowHeader.Name = "flowHeader";
            this.flowHeader.Size = new System.Drawing.Size(413, 93);
            this.flowHeader.TabIndex = 0;
            this.flowHeader.WrapContents = false;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(33, 37, 41);
            this.lblTitle.Location = new System.Drawing.Point(0, 0);
            this.lblTitle.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(413, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Авторегрессия (AR) — синтетический ряд и прогноз";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblStatus.ForeColor = System.Drawing.Color.DimGray;
            this.lblStatus.Location = new System.Drawing.Point(0, 24);
            this.lblStatus.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(58, 15);
            this.lblStatus.TabIndex = 1;
            this.lblStatus.Text = "Загрузка…";
            // 
            // lblMetrics
            // 
            this.lblMetrics.AutoSize = true;
            this.lblMetrics.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblMetrics.ForeColor = System.Drawing.Color.FromArgb(73, 80, 87);
            this.lblMetrics.Location = new System.Drawing.Point(0, 45);
            this.lblMetrics.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblMetrics.Name = "lblMetrics";
            this.lblMetrics.Size = new System.Drawing.Size(0, 15);
            this.lblMetrics.TabIndex = 2;
            // 
            // btnRepeat
            // 
            this.btnRepeat.AutoSize = true;
            this.btnRepeat.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.btnRepeat.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnRepeat.Location = new System.Drawing.Point(0, 68);
            this.btnRepeat.Margin = new System.Windows.Forms.Padding(0);
            this.btnRepeat.Name = "btnRepeat";
            this.btnRepeat.Size = new System.Drawing.Size(140, 25);
            this.btnRepeat.TabIndex = 3;
            this.btnRepeat.Text = "Повторить с новым шумом";
            this.btnRepeat.UseVisualStyleBackColor = true;
            // 
            // chartVisual1
            // 
            this.chartVisual1.AutoScroll = true;
            this.chartVisual1.BackColor = System.Drawing.Color.White;
            this.chartVisual1.ChartName = "График";
            this.chartVisual1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartVisual1.ForeColor = System.Drawing.Color.Black;
            this.chartVisual1.IsContextMenu = true;
            this.chartVisual1.IsLogScale = false;
            this.chartVisual1.IsMoove = true;
            this.chartVisual1.IsScale = true;
            this.chartVisual1.IsShowXY = true;
            this.chartVisual1.LabelX = "Ось X";
            this.chartVisual1.LabelY = "Ось Y";
            this.chartVisual1.Location = new System.Drawing.Point(0, 120);
            this.chartVisual1.Margin = new System.Windows.Forms.Padding(0);
            this.chartVisual1.Name = "chartVisual1";
            this.chartVisual1.Size = new System.Drawing.Size(1004, 521);
            this.chartVisual1.TabIndex = 1;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(245, 246, 248);
            this.ClientSize = new System.Drawing.Size(1004, 641);
            this.Controls.Add(this.tableLayoutRoot);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(720, 480);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "AutoRegr — авторегрессия";
            this.tableLayoutRoot.ResumeLayout(false);
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.flowHeader.ResumeLayout(false);
            this.flowHeader.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutRoot;
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.FlowLayoutPanel flowHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblMetrics;
        private System.Windows.Forms.Button btnRepeat;
        private AI.Charts.WinForms.ChartVisual chartVisual1;
    }
}
