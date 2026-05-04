using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AI.ComputerVision;
using AI.ComputerVision.SpatialFilters;
using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace TestGUI
{
    public partial class FImg : Form
    {
        private string _currentImagePath = "";

        public FImg()
        {
            InitializeComponent();
        }

        // --- Конвертация на границе WinForms <-> SkiaSharp ---
        // ВАЖНО: System.Drawing.Bitmap, созданный из потока, держит ссылку на него
        // на всё время жизни; диспос потока ломает Bitmap (GDI+: "A generic error occurred").
        // Поэтому делаем независимую копию пикселей через промежуточный Bitmap.
        private static Bitmap SkToDrawing(SKBitmap sk)
        {
            using var img = SKImage.FromBitmap(sk);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(data.ToArray());
            using var tmp = new Bitmap(ms);
            return new Bitmap(tmp);
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            using var openFile = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Title = "Выберите изображение"
            };

            if (openFile.ShowDialog() == DialogResult.OK)
            {
                _currentImagePath = openFile.FileName;
                try
                {
                    var m = ImageMatrixConverter.LoadAsMatrix(_currentImagePath);
                    using var skBmp = ImageMatrixConverter.ToBitmap(m);
                    UpdateImage(SkToDrawing(skBmp));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Ошибка загрузки изображения:\n{ex.Message}", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void ProcessImageAsync(Func<string, SKBitmap> processFunc)
        {
            if (string.IsNullOrEmpty(_currentImagePath))
            {
                MessageBox.Show(this, "Сначала загрузите изображение.", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetLoadingState(true);
            try
            {
                string path = _currentImagePath;
                using SKBitmap skResult = await Task.Run(() => processFunc(path));
                UpdateImage(SkToDrawing(skResult));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка обработки изображения:\n{ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void UpdateImage(Bitmap bmp)
        {
            pictureBox1.Image?.Dispose();
            pictureBox1.Image = bmp;
        }

        private void SetLoadingState(bool isLoading)
        {
            flowLayoutPanel1.Enabled = !isLoading;
            Cursor = isLoading ? Cursors.WaitCursor : Cursors.Default;
        }

        private void btnPencil_Click(object sender, EventArgs e)
        {
            ProcessImageAsync(path =>
            {
                var m = ImageMatrixConverter.LoadAsMatrix(path);
                var f = new Matrix(3, 3) - 1.0 / 9.0;
                f[1, 1] = 8 / 9.0;
                m = new CustomFilter(f).Filtration(m);
                double std = m.Std();
                m = m / (3 * std);
                return ImageMatrixConverter.ToBitmap(255 * (1 - m));
            });
        }

        private void btnHLine_Click(object sender, EventArgs e)
        {
            ProcessImageAsync(path => new HLine().Filtration(path));
        }

        private void btnWLine_Click(object sender, EventArgs e)
        {
            ProcessImageAsync(path => new WLine().Filtration(path));
        }

        private void btnSharpness_Click(object sender, EventArgs e)
        {
            ProcessImageAsync(path => new Sharpness().Filtration(path));
        }

        private void btnSmoothing_Click(object sender, EventArgs e)
        {
            ProcessImageAsync(path => new Smoothing().Filtration(path));
        }

        private void btnStd_Click(object sender, EventArgs e)
        {
            ProcessImageAsync(path =>
            {
                var m = ImageMatrixConverter.LoadAsMatrix(path);
                var f = new Matrix(3, 3) + 1.0;
                m = ImgFilters.StdFilter(m, f);
                return ImageMatrixConverter.ToBitmap(255 - m);
            });
        }

        private void btnMedian_Click(object sender, EventArgs e)
        {
            ProcessImageAsync(path =>
            {
                var m = ImageMatrixConverter.LoadAsMatrix(path);
                m = ImgFilters.MedianFilter(m, 3, 3);
                return ImageMatrixConverter.ToBitmap(m);
            });
        }
    }
}
