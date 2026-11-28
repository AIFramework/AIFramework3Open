namespace AI.DataPrepaire.NLPUtils.RegexpNLP.SimpleNER.SpecialNers;

/// <summary>
/// Процессор обрабатывающий порядковые номера в т.ч. и буквенные
/// </summary>
/// <remarks>
/// Точка в конце номера («НК-40.», «7.4.») входит в сущность, но конец предложения после неё
/// решает общее правило разбивки, а не этот шаблон.
/// </remarks>
public class OrderNumberNerProcessor : RegexNer
{
    const string orderNumberPatten =
        @"\b(\d+\.)+\d*";

    /// <summary>
    /// Процессор обрабатывающий порядковые номера в т.ч. и буквенные
    /// </summary>
    public OrderNumberNerProcessor() 
        : base(orderNumberPatten, "orderNumber") { }
}
