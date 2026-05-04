namespace ModulationTest
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
            this.label1 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.labelModulation = new System.Windows.Forms.Label();
            this.comboBoxModulation = new System.Windows.Forms.ComboBox();
            this.button1 = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.textBoxDecoded = new System.Windows.Forms.TextBox();
            this.chartVisual1 = new AI.Charts.WinForms.ChartVisual();
            this.chartVisual2 = new AI.Charts.WinForms.ChartVisual();
            this.chartVisual3 = new AI.Charts.WinForms.ChartVisual();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panelTop = new System.Windows.Forms.Panel();
            this.tableLayoutPanel1.SuspendLayout();
            this.panelTop.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(9, 23);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(101, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "Введите строку:";
            // 
            // textBox1
            // 
            this.textBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.textBox1.ForeColor = System.Drawing.Color.White;
            this.textBox1.Location = new System.Drawing.Point(118, 20);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(200, 25);
            this.textBox1.TabIndex = 1;
            this.textBox1.Text = "Hello, Signal!";
            // 
            // labelModulation
            // 
            this.labelModulation.AutoSize = true;
            this.labelModulation.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.labelModulation.ForeColor = System.Drawing.Color.White;
            this.labelModulation.Location = new System.Drawing.Point(325, 23);
            this.labelModulation.Name = "labelModulation";
            this.labelModulation.Size = new System.Drawing.Size(79, 17);
            this.labelModulation.TabIndex = 2;
            this.labelModulation.Text = "Модуляция:";
            // 
            // comboBoxModulation
            // 
            this.comboBoxModulation.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.comboBoxModulation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxModulation.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboBoxModulation.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.comboBoxModulation.ForeColor = System.Drawing.Color.White;
            this.comboBoxModulation.FormattingEnabled = true;
            this.comboBoxModulation.Location = new System.Drawing.Point(405, 20);
            this.comboBoxModulation.Name = "comboBoxModulation";
            this.comboBoxModulation.Size = new System.Drawing.Size(100, 25);
            this.comboBoxModulation.TabIndex = 3;
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(122)))), ((int)(((byte)(204)))));
            this.button1.FlatAppearance.BorderSize = 0;
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.button1.ForeColor = System.Drawing.Color.White;
            this.button1.Location = new System.Drawing.Point(515, 19);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(150, 27);
            this.button1.TabIndex = 4;
            this.button1.Text = "Модуляция и Декод.";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(675, 23);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(158, 17);
            this.label2.TabIndex = 5;
            this.label2.Text = "Декодированный сигнал:";
            // 
            // textBoxDecoded
            // 
            this.textBoxDecoded.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.textBoxDecoded.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxDecoded.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.textBoxDecoded.ForeColor = System.Drawing.Color.White;
            this.textBoxDecoded.Location = new System.Drawing.Point(840, 20);
            this.textBoxDecoded.Name = "textBoxDecoded";
            this.textBoxDecoded.ReadOnly = true;
            this.textBoxDecoded.Size = new System.Drawing.Size(320, 25);
            this.textBoxDecoded.TabIndex = 6;
            // 
            // chartVisual1
            // 
            this.chartVisual1.AutoScroll = true;
            this.chartVisual1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.chartVisual1.ChartName = "Модулированный сигнал";
            this.tableLayoutPanel1.SetColumnSpan(this.chartVisual1, 2);
            this.chartVisual1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartVisual1.ForeColor = System.Drawing.Color.White;
            this.chartVisual1.IsContextMenu = true;
            this.chartVisual1.IsLogScale = false;
            this.chartVisual1.IsMoove = true;
            this.chartVisual1.IsScale = true;
            this.chartVisual1.IsShowXY = true;
            this.chartVisual1.LabelX = "Время (отсчеты)";
            this.chartVisual1.LabelY = "Амплитуда";
            this.chartVisual1.Location = new System.Drawing.Point(5, 75);
            this.chartVisual1.Margin = new System.Windows.Forms.Padding(5);
            this.chartVisual1.Name = "chartVisual1";
            this.chartVisual1.Size = new System.Drawing.Size(1174, 266);
            this.chartVisual1.TabIndex = 7;
            // 
            // chartVisual2
            // 
            this.chartVisual2.AutoScroll = true;
            this.chartVisual2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.chartVisual2.ChartName = "Созвездие (IQ)";
            this.chartVisual2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartVisual2.ForeColor = System.Drawing.Color.White;
            this.chartVisual2.IsContextMenu = true;
            this.chartVisual2.IsLogScale = false;
            this.chartVisual2.IsMoove = true;
            this.chartVisual2.IsScale = true;
            this.chartVisual2.IsShowXY = true;
            this.chartVisual2.LabelX = "In-Phase (I)";
            this.chartVisual2.LabelY = "Quadrature (Q)";
            this.chartVisual2.Location = new System.Drawing.Point(5, 351);
            this.chartVisual2.Margin = new System.Windows.Forms.Padding(5);
            this.chartVisual2.Name = "chartVisual2";
            this.chartVisual2.Size = new System.Drawing.Size(404, 405);
            this.chartVisual2.TabIndex = 8;
            // 
            // chartVisual3
            // 
            this.chartVisual3.AutoScroll = true;
            this.chartVisual3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.chartVisual3.ChartName = "Синфазная и квадратурная составляющие";
            this.chartVisual3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartVisual3.ForeColor = System.Drawing.Color.White;
            this.chartVisual3.IsContextMenu = true;
            this.chartVisual3.IsLogScale = false;
            this.chartVisual3.IsMoove = true;
            this.chartVisual3.IsScale = true;
            this.chartVisual3.IsShowXY = true;
            this.chartVisual3.LabelX = "Время (отсчеты)";
            this.chartVisual3.LabelY = "Амплитуда";
            this.chartVisual3.Location = new System.Drawing.Point(419, 351);
            this.chartVisual3.Margin = new System.Windows.Forms.Padding(5);
            this.chartVisual3.Name = "chartVisual3";
            this.chartVisual3.Size = new System.Drawing.Size(760, 405);
            this.chartVisual3.TabIndex = 9;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.tableLayoutPanel1.Controls.Add(this.panelTop, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.chartVisual1, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.chartVisual2, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.chartVisual3, 1, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1184, 761);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // panelTop
            // 
            this.panelTop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(37)))), ((int)(((byte)(37)))), ((int)(((byte)(38)))));
            this.tableLayoutPanel1.SetColumnSpan(this.panelTop, 2);
            this.panelTop.Controls.Add(this.label1);
            this.panelTop.Controls.Add(this.textBox1);
            this.panelTop.Controls.Add(this.labelModulation);
            this.panelTop.Controls.Add(this.comboBoxModulation);
            this.panelTop.Controls.Add(this.button1);
            this.panelTop.Controls.Add(this.label2);
            this.panelTop.Controls.Add(this.textBoxDecoded);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelTop.Location = new System.Drawing.Point(3, 3);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1178, 64);
            this.panelTop.TabIndex = 0;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.ClientSize = new System.Drawing.Size(1184, 761);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.MinimumSize = new System.Drawing.Size(1000, 600);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Анализ модуляции сигнала";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label labelModulation;
        private System.Windows.Forms.ComboBox comboBoxModulation;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxDecoded;
        private AI.Charts.WinForms.ChartVisual chartVisual1;
        private AI.Charts.WinForms.ChartVisual chartVisual2;
        private AI.Charts.WinForms.ChartVisual chartVisual3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panelTop;
    }
}