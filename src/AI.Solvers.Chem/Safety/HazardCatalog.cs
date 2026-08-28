namespace AI.Solvers.Chem.Safety;

/// <summary>
/// Класс опасности по СГС (GHS/CLP)
/// </summary>
public enum HazardClass
{
    /// <summary>Воспламеняющаяся жидкость</summary>
    FlammableLiquid,

    /// <summary>Воспламеняющееся твёрдое вещество</summary>
    FlammableSolid,

    /// <summary>Окисляющая жидкость</summary>
    OxidisingLiquid,

    /// <summary>Окисляющее твёрдое вещество</summary>
    OxidisingSolid,

    /// <summary>Газ под давлением</summary>
    GasUnderPressure,

    /// <summary>Вещество, вызывающее коррозию металлов</summary>
    CorrosiveToMetals,

    /// <summary>Острая токсичность при проглатывании</summary>
    AcuteToxicityOral,

    /// <summary>Острая токсичность при попадании на кожу</summary>
    AcuteToxicityDermal,

    /// <summary>Острая токсичность при вдыхании</summary>
    AcuteToxicityInhalation,

    /// <summary>Разъедание кожи</summary>
    SkinCorrosion,

    /// <summary>Раздражение кожи</summary>
    SkinIrritation,

    /// <summary>Серьёзное повреждение глаз</summary>
    EyeDamage,

    /// <summary>Раздражение глаз</summary>
    EyeIrritation,

    /// <summary>Сенсибилизация дыхательных путей</summary>
    RespiratorySensitisation,

    /// <summary>Сенсибилизация кожи</summary>
    SkinSensitisation,

    /// <summary>Мутагенность</summary>
    Mutagenicity,

    /// <summary>Канцерогенность</summary>
    Carcinogenicity,

    /// <summary>Репродуктивная токсичность</summary>
    ReproductiveToxicity,

    /// <summary>Избирательная токсичность при однократном воздействии</summary>
    StotSingle,

    /// <summary>Избирательная токсичность при многократном воздействии</summary>
    StotRepeated,

    /// <summary>Аспирационная опасность</summary>
    AspirationHazard,

    /// <summary>Острая опасность для водной среды</summary>
    AquaticAcute,

    /// <summary>Хроническая опасность для водной среды</summary>
    AquaticChronic
}

/// <summary>
/// Пиктограмма СГС
/// </summary>
public enum Pictogram
{
    /// <summary>Нет пиктограммы</summary>
    None,

    /// <summary>GHS01: взрывающаяся бомба</summary>
    Ghs01Explosive,

    /// <summary>GHS02: пламя</summary>
    Ghs02Flame,

    /// <summary>GHS03: пламя над окружностью</summary>
    Ghs03Oxidising,

    /// <summary>GHS04: газовый баллон</summary>
    Ghs04GasCylinder,

    /// <summary>GHS05: коррозия</summary>
    Ghs05Corrosion,

    /// <summary>GHS06: череп и скрещённые кости</summary>
    Ghs06Skull,

    /// <summary>GHS07: восклицательный знак</summary>
    Ghs07Exclamation,

    /// <summary>GHS08: опасность для здоровья</summary>
    Ghs08HealthHazard,

    /// <summary>GHS09: окружающая среда</summary>
    Ghs09Environment
}

/// <summary>
/// Сигнальное слово
/// </summary>
public enum SignalWord
{
    /// <summary>Не требуется</summary>
    None,

    /// <summary>Осторожно</summary>
    Warning,

    /// <summary>Опасно</summary>
    Danger
}

/// <summary>
/// Класс опасности вместе с категорией: "Skin Corr. 1A", "Acute Tox. 4"
/// </summary>
/// <param name="Class">Класс опасности</param>
/// <param name="Category">Категория: "1", "1A", "2", "3", "4"</param>
public readonly record struct HazardCategory(HazardClass Class, string Category)
{
    /// <summary>Запись в принятой нотации CLP</summary>
    public override string ToString() => $"{HazardCatalog.ShortName(Class)} {Category}";
}

/// <summary>
/// Запись справочника: фраза об опасности, пиктограмма, сигнальное слово
/// </summary>
/// <param name="Statement">Код H-фразы</param>
/// <param name="Pictogram">Пиктограмма</param>
/// <param name="Signal">Сигнальное слово</param>
public readonly record struct HazardEntry(string Statement, Pictogram Pictogram, SignalWord Signal);

