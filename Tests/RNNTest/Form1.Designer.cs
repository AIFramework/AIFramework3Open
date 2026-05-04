namespace RNNTest
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
            this.chartVisual1 = new AI.Charts.WinForms.ChartVisual();
            this.chartVisual2 = new AI.Charts.WinForms.ChartVisual();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.progressBarTraining = new System.Windows.Forms.ProgressBar();
            this.labelTraining = new System.Windows.Forms.Label();
            this.comboDevice = new System.Windows.Forms.ComboBox();
            this.labelDevice = new System.Windows.Forms.Label();
            this.labelTiming = new System.Windows.Forms.Label();
            this.comboBatch = new System.Windows.Forms.ComboBox();
            this.labelBatch = new System.Windows.Forms.Label();
            this.comboArch = new System.Windows.Forms.ComboBox();
            this.labelArch = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // chartVisual1
            // 
            this.chartVisual1.AutoScroll = true;
            this.chartVisual1.BackColor = System.Drawing.Color.White;
            this.chartVisual1.ChartName = "Вход";
            this.chartVisual1.ForeColor = System.Drawing.Color.Black;
            this.chartVisual1.IsContextMenu = true;
            this.chartVisual1.IsLogScale = false;
            this.chartVisual1.IsMoove = true;
            this.chartVisual1.IsScale = true;
            this.chartVisual1.IsShowXY = true;
            this.chartVisual1.LabelX = "Ось Х";
            this.chartVisual1.LabelY = "Ось Y";
            this.chartVisual1.Location = new System.Drawing.Point(13, 40);
            this.chartVisual1.Name = "chartVisual1";
            this.chartVisual1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.chartVisual1.Size = new System.Drawing.Size(775, 320);
            this.chartVisual1.TabIndex = 0;
            // 
            // chartVisual2
            // 
            this.chartVisual2.AutoScroll = true;
            this.chartVisual2.BackColor = System.Drawing.Color.White;
            this.chartVisual2.ChartName = "Выход";
            this.chartVisual2.ForeColor = System.Drawing.Color.Black;
            this.chartVisual2.IsContextMenu = true;
            this.chartVisual2.IsLogScale = false;
            this.chartVisual2.IsMoove = true;
            this.chartVisual2.IsScale = true;
            this.chartVisual2.IsShowXY = true;
            this.chartVisual2.LabelX = "Ось Х";
            this.chartVisual2.LabelY = "Ось Y";
            this.chartVisual2.Location = new System.Drawing.Point(13, 370);
            this.chartVisual2.Name = "chartVisual2";
            this.chartVisual2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.chartVisual2.Size = new System.Drawing.Size(775, 320);
            this.chartVisual2.TabIndex = 0;
            // 
            // comboDevice
            // 
            this.comboDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDevice.Location = new System.Drawing.Point(310, 10);
            this.comboDevice.Name = "comboDevice";
            this.comboDevice.Size = new System.Drawing.Size(240, 23);
            this.comboDevice.TabIndex = 10;
            // 
            // labelDevice
            // 
            this.labelDevice.AutoSize = true;
            this.labelDevice.Location = new System.Drawing.Point(190, 14);
            this.labelDevice.Name = "labelDevice";
            this.labelDevice.Size = new System.Drawing.Size(70, 15);
            this.labelDevice.TabIndex = 11;
            this.labelDevice.Text = "Устройство:";
            // 
            // comboBatch
            // 
            this.comboBatch.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBatch.Location = new System.Drawing.Point(80, 10);
            this.comboBatch.Name = "comboBatch";
            this.comboBatch.Size = new System.Drawing.Size(90, 23);
            this.comboBatch.TabIndex = 13;
            // 
            // labelBatch
            // 
            this.labelBatch.AutoSize = true;
            this.labelBatch.Location = new System.Drawing.Point(13, 14);
            this.labelBatch.Name = "labelBatch";
            this.labelBatch.Size = new System.Drawing.Size(60, 15);
            this.labelBatch.TabIndex = 14;
            this.labelBatch.Text = "Батч:";
            // 
            // comboArch
            // 
            this.comboArch.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboArch.Location = new System.Drawing.Point(640, 10);
            this.comboArch.Name = "comboArch";
            this.comboArch.Size = new System.Drawing.Size(148, 23);
            this.comboArch.TabIndex = 15;
            // 
            // labelArch
            // 
            this.labelArch.AutoSize = true;
            this.labelArch.Location = new System.Drawing.Point(560, 14);
            this.labelArch.Name = "labelArch";
            this.labelArch.Size = new System.Drawing.Size(80, 15);
            this.labelArch.TabIndex = 16;
            this.labelArch.Text = "Архитектура:";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(13, 700);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(100, 28);
            this.button1.TabIndex = 1;
            this.button1.Text = "Обучить";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(120, 700);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(100, 28);
            this.button2.TabIndex = 2;
            this.button2.Text = "Фаза +0.1";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // labelTiming
            // 
            this.labelTiming.AutoSize = true;
            this.labelTiming.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.labelTiming.ForeColor = System.Drawing.Color.DarkBlue;
            this.labelTiming.Location = new System.Drawing.Point(13, 758);
            this.labelTiming.Name = "labelTiming";
            this.labelTiming.Size = new System.Drawing.Size(0, 15);
            this.labelTiming.TabIndex = 12;
            this.labelTiming.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            // 
            // progressBarTraining
            // 
            this.progressBarTraining.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBarTraining.Location = new System.Drawing.Point(13, 735);
            this.progressBarTraining.Name = "progressBarTraining";
            this.progressBarTraining.Size = new System.Drawing.Size(775, 18);
            this.progressBarTraining.TabIndex = 3;
            // 
            // labelTraining
            // 
            this.labelTraining.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelTraining.AutoSize = true;
            this.labelTraining.Location = new System.Drawing.Point(233, 710);
            this.labelTraining.Name = "labelTraining";
            this.labelTraining.Size = new System.Drawing.Size(0, 15);
            this.labelTraining.TabIndex = 4;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 785);
            this.Controls.Add(this.labelTiming);
            this.Controls.Add(this.labelArch);
            this.Controls.Add(this.comboArch);
            this.Controls.Add(this.labelBatch);
            this.Controls.Add(this.comboBatch);
            this.Controls.Add(this.labelDevice);
            this.Controls.Add(this.comboDevice);
            this.Controls.Add(this.labelTraining);
            this.Controls.Add(this.progressBarTraining);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.chartVisual2);
            this.Controls.Add(this.chartVisual1);
            this.Name = "Form1";
            this.Text = "Neural Network Demo — Фильтр / LSTM / GRU / Transformer";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private AI.Charts.WinForms.ChartVisual chartVisual1;
        private AI.Charts.WinForms.ChartVisual chartVisual2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.ProgressBar progressBarTraining;
        private System.Windows.Forms.Label labelTraining;
        private System.Windows.Forms.ComboBox comboDevice;
        private System.Windows.Forms.Label labelDevice;
        private System.Windows.Forms.Label labelTiming;
        private System.Windows.Forms.ComboBox comboBatch;
        private System.Windows.Forms.Label labelBatch;
        private System.Windows.Forms.ComboBox comboArch;
        private System.Windows.Forms.Label labelArch;
    }
}
