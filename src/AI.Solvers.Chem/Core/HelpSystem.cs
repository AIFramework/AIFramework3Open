using System.Text;

namespace AI.Solvers.Chem.Core;

// ═══════════════════════════════════════════════════════════
// СИСТЕМА ПОМОЩИ
// ═══════════════════════════════════════════════════════════
public static class HelpSystem
{
    public static string GetHelp(string topic = "")
    {
        if (string.IsNullOrWhiteSpace(topic))
            return GetMainHelp();

        return topic.ToLower() switch
        {
            "pharmacokinetics" or "pk" or "фармакокинетика" => GetPharmacokineticHelp(),
            "blood" or "кровь" or "кос" => GetBloodGasHelp(),
            "enzyme" or "enzymes" or "ферменты" => GetEnzymeKineticsHelp(),
            "buffer" or "буфер" or "буферы" => GetBufferHelp(),
            "titration" or "титрование" => GetTitrationHelp(),
            "solubility" or "ksp" or "растворимость" => GetSolubilityHelp(),
            "complex" or "complexes" or "комплексы" => GetComplexHelp(),
            "kinetics" or "кинетика" => GetKineticsHelp(),
            "spectroscopy" or "beer" or "спектроскопия" => GetSpectroscopyHelp(),
            "inorganic" or "неорганика" => GetInorganicHelp(),
            "organic" or "органика" => GetOrganicHelp(),
            "medical" or "медицина" => GetMedicalHelp(),
            "analytical" or "аналитика" => GetAnalyticalHelp(),
            "all" or "всё" => GetDetailedHelp(),
            _ => $"Тема '{topic}' не найдена. Используйте 'help' для списка тем."
        };
    }

