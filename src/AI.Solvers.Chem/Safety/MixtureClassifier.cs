using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Safety;

/// <summary>
/// Обоснование одного отнесения: какое правило сработало и на каких числах
/// </summary>
/// <param name="Category">Присвоенный класс и категория</param>
/// <param name="Rule">Формулировка правила</param>
/// <param name="Value">Рассчитанная величина (сумма содержаний либо ATE)</param>
/// <param name="Threshold">Порог, с которым сравнивали</param>
public readonly record struct ClassificationReason(HazardCategory Category, string Rule, double Value, double Threshold)
{
    /// <summary>Обоснование текстом</summary>
    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture, "{0}: {1} ({2:G4} против порога {3:G4})",
            Category, Rule, Value, Threshold);
}

/// <summary>
/// Результат классификации смеси
/// </summary>
public sealed class MixtureClassification
{
    /// <summary>Присвоенные классы и категории опасности</summary>
    public IReadOnlyList<HazardCategory> Hazards { get; init; } = Array.Empty<HazardCategory>();

    /// <summary>Обоснования отнесений</summary>
    public IReadOnlyList<ClassificationReason> Reasons { get; init; } = Array.Empty<ClassificationReason>();

    /// <summary>Коды H-фраз</summary>
    public IReadOnlyList<string> HazardStatements { get; init; } = Array.Empty<string>();

    /// <summary>Коды P-фраз</summary>
    public IReadOnlyList<string> Precautions { get; init; } = Array.Empty<string>();

    /// <summary>Пиктограммы</summary>
    public IReadOnlyList<Pictogram> Pictograms { get; init; } = Array.Empty<Pictogram>();

    /// <summary>Сигнальное слово</summary>
    public SignalWord Signal { get; init; }

    /// <summary>Признана ли смесь опасной</summary>
    public bool IsHazardous => Hazards.Count > 0;

    /// <summary>Отчёт о классификации с обоснованиями</summary>
    public string Report()
    {
        var text = new StringBuilder();

        text.AppendLine("Классификация смеси по СГС");

        if (!IsHazardous)
        {
            text.AppendLine("  Смесь не классифицируется как опасная по реализованным правилам");
            return text.ToString();
        }

        text.AppendLine($"  Сигнальное слово: {HazardCatalog.Text(Signal)}");
        text.AppendLine($"  Пиктограммы: {(Pictograms.Count == 0 ? "не требуются" : string.Join(", ", Pictograms.Select(HazardCatalog.Code)))}");
        text.AppendLine();
        text.AppendLine("  Виды опасности:");

        foreach (ClassificationReason reason in Reasons)
            text.AppendLine($"    {reason}");

        text.AppendLine();
        text.AppendLine("  Фразы об опасности:");

        foreach (string code in HazardStatements)
            text.AppendLine($"    {code}: {HazardCatalog.HazardText(code)}");

        return text.ToString();
    }
}

/// <summary>
/// Классификация смеси по составу: расчётные правила СГС (Регламент CLP, приложение I;
/// в российской практике - ТР ТС 041 и ГОСТ 32419).
/// </summary>
/// <remarks>
/// Правила трёх типов: пороговое содержание опасного компонента, суммирование
/// содержаний внутри класса и расчёт ATE для острой токсичности. Физические виды
/// опасности расчётом не определяются - они задаются по результатам испытаний
/// в <see cref="Mixture.PhysicalHazards"/>.
/// <para>
/// Каждое отнесение сопровождается обоснованием: видно, какое правило сработало
/// и на каких числах, - именно это и требуется предъявить при проверке.
/// </para>
/// </remarks>
public static class MixtureClassifier
{
    // Пересчёт категории острой токсичности в оценку ATE (CLP, таблица 3.1.2)
    private static readonly Dictionary<ExposureRoute, Dictionary<string, double>> ConvertedAte = new()
    {
        [ExposureRoute.Oral] = new() { ["1"] = 0.5, ["2"] = 5, ["3"] = 100, ["4"] = 500 },
        [ExposureRoute.Dermal] = new() { ["1"] = 5, ["2"] = 50, ["3"] = 300, ["4"] = 1100 },
        [ExposureRoute.Inhalation] = new() { ["1"] = 0.05, ["2"] = 0.5, ["3"] = 3, ["4"] = 11 }
    };

    // Границы категорий по ATE
    private static readonly Dictionary<ExposureRoute, (double C1, double C2, double C3, double C4)> AteLimits = new()
    {
        [ExposureRoute.Oral] = (5, 50, 300, 2000),
        [ExposureRoute.Dermal] = (50, 200, 1000, 2000),
        [ExposureRoute.Inhalation] = (0.5, 2.0, 10, 20)
    };

