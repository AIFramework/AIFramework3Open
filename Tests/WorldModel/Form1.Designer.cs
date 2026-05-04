namespace WorldModel
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
            components = new System.ComponentModel.Container();
            tableLayoutRoot = new System.Windows.Forms.TableLayoutPanel();
            panelHeader = new System.Windows.Forms.Panel();
            flowHeader = new System.Windows.Forms.FlowLayoutPanel();
            lblTitle = new System.Windows.Forms.Label();
            lblHint = new System.Windows.Forms.Label();
            flowToolbar = new System.Windows.Forms.FlowLayoutPanel();
            lblTick = new System.Windows.Forms.Label();
            btnPause = new System.Windows.Forms.Button();
            lblStatus = new System.Windows.Forms.Label();
            tableGrid = new System.Windows.Forms.TableLayoutPanel();
            heatMapControl1 = new AI.Charts.WinForms.HeatMapControl();
            chartVisual1 = new AI.Charts.WinForms.ChartVisual();
            heatMapControl2 = new AI.Charts.WinForms.HeatMapControl();
            chartVisual2 = new AI.Charts.WinForms.ChartVisual();
            timer1 = new System.Windows.Forms.Timer(components);
            tableLayoutRoot.SuspendLayout();
            panelHeader.SuspendLayout();
            flowHeader.SuspendLayout();
            flowToolbar.SuspendLayout();
            tableGrid.SuspendLayout();
            SuspendLayout();
            // 
            // tableLayoutRoot
            // 
            tableLayoutRoot.ColumnCount = 1;
            tableLayoutRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutRoot.Controls.Add(panelHeader, 0, 0);
            tableLayoutRoot.Controls.Add(tableGrid, 0, 1);
            tableLayoutRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutRoot.Location = new System.Drawing.Point(0, 0);
            tableLayoutRoot.Margin = new System.Windows.Forms.Padding(0);
            tableLayoutRoot.Name = "tableLayoutRoot";
            tableLayoutRoot.RowCount = 2;
            tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle());
            tableLayoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutRoot.Size = new System.Drawing.Size(1100, 720);
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
            panelHeader.Padding = new System.Windows.Forms.Padding(20, 14, 20, 12);
            panelHeader.Size = new System.Drawing.Size(1100, 132);
            panelHeader.TabIndex = 0;
            panelHeader.Paint += panelHeader_Paint;
            // 
            // flowHeader
            // 
            flowHeader.AutoSize = true;
            flowHeader.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowHeader.Controls.Add(lblTitle);
            flowHeader.Controls.Add(lblHint);
            flowHeader.Controls.Add(flowToolbar);
            flowHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            flowHeader.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            flowHeader.Location = new System.Drawing.Point(20, 14);
            flowHeader.Margin = new System.Windows.Forms.Padding(0);
            flowHeader.Name = "flowHeader";
            flowHeader.Size = new System.Drawing.Size(1060, 106);
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
            lblTitle.Size = new System.Drawing.Size(380, 23);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "World model — две цепи Маркова (HMM)";
            // 
            // lblHint
            // 
            lblHint.AutoSize = true;
            lblHint.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblHint.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblHint.Location = new System.Drawing.Point(0, 29);
            lblHint.Margin = new System.Windows.Forms.Padding(0, 0, 0, 10);
            lblHint.MaximumSize = new System.Drawing.Size(1040, 0);
            lblHint.Name = "lblHint";
            lblHint.Size = new System.Drawing.Size(1038, 30);
            lblHint.TabIndex = 1;
            lblHint.Text = "Матрица переходов 512×512; тепловая карта 3×3 по коду Грея состояния; столбцы матрицы — нормированное распределение по шагу цепи.";
            // 
            // flowToolbar
            // 
            flowToolbar.AutoSize = true;
            flowToolbar.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowToolbar.Controls.Add(lblTick);
            flowToolbar.Controls.Add(btnPause);
            flowToolbar.Controls.Add(lblStatus);
            flowToolbar.Location = new System.Drawing.Point(0, 69);
            flowToolbar.Margin = new System.Windows.Forms.Padding(0);
            flowToolbar.Name = "flowToolbar";
            flowToolbar.Size = new System.Drawing.Size(600, 38);
            flowToolbar.TabIndex = 2;
            flowToolbar.WrapContents = false;
            // 
            // lblTick
            // 
            lblTick.AutoSize = true;
            lblTick.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            lblTick.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            lblTick.Location = new System.Drawing.Point(0, 10);
            lblTick.Margin = new System.Windows.Forms.Padding(0, 10, 16, 0);
            lblTick.Name = "lblTick";
            lblTick.Size = new System.Drawing.Size(28, 15);
            lblTick.TabIndex = 0;
            lblTick.Text = "s₁ · s₂";
            // 
            // btnPause
            // 
            btnPause.AutoSize = false;
            btnPause.BackColor = System.Drawing.Color.White;
            btnPause.Cursor = System.Windows.Forms.Cursors.Hand;
            btnPause.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(203, 213, 225);
            btnPause.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(241, 245, 249);
            btnPause.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            btnPause.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnPause.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            btnPause.ForeColor = System.Drawing.Color.FromArgb(51, 65, 85);
            btnPause.Location = new System.Drawing.Point(44, 0);
            btnPause.Margin = new System.Windows.Forms.Padding(0, 0, 16, 0);
            btnPause.Name = "btnPause";
            btnPause.Size = new System.Drawing.Size(120, 38);
            btnPause.TabIndex = 1;
            btnPause.Text = "Пауза";
            btnPause.UseVisualStyleBackColor = false;
            btnPause.Click += btnPause_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblStatus.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
            lblStatus.Location = new System.Drawing.Point(180, 11);
            lblStatus.Margin = new System.Windows.Forms.Padding(0, 11, 0, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(72, 15);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Инициализация…";
            // 
            // tableGrid
            // 
            tableGrid.BackColor = System.Drawing.Color.FromArgb(237, 241, 245);
            tableGrid.ColumnCount = 2;
            tableGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableGrid.Controls.Add(heatMapControl1, 0, 0);
            tableGrid.Controls.Add(chartVisual1, 1, 0);
            tableGrid.Controls.Add(heatMapControl2, 0, 1);
            tableGrid.Controls.Add(chartVisual2, 1, 1);
            tableGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            tableGrid.Location = new System.Drawing.Point(0, 132);
            tableGrid.Margin = new System.Windows.Forms.Padding(0);
            tableGrid.Name = "tableGrid";
            tableGrid.Padding = new System.Windows.Forms.Padding(10);
            tableGrid.RowCount = 2;
            tableGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            tableGrid.Size = new System.Drawing.Size(1100, 588);
            tableGrid.TabIndex = 1;
            // 
            // heatMapControl1
            // 
            heatMapControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            heatMapControl1.Location = new System.Drawing.Point(13, 13);
            heatMapControl1.Margin = new System.Windows.Forms.Padding(3);
            heatMapControl1.Name = "heatMapControl1";
            heatMapControl1.Size = new System.Drawing.Size(531, 278);
            heatMapControl1.TabIndex = 0;
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
            chartVisual1.Location = new System.Drawing.Point(550, 13);
            chartVisual1.Margin = new System.Windows.Forms.Padding(3);
            chartVisual1.Name = "chartVisual1";
            chartVisual1.Size = new System.Drawing.Size(531, 278);
            chartVisual1.TabIndex = 1;
            // 
            // heatMapControl2
            // 
            heatMapControl2.Dock = System.Windows.Forms.DockStyle.Fill;
            heatMapControl2.Location = new System.Drawing.Point(13, 297);
            heatMapControl2.Margin = new System.Windows.Forms.Padding(3);
            heatMapControl2.Name = "heatMapControl2";
            heatMapControl2.Size = new System.Drawing.Size(531, 278);
            heatMapControl2.TabIndex = 2;
            // 
            // chartVisual2
            // 
            chartVisual2.AutoScroll = true;
            chartVisual2.BackColor = System.Drawing.Color.White;
            chartVisual2.ChartName = "График";
            chartVisual2.Dock = System.Windows.Forms.DockStyle.Fill;
            chartVisual2.ForeColor = System.Drawing.Color.Black;
            chartVisual2.IsContextMenu = true;
            chartVisual2.IsLogScale = false;
            chartVisual2.IsMoove = true;
            chartVisual2.IsScale = true;
            chartVisual2.IsShowXY = true;
            chartVisual2.LabelX = "Ось X";
            chartVisual2.LabelY = "Ось Y";
            chartVisual2.Location = new System.Drawing.Point(550, 297);
            chartVisual2.Margin = new System.Windows.Forms.Padding(3);
            chartVisual2.Name = "chartVisual2";
            chartVisual2.Size = new System.Drawing.Size(531, 278);
            chartVisual2.TabIndex = 3;
            // 
            // timer1
            // 
            timer1.Interval = 300;
            timer1.Tick += timer1_Tick;
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(237, 241, 245);
            ClientSize = new System.Drawing.Size(1100, 720);
            Controls.Add(tableLayoutRoot);
            Font = new System.Drawing.Font("Segoe UI", 9F);
            MinimumSize = new System.Drawing.Size(900, 600);
            Name = "Form1";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "WorldModel — HMM и тепловые карты";
            Load += Form1_Load;
            tableLayoutRoot.ResumeLayout(false);
            panelHeader.ResumeLayout(false);
            panelHeader.PerformLayout();
            flowHeader.ResumeLayout(false);
            flowHeader.PerformLayout();
            flowToolbar.ResumeLayout(false);
            flowToolbar.PerformLayout();
            tableGrid.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutRoot;
        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.FlowLayoutPanel flowHeader;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblHint;
        private System.Windows.Forms.FlowLayoutPanel flowToolbar;
        private System.Windows.Forms.Label lblTick;
        private System.Windows.Forms.Button btnPause;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TableLayoutPanel tableGrid;
        private AI.Charts.WinForms.HeatMapControl heatMapControl1;
        private AI.Charts.WinForms.ChartVisual chartVisual1;
        private AI.Charts.WinForms.HeatMapControl heatMapControl2;
        private AI.Charts.WinForms.ChartVisual chartVisual2;
        private System.Windows.Forms.Timer timer1;
    }
}