    private static string GetMainHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║        ХИМИЧЕСКИЙ ДВИЖОК - СИСТЕМА ПОМОЩИ v2.0            ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine("ДОСТУПНЫЕ КАТЕГОРИИ:");
        sb.AppendLine();
        sb.AppendLine("НЕОРГАНИЧЕСКАЯ ХИМИЯ:");
        sb.AppendLine("   • help buffer        - Буферные системы");
        sb.AppendLine("   • help titration     - Титрование");
        sb.AppendLine("   • help solubility    - Растворимость и Ksp");
        sb.AppendLine("   • help complex       - Комплексные соединения");
        sb.AppendLine("   • help inorganic     - Все неорганические расчёты");
        sb.AppendLine();
        sb.AppendLine("ФИЗИЧЕСКАЯ ХИМИЯ:");
        sb.AppendLine("   • help kinetics      - Расширенная кинетика");
        sb.AppendLine();
        sb.AppendLine("МЕДИЦИНСКИЕ РАСЧЁТЫ:");
        sb.AppendLine("   • help pharmacokinetics - Фармакокинетика");
        sb.AppendLine("   • help blood            - Анализ газов крови");
        sb.AppendLine("   • help enzyme           - Кинетика ферментов");
        sb.AppendLine("   • help medical          - Все медицинские расчёты");
        sb.AppendLine();
        sb.AppendLine("АНАЛИТИЧЕСКАЯ ХИМИЯ:");
        sb.AppendLine("   • help spectroscopy  - Спектроскопия (закон Бера)");
        sb.AppendLine("   • help analytical    - Все аналитические расчёты");
        sb.AppendLine();
        sb.AppendLine("ОРГАНИЧЕСКАЯ ХИМИЯ:");
        sb.AppendLine("   • help organic       - SMILES, ретросинтез, изомеры");
        sb.AppendLine();
        sb.AppendLine("ДОПОЛНИТЕЛЬНО:");
        sb.AppendLine("   • help all           - Подробная справка по всем модулям");
        sb.AppendLine();
        sb.AppendLine("Всего доступно: 51 тип задач!");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetPharmacokineticHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("ФАРМАКОКИНЕТИКА");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("ДОСТУПНЫЕ РАСЧЁТЫ:");
        sb.AppendLine();
        sb.AppendLine("1. ОДНОКАМЕРНАЯ МОДЕЛЬ - IV БОЛЮС:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > pharmacokinetics type=iv_bolus dose=500mg Vd=50L t_half=6h time=12h");
        sb.AppendLine("   Результат: C(12h), k, CL, AUC");
        sb.AppendLine();
        sb.AppendLine("2. НЕПРЕРЫВНАЯ ИНФУЗИЯ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > pharmacokinetics type=continuous infusion_rate=50mg/h Vd=40L t_half=4h time=12h");
        sb.AppendLine("   Результат: Css, время до стационарной концентрации");
        sb.AppendLine();
        sb.AppendLine("3. ПЕРОРАЛЬНОЕ ВВЕДЕНИЕ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > pharmacokinetics type=oral dose=500mg bioavailability=0.8 Vd=50L ka=1.2 t_half=6h time=3h");
        sb.AppendLine("   Результат: C(t), Cmax, tmax");
        sb.AppendLine();
        sb.AppendLine("4. РАСЧЁТ ДОЗЫ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > dose target_concentration=10mg/L Vd=50L bioavailability=0.9 t_half=6h");
        sb.AppendLine("   Результат: Loading dose, Maintenance rate");
        sb.AppendLine();
        sb.AppendLine("5. ОПРЕДЕЛЕНИЕ T½:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > pharmacokinetics calculate_half_life C1=10 C2=5 t1=0h t2=6h");
        sb.AppendLine("   Результат: период полувыведения из данных");
        sb.AppendLine();
        sb.AppendLine("ОСНОВНЫЕ ФОРМУЛЫ:");
        sb.AppendLine("   • C(t) = C₀·e⁻ᵏᵗ");
        sb.AppendLine("   • t½ = 0.693/k");
        sb.AppendLine("   • CL = k·Vd");
        sb.AppendLine("   • AUC = Dose/CL");
        sb.AppendLine("   • Css = R/CL (инфузия)");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetBloodGasHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("АНАЛИЗ ГАЗОВ КРОВИ (КОС)");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("ДОСТУПНЫЕ РАСЧЁТЫ:");
        sb.AppendLine();
        sb.AppendLine("1. ПОЛНЫЙ АНАЛИЗ КОС:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > blood gas pH=7.25 pCO2=55mmHg HCO3=24mEq/L");
        sb.AppendLine("   Опционально: Na=140 Cl=105 (для Anion Gap)");
        sb.AppendLine("   Диагностика:");
        sb.AppendLine("   - Respiratory Acidosis/Alkalosis");
        sb.AppendLine("   - Metabolic Acidosis/Alkalosis");
        sb.AppendLine("   - Проверка компенсации");
        sb.AppendLine();
        sb.AppendLine("2. РАСЧЁТ БИКАРБОНАТА:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > bicarbonate pH=7.40 pCO2=40mmHg");
        sb.AppendLine("   Формула: Henderson-Hasselbalch");
        sb.AppendLine("   pH = 6.1 + log([HCO₃⁻]/[0.03·pCO₂])");
        sb.AppendLine();
        sb.AppendLine("3. BASE EXCESS:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > base excess HCO3=18mEq/L pH=7.30");
        sb.AppendLine("   Формула Van Slyke:");
        sb.AppendLine("   BE = 0.93·(HCO₃⁻ - 24.4 + 14.8·(pH - 7.4))");
        sb.AppendLine();
        sb.AppendLine("НОРМАЛЬНЫЕ ЗНАЧЕНИЯ:");
        sb.AppendLine("   • pH: 7.35-7.45");
        sb.AppendLine("   • pCO₂: 35-45 mmHg");
        sb.AppendLine("   • HCO₃⁻: 22-26 mEq/L");
        sb.AppendLine("   • BE: -2 to +2 mEq/L");
        sb.AppendLine("   • Anion Gap: 8-16 mEq/L");
        sb.AppendLine();
        sb.AppendLine("ИНТЕРПРЕТАЦИЯ:");
        sb.AppendLine("   pH < 7.35 → Acidosis");
        sb.AppendLine("   pH > 7.45 → Alkalosis");
        sb.AppendLine("   pCO₂ изменён → Respiratory");
        sb.AppendLine("   HCO₃⁻ изменён → Metabolic");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetEnzymeKineticsHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("КИНЕТИКА ФЕРМЕНТОВ");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("ДОСТУПНЫЕ РАСЧЁТЫ:");
        sb.AppendLine();
        sb.AppendLine("1. МИХАЭЛИС-МЕНТЕН:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > Michaelis-Menten Vmax=100 Km=0.5M S=1.0M");
        sb.AppendLine("   Формула: v = (Vmax·[S])/(Km + [S])");
        sb.AppendLine("   Рассчитывает скорость реакции");
        sb.AppendLine();
        sb.AppendLine("2. LINEWEAVER-BURK:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > Lineweaver-Burk substrate=0.001,0.002,0.005,0.01 velocity=10,18,33,45");
        sb.AppendLine("   Определяет Km и Vmax из экспериментальных данных");
        sb.AppendLine("   Метод: двойные обратные величины");
        sb.AppendLine("   1/v = (Km/Vmax)·(1/[S]) + 1/Vmax");
        sb.AppendLine();
        sb.AppendLine("3. КОНКУРЕНТНОЕ ИНГИБИРОВАНИЕ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > enzyme inhibition type=competitive Vmax=100 Km=0.5 S=1.0 I=0.1 Ki=0.05");
        sb.AppendLine("   Km' = Km·(1 + [I]/Ki)");
        sb.AppendLine("   Vmax не изменяется");
        sb.AppendLine();
        sb.AppendLine("4. НЕКОНКУРЕНТНОЕ ИНГИБИРОВАНИЕ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > enzyme inhibition type=noncompetitive Vmax=100 Km=0.5 S=1.0 I=0.1 Ki=0.05");
        sb.AppendLine("   Vmax' = Vmax/(1 + [I]/Ki)");
        sb.AppendLine("   Km не изменяется");
        sb.AppendLine();
        sb.AppendLine("5. УДЕЛЬНАЯ АКТИВНОСТЬ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > specific activity activity=500units protein=2.5mg");
        sb.AppendLine("   Specific Activity = units/mg protein");
        sb.AppendLine();
        sb.AppendLine("КЛЮЧЕВЫЕ ПОНЯТИЯ:");
        sb.AppendLine("   • Km - концентрация субстрата при v = Vmax/2");
        sb.AppendLine("   • Vmax - максимальная скорость");
        sb.AppendLine("   • Ki - константа ингибирования");
        sb.AppendLine("   • [S] << Km: кинетика 1-го порядка");
        sb.AppendLine("   • [S] >> Km: кинетика 0-го порядка");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetBufferHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("БУФЕРНЫЕ СИСТЕМЫ");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("ДОСТУПНЫЕ РАСЧЁТЫ:");
        sb.AppendLine();
        sb.AppendLine("1. HENDERSON-HASSELBALCH (pH БУФЕРА):");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > buffer pKa=4.76 acid=0.1M base=0.15M");
        sb.AppendLine("   > buffer Ka=1.8e-5 acid=0.1M base=0.15M");
        sb.AppendLine("   Формула: pH = pKa + log([A-]/[HA])");
        sb.AppendLine("   Рассчитывает pH и буферную ёмкость");
        sb.AppendLine();
        sb.AppendLine("2. БУФЕРНАЯ ЁМКОСТЬ (β):");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > calculate buffer capacity pKa=4.76 acid=0.1M base=0.1M");
        sb.AppendLine("   β = 2.3·C·f·(1-f), где f = [HA]/C");
        sb.AppendLine("   Показывает, сколько кислоты/щелочи может поглотить буфер");
        sb.AppendLine();
        sb.AppendLine("ЭФФЕКТИВНОСТЬ БУФЕРА:");
        sb.AppendLine("   Оптимальна: pH = pKa ± 1");
        sb.AppendLine("   [A-]/[HA] в диапазоне 0.1-10");
        sb.AppendLine("   Вне диапазона: низкая эффективность");
        sb.AppendLine();
        sb.AppendLine("ПРИМЕРЫ БУФЕРОВ:");
        sb.AppendLine("   • Уксусная к-та/ацетат: pKa = 4.76");
        sb.AppendLine("   • Фосфатный: pKa2 = 7.21");
        sb.AppendLine("   • Аммиачный: pKa = 9.25");
        sb.AppendLine("   • TRIS: pKa = 8.06");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetTitrationHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("ТИТРОВАНИЕ");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("ТИПЫ ТИТРОВАНИЯ:");
        sb.AppendLine();
        sb.AppendLine("1. КРИВАЯ ТИТРОВАНИЯ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > titration curve acid=0.1M base=0.1M V_acid=25ml");
        sb.AppendLine("   > titration curve acid=0.1M base=0.1M V_acid=25ml pKa=4.76");
        sb.AppendLine("   Рассчитывает и строит кривую pH vs V_base");
        sb.AppendLine("   Определяет точку эквивалентности и буферную область");
        sb.AppendLine();
        sb.AppendLine("2. СИЛЬНАЯ КИСЛОТА + СИЛЬНОЕ ОСНОВАНИЕ:");
        sb.AppendLine("   Точка эквивалентности: pH = 7.0");
        sb.AppendLine("   Пример: HCl + NaOH");
        sb.AppendLine();
        sb.AppendLine("3. СЛАБАЯ КИСЛОТА + СИЛЬНОЕ ОСНОВАНИЕ:");
        sb.AppendLine("   Точка эквивалентности: pH > 7 (гидролиз соли)");
        sb.AppendLine("   Пример: CH3COOH + NaOH");
        sb.AppendLine();
        sb.AppendLine("4. ПОЛИПРОТОННЫЕ КИСЛОТЫ:");
        sb.AppendLine("   Несколько точек эквивалентности");
        sb.AppendLine("   Пример: H3PO4 (pKa1=2.15, pKa2=7.21, pKa3=12.32)");
        sb.AppendLine();
        sb.AppendLine("ИНДИКАТОРЫ:");
        sb.AppendLine("   • Метиловый оранжевый: 3.1-4.4");
        sb.AppendLine("   • Метиловый красный: 4.8-6.0");
        sb.AppendLine("   • Фенолфталеин: 8.2-10.0");
        sb.AppendLine("   Выбирают по pH точки эквивалентности");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetSolubilityHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("РАСТВОРИМОСТЬ И Ksp");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("ДОСТУПНЫЕ РАСЧЁТЫ:");
        sb.AppendLine();
        sb.AppendLine("1. РАСЧЁТ РАСТВОРИМОСТИ ИЗ Ksp:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > solubility of AgCl");
        sb.AppendLine("   > solubility of PbI2");
        sb.AppendLine("   Рассчитывает молярную растворимость (s) и концентрации ионов");
        sb.AppendLine();
        sb.AppendLine("2. ОБЩИЙ ИОННЫЙ ЭФФЕКТ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > common ion compound=AgCl ion=Cl concentration=0.1M");
        sb.AppendLine("   Показывает снижение растворимости в присутствии общего иона");
        sb.AppendLine();
        sb.AppendLine("3. ПРЕДСКАЗАНИЕ ОСАЖДЕНИЯ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > predict precipitation compound=CaF2 [Ca]=0.01M [F]=0.001M");
        sb.AppendLine("   Сравнивает ионное произведение Q с Ksp");
        sb.AppendLine();
        sb.AppendLine("4. ДРОБНОЕ ОСАЖДЕНИЕ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > fractional precipitation compound1=AgCl compound2=AgBr anion=Ag");
        sb.AppendLine("   Определяет, какой осадок выпадет первым");
        sb.AppendLine();
        sb.AppendLine("БАЗА ДАННЫХ Ksp:");
        sb.AppendLine("   Хлориды: AgCl, PbCl₂, Hg₂Cl₂");
        sb.AppendLine("   Сульфаты: BaSO₄, CaSO₄, PbSO₄");
        sb.AppendLine("   Карбонаты: CaCO₃, BaCO₃");
        sb.AppendLine("   Гидроксиды: Mg(OH)₂, Fe(OH)₃");
        sb.AppendLine("   Сульфиды: CuS, ZnS, PbS");
        sb.AppendLine("   +20 других соединений");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetComplexHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("КОМПЛЕКСНЫЕ СОЕДИНЕНИЯ");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("ДОСТУПНЫЕ РАСЧЁТЫ:");
        sb.AppendLine();
        sb.AppendLine("1. ОБРАЗОВАНИЕ КОМПЛЕКСА:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > complex metal=Cu ligand=NH3 [metal]=0.1M [ligand]=1.0M");
        sb.AppendLine("   Рассчитывает концентрацию комплекса [MLn] и свободного металла");
        sb.AppendLine();
        sb.AppendLine("2. СТУПЕНЧАТОЕ КОМПЛЕКСООБРАЗОВАНИЕ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > stepwise complex metal=Ag ligand=NH3 [ligand]=0.1M");
        sb.AppendLine("   Рассчитывает распределение всех форм комплекса");
        sb.AppendLine("   [Ag+], [Ag(NH3)+], [Ag(NH3)2+]");
        sb.AppendLine();
        sb.AppendLine("3. ВЛИЯНИЕ pH НА КОМПЛЕКСООБРАЗОВАНИЕ (EDTA):");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > complex metal=Ca ligand=EDTA pH=10 [metal]=0.01M [EDTA]=0.01M");
        sb.AppendLine("   Учитывает протонирование лиганда при заданном pH");
        sb.AppendLine("   Рассчитывает условную константу K' и концентрации");
        sb.AppendLine();
        sb.AppendLine("4. ХЕЛАТНЫЙ ЭФФЕКТ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > chelate effect metal=Ni ligand1=NH3 ligand2=en");
        sb.AppendLine("   Демонстрирует разницу в стабильности монодентатных и бидентатных лигандов");
        sb.AppendLine();
        sb.AppendLine("ЛИГАНДЫ В БАЗЕ:");
        sb.AppendLine("   • NH3, Cl-, CN-, SCN-, OH-");
        sb.AppendLine("   • EDTA, en (этилендиамин)");
        sb.AppendLine("   • S2O3 (тиосульфат)");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetKineticsHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("РАСШИРЕННАЯ КИНЕТИКА");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("ДОСТУПНЫЕ РАСЧЁТЫ:");
        sb.AppendLine();
        sb.AppendLine("1. ЗАКОН ДЕЙСТВУЮЩИХ МАСС:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > rate law k=0.05 A=0.1 B=0.2 orderA=1 orderB=2");
        sb.AppendLine("   Формула: v = k·[A]^m·[B]^n");
        sb.AppendLine();
        sb.AppendLine("2. ПЕРИОД ПОЛУРАСПАДА:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > half-life k=0.05 order=1");
        sb.AppendLine("   > half-life k=0.05 order=2 A0=1.0");
        sb.AppendLine("   Рассчитывает t½ для реакций 0, 1 и 2 порядка");
        sb.AppendLine();
        sb.AppendLine("3. УРАВНЕНИЕ АРРЕНИУСА:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > Arrhenius A=1e13 Ea=50000 T=298");
        sb.AppendLine("   > Arrhenius k1=0.01 T1=300 k2=0.05 T2=320");
        sb.AppendLine("   Рассчитывает константу скорости (k) или энергию активации (Ea)");
        sb.AppendLine();
        sb.AppendLine("4. ОПРЕДЕЛЕНИЕ ПОРЯДКА РЕАКЦИИ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > determine order rate1=0.1 rate2=0.4 conc1=0.1 conc2=0.2");
        sb.AppendLine("   Метод начальных скоростей: определяет порядок реакции по данным");
        sb.AppendLine();
        sb.AppendLine("5. ИНТЕГРИРОВАННЫЕ УРАВНЕНИЯ СКОРОСТИ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > integrated rate law order=1 A0=1.0 k=0.05 time=10");
        sb.AppendLine("   Рассчитывает концентрацию [A] в момент времени t");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetSpectroscopyHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("СПЕКТРОСКОПИЯ (ЗАКОН БЕРА)");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("ДОСТУПНЫЕ РАСЧЁТЫ:");
        sb.AppendLine();
        sb.AppendLine("1. ЗАКОН БУГЕРА-ЛАМБЕРТА-БЕРА (Поиск A):");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > Beer's law eps=150 c=0.005M l=1cm");
        sb.AppendLine("   Формула: A = ε·c·l");
        sb.AppendLine();
        sb.AppendLine("2. РАСЧЁТ КОНЦЕНТРАЦИИ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > Beer's law A=0.45 eps=1500 l=1");
        sb.AppendLine("   Формула: c = A/(ε·l)");
        sb.AppendLine();
        sb.AppendLine("3. РАСЧЁТ ε (Молярный коэффициент):");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > Beer's law A=0.8 c=0.002M l=1cm");
        sb.AppendLine("   Формула: ε = A/(c·l)");
        sb.AppendLine();
        sb.AppendLine("4. КОНВЕРТАЦИЯ A ↔ %T:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > Beer's law T=45%");
        sb.AppendLine("   > Beer's law A=0.35");
        sb.AppendLine("   Формулы:");
        sb.AppendLine("   T = 10^(-A)");
        sb.AppendLine("   A = -log₁₀(T)");
        sb.AppendLine();
        sb.AppendLine("5. АНАЛИЗ СМЕСИ (2 компонента):");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > mixture analysis A1=0.5 A2=0.7 eps1_1=100 eps1_2=50 eps2_1=40 eps2_2=120 l=1");
        sb.AppendLine("   Решает систему уравнений для c1 и c2");
        sb.AppendLine();
        sb.AppendLine("6. КАЛИБРОВОЧНАЯ КРИВАЯ:");
        sb.AppendLine("   Команда:");
        sb.AppendLine("   > calibration concentrations=1,2,3,4,5 absorbance=0.1,0.2,0.3,0.4,0.5");
        sb.AppendLine("   Строит график A vs c и находит уравнение прямой");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetInorganicHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║          НЕОРГАНИЧЕСКАЯ ХИМИЯ - ВСЕ МОДУЛИ                ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine("1. Балансировка уравнений");
        sb.AppendLine("2. Стехиометрия (расчёты по уравнениям)");
        sb.AppendLine("3. Молярная масса");
        sb.AppendLine("4. Растворы (молярность, разбавление, смешивание)");
        sb.AppendLine("5. Расчёты pH (сильные/слабые к-ты и основания)");
        sb.AppendLine("6. Буферные системы (Henderson-Hasselbalch) NEW");
        sb.AppendLine("7. Титрование (полная реализация) NEW");
        sb.AppendLine("8. Степени окисления");
        sb.AppendLine("9. Redox реакции (балансировка)");
        sb.AppendLine("10. Растворимость и Ksp NEW");
        sb.AppendLine("11. Комплексные соединения NEW");
        sb.AppendLine();
        sb.AppendLine("Используйте 'help [название]' для подробностей");
        return sb.ToString();
    }

    private static string GetMedicalHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║           МЕДИЦИНСКИЕ РАСЧЁТЫ - ВСЕ МОДУЛИ               ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine("ФАРМАКОКИНЕТИКА:");
        sb.AppendLine("   • Однокамерная модель (IV болюс)");
        sb.AppendLine("   • Непрерывная инфузия");
        sb.AppendLine("   • Пероральное введение");
        sb.AppendLine("   • Расчёт дозы");
        sb.AppendLine();
        sb.AppendLine("АНАЛИЗ КРОВИ:");
        sb.AppendLine("   • Газы крови (pH, pCO₂, HCO₃⁻)");
        sb.AppendLine("   • Диагностика ацидоза/алкалоза");
        sb.AppendLine("   • Base Excess");
        sb.AppendLine("   • Anion Gap");
        sb.AppendLine();
        sb.AppendLine("БИОХИМИЯ:");
        sb.AppendLine("   • Кинетика ферментов (Михаэлис-Ментен)");
        sb.AppendLine("   • Lineweaver-Burk");
        sb.AppendLine("   • Ингибирование ферментов");
        sb.AppendLine("   • Удельная активность");
        sb.AppendLine();
        sb.AppendLine("Используйте 'help [название]' для подробностей");
        return sb.ToString();
    }

    private static string GetOrganicHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║            ОРГАНИЧЕСКАЯ ХИМИЯ - ВСЕ МОДУЛИ                ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine("1. Парсинг SMILES");
        sb.AppendLine("2. Генерация SMILES");
        sb.AppendLine("3. Генерация изомеров");
        sb.AppendLine("4. Определение функциональных групп");
        sb.AppendLine("5. Предсказание продуктов реакций");
        sb.AppendLine("6. Ретросинтез (графовый)");
        sb.AppendLine("7. IUPAC номенклатура");
        sb.AppendLine("8. Расчёт молекулярных свойств");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetAnalyticalHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║          АНАЛИТИЧЕСКАЯ ХИМИЯ - ВСЕ МОДУЛИ                 ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine("СПЕКТРОСКОПИЯ:");
        sb.AppendLine("   • Закон Бера (A = ε·c·l)");
        sb.AppendLine("   • Расчёт концентрации");
        sb.AppendLine("   • Определение ε");
        sb.AppendLine("   • Конвертация A ↔ %T");
        sb.AppendLine("   • Анализ смесей (2 компонента)");
        sb.AppendLine("   • Калибровочные кривые");
        sb.AppendLine();
        sb.AppendLine("ТИТРОВАНИЕ:");
        sb.AppendLine("   • Кривые титрования");
        sb.AppendLine("   • Точка эквивалентности");
        sb.AppendLine("   • Выбор индикаторов");
        sb.AppendLine();
        sb.AppendLine("ГРАВИМЕТРИЯ:");
        sb.AppendLine("   • Расчёты навесок (базовые)");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string GetDetailedHelp()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║      ПОЛНАЯ СПРАВКА - ВСЕ 51 ТИП ЗАДАЧ                    ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine(GetInorganicHelp());
        sb.AppendLine();
        sb.AppendLine(GetMedicalHelp());
        sb.AppendLine();
        sb.AppendLine(GetOrganicHelp());
        sb.AppendLine();
        sb.AppendLine(GetAnalyticalHelp());
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("ДОПОЛНИТЕЛЬНАЯ ИНФОРМАЦИЯ:");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine("Версия: 2.0");
        sb.AppendLine("Таблица Менделеева: 118 элементов");
        sb.AppendLine("База соединений: 200+ веществ");
        sb.AppendLine("База Ksp: 30+ соединений");
        sb.AppendLine("Константы комплексов: 40+ систем");
        sb.AppendLine();
        sb.AppendLine("Для быстрой справки по конкретной теме:");
        sb.AppendLine("   help [pharmacokinetics|blood|enzyme|buffer|etc]");
        sb.AppendLine();
        return sb.ToString();
    }
}

