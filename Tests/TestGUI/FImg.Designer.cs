namespace TestGUI
{
    partial class FImg
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.btnLoad = new System.Windows.Forms.Button();
            this.btnPencil = new System.Windows.Forms.Button();
            this.btnHLine = new System.Windows.Forms.Button();
            this.btnWLine = new System.Windows.Forms.Button();
            this.btnSharpness = new System.Windows.Forms.Button();
            this.btnSmoothing = new System.Windows.Forms.Button();
            this.btnStd = new System.Windows.Forms.Button();
            this.btnMedian = new System.Windows.Forms.Button();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(784, 501);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // btnLoad
            // 
            this.btnLoad.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.btnLoad.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.btnLoad.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLoad.ForeColor = System.Drawing.Color.White;
            this.btnLoad.Location = new System.Drawing.Point(10, 10);
            this.btnLoad.Margin = new System.Windows.Forms.Padding(10, 10, 5, 10);
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(90, 30);
            this.btnLoad.TabIndex = 1;
            this.btnLoad.Text = "Загрузить";
            this.btnLoad.UseVisualStyleBackColor = false;
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);
            // 
            // btnPencil
            // 
            this.btnPencil.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.btnPencil.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.btnPencil.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPencil.ForeColor = System.Drawing.Color.White;
            this.btnPencil.Location = new System.Drawing.Point(110, 10);
            this.btnPencil.Margin = new System.Windows.Forms.Padding(5, 10, 5, 10);
            this.btnPencil.Name = "btnPencil";
            this.btnPencil.Size = new System.Drawing.Size(90, 30);
            this.btnPencil.TabIndex = 2;
            this.btnPencil.Text = "Карандаш";
            this.btnPencil.UseVisualStyleBackColor = false;
            this.btnPencil.Click += new System.EventHandler(this.btnPencil_Click);
            // 
            // btnHLine
            // 
            this.btnHLine.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.btnHLine.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.btnHLine.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnHLine.ForeColor = System.Drawing.Color.White;
            this.btnHLine.Location = new System.Drawing.Point(210, 10);
            this.btnHLine.Margin = new System.Windows.Forms.Padding(5, 10, 5, 10);
            this.btnHLine.Name = "btnHLine";
            this.btnHLine.Size = new System.Drawing.Size(90, 30);
            this.btnHLine.TabIndex = 3;
            this.btnHLine.Text = "Гор. линии";
            this.btnHLine.UseVisualStyleBackColor = false;
            this.btnHLine.Click += new System.EventHandler(this.btnHLine_Click);
            // 
            // btnWLine
            // 
            this.btnWLine.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.btnWLine.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.btnWLine.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnWLine.ForeColor = System.Drawing.Color.White;
            this.btnWLine.Location = new System.Drawing.Point(310, 10);
            this.btnWLine.Margin = new System.Windows.Forms.Padding(5, 10, 5, 10);
            this.btnWLine.Name = "btnWLine";
            this.btnWLine.Size = new System.Drawing.Size(90, 30);
            this.btnWLine.TabIndex = 4;
            this.btnWLine.Text = "Верт. линии";
            this.btnWLine.UseVisualStyleBackColor = false;
            this.btnWLine.Click += new System.EventHandler(this.btnWLine_Click);
            // 
            // btnSharpness
            // 
            this.btnSharpness.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.btnSharpness.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.btnSharpness.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSharpness.ForeColor = System.Drawing.Color.White;
            this.btnSharpness.Location = new System.Drawing.Point(410, 10);
            this.btnSharpness.Margin = new System.Windows.Forms.Padding(5, 10, 5, 10);
            this.btnSharpness.Name = "btnSharpness";
            this.btnSharpness.Size = new System.Drawing.Size(90, 30);
            this.btnSharpness.TabIndex = 5;
            this.btnSharpness.Text = "Резкость";
            this.btnSharpness.UseVisualStyleBackColor = false;
            this.btnSharpness.Click += new System.EventHandler(this.btnSharpness_Click);
            // 
            // btnSmoothing
            // 
            this.btnSmoothing.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.btnSmoothing.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.btnSmoothing.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSmoothing.ForeColor = System.Drawing.Color.White;
            this.btnSmoothing.Location = new System.Drawing.Point(510, 10);
            this.btnSmoothing.Margin = new System.Windows.Forms.Padding(5, 10, 5, 10);
            this.btnSmoothing.Name = "btnSmoothing";
            this.btnSmoothing.Size = new System.Drawing.Size(90, 30);
            this.btnSmoothing.TabIndex = 6;
            this.btnSmoothing.Text = "Сглаживание";
            this.btnSmoothing.UseVisualStyleBackColor = false;
            this.btnSmoothing.Click += new System.EventHandler(this.btnSmoothing_Click);
            // 
            // btnStd
            // 
            this.btnStd.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.btnStd.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.btnStd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStd.ForeColor = System.Drawing.Color.White;
            this.btnStd.Location = new System.Drawing.Point(610, 10);
            this.btnStd.Margin = new System.Windows.Forms.Padding(5, 10, 5, 10);
            this.btnStd.Name = "btnStd";
            this.btnStd.Size = new System.Drawing.Size(60, 30);
            this.btnStd.TabIndex = 7;
            this.btnStd.Text = "СКО";
            this.btnStd.UseVisualStyleBackColor = false;
            this.btnStd.Click += new System.EventHandler(this.btnStd_Click);
            // 
            // btnMedian
            // 
            this.btnMedian.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.btnMedian.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.btnMedian.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMedian.ForeColor = System.Drawing.Color.White;
            this.btnMedian.Location = new System.Drawing.Point(680, 10);
            this.btnMedian.Margin = new System.Windows.Forms.Padding(5, 10, 10, 10);
            this.btnMedian.Name = "btnMedian";
            this.btnMedian.Size = new System.Drawing.Size(70, 30);
            this.btnMedian.TabIndex = 8;
            this.btnMedian.Text = "Медиана";
            this.btnMedian.UseVisualStyleBackColor = false;
            this.btnMedian.Click += new System.EventHandler(this.btnMedian_Click);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.AutoSize = true;
            this.flowLayoutPanel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(28)))), ((int)(((byte)(28)))));
            this.flowLayoutPanel1.Controls.Add(this.btnLoad);
            this.flowLayoutPanel1.Controls.Add(this.btnPencil);
            this.flowLayoutPanel1.Controls.Add(this.btnHLine);
            this.flowLayoutPanel1.Controls.Add(this.btnWLine);
            this.flowLayoutPanel1.Controls.Add(this.btnSharpness);
            this.flowLayoutPanel1.Controls.Add(this.btnSmoothing);
            this.flowLayoutPanel1.Controls.Add(this.btnStd);
            this.flowLayoutPanel1.Controls.Add(this.btnMedian);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(0, 501);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(784, 60);
            this.flowLayoutPanel1.TabIndex = 9;
            this.flowLayoutPanel1.WrapContents = true;
            // 
            // FImg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.MinimumSize = new System.Drawing.Size(800, 400);
            this.Name = "FImg";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Фильтры изображений";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button btnLoad;
        private System.Windows.Forms.Button btnPencil;
        private System.Windows.Forms.Button btnHLine;
        private System.Windows.Forms.Button btnWLine;
        private System.Windows.Forms.Button btnSharpness;
        private System.Windows.Forms.Button btnSmoothing;
        private System.Windows.Forms.Button btnStd;
        private System.Windows.Forms.Button btnMedian;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
    }
}