    private static readonly Dictionary<ExposureRoute, HazardClass> AcuteClasses = new()
    {
        [ExposureRoute.Oral] = HazardClass.AcuteToxicityOral,
        [ExposureRoute.Dermal] = HazardClass.AcuteToxicityDermal,
        [ExposureRoute.Inhalation] = HazardClass.AcuteToxicityInhalation
    };

    /// <summary>Классифицирует смесь</summary>
    /// <param name="mixture">Смесь</param>
    public static MixtureClassification Classify(Mixture mixture)
    {
        ArgumentNullException.ThrowIfNull(mixture);

        var reasons = new List<ClassificationReason>();

        foreach (HazardCategory physical in mixture.PhysicalHazards)
            reasons.Add(new ClassificationReason(physical, "физическая опасность по результатам испытаний", 0, 0));

        foreach (ExposureRoute route in AcuteClasses.Keys)
            AddAcuteToxicity(mixture, route, reasons);

        AddSkinAndEye(mixture, reasons);
        AddSensitisation(mixture, reasons);
        AddGermCellAndCarcinogenic(mixture, reasons);
        AddSystemicToxicity(mixture, reasons);
        AddAspiration(mixture, reasons);
        AddAquatic(mixture, reasons);

        var hazards = reasons.Select(r => r.Category).Distinct().ToList();
        var entries = hazards.Where(HazardCatalog.Contains).Select(HazardCatalog.Entry).ToList();

        var pictograms = entries
            .Select(e => e.Pictogram)
            .Where(p => p != Pictogram.None)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        SignalWord signal = entries.Count == 0 ? SignalWord.None : entries.Max(e => e.Signal);

        return new MixtureClassification
        {
            Hazards = hazards,
            Reasons = reasons,
            HazardStatements = entries.Select(e => e.Statement).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList(),
            Precautions = HazardCatalog.Precautions(pictograms),
            Pictograms = pictograms,
            Signal = signal
        };
    }

    /// <summary>
    /// Оценка острой токсичности смеси: 100/ATE(смеси) = сумма(Ci/ATEi)
    /// </summary>
    /// <param name="mixture">Смесь</param>
    /// <param name="route">Путь поступления</param>
    public static double? AcuteToxicityEstimate(Mixture mixture, ExposureRoute route)
    {
        ArgumentNullException.ThrowIfNull(mixture);

        double sum = 0;
        bool any = false;

        foreach (MixtureComponent component in mixture.Components)
        {
            // Компоненты менее 1% в расчёт ATE не берутся
            if (component.ContentPercent < 1)
                continue;

            double? ate = AteOf(component, route);

            if (ate is null or <= 0)
                continue;

            sum += component.ContentPercent / ate.Value;
            any = true;
        }

        return any && sum > 0 ? 100.0 / sum : null;
    }

    private static double? AteOf(MixtureComponent component, ExposureRoute route)
    {
        if (component.AcuteToxicityEstimates != null
            && component.AcuteToxicityEstimates.TryGetValue(route, out double ate) && ate > 0)
        {
            return ate;
        }

        string category = component.CategoryOf(AcuteClasses[route]);

        if (string.IsNullOrEmpty(category))
            return null;

        return ConvertedAte[route].TryGetValue(category, out double converted) ? converted : null;
    }

    private static void AddAcuteToxicity(Mixture mixture, ExposureRoute route, List<ClassificationReason> reasons)
    {
        double? ate = AcuteToxicityEstimate(mixture, route);

        if (ate is null)
            return;

        var (c1, c2, c3, c4) = AteLimits[route];
        string category = ate <= c1 ? "1" : ate <= c2 ? "2" : ate <= c3 ? "3" : ate <= c4 ? "4" : null;

        if (category == null)
            return;

        double threshold = category switch { "1" => c1, "2" => c2, "3" => c3, _ => c4 };
        string routeName = route switch
        {
            ExposureRoute.Oral => "перорально",
            ExposureRoute.Dermal => "накожно",
            _ => "ингаляционно"
        };

        reasons.Add(new ClassificationReason(
            new HazardCategory(AcuteClasses[route], category),
            $"ATE смеси {routeName} не выше границы категории",
            ate.Value,
            threshold));
    }

