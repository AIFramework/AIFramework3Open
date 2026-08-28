using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Safety;

/// <summary>
/// Сведения для раздела о перевозке
/// </summary>
/// <param name="UnNumber">Номер ООН</param>
/// <param name="ShippingName">Надлежащее отгрузочное наименование</param>
/// <param name="TransportClass">Класс опасности груза</param>
/// <param name="PackingGroup">Группа упаковки</param>
/// <param name="MarinePollutant">Загрязнитель моря по МКМПОГ</param>
/// <param name="Source">Откуда взяты данные</param>
public readonly record struct TransportClassification(
    string UnNumber,
    string ShippingName,
    string TransportClass,
    string PackingGroup,
    bool MarinePollutant,
    string Source)
{
    /// <summary>Отнесён ли продукт к опасным грузам</summary>
    public bool IsDangerousGoods => !string.IsNullOrEmpty(UnNumber);
}

/// <summary>
/// Паспорт безопасности химической продукции: 16 разделов.
/// </summary>
/// <remarks>
/// Разделы, которые выводятся из состава - классификация, маркировка, состав,
/// токсикология, экология, перевозка, - заполняются расчётом. Разделы,
/// требующие описания конкретного производства (первая помощь, хранение,
/// утилизация), собираются из рекомендованных мер предосторожности и
/// дополняются текстом автора через <see cref="SetNarrative"/>: правила отвечают
/// за классификацию, человек или языковая модель - за формулировки.
/// </remarks>
public sealed class SafetyDataSheet
{
    private readonly Dictionary<int, string> _narrative = new();

    /// <summary>Смесь</summary>
    public Mixture Mixture { get; }

    /// <summary>Классификация смеси</summary>
    public MixtureClassification Classification { get; }

    /// <summary>Наименование поставщика</summary>
    public string Supplier { get; set; } = string.Empty;

    /// <summary>Адрес поставщика</summary>
    public string SupplierAddress { get; set; } = string.Empty;

    /// <summary>Телефон поставщика</summary>
    public string SupplierPhone { get; set; } = string.Empty;

    /// <summary>Телефон экстренной связи</summary>
    public string EmergencyPhone { get; set; } = string.Empty;

    /// <summary>Дата пересмотра</summary>
    public string RevisionDate { get; set; } = string.Empty;

    /// <summary>Версия паспорта</summary>
    public string Version { get; set; } = "1";

    /// <summary>Сведения о перевозке</summary>
    public TransportClassification Transport { get; }

    /// <summary>Создаёт паспорт по смеси и её классификации</summary>
    /// <param name="mixture">Смесь</param>
    /// <param name="classification">Классификация</param>
    public SafetyDataSheet(Mixture mixture, MixtureClassification classification)
    {
        Mixture = mixture ?? throw new ArgumentNullException(nameof(mixture));
        Classification = classification ?? throw new ArgumentNullException(nameof(classification));
        Transport = ClassifyTransport(mixture, classification);
    }

    /// <summary>
    /// Задаёт текст раздела, который нельзя вывести из состава
    /// </summary>
    /// <param name="section">Номер раздела, 1..16</param>
    /// <param name="text">Текст</param>
    public SafetyDataSheet SetNarrative(int section, string text)
    {
        if (section is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(section), "Разделов паспорта шестнадцать");

        _narrative[section] = text;
        return this;
    }

    /// <summary>Разделы, которые остались без текста автора</summary>
    public IReadOnlyList<int> MissingNarrativeSections
        => new[] { 4, 5, 6, 7, 8, 10, 13, 15 }.Where(s => !_narrative.ContainsKey(s)).ToList();

