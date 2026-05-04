using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.SignalLab.Filters;
using AI.SignalLab.Modulation.Demodulation;
using AI.SignalLab.Modulation.Modulation;
using AI.SignalLab.Modulation.Modulation.DigitalModulations;
using System;
using System.Collections;
using System.Numerics;
using System.Text;
using System.Windows.Forms;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace ModulationTest
{
    public partial class Form1 : Form
    {
        const double Sr = 144.1e3;       // частота дискретизации
        const double F0 = 3e3;           // несущая
        const double BitDuration = 9e-4; // длительность бита (символа)
        const double RollOff = 0.35;     // коэффициент скатывания SRRC
        const int SpanSymbols = 4;       // длина SRRC в символах

        // SRRC-фильтры передатчика (отдельно для синфазного и квадратурного каналов)
        readonly RootRaisedCosineFilter _txSrrcI;
        readonly RootRaisedCosineFilter _txSrrcQ;

        // Квадратурный демодулятор со SRRC
        readonly QudratureDemodulation _demod;

        // Текущая выбранная модуляция
        BaseIQModulation _currentModulation;

        public Form1()
        {
            InitializeComponent();

            _txSrrcI = new RootRaisedCosineFilter(BitDuration, Sr, RollOff, SpanSymbols);
            _txSrrcQ = new RootRaisedCosineFilter(BitDuration, Sr, RollOff, SpanSymbols);
            _demod = new QudratureDemodulation(F0, Sr, BitDuration, RollOff, SpanSymbols);

            // Настройка выпадающего списка модуляций
            comboBoxModulation.Items.AddRange(new string[] { "BPSK", "QPSK", "8-QAM", "16-QAM" });
            comboBoxModulation.SelectedIndex = 0; // По умолчанию BPSK
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Очистка предыдущего вывода
            textBoxDecoded.Text = "";
            textBoxDecoded.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);

            // 1. Текст -> Биты
            byte[] bytes = ModulationBitsTools.Obj2Bytes(textBox1.Text);
            BitArray bitArray = new BitArray(bytes);
            bool[] bits = new bool[bitArray.Length];
            bitArray.CopyTo(bits, 0);

            // 2. Выбор модуляции
            switch (comboBoxModulation.SelectedIndex)
            {
                case 0: _currentModulation = new BPSK(); break;
                case 1: _currentModulation = new QPSK(); break;
                case 2: _currentModulation = new QAM8(); break;
                case 3: _currentModulation = new QAM16(); break;
            }

            // 3. Маппинг битов в символы созвездия
            Complex[] mappedSymbols = _currentModulation.MapBitsToSymbols(bits);

            int samplesPerSymbol = (int)Math.Round(BitDuration * Sr);

            // Задержки фильтров (в отсчетах)
            int txDelay = _txSrrcI.Length / 2;
            int rxDelay = _demod.FilterDelay;
            int totalDelay = txDelay + rxDelay;

            // 4. Символы -> Импульсы (Dirac) для I и Q каналов
            Vector impulsesI = new Vector((mappedSymbols.Length * samplesPerSymbol) + totalDelay + samplesPerSymbol);
            Vector impulsesQ = new Vector((mappedSymbols.Length * samplesPerSymbol) + totalDelay + samplesPerSymbol);

            for (int i = 0; i < mappedSymbols.Length; i++)
            {
                impulsesI[i * samplesPerSymbol] = mappedSymbols[i].Real;
                impulsesQ[i * samplesPerSymbol] = mappedSymbols[i].Imaginary;
            }

            // 5. Формирующий SRRC на передатчике
            Vector basebandI = _txSrrcI.FilterOutp(impulsesI);
            Vector basebandQ = _txSrrcQ.FilterOutp(impulsesQ);

            // 6. Квадратурная модуляция несущей
            Vector modulated = new Vector(basebandI.Count);
            for (int i = 0; i < basebandI.Count; i++)
            {
                double arg = 2 * Math.PI * F0 * i / Sr;
                // s(t) = I(t)*cos(wt) + Q(t)*sin(wt)
                modulated[i] = (basebandI[i] * Math.Cos(arg)) + (basebandQ[i] * Math.Sin(arg));
            }

            // 7. Демодуляция: полный IQ + точки созвездия
            var (iqFull, iqSymbols) = _demod.GetIQBoth(modulated, txDelay);

            // 8. Декодирование сигнала обратно в текст
            DecodeSignal(iqSymbols, bits.Length);

            Vector x = Vector.SeqBeginsWithZero(1, modulated.Count);

            // --- Отображение ---
            chartVisual1.Clear();
            chartVisual1.AddPlot(x, modulated, "Сигнал", width: 1);

            chartVisual2.Clear();
            chartVisual2.ScatterComplexPlane(iqSymbols);

            chartVisual3.Clear();
            chartVisual3.AddPlot(x, iqFull.RealVector, "Синфазная составляющая (I)");
            chartVisual3.AddPlot(x, iqFull.ImaginaryVector, "Квадратурная составляющая (Q)");
        }

        private void DecodeSignal(ComplexVector iqSymbols, int expectedBits)
        {
            try
            {
                // Демаппинг символов обратно в биты (Minimum Distance Decoding)
                bool[] exactBits = _currentModulation.DemapSymbolsToBits(iqSymbols, expectedBits);

                // Переводим биты в байты
                BitArray bitArray = new BitArray(exactBits);
                byte[] decodedBytes = new byte[(bitArray.Length + 7) / 8];
                bitArray.CopyTo(decodedBytes, 0);

                string decodedText = Encoding.UTF8.GetString(decodedBytes);
                textBoxDecoded.Text = decodedText;
                textBoxDecoded.BackColor = System.Drawing.Color.DarkGreen;
            }
            catch (Exception ex)
            {
                textBoxDecoded.Text = "Ошибка: " + ex.Message;
                textBoxDecoded.BackColor = System.Drawing.Color.DarkRed; // Индикация ошибки
            }
        }
    }
}