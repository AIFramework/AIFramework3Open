namespace AGC
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

        #region Код, автоматически созданный конструктором форм Windows

        private void InitializeComponent()
        {
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.comboBoxAgcType = new System.Windows.Forms.ComboBox();
            this.buttonProcess = new System.Windows.Forms.Button();
            this.chartVisualOriginal = new AI.Charts.WinForms.ChartVisual();
            this.chartVisualAgc = new AI.Charts.WinForms.ChartVisual();
            this.tableLayoutPanel1.SuspendLayout();
            this.panelTop.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.panelTop, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.chartVisualOriginal, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.chartVisualAgc, 0, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1008, 729);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // panelTop
            // 
            this.panelTop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(37)))), ((int)(((byte)(38)))));
            this.panelTop.Controls.Add(this.label1);
            this.panelTop.Controls.Add(this.comboBoxAgcType);
            this.panelTop.Controls.Add(this.buttonProcess);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTop.Location = new System.Drawing.Point(3, 3);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1002, 54);
            this.panelTop.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(9, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "Тип АРУ:";
            // 
            // comboBoxAgcType
            // 
            this.comboBoxAgcType.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.comboBoxAgcType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxAgcType.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboBoxAgcType.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.comboBoxAgcType.ForeColor = System.Drawing.Color.White;
            this.comboBoxAgcType.FormattingEnabled = true;
            this.comboBoxAgcType.Location = new System.Drawing.Point(80, 15);
            this.comboBoxAgcType.Name = "comboBoxAgcType";
            this.comboBoxAgcType.Size = new System.Drawing.Size(200, 25);
            this.comboBoxAgcType.TabIndex = 1;
            // 
            // buttonProcess
            // 
            this.buttonProcess.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(122)))), ((int)(((byte)(204)))));
            this.buttonProcess.FlatAppearance.BorderSize = 0;
            this.buttonProcess.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonProcess.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.buttonProcess.ForeColor = System.Drawing.Color.White;
            this.buttonProcess.Location = new System.Drawing.Point(300, 14);
            this.buttonProcess.Name = "buttonProcess";
            this.buttonProcess.Size = new System.Drawing.Size(250, 27);
            this.buttonProcess.TabIndex = 2;
            this.buttonProcess.Text = "Сгенерировать и обработать";
            this.buttonProcess.UseVisualStyleBackColor = false;
            this.buttonProcess.Click += new System.EventHandler(this.buttonProcess_Click);
            // 
            // chartVisualOriginal
            // 
            this.chartVisualOriginal.AutoScroll = true;
            this.chartVisualOriginal.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.chartVisualOriginal.ChartName = "Оригинальный сигнал (с резкими скачками амплитуды)";
            this.chartVisualOriginal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartVisualOriginal.ForeColor = System.Drawing.Color.White;
            this.chartVisualOriginal.IsContextMenu = true;
            this.chartVisualOriginal.IsLogScale = false;
            this.chartVisualOriginal.IsMoove = true;
            this.chartVisualOriginal.IsScale = true;
            this.chartVisualOriginal.IsShowXY = true;
            this.chartVisualOriginal.LabelX = "Время (отсчеты)";
            this.chartVisualOriginal.LabelY = "Амплитуда";
            this.chartVisualOriginal.Location = new System.Drawing.Point(5, 65);
            this.chartVisualOriginal.Margin = new System.Windows.Forms.Padding(5);
            this.chartVisualOriginal.Name = "chartVisualOriginal";
            this.chartVisualOriginal.Size = new System.Drawing.Size(998, 324);
            this.chartVisualOriginal.TabIndex = 3;
            // 
            // chartVisualAgc
            // 
            this.chartVisualAgc.AutoScroll = true;
            this.chartVisualAgc.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.chartVisualAgc.ChartName = "Сигнал после АРУ";
            this.chartVisualAgc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartVisualAgc.ForeColor = System.Drawing.Color.White;
            this.chartVisualAgc.IsContextMenu = true;
            this.chartVisualAgc.IsLogScale = false;
            this.chartVisualAgc.IsMoove = true;
            this.chartVisualAgc.IsScale = true;
            this.chartVisualAgc.IsShowXY = true;
            this.chartVisualAgc.LabelX = "Время (отсчеты)";
            this.chartVisualAgc.LabelY = "Амплитуда";
            this.chartVisualAgc.Location = new System.Drawing.Point(5, 399);
            this.chartVisualAgc.Margin = new System.Windows.Forms.Padding(5);
            this.chartVisualAgc.Name = "chartVisualAgc";
            this.chartVisualAgc.Size = new System.Drawing.Size(998, 325);
            this.chartVisualAgc.TabIndex = 4;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.ClientSize = new System.Drawing.Size(1008, 729);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Демонстрация АРУ (Automatic Gain Control)";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboBoxAgcType;
        private System.Windows.Forms.Button buttonProcess;
        private AI.Charts.WinForms.ChartVisual chartVisualOriginal;
        private AI.Charts.WinForms.ChartVisual chartVisualAgc;
    }
}