    private static void AddSkinAndEye(Mixture mixture, List<ClassificationReason> reasons)
    {
        double corrosive = Sum(mixture, HazardClass.SkinCorrosion);
        double skinIrritant = Sum(mixture, HazardClass.SkinIrritation, "2");
        double eyeDamage = Sum(mixture, HazardClass.EyeDamage, "1");
        double eyeIrritant = Sum(mixture, HazardClass.EyeIrritation, "2");

        if (corrosive >= 5)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.SkinCorrosion, "1"),
                "сумма разъедающих кожу компонентов не менее 5%", corrosive, 5));
        }
        else if (corrosive >= 1)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.SkinIrritation, "2"),
                "сумма разъедающих компонентов от 1% до 5%", corrosive, 1));
        }
        else if (skinIrritant >= 10)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.SkinIrritation, "2"),
                "сумма раздражающих кожу компонентов не менее 10%", skinIrritant, 10));
        }
        else if ((10 * corrosive) + skinIrritant >= 10)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.SkinIrritation, "2"),
                "аддитивность: 10 x разъедающие + раздражающие не менее 10%", (10 * corrosive) + skinIrritant, 10));
        }

        // Разъедающие кожу компоненты одновременно повреждают глаза
        double eyeSevere = eyeDamage + corrosive;

        if (eyeSevere >= 3)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.EyeDamage, "1"),
                "сумма повреждающих глаза и разъедающих компонентов не менее 3%", eyeSevere, 3));
        }
        else if (eyeSevere >= 1)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.EyeIrritation, "2"),
                "сумма повреждающих глаза компонентов от 1% до 3%", eyeSevere, 1));
        }
        else if (eyeIrritant >= 10)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.EyeIrritation, "2"),
                "сумма раздражающих глаза компонентов не менее 10%", eyeIrritant, 10));
        }
        else if ((10 * eyeSevere) + eyeIrritant >= 10)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.EyeIrritation, "2"),
                "аддитивность: 10 x повреждающие + раздражающие не менее 10%", (10 * eyeSevere) + eyeIrritant, 10));
        }
    }

    private static void AddSensitisation(Mixture mixture, List<ClassificationReason> reasons)
    {
        double skinStrong = Sum(mixture, HazardClass.SkinSensitisation, "1A");
        double skinOther = Sum(mixture, HazardClass.SkinSensitisation, "1", "1B");

        if (skinStrong >= 0.1)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.SkinSensitisation, "1"),
                "сильные сенсибилизаторы кожи (1A) не менее 0.1%", skinStrong, 0.1));
        }
        else if (skinOther >= 1)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.SkinSensitisation, "1"),
                "сенсибилизаторы кожи не менее 1%", skinOther, 1));
        }

        double respStrong = Sum(mixture, HazardClass.RespiratorySensitisation, "1A");
        double respOther = Sum(mixture, HazardClass.RespiratorySensitisation, "1", "1B");
        double respThreshold = mixture.State == PhysicalState.Gas ? 0.2 : 1.0;

        if (respStrong >= 0.1)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.RespiratorySensitisation, "1"),
                "сильные сенсибилизаторы дыхательных путей (1A) не менее 0.1%", respStrong, 0.1));
        }
        else if (respOther >= respThreshold)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.RespiratorySensitisation, "1"),
                $"сенсибилизаторы дыхательных путей не менее {respThreshold.ToString(CultureInfo.InvariantCulture)}%",
                respOther, respThreshold));
        }
    }

    private static void AddGermCellAndCarcinogenic(Mixture mixture, List<ClassificationReason> reasons)
    {
        AddCmr(mixture, reasons, HazardClass.Mutagenicity, 0.1, 1.0);
        AddCmr(mixture, reasons, HazardClass.Carcinogenicity, 0.1, 1.0);
        AddCmr(mixture, reasons, HazardClass.ReproductiveToxicity, 0.3, 3.0);
    }

    private static void AddCmr(Mixture mixture, List<ClassificationReason> reasons,
        HazardClass hazardClass, double limitCategory1, double limitCategory2)
    {
        double category1 = Sum(mixture, hazardClass, "1A", "1B", "1");
        double category2 = Sum(mixture, hazardClass, "2");

        if (category1 >= limitCategory1)
        {
            reasons.Add(new ClassificationReason(new(hazardClass, "1B"),
                $"компоненты категории 1 не менее {limitCategory1.ToString(CultureInfo.InvariantCulture)}%",
                category1, limitCategory1));
        }
        else if (category2 >= limitCategory2)
        {
            reasons.Add(new ClassificationReason(new(hazardClass, "2"),
                $"компоненты категории 2 не менее {limitCategory2.ToString(CultureInfo.InvariantCulture)}%",
                category2, limitCategory2));
        }
    }

    private static void AddSystemicToxicity(Mixture mixture, List<ClassificationReason> reasons)
    {
        double single1 = Sum(mixture, HazardClass.StotSingle, "1");
        double single2 = Sum(mixture, HazardClass.StotSingle, "2");
        double single3 = Sum(mixture, HazardClass.StotSingle, "3");

        if (single1 >= 10)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.StotSingle, "1"),
                "компоненты STOT SE 1 не менее 10%", single1, 10));
        }
        else if (single1 >= 1)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.StotSingle, "2"),
                "компоненты STOT SE 1 от 1% до 10%", single1, 1));
        }
        else if (single2 >= 10)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.StotSingle, "2"),
                "компоненты STOT SE 2 не менее 10%", single2, 10));
        }

        if (single3 >= 20)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.StotSingle, "3"),
                "компоненты STOT SE 3 не менее 20%", single3, 20));
        }

        double repeated1 = Sum(mixture, HazardClass.StotRepeated, "1");
        double repeated2 = Sum(mixture, HazardClass.StotRepeated, "2");

        if (repeated1 >= 10)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.StotRepeated, "1"),
                "компоненты STOT RE 1 не менее 10%", repeated1, 10));
        }
        else if (repeated1 >= 1)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.StotRepeated, "2"),
                "компоненты STOT RE 1 от 1% до 10%", repeated1, 1));
        }
        else if (repeated2 >= 10)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.StotRepeated, "2"),
                "компоненты STOT RE 2 не менее 10%", repeated2, 10));
        }
    }

    private static void AddAspiration(Mixture mixture, List<ClassificationReason> reasons)
    {
        double aspiration = Sum(mixture, HazardClass.AspirationHazard, "1");

        if (aspiration < 10)
            return;

        // Отнесение требует ещё и достаточной текучести смеси
        double? viscosity = mixture.KinematicViscosity;

        if (viscosity is > 20.5)
            return;

        reasons.Add(new ClassificationReason(new(HazardClass.AspirationHazard, "1"),
            viscosity is null
                ? "компоненты Asp. Tox. 1 не менее 10% (вязкость смеси не задана, принято худшее)"
                : "компоненты Asp. Tox. 1 не менее 10% при вязкости не выше 20.5 мм2/с",
            aspiration, 10));
    }

    private static void AddAquatic(Mixture mixture, List<ClassificationReason> reasons)
    {
        double acute = mixture.Components
            .Where(c => c.HasHazard(HazardClass.AquaticAcute, "1"))
            .Sum(c => c.ContentPercent * c.AcuteMFactor);

        if (acute >= 25)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.AquaticAcute, "1"),
                "сумма содержаний с коэффициентом M не менее 25%", acute, 25));
        }

        double chronic1 = mixture.Components
            .Where(c => c.HasHazard(HazardClass.AquaticChronic, "1"))
            .Sum(c => c.ContentPercent * c.ChronicMFactor);

        double chronic2 = Sum(mixture, HazardClass.AquaticChronic, "2");
        double chronic3 = Sum(mixture, HazardClass.AquaticChronic, "3");
        double chronic4 = Sum(mixture, HazardClass.AquaticChronic, "4");

        if (chronic1 >= 25)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.AquaticChronic, "1"),
                "сумма хронически опасных компонентов категории 1 с коэффициентом M не менее 25%", chronic1, 25));
        }
        else if ((10 * chronic1) + chronic2 >= 25)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.AquaticChronic, "2"),
                "аддитивность: 10 x категория 1 + категория 2 не менее 25%", (10 * chronic1) + chronic2, 25));
        }
        else if ((100 * chronic1) + (10 * chronic2) + chronic3 >= 25)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.AquaticChronic, "3"),
                "аддитивность: 100 x кат. 1 + 10 x кат. 2 + кат. 3 не менее 25%",
                (100 * chronic1) + (10 * chronic2) + chronic3, 25));
        }
        else if (chronic4 >= 25)
        {
            reasons.Add(new ClassificationReason(new(HazardClass.AquaticChronic, "4"),
                "сумма компонентов категории 4 не менее 25%", chronic4, 25));
        }
    }

    private static double Sum(Mixture mixture, HazardClass hazardClass, params string[] categories)
        => mixture.Components.Where(c => c.HasHazard(hazardClass, categories)).Sum(c => c.ContentPercent);
}
