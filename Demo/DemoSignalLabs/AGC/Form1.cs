using System;
using System.Drawing;
using System.Windows.Forms;
using AI.DataStructs.Algebraic;
using AI.SignalLab.AGC;
using AI.SignalLab.AGC.CustomAGC;

namespace AGC
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            
            // Настраиваем выпадающий список
            comboBoxAgcType.Items.AddRange(new string[] 
            { 
                "DirectAGC (Прямая АРУ)", 
                "LogAGC (Логарифмическая АРУ)", 
                "MinCombineAGC (Комбинированная)" 
            });
            comboBoxAgcType.SelectedIndex = 0; // По умолчанию DirectAGC
        }

        private void buttonProcess_Click(object sender, EventArgs e)
        {
            // 1. Генерируем тестовый сигнал с резкими скачками амплитуды
            Vector originalSignal = GenerateTestSignal();

            // 2. Выбираем нужный алгоритм АРУ
            IAGC agc = null;
            switch (comboBoxAgcType.SelectedIndex)
            {
                case 0: agc = new DirectAGC(); break;
                case 1: agc = new LogAGC(); break;
                case 2: agc = new MinCombineAGC(); break;
            }

            // 3. Пропускаем сигнал через АРУ (потоковый режим)
            Vector processedSignal = new Vector(originalSignal.Count);
            for (int i = 0; i < originalSignal.Count; i++)
            {
                processedSignal[i] = agc.Calculate(originalSignal[i]);
            }

            // 4. Отрисовываем графики
            chartVisualOriginal.Clear();
            chartVisualOriginal.AddPlot(Vector.SeqBeginsWithZero(1, originalSignal.Count), originalSignal, "Оригинальный сигнал");

            chartVisualAgc.Clear();
            chartVisualAgc.AddPlot(Vector.SeqBeginsWithZero(1, processedSignal.Count), processedSignal, "Сигнал после АРУ");
        }

        /// <summary>
        /// Генерирует синусоиду с резкими изменениями амплитуды (тишина -> громко -> очень громко -> тихо)
        /// </summary>
        private Vector GenerateTestSignal()
        {
            int sr = 44100; // Частота дискретизации
            double duration = 2.0; // 2 секунды
            int totalSamples = (int)(sr * duration);
            Vector signal = new Vector(totalSamples);

            double freq = 1000; // 1 кГц
            double dt = 1.0 / sr;

            for (int i = 0; i < totalSamples; i++)
            {
                double t = i * dt;
                double amplitude = 0.1; // Базовая тихая амплитуда

                // Скачки амплитуды:
                if (t > 0.5 && t < 1.0)
                {
                    amplitude = 2.0; // Средняя громкость
                }
                else if (t >= 1.0 && t < 1.5)
                {
                    amplitude = 10.0; // Резкий громкий всплеск
                }

                signal[i] = amplitude * Math.Sin(2 * Math.PI * freq * t);
            }

            return signal;
        }
    }
}