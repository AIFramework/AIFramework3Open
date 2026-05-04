namespace AI.Charts.WinForms;

public partial class ChartVisual
{
    /// <summary>
    /// Designer variable used to keep track of non-visual components.
    /// </summary>
    private System.ComponentModel.IContainer components = null;
    private SkiaSharp.Views.Desktop.SKControl skChart;
    private System.Windows.Forms.ContextMenuStrip contextMenu;
    private System.Windows.Forms.ToolStripMenuItem сохранитьToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem отправитьИзображениеВБуферОбменаToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem выборФонаToolStripMenuItem;
    private System.Windows.Forms.Label labelXY;
    private System.Windows.Forms.ToolStripMenuItem масштабToolStripMenuItem;

    /// <summary>
    /// Disposes resources used by the control.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _backgroundSkImage?.Dispose();
            _backgroundSkImage = null;
            if (components != null)
            {
                components.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// This method is required for Windows Forms designer support.
    /// </summary>
    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.skChart = new SkiaSharp.Views.Desktop.SKControl();
        this.contextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
        this.сохранитьToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.отправитьИзображениеВБуферОбменаToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.выборФонаToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.newWindowOutp = new System.Windows.Forms.ToolStripMenuItem();
        this.преобразованияToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.спектрToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.гистограммаToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.diff = new System.Windows.Forms.ToolStripMenuItem();
        this.integ = new System.Windows.Forms.ToolStripMenuItem();
        this.масштабToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        this.labelXY = new System.Windows.Forms.Label();
        this.contextMenu.SuspendLayout();
        this.SuspendLayout();
        //
        // skChart
        //
        this.skChart.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
        | System.Windows.Forms.AnchorStyles.Left)
        | System.Windows.Forms.AnchorStyles.Right)));
        this.skChart.BackColor = System.Drawing.Color.White;
        this.skChart.ForeColor = System.Drawing.Color.Black;
        this.skChart.Location = new System.Drawing.Point(-2, 3);
        this.skChart.Name = "skChart";
        this.skChart.Size = new System.Drawing.Size(447, 301);
        this.skChart.TabIndex = 0;
        this.skChart.ContextMenuStrip = this.contextMenu;
        this.skChart.PaintSurface += this.SkChart_PaintSurface;
        this.skChart.MouseMove += new System.Windows.Forms.MouseEventHandler(this.SkChart_MouseMove);
        this.skChart.MouseUp += new System.Windows.Forms.MouseEventHandler(this.SkChart_MouseUp);
        this.skChart.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.Chart1_MouseWheel);
        this.skChart.BackColorChanged += new System.EventHandler(this.SkChart_BackColorChanged);
        this.skChart.ForeColorChanged += new System.EventHandler(this.SkChart_ForeColorChanged);
        //
        // contextMenu
        //
        this.contextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
        this.сохранитьToolStripMenuItem,
        this.отправитьИзображениеВБуферОбменаToolStripMenuItem,
        this.выборФонаToolStripMenuItem,
        this.newWindowOutp,
        this.преобразованияToolStripMenuItem,
        this.масштабToolStripMenuItem});
        this.contextMenu.Name = "contextMenu";
        this.contextMenu.Size = new System.Drawing.Size(302, 136);
        //
        // сохранитьToolStripMenuItem
        //
        this.сохранитьToolStripMenuItem.Name = "сохранитьToolStripMenuItem";
        this.сохранитьToolStripMenuItem.Size = new System.Drawing.Size(301, 22);
        this.сохранитьToolStripMenuItem.Text = "Сохранить";
        this.сохранитьToolStripMenuItem.Click += new System.EventHandler(this.сохранитьToolStripMenuItem_Click);
        //
        // отправитьИзображениеВБуферОбменаToolStripMenuItem
        //
        this.отправитьИзображениеВБуферОбменаToolStripMenuItem.Name = "отправитьИзображениеВБуферОбменаToolStripMenuItem";
        this.отправитьИзображениеВБуферОбменаToolStripMenuItem.Size = new System.Drawing.Size(301, 22);
        this.отправитьИзображениеВБуферОбменаToolStripMenuItem.Text = "Отправить изображение в буфер обмена";
        this.отправитьИзображениеВБуферОбменаToolStripMenuItem.Click += new System.EventHandler(this.отправитьИзображениеВБуферОбменаToolStripMenuItem_Click);
        //
        // выборФонаToolStripMenuItem
        //
        this.выборФонаToolStripMenuItem.Name = "выборФонаToolStripMenuItem";
        this.выборФонаToolStripMenuItem.Size = new System.Drawing.Size(301, 22);
        this.выборФонаToolStripMenuItem.Text = "Выбор фона";
        this.выборФонаToolStripMenuItem.Click += new System.EventHandler(this.выборФонаToolStripMenuItem_Click);
        //
        // newWindowOutp
        //
        this.newWindowOutp.Name = "newWindowOutp";
        this.newWindowOutp.Size = new System.Drawing.Size(301, 22);
        this.newWindowOutp.Text = "Вывести в отдельном окне";
        this.newWindowOutp.Click += new System.EventHandler(this.NewWindowOutp_Click);
        //
        // преобразованияToolStripMenuItem
        //
        this.преобразованияToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
        this.спектрToolStripMenuItem,
        this.гистограммаToolStripMenuItem,
        this.diff,
        this.integ});
        this.преобразованияToolStripMenuItem.Name = "преобразованияToolStripMenuItem";
        this.преобразованияToolStripMenuItem.Size = new System.Drawing.Size(301, 22);
        this.преобразованияToolStripMenuItem.Text = "Преобразования";
        //
        // спектрToolStripMenuItem
        //
        this.спектрToolStripMenuItem.Name = "спектрToolStripMenuItem";
        this.спектрToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
        this.спектрToolStripMenuItem.Text = "Спектр";
        this.спектрToolStripMenuItem.Click += new System.EventHandler(this.СпектрToolStripMenuItem_Click);
        //
        // гистограммаToolStripMenuItem
        //
        this.гистограммаToolStripMenuItem.Name = "гистограммаToolStripMenuItem";
        this.гистограммаToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
        this.гистограммаToolStripMenuItem.Text = "Гистограмма";
        this.гистограммаToolStripMenuItem.Click += new System.EventHandler(this.ГистограммаToolStripMenuItem_Click);
        //
        // diff
        //
        this.diff.Name = "diff";
        this.diff.Size = new System.Drawing.Size(147, 22);
        this.diff.Text = "Производная";
        this.diff.Click += new System.EventHandler(this.Diff_Click);
        //
        // integ
        //
        this.integ.Name = "integ";
        this.integ.Size = new System.Drawing.Size(147, 22);
        this.integ.Text = "Интеграл";
        this.integ.Click += new System.EventHandler(this.Integ_Click);
        //
        // масштабToolStripMenuItem
        //
        this.масштабToolStripMenuItem.Name = "масштабToolStripMenuItem";
        this.масштабToolStripMenuItem.Size = new System.Drawing.Size(301, 22);
        this.масштабToolStripMenuItem.Text = "Масштаб по умолчанию";
        this.масштабToolStripMenuItem.Click += new System.EventHandler(this.масштабToolStripMenuItem_Click);
        //
        // labelXY
        //
        this.labelXY.AutoSize = true;
        this.labelXY.BackColor = System.Drawing.Color.Transparent;
        this.labelXY.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
        this.labelXY.Location = new System.Drawing.Point(3, 0);
        this.labelXY.Name = "labelXY";
        this.labelXY.Size = new System.Drawing.Size(30, 13);
        this.labelXY.TabIndex = 1;
        this.labelXY.Text = "X: Y:";
        this.labelXY.Click += new System.EventHandler(this.LabelXY_Click);
        //
        // ChartVisual
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        this.AutoScroll = true;
        this.BackColor = System.Drawing.Color.White;
        this.Controls.Add(this.labelXY);
        this.Controls.Add(this.skChart);
        this.ForeColor = System.Drawing.Color.Black;
        this.Name = "ChartVisual";
        this.Size = new System.Drawing.Size(443, 302);
        this.contextMenu.ResumeLayout(false);
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.ToolStripMenuItem преобразованияToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem спектрToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem гистограммаToolStripMenuItem;
    private System.Windows.Forms.ToolStripMenuItem newWindowOutp;
    private System.Windows.Forms.ToolStripMenuItem diff;
    private System.Windows.Forms.ToolStripMenuItem integ;
}