    /// <summary>Формирует паспорт целиком</summary>
    public string Render()
    {
        var text = new StringBuilder();

        text.AppendLine("ПАСПОРТ БЕЗОПАСНОСТИ ХИМИЧЕСКОЙ ПРОДУКЦИИ");
        text.AppendLine($"Продукция: {Mixture.Name}");
        text.AppendLine($"Версия: {Version}{(string.IsNullOrEmpty(RevisionDate) ? string.Empty : $", дата пересмотра: {RevisionDate}")}");
        text.AppendLine();

        Section(text, 1, "Идентификация химической продукции и сведения о поставщике", IdentificationSection());
        Section(text, 2, "Идентификация опасности (опасностей)", HazardSection());
        Section(text, 3, "Состав (информация о компонентах)", CompositionSection());
        Section(text, 4, "Меры первой помощи", FirstAidSection());
        Section(text, 5, "Меры и средства обеспечения пожаровзрывобезопасности", FireSection());
        Section(text, 6, "Меры по предотвращению и ликвидации аварийных ситуаций", AccidentSection());
        Section(text, 7, "Правила хранения и обращения", HandlingSection());
        Section(text, 8, "Средства контроля за опасным воздействием и средства защиты", ProtectionSection());
        Section(text, 9, "Физико-химические свойства", PhysicalSection());
        Section(text, 10, "Стабильность и реакционная способность", Narrative(10));
        Section(text, 11, "Информация о токсичности", ToxicologySection());
        Section(text, 12, "Информация о воздействии на окружающую среду", EcologySection());
        Section(text, 13, "Рекомендации по удалению отходов", Narrative(13));
        Section(text, 14, "Информация при перевозках (транспортировании)", TransportSection());
        Section(text, 15, "Информация о национальном и международном законодательстве", Narrative(15));
        Section(text, 16, "Дополнительная информация", AdditionalSection());

        return text.ToString();
    }

    private static void Section(StringBuilder text, int number, string title, string body)
    {
        text.AppendLine($"РАЗДЕЛ {number}. {title}");
        text.AppendLine(body.TrimEnd());
        text.AppendLine();
    }

    private string Narrative(int section)
        => _narrative.TryGetValue(section, out string text) && !string.IsNullOrWhiteSpace(text)
            ? Indent(text)
            : "  [требуется текст раздела]";

    private static string Indent(string text)
        => string.Join(Environment.NewLine, text.Split('\n').Select(line => "  " + line.TrimEnd()));

    private string IdentificationSection()
    {
        var text = new StringBuilder();
        text.AppendLine($"  Наименование продукции: {Mixture.Name}");

        if (!string.IsNullOrEmpty(Mixture.Use))
            text.AppendLine($"  Назначение: {Mixture.Use}");

        text.AppendLine($"  Поставщик: {Or(Supplier)}");
        text.AppendLine($"  Адрес: {Or(SupplierAddress)}");
        text.AppendLine($"  Телефон: {Or(SupplierPhone)}");
        text.AppendLine($"  Телефон экстренной связи: {Or(EmergencyPhone)}");

        if (_narrative.TryGetValue(1, out string extra))
            text.AppendLine(Indent(extra));

        return text.ToString();
    }

    private string HazardSection()
    {
        var text = new StringBuilder();

        if (!Classification.IsHazardous)
        {
            text.AppendLine("  Смесь не классифицируется как опасная по расчётным правилам СГС.");
            text.AppendLine("  Физические виды опасности определяются испытаниями.");
            return text.ToString();
        }

        text.AppendLine("  Классификация:");

        foreach (ClassificationReason reason in Classification.Reasons)
            text.AppendLine($"    {reason.Category} - {reason.Rule}");

        text.AppendLine();
        text.AppendLine("  Элементы маркировки:");
        text.AppendLine($"    Сигнальное слово: {HazardCatalog.Text(Classification.Signal)}");
        text.AppendLine($"    Пиктограммы: {(Classification.Pictograms.Count == 0 ? "не требуются" : string.Join(", ", Classification.Pictograms.Select(p => $"{HazardCatalog.Code(p)} ({HazardCatalog.Title(p)})")))}");
        text.AppendLine();
        text.AppendLine("    Краткая характеристика опасности:");

        foreach (string code in Classification.HazardStatements)
            text.AppendLine($"      {code} {HazardCatalog.HazardText(code)}");

        text.AppendLine();
        text.AppendLine("    Меры предупредительные:");

        foreach (string code in Classification.Precautions)
            text.AppendLine($"      {code} {HazardCatalog.PrecautionaryText(code)}");

        return text.ToString();
    }

    private string CompositionSection()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine("  Наименование                CAS           Содержание, %   Классификация");

        foreach (MixtureComponent component in Mixture.Components.OrderByDescending(c => c.ContentPercent))
        {
            string hazards = component.Classifications.Count == 0
                ? "не классифицирован"
                : string.Join("; ", component.Classifications.Select(c => c.ToString()));

            text.AppendLine(string.Format(culture, "  {0,-27} {1,-13} {2,13:F2}   {3}",
                Truncate(component.Name, 27), Or(component.CasNumber, "-"), component.ContentPercent, hazards));
        }

        double total = Mixture.TotalContentPercent;

