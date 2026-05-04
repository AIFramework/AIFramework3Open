namespace MCMCTest
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
            flowHeader = new System.Windows.Forms.FlowLayoutPanel();
            lblTitle = new System.Windows.Forms.Label();
            lblHint = new System.Windows.Forms.Label();
            lblStatus = new System.Windows.Forms.Label();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnHistogram = new System.Windows.Forms.Button();
            btnIntegral = new System.Windows.Forms.Button();
            panelContent = new System.Windows.Forms.Panel();
            chartVisual1 = new AI.Charts.WinForms.ChartVisual();
            tableLayoutRoot.SuspendLayout();
            panelHeader.SuspendLayout();
            flowHeader.SuspendLayout();
            flowActions.SuspendLayout();
            panelContent.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutRoot
            // 
            tableLayoutRoot.ColumnCount = 1;
            tableLayoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutRoot.Controls.Add(panelHeader, 0, 0);
            tableLayoutRoot.Controls.Add(flowActions, 0, 1);
            tableLayoutRoot.Controls.Add(panelContent, 0, 2);
            tableLayoutRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutRoot.Location = new System.Drawing.Point(0, 0);
            tableLayoutRoot.Margin = new System.Windows.Forms.Padding(0);
            tableLayoutRoot.Name = "tableLayoutRoot";
            tableLayoutRoot.RowCount = 3;
            tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutRoot.Size = new System.Drawing.Size(960, 640);
            tableLayoutRoot.TabIndex = 0;
            // 
            // panelHeader
            // 
            panelHeader.BackColor = System.Drawing.Color.White;
            panelHeader.Controls.Add(flowHeader);
            panelHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            panelHeader.Location = new System.Drawing.Point(0, 0);
            panelHeader.Margin = new System.Windows.Forms.Padding(0);
            panelHeader.Name = "panelHeader";
            panelHeader.Padding = new System.Windows.Forms.Padding(20, 16, 20, 14);
            panelHeader.Size = new System.Drawing.Size(960, 118);
            panelHeader.TabIndex = 0;
            panelHeader.Paint += panelHeader_Paint;
            // 
            // flowHeader
            // 
            flowHeader.AutoSize = true;
            flowHeader.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowHeader.Controls.Add(lblTitle);
            flowHeader.Controls.Add(lblHint);
            flowHeader.Controls.Add(lblStatus);
            flowHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            flowHeader.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            flowHeader.Location = new System.Drawing.Point(20, 16);
            flowHeader.Margin = new System.Windows.Forms.Padding(0);
            flowHeader.Name = "flowHeader";
            flowHeader.Size = new System.Drawing.Size(920, 88);
            flowHeader.TabIndex = 0;
            flowHeader.WrapContents = false;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new System.Drawing.Font("Segoe UI", 12.75F, System.Drawing.FontStyle.Bold);
            lblTitle.ForeColor = System.Drawing.Color.FromArgb(15, 23, 42);
            lblTitle.Location = new System.Drawing.Point(0, 0);
            lblTitle.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new System.Drawing.Size(420, 23);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "Монте-Карло и цепи Маркова (MCMC)";
            // 
            // lblHint
            // 
            lblHint.AutoSize = true;
            lblHint.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblHint.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblHint.Location = new System.Drawing.Point(0, 29);
            lblHint.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            lblHint.MaximumSize = new System.Drawing.Size(900, 0);
            lblHint.Name = "lblHint";
            lblHint.Size = new System.Drawing.Size(898, 30);
            lblHint.TabIndex = 1;
            lblHint.Text = "Гистограмма: выборка из плотности ∝ exp(−(x⁴−2x²)/2). Интеграл: сравнение точного значения и оценки ∫ f(x)dx методом Монте-Карло.";
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblStatus.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblStatus.Location = new System.Drawing.Point(0, 67);
            lblStatus.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(52, 15);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Загрузка";
            // 
            // flowActions
            // 
            flowActions.AutoSize = true;
            flowActions.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            flowActions.Controls.Add(btnHistogram);
            flowActions.Controls.Add(btnIntegral);
            flowActions.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions.Location = new System.Drawing.Point(0, 118);
            flowActions.Margin = new System.Windows.Forms.Padding(0);
            flowActions.Name = "flowActions";
            flowActions.Padding = new System.Windows.Forms.Padding(16, 10, 16, 10);
            flowActions.Size = new System.Drawing.Size(960, 58);
            flowActions.TabIndex = 1;
            flowActions.WrapContents = false;
            // 
            // btnHistogram
            // 
            btnHistogram.AutoSize = false;
            btnHistogram.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            btnHistogram.Cursor = System.Windows.Forms.Cursors.Hand;
            btnHistogram.FlatAppearance.BorderSize = 0;
            btnHistogram.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(30, 64, 175);
            btnHistogram.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(29, 78, 216);
            btnHistogram.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnHistogram.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            btnHistogram.ForeColor = System.Drawing.Color.White;
            btnHistogram.Location = new System.Drawing.Point(16, 10);
            btnHistogram.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            btnHistogram.MinimumSize = new System.Drawing.Size(220, 38);
            btnHistogram.Name = "btnHistogram";
            btnHistogram.Size = new System.Drawing.Size(240, 38);
            btnHistogram.TabIndex = 0;
            btnHistogram.Text = "MCMC: гистограмма и плотность";
            btnHistogram.UseVisualStyleBackColor = false;
            btnHistogram.Click += btnHistogram_Click;
            // 
            // btnIntegral
            // 
            btnIntegral.AutoSize = false;
            btnIntegral.BackColor = System.Drawing.Color.White;
            btnIntegral.Cursor = System.Windows.Forms.Cursors.Hand;
            btnIntegral.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            btnIntegral.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(241, 245, 249);
            btnIntegral.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            btnIntegral.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnIntegral.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            btnIntegral.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            btnIntegral.Location = new System.Drawing.Point(268, 10);
            btnIntegral.Margin = new System.Windows.Forms.Padding(0);
            btnIntegral.MinimumSize = new System.Drawing.Size(260, 38);
            btnIntegral.Name = "btnIntegral";
            btnIntegral.Size = new System.Drawing.Size(320, 38);
            btnIntegral.TabIndex = 1;
            btnIntegral.Text = "Интегрирование методом Монте-Карло";
            btnIntegral.UseVisualStyleBackColor = false;
            btnIntegral.Click += btnIntegral_Click;
            // 
            // panelContent
            // 
            panelContent.BackColor = System.Drawing.Color.FromArgb(237, 241, 245);
            panelContent.Controls.Add(chartVisual1);
            panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent.Location = new System.Drawing.Point(0, 176);
            panelContent.Margin = new System.Windows.Forms.Padding(0);
            panelContent.Name = "panelContent";
            panelContent.Padding = new System.Windows.Forms.Padding(12);
            panelContent.Size = new System.Drawing.Size(960, 464);
            panelContent.TabIndex = 2;
            // 
            // chartVisual1
            // 
            chartVisual1.AutoScroll = true;
            chartVisual1.BackColor = System.Drawing.Color.White;
            chartVisual1.ChartName = "График";
            chartVisual1.Dock = System.Windows.Forms.DockStyle.Fill;
            chartVisual1.ForeColor = System.Drawing.Color.Black;
            chartVisual1.IsContextMenu = true;
            chartVisual1.IsLogScale = false;
            chartVisual1.IsMoove = true;
            chartVisual1.IsScale = true;
            chartVisual1.IsShowXY = true;
            chartVisual1.LabelX = "Ось X";
            chartVisual1.LabelY = "Ось Y";
            chartVisual1.Location = new System.Drawing.Point(12, 12);
            chartVisual1.Margin = new System.Windows.Forms.Padding(0);
            chartVisual1.Name = "chartVisual1";
            chartVisual1.Size = new System.Drawing.Size(936, 440);
            chartVisual1.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(237, 241, 245);
            ClientSize = new System.Drawing.Size(960, 640);
            Controls.Add(tableLayoutRoot);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            MinimumSize = new System.Drawing.Size(720, 480);
            Name = "Form1";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "MCMCTest — Монте-Карло и MCMC";
            tableLayoutRoot.ResumeLayout(false);
            tableLayoutRoot.PerformLayout();
            panelHeader.ResumeLayout(false);
            panelHeader.PerformLayout();
            flowHeader.ResumeLayout(false);
            flowHeader.PerformLayout();
            flowActions.ResumeLayout(false);
            panelContent.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutRoot;
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.FlowLayoutPanel flowHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnHistogram;
        private System.Windows.Forms.Button btnIntegral;
        private System.Windows.Forms.Panel panelContent;
        private AI.Charts.WinForms.ChartVisual chartVisual1;
    }
}