/// <summary>
/// Справочник СГС: соответствие «класс и категория опасности - H-фраза, пиктограмма,
/// сигнальное слово», тексты H- и P-фраз.
/// </summary>
/// <remarks>
/// Тексты приведены по ГОСТ 31340 и Регламенту CLP; коды совпадают, поэтому один
/// справочник обслуживает и паспорт по ТР ТС, и SDS по европейскому образцу.
/// Классификация смеси считается детерминированно (<see cref="MixtureClassifier"/>),
/// а связный текст паспорта пишется поверх неё.
/// </remarks>
public static class HazardCatalog
{
    private static readonly Dictionary<HazardCategory, HazardEntry> Entries = new()
    {
        // Физические виды опасности
        [new(HazardClass.FlammableLiquid, "1")] = new("H224", Pictogram.Ghs02Flame, SignalWord.Danger),
        [new(HazardClass.FlammableLiquid, "2")] = new("H225", Pictogram.Ghs02Flame, SignalWord.Danger),
        [new(HazardClass.FlammableLiquid, "3")] = new("H226", Pictogram.Ghs02Flame, SignalWord.Warning),
        [new(HazardClass.FlammableLiquid, "4")] = new("H227", Pictogram.None, SignalWord.Warning),
        [new(HazardClass.FlammableSolid, "1")] = new("H228", Pictogram.Ghs02Flame, SignalWord.Danger),
        [new(HazardClass.FlammableSolid, "2")] = new("H228", Pictogram.Ghs02Flame, SignalWord.Warning),
        [new(HazardClass.OxidisingLiquid, "1")] = new("H271", Pictogram.Ghs03Oxidising, SignalWord.Danger),
        [new(HazardClass.OxidisingLiquid, "2")] = new("H272", Pictogram.Ghs03Oxidising, SignalWord.Danger),
        [new(HazardClass.OxidisingLiquid, "3")] = new("H272", Pictogram.Ghs03Oxidising, SignalWord.Warning),
        [new(HazardClass.OxidisingSolid, "1")] = new("H271", Pictogram.Ghs03Oxidising, SignalWord.Danger),
        [new(HazardClass.OxidisingSolid, "2")] = new("H272", Pictogram.Ghs03Oxidising, SignalWord.Danger),
        [new(HazardClass.OxidisingSolid, "3")] = new("H272", Pictogram.Ghs03Oxidising, SignalWord.Warning),
        [new(HazardClass.GasUnderPressure, "1")] = new("H280", Pictogram.Ghs04GasCylinder, SignalWord.Warning),
        [new(HazardClass.GasUnderPressure, "2")] = new("H281", Pictogram.Ghs04GasCylinder, SignalWord.Warning),
        [new(HazardClass.CorrosiveToMetals, "1")] = new("H290", Pictogram.Ghs05Corrosion, SignalWord.Warning),

        // Острая токсичность
        [new(HazardClass.AcuteToxicityOral, "1")] = new("H300", Pictogram.Ghs06Skull, SignalWord.Danger),
        [new(HazardClass.AcuteToxicityOral, "2")] = new("H300", Pictogram.Ghs06Skull, SignalWord.Danger),
        [new(HazardClass.AcuteToxicityOral, "3")] = new("H301", Pictogram.Ghs06Skull, SignalWord.Danger),
        [new(HazardClass.AcuteToxicityOral, "4")] = new("H302", Pictogram.Ghs07Exclamation, SignalWord.Warning),
        [new(HazardClass.AcuteToxicityDermal, "1")] = new("H310", Pictogram.Ghs06Skull, SignalWord.Danger),
        [new(HazardClass.AcuteToxicityDermal, "2")] = new("H310", Pictogram.Ghs06Skull, SignalWord.Danger),
        [new(HazardClass.AcuteToxicityDermal, "3")] = new("H311", Pictogram.Ghs06Skull, SignalWord.Danger),
        [new(HazardClass.AcuteToxicityDermal, "4")] = new("H312", Pictogram.Ghs07Exclamation, SignalWord.Warning),
        [new(HazardClass.AcuteToxicityInhalation, "1")] = new("H330", Pictogram.Ghs06Skull, SignalWord.Danger),
        [new(HazardClass.AcuteToxicityInhalation, "2")] = new("H330", Pictogram.Ghs06Skull, SignalWord.Danger),
        [new(HazardClass.AcuteToxicityInhalation, "3")] = new("H331", Pictogram.Ghs06Skull, SignalWord.Danger),
        [new(HazardClass.AcuteToxicityInhalation, "4")] = new("H332", Pictogram.Ghs07Exclamation, SignalWord.Warning),

        // Кожа и глаза
        [new(HazardClass.SkinCorrosion, "1A")] = new("H314", Pictogram.Ghs05Corrosion, SignalWord.Danger),
        [new(HazardClass.SkinCorrosion, "1B")] = new("H314", Pictogram.Ghs05Corrosion, SignalWord.Danger),
        [new(HazardClass.SkinCorrosion, "1C")] = new("H314", Pictogram.Ghs05Corrosion, SignalWord.Danger),
        [new(HazardClass.SkinCorrosion, "1")] = new("H314", Pictogram.Ghs05Corrosion, SignalWord.Danger),
        [new(HazardClass.SkinIrritation, "2")] = new("H315", Pictogram.Ghs07Exclamation, SignalWord.Warning),
        [new(HazardClass.EyeDamage, "1")] = new("H318", Pictogram.Ghs05Corrosion, SignalWord.Danger),
        [new(HazardClass.EyeIrritation, "2")] = new("H319", Pictogram.Ghs07Exclamation, SignalWord.Warning),

        // Сенсибилизация
        [new(HazardClass.RespiratorySensitisation, "1")] = new("H334", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.RespiratorySensitisation, "1A")] = new("H334", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.RespiratorySensitisation, "1B")] = new("H334", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.SkinSensitisation, "1")] = new("H317", Pictogram.Ghs07Exclamation, SignalWord.Warning),
        [new(HazardClass.SkinSensitisation, "1A")] = new("H317", Pictogram.Ghs07Exclamation, SignalWord.Warning),
        [new(HazardClass.SkinSensitisation, "1B")] = new("H317", Pictogram.Ghs07Exclamation, SignalWord.Warning),

        // Мутагенность, канцерогенность, репродуктивная токсичность
        [new(HazardClass.Mutagenicity, "1A")] = new("H340", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.Mutagenicity, "1B")] = new("H340", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.Mutagenicity, "2")] = new("H341", Pictogram.Ghs08HealthHazard, SignalWord.Warning),
        [new(HazardClass.Carcinogenicity, "1A")] = new("H350", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.Carcinogenicity, "1B")] = new("H350", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.Carcinogenicity, "2")] = new("H351", Pictogram.Ghs08HealthHazard, SignalWord.Warning),
        [new(HazardClass.ReproductiveToxicity, "1A")] = new("H360", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.ReproductiveToxicity, "1B")] = new("H360", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.ReproductiveToxicity, "2")] = new("H361", Pictogram.Ghs08HealthHazard, SignalWord.Warning),

        // Избирательная токсичность и аспирация
        [new(HazardClass.StotSingle, "1")] = new("H370", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.StotSingle, "2")] = new("H371", Pictogram.Ghs08HealthHazard, SignalWord.Warning),
        [new(HazardClass.StotSingle, "3")] = new("H335", Pictogram.Ghs07Exclamation, SignalWord.Warning),
        [new(HazardClass.StotRepeated, "1")] = new("H372", Pictogram.Ghs08HealthHazard, SignalWord.Danger),
        [new(HazardClass.StotRepeated, "2")] = new("H373", Pictogram.Ghs08HealthHazard, SignalWord.Warning),
        [new(HazardClass.AspirationHazard, "1")] = new("H304", Pictogram.Ghs08HealthHazard, SignalWord.Danger),

        // Опасность для водной среды
        [new(HazardClass.AquaticAcute, "1")] = new("H400", Pictogram.Ghs09Environment, SignalWord.Warning),
        [new(HazardClass.AquaticChronic, "1")] = new("H410", Pictogram.Ghs09Environment, SignalWord.Warning),
        [new(HazardClass.AquaticChronic, "2")] = new("H411", Pictogram.Ghs09Environment, SignalWord.None),
        [new(HazardClass.AquaticChronic, "3")] = new("H412", Pictogram.None, SignalWord.None),
        [new(HazardClass.AquaticChronic, "4")] = new("H413", Pictogram.None, SignalWord.None)
    };

    private static readonly Dictionary<string, string> HazardTexts = new(StringComparer.Ordinal)
    {
        ["H224"] = "Чрезвычайно легковоспламеняющаяся жидкость и пар",
        ["H225"] = "Легковоспламеняющаяся жидкость и пар",
        ["H226"] = "Воспламеняющаяся жидкость и пар",
        ["H227"] = "Горючая жидкость",
        ["H228"] = "Воспламеняющееся твёрдое вещество",
        ["H271"] = "Может вызвать возгорание или взрыв; сильный окислитель",
        ["H272"] = "Может усилить возгорание; окислитель",
        ["H280"] = "Содержит газ под давлением; при нагревании может взорваться",
        ["H281"] = "Содержит охлаждённый газ; может вызвать криогенные ожоги или травмы",
        ["H290"] = "Может вызывать коррозию металлов",
        ["H300"] = "Смертельно при проглатывании",
        ["H301"] = "Токсично при проглатывании",
        ["H302"] = "Вредно при проглатывании",
        ["H304"] = "Может быть смертельно при проглатывании и попадании в дыхательные пути",
        ["H310"] = "Смертельно при контакте с кожей",
        ["H311"] = "Токсично при контакте с кожей",
        ["H312"] = "Вредно при контакте с кожей",
        ["H314"] = "Вызывает серьёзные ожоги кожи и повреждение глаз",
        ["H315"] = "Вызывает раздражение кожи",
        ["H317"] = "Может вызывать аллергическую реакцию кожи",
        ["H318"] = "Вызывает серьёзное повреждение глаз",
        ["H319"] = "Вызывает серьёзное раздражение глаз",
        ["H330"] = "Смертельно при вдыхании",
        ["H331"] = "Токсично при вдыхании",
        ["H332"] = "Вредно при вдыхании",
        ["H334"] = "Может вызывать симптомы аллергии или астмы либо затруднение дыхания при вдыхании",
        ["H335"] = "Может вызывать раздражение дыхательных путей",
        ["H336"] = "Может вызывать сонливость или головокружение",
        ["H340"] = "Может вызывать генетические дефекты",
        ["H341"] = "Предполагается, что вызывает генетические дефекты",
        ["H350"] = "Может вызывать онкологические заболевания",
        ["H351"] = "Предполагается, что вызывает онкологические заболевания",
        ["H360"] = "Может нанести вред репродуктивному здоровью или неродившемуся ребёнку",
        ["H361"] = "Предполагается, что наносит вред репродуктивному здоровью или неродившемуся ребёнку",
        ["H370"] = "Наносит ущерб органам",
        ["H371"] = "Может наносить ущерб органам",
        ["H372"] = "Наносит ущерб органам в результате многократного или продолжительного воздействия",
        ["H373"] = "Может наносить ущерб органам в результате многократного или продолжительного воздействия",
        ["H400"] = "Чрезвычайно токсично для водных организмов",
        ["H410"] = "Чрезвычайно токсично для водных организмов с долгосрочными последствиями",
        ["H411"] = "Токсично для водных организмов с долгосрочными последствиями",
        ["H412"] = "Вредно для водных организмов с долгосрочными последствиями",
        ["H413"] = "Может вызывать долгосрочные вредные последствия для водных организмов"
    };

    private static readonly Dictionary<string, string> PrecautionaryTexts = new(StringComparer.Ordinal)
    {
        ["P101"] = "При обращении за медицинской консультацией держите упаковку или этикетку под рукой",
        ["P102"] = "Хранить в недоступном для детей месте",
        ["P103"] = "Перед использованием ознакомьтесь с этикеткой",
        ["P201"] = "Перед использованием получить специальные инструкции",
        ["P202"] = "Не приступать к работе до ознакомления со всеми мерами безопасности",
        ["P220"] = "Не допускать контакта с горючими материалами",
        ["P410+P403"] = "Беречь от солнечных лучей; хранить в хорошо вентилируемом месте",
        ["P210"] = "Беречь от тепла, горячих поверхностей, искр, открытого огня; не курить",
        ["P233"] = "Держать ёмкость плотно закрытой",
        ["P240"] = "Заземлить и электрически соединить ёмкость и приёмное устройство",
        ["P241"] = "Использовать взрывобезопасное электрическое и вентиляционное оборудование",
        ["P242"] = "Использовать инструмент, не дающий искр",
        ["P243"] = "Принять меры против накопления статического электричества",
        ["P260"] = "Не вдыхать пыль, дым, газ, туман, пары, аэрозоли",
        ["P261"] = "Избегать вдыхания пыли, дыма, газа, тумана, паров, аэрозолей",
        ["P264"] = "После работы тщательно вымыть руки",
        ["P270"] = "Не есть, не пить и не курить во время работы",
        ["P271"] = "Использовать только на открытом воздухе или в хорошо вентилируемом помещении",
        ["P272"] = "Загрязнённую рабочую одежду не выносить за пределы рабочего места",
        ["P273"] = "Не допускать попадания в окружающую среду",
        ["P280"] = "Использовать защитные перчатки, защитную одежду, средства защиты глаз и лица",
        ["P301+P310"] = "ПРИ ПРОГЛАТЫВАНИИ: немедленно обратиться в токсикологический центр или к врачу",
        ["P301+P330+P331"] = "ПРИ ПРОГЛАТЫВАНИИ: прополоскать рот; НЕ вызывать рвоту",
        ["P302+P352"] = "ПРИ ПОПАДАНИИ НА КОЖУ: промыть большим количеством воды с мылом",
        ["P303+P361+P353"] = "ПРИ ПОПАДАНИИ НА КОЖУ (или волосы): немедленно снять загрязнённую одежду; промыть кожу водой",
        ["P304+P340"] = "ПРИ ВДЫХАНИИ: вынести пострадавшего на свежий воздух и обеспечить покой в удобном для дыхания положении",
        ["P305+P351+P338"] = "ПРИ ПОПАДАНИИ В ГЛАЗА: осторожно промыть глаза водой в течение нескольких минут; снять контактные линзы, если они есть",
        ["P308+P313"] = "ПРИ воздействии или подозрении на него: обратиться за медицинской помощью",
        ["P310"] = "Немедленно обратиться в токсикологический центр или к врачу",
        ["P312"] = "При плохом самочувствии обратиться в токсикологический центр или к врачу",
        ["P321"] = "Специфическое лечение (см. указания на этикетке)",
        ["P330"] = "Прополоскать рот",
        ["P333+P313"] = "При раздражении кожи или появлении сыпи: обратиться за медицинской помощью",
        ["P337+P313"] = "Если раздражение глаз не проходит: обратиться за медицинской помощью",
        ["P362+P364"] = "Снять загрязнённую одежду и выстирать её перед повторным использованием",
        ["P363"] = "Загрязнённую одежду выстирать перед повторным использованием",
        ["P370+P378"] = "В случае пожара: использовать подходящие средства пожаротушения",
        ["P391"] = "Собрать пролитый продукт",
        ["P403+P233"] = "Хранить в хорошо вентилируемом месте; держать ёмкость плотно закрытой",
        ["P403+P235"] = "Хранить в хорошо вентилируемом месте; хранить в прохладном месте",
        ["P405"] = "Хранить под замком",
        ["P501"] = "Утилизировать содержимое и упаковку в соответствии с требованиями законодательства"
    };

    private static readonly Dictionary<Pictogram, string[]> PrecautionsByPictogram = new()
    {
        [Pictogram.Ghs02Flame] = new[] { "P210", "P233", "P240", "P241", "P242", "P243", "P280", "P303+P361+P353", "P370+P378", "P403+P235", "P501" },
        [Pictogram.Ghs03Oxidising] = new[] { "P210", "P220", "P280", "P370+P378", "P501" },
        [Pictogram.Ghs04GasCylinder] = new[] { "P410+P403" },
        [Pictogram.Ghs05Corrosion] = new[] { "P260", "P264", "P280", "P301+P330+P331", "P303+P361+P353", "P304+P340", "P305+P351+P338", "P310", "P321", "P363", "P405", "P501" },
        [Pictogram.Ghs06Skull] = new[] { "P260", "P264", "P270", "P271", "P280", "P301+P310", "P304+P340", "P321", "P330", "P403+P233", "P405", "P501" },
        [Pictogram.Ghs07Exclamation] = new[] { "P261", "P264", "P270", "P271", "P272", "P280", "P302+P352", "P305+P351+P338", "P333+P313", "P337+P313", "P362+P364", "P501" },
        [Pictogram.Ghs08HealthHazard] = new[] { "P201", "P202", "P260", "P264", "P270", "P280", "P308+P313", "P405", "P501" },
        [Pictogram.Ghs09Environment] = new[] { "P273", "P391", "P501" }
    };

    /// <summary>Есть ли запись для класса и категории</summary>
    /// <param name="category">Класс и категория опасности</param>
    public static bool Contains(HazardCategory category) => Entries.ContainsKey(category);

    /// <summary>Все известные справочнику классификации</summary>
    public static IReadOnlyCollection<HazardCategory> Known => Entries.Keys;

    /// <summary>
    /// Разбирает запись классификации в нотации CLP: "Skin Corr. 1B", "Acute Tox. 4 (oral)",
    /// "STOT SE 3", "Aquatic Chronic 2"
    /// </summary>
    /// <param name="text">Текстовая запись</param>
    /// <param name="category">Разобранная классификация</param>
    public static bool TryParse(string text, out HazardCategory category)
    {
        category = default;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string normalized = text.Trim().Replace('_', ' ');
        string route = null;

        int bracket = normalized.IndexOf('(');

        if (bracket >= 0)
        {
            route = normalized[bracket..].Trim('(', ')', ' ').ToLowerInvariant();
            normalized = normalized[..bracket].Trim();
        }

        int split = normalized.LastIndexOf(' ');

        if (split <= 0)
            return false;

        string className = normalized[..split].Trim().TrimEnd('.');
        string categoryName = normalized[(split + 1)..].Trim().ToUpperInvariant();

        HazardClass? parsed = ParseClass(className, route);

        if (parsed == null)
            return false;

        category = new HazardCategory(parsed.Value, categoryName);

        return Entries.ContainsKey(category);
    }

    private static HazardClass? ParseClass(string name, string route)
    {
        string key = name.Replace(".", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();

        return key switch
        {
            "flamliq" or "flammableliquid" => HazardClass.FlammableLiquid,
            "flamsol" or "flammablesolid" => HazardClass.FlammableSolid,
            "oxliq" => HazardClass.OxidisingLiquid,
            "oxsol" => HazardClass.OxidisingSolid,
            "pressgas" => HazardClass.GasUnderPressure,
            "metcorr" => HazardClass.CorrosiveToMetals,
            "acutetox" => route switch
            {
                "dermal" or "кожа" or "накожно" => HazardClass.AcuteToxicityDermal,
                "inhalation" or "ингаляционно" => HazardClass.AcuteToxicityInhalation,
                _ => HazardClass.AcuteToxicityOral
            },
            "skincorr" => HazardClass.SkinCorrosion,
            "skinirrit" => HazardClass.SkinIrritation,
            "eyedam" => HazardClass.EyeDamage,
            "eyeirrit" => HazardClass.EyeIrritation,
            "respsens" => HazardClass.RespiratorySensitisation,
            "skinsens" => HazardClass.SkinSensitisation,
            "muta" => HazardClass.Mutagenicity,
            "carc" => HazardClass.Carcinogenicity,
            "repr" => HazardClass.ReproductiveToxicity,
            "stotse" => HazardClass.StotSingle,
            "stotre" => HazardClass.StotRepeated,
            "asptox" => HazardClass.AspirationHazard,
            "aquaticacute" => HazardClass.AquaticAcute,
            "aquaticchronic" => HazardClass.AquaticChronic,
            _ => null
        };
    }

    /// <summary>Запись справочника для класса и категории</summary>
    /// <param name="category">Класс и категория опасности</param>
    public static HazardEntry Entry(HazardCategory category)
        => Entries.TryGetValue(category, out var entry)
            ? entry
            : throw new ArgumentException($"Классификация '{category}' отсутствует в справочнике", nameof(category));

    /// <summary>Текст H-фразы по коду</summary>
    /// <param name="code">Код фразы</param>
    public static string HazardText(string code)
        => HazardTexts.TryGetValue(code, out string text) ? text : code;

    /// <summary>Текст P-фразы по коду</summary>
    /// <param name="code">Код фразы</param>
    public static string PrecautionaryText(string code)
        => PrecautionaryTexts.TryGetValue(code, out string text) ? text : code;

    /// <summary>Рекомендуемые меры предосторожности для набора пиктограмм</summary>
    /// <param name="pictograms">Пиктограммы</param>
    public static IReadOnlyList<string> Precautions(IEnumerable<Pictogram> pictograms)
    {
        var codes = new List<string> { "P101", "P102", "P103" };

        foreach (Pictogram pictogram in pictograms.Distinct())
        {
            if (PrecautionsByPictogram.TryGetValue(pictogram, out string[] recommended))
                codes.AddRange(recommended.Where(PrecautionaryTexts.ContainsKey));
        }

        return codes.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Обозначение пиктограммы</summary>
    /// <param name="pictogram">Пиктограмма</param>
    public static string Code(Pictogram pictogram) => pictogram switch
    {
        Pictogram.Ghs01Explosive => "GHS01",
        Pictogram.Ghs02Flame => "GHS02",
        Pictogram.Ghs03Oxidising => "GHS03",
        Pictogram.Ghs04GasCylinder => "GHS04",
        Pictogram.Ghs05Corrosion => "GHS05",
        Pictogram.Ghs06Skull => "GHS06",
        Pictogram.Ghs07Exclamation => "GHS07",
        Pictogram.Ghs08HealthHazard => "GHS08",
        Pictogram.Ghs09Environment => "GHS09",
        _ => string.Empty
    };

    /// <summary>Название пиктограммы</summary>
    /// <param name="pictogram">Пиктограмма</param>
    public static string Title(Pictogram pictogram) => pictogram switch
    {
        Pictogram.Ghs01Explosive => "взрывающаяся бомба",
        Pictogram.Ghs02Flame => "пламя",
        Pictogram.Ghs03Oxidising => "пламя над окружностью",
        Pictogram.Ghs04GasCylinder => "газовый баллон",
        Pictogram.Ghs05Corrosion => "коррозия",
        Pictogram.Ghs06Skull => "череп и скрещённые кости",
        Pictogram.Ghs07Exclamation => "восклицательный знак",
        Pictogram.Ghs08HealthHazard => "опасность для здоровья",
        Pictogram.Ghs09Environment => "окружающая среда",
        _ => string.Empty
    };

    /// <summary>Сигнальное слово текстом</summary>
    /// <param name="word">Сигнальное слово</param>
    public static string Text(SignalWord word) => word switch
    {
        SignalWord.Danger => "Опасно",
        SignalWord.Warning => "Осторожно",
        _ => "не требуется"
    };

    /// <summary>Сокращённое обозначение класса в нотации CLP</summary>
    /// <param name="hazardClass">Класс опасности</param>
    public static string ShortName(HazardClass hazardClass) => hazardClass switch
    {
        HazardClass.FlammableLiquid => "Flam. Liq.",
        HazardClass.FlammableSolid => "Flam. Sol.",
        HazardClass.OxidisingLiquid => "Ox. Liq.",
        HazardClass.OxidisingSolid => "Ox. Sol.",
        HazardClass.GasUnderPressure => "Press. Gas",
        HazardClass.CorrosiveToMetals => "Met. Corr.",
        HazardClass.AcuteToxicityOral => "Acute Tox. (орально)",
        HazardClass.AcuteToxicityDermal => "Acute Tox. (кожа)",
        HazardClass.AcuteToxicityInhalation => "Acute Tox. (ингаляционно)",
        HazardClass.SkinCorrosion => "Skin Corr.",
        HazardClass.SkinIrritation => "Skin Irrit.",
        HazardClass.EyeDamage => "Eye Dam.",
        HazardClass.EyeIrritation => "Eye Irrit.",
        HazardClass.RespiratorySensitisation => "Resp. Sens.",
        HazardClass.SkinSensitisation => "Skin Sens.",
        HazardClass.Mutagenicity => "Muta.",
        HazardClass.Carcinogenicity => "Carc.",
        HazardClass.ReproductiveToxicity => "Repr.",
        HazardClass.StotSingle => "STOT SE",
        HazardClass.StotRepeated => "STOT RE",
        HazardClass.AspirationHazard => "Asp. Tox.",
        HazardClass.AquaticAcute => "Aquatic Acute",
        HazardClass.AquaticChronic => "Aquatic Chronic",
        _ => hazardClass.ToString()
    };
}