        if (Math.Abs(total - 100) > 0.5)
            text.AppendLine(string.Format(culture, "  Сумма содержаний компонентов: {0:F2}% (остальное - неопасные компоненты)", total));

        return text.ToString();
    }

    private string FirstAidSection()
    {
        var text = new StringBuilder();
        var relevant = Classification.Precautions.Where(p => p.StartsWith("P3", StringComparison.Ordinal)).ToList();

        if (relevant.Count > 0)
        {
            text.AppendLine("  По результатам классификации:");

            foreach (string code in relevant)
                text.AppendLine($"    {code} {HazardCatalog.PrecautionaryText(code)}");
        }

        text.AppendLine(Narrative(4));

        return text.ToString();
    }

    private string FireSection()
    {
        var text = new StringBuilder();

        if (Classification.Hazards.Any(h => h.Class is HazardClass.FlammableLiquid or HazardClass.FlammableSolid))
            text.AppendLine("  Продукция классифицирована как воспламеняющаяся: исключить источники зажигания.");

        if (Classification.Hazards.Any(h => h.Class is HazardClass.OxidisingLiquid or HazardClass.OxidisingSolid))
            text.AppendLine("  Продукция классифицирована как окислитель: возможно усиление горения.");

        text.AppendLine(Narrative(5));

        return text.ToString();
    }

    private string AccidentSection()
    {
        var text = new StringBuilder();

        if (Classification.Hazards.Any(h => h.Class is HazardClass.AquaticAcute or HazardClass.AquaticChronic))
            text.AppendLine("  Не допускать попадания в водоёмы и канализацию (см. раздел 12).");

        text.AppendLine(Narrative(6));

        return text.ToString();
    }

    private string HandlingSection() => Narrative(7);

    private string ProtectionSection()
    {
        var text = new StringBuilder();

        if (Classification.Pictograms.Contains(Pictogram.Ghs05Corrosion) || Classification.Pictograms.Contains(Pictogram.Ghs06Skull))
            text.AppendLine("  Требуются перчатки, защитные очки и спецодежда, стойкие к продукции.");

        text.AppendLine(Narrative(8));

        return text.ToString();
    }

    private string PhysicalSection()
    {
        var text = new StringBuilder();
        string state = Mixture.State switch
        {
            PhysicalState.Liquid => "жидкость",
            PhysicalState.Solid => "твёрдое вещество",
            _ => "газ"
        };

        text.AppendLine($"  Агрегатное состояние: {state}");

        if (Mixture.KinematicViscosity.HasValue)
        {
            text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  Кинематическая вязкость: {0:G4} мм2/с", Mixture.KinematicViscosity.Value));
        }

        if (_narrative.TryGetValue(9, out string extra))
            text.AppendLine(Indent(extra));
        else
            text.AppendLine("  Остальные показатели определяются испытаниями продукции");

        return text.ToString();
    }

    private string ToxicologySection()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        foreach (ExposureRoute route in new[] { ExposureRoute.Oral, ExposureRoute.Dermal, ExposureRoute.Inhalation })
        {
            double? ate = MixtureClassifier.AcuteToxicityEstimate(Mixture, route);

            if (ate is null)
                continue;

            string name = route switch
            {
                ExposureRoute.Oral => "перорально, мг/кг",
                ExposureRoute.Dermal => "накожно, мг/кг",
                _ => "ингаляционно (пары), мг/л"
            };

            text.AppendLine(string.Format(culture, "  Расчётная оценка острой токсичности {0}: {1:G4}", name, ate.Value));
        }

        var effects = Classification.Hazards
            .Where(h => h.Class is HazardClass.SkinCorrosion or HazardClass.SkinIrritation
                or HazardClass.EyeDamage or HazardClass.EyeIrritation
                or HazardClass.SkinSensitisation or HazardClass.RespiratorySensitisation
                or HazardClass.Carcinogenicity or HazardClass.Mutagenicity or HazardClass.ReproductiveToxicity
                or HazardClass.StotSingle or HazardClass.StotRepeated or HazardClass.AspirationHazard)
            .ToList();

        if (effects.Count > 0)
            text.AppendLine($"  Прочие эффекты по классификации: {string.Join("; ", effects)}");

        if (text.Length == 0)
            text.AppendLine("  Данных для расчётной оценки токсичности недостаточно");

        if (_narrative.TryGetValue(11, out string extra))
            text.AppendLine(Indent(extra));

        return text.ToString();
    }

    private string EcologySection()
    {
        var text = new StringBuilder();

        var aquatic = Classification.Hazards
            .Where(h => h.Class is HazardClass.AquaticAcute or HazardClass.AquaticChronic)
            .ToList();

        text.AppendLine(aquatic.Count > 0
            ? $"  Классификация по опасности для водной среды: {string.Join("; ", aquatic)}"
            : "  По расчётным правилам смесь не классифицируется как опасная для водной среды");

        if (_narrative.TryGetValue(12, out string extra))
            text.AppendLine(Indent(extra));

        return text.ToString();
    }

    private string TransportSection()
    {
        var text = new StringBuilder();

        if (!Transport.IsDangerousGoods)
        {
            text.AppendLine("  По имеющимся данным продукция не отнесена к опасным грузам.");
            text.AppendLine("  Отнесение подтверждается по правилам ДОПОГ/МКМПОГ для конкретной упаковки.");
            return text.ToString();
        }

        text.AppendLine($"  Номер ООН: {Transport.UnNumber}");
        text.AppendLine($"  Надлежащее отгрузочное наименование: {Or(Transport.ShippingName)}");
        text.AppendLine($"  Класс опасности груза: {Or(Transport.TransportClass)}");
        text.AppendLine($"  Группа упаковки: {Or(Transport.PackingGroup)}");
        text.AppendLine($"  Загрязнитель моря (МКМПОГ): {(Transport.MarinePollutant ? "да" : "нет")}");
        text.AppendLine($"  Источник данных: {Transport.Source}");

        return text.ToString();
    }

    private string AdditionalSection()
    {
        var text = new StringBuilder();

        if (Classification.HazardStatements.Count > 0)
        {
            text.AppendLine("  Полные тексты фраз об опасности:");

            foreach (string code in Classification.HazardStatements)
                text.AppendLine($"    {code}: {HazardCatalog.HazardText(code)}");
        }

        var componentCodes = Mixture.Components
            .SelectMany(c => c.Classifications)
            .Where(HazardCatalog.Contains)
            .Select(c => HazardCatalog.Entry(c).Statement)
            .Distinct(StringComparer.Ordinal)
            .Except(Classification.HazardStatements, StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        if (componentCodes.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("  Фразы компонентов, не перешедшие в классификацию смеси:");

            foreach (string code in componentCodes)
                text.AppendLine($"    {code}: {HazardCatalog.HazardText(code)}");
        }

        IReadOnlyList<int> missing = MissingNarrativeSections;

        if (missing.Count > 0)
        {
            text.AppendLine();
            text.AppendLine($"  Требуют заполнения разделы: {string.Join(", ", missing)}");
        }

        if (_narrative.TryGetValue(16, out string extra))
            text.AppendLine(Indent(extra));

        return text.ToString();
    }

    /// <summary>
    /// Определяет сведения о перевозке по наиболее опасному компоненту
    /// </summary>
    private static TransportClassification ClassifyTransport(Mixture mixture, MixtureClassification classification)
    {
        bool marinePollutant = classification.Hazards.Any(h =>
            (h.Class == HazardClass.AquaticAcute && h.Category == "1")
            || (h.Class == HazardClass.AquaticChronic && h.Category is "1" or "2"));

        // Груз опознаётся по компоненту с номером ООН и наибольшим содержанием:
        // окончательное отнесение смеси делается по правилам ДОПОГ для упаковки
        MixtureComponent carrier = mixture.Components
            .Where(c => !string.IsNullOrEmpty(c.UnNumber))
            .OrderByDescending(c => c.ContentPercent)
            .FirstOrDefault();

        if (carrier == null)
            return new TransportClassification(string.Empty, string.Empty, string.Empty, string.Empty, marinePollutant, "нет данных по компонентам");

        return new TransportClassification(
            carrier.UnNumber,
            string.IsNullOrEmpty(carrier.ShippingName) ? carrier.Name : carrier.ShippingName,
            carrier.TransportClass,
            carrier.PackingGroup,
            marinePollutant,
            $"по компоненту {carrier.Name} ({carrier.ContentPercent.ToString("F1", CultureInfo.InvariantCulture)}%)");
    }

    private static string Or(string value, string fallback = "[не указано]")
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string Truncate(string value, int length)
    {
        if (string.IsNullOrEmpty(value))
            return "-";

        return value.Length <= length ? value : value.Substring(0, length - 1) + ".";
    }
}
