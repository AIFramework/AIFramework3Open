using AI.Script.Binding;
using AI.Script.Hosting;

namespace AI.Script.Office;

/// <summary>
/// Подключение книг Excel и документов к хосту.
/// </summary>
/// <remarks>
/// Отдельным вызовом, как графики и зрение: хост, которому документы не нужны, не платит за
/// три чужих пакета ни размером поставки, ни временем запуска. Подключили — <c>io.load</c> сам
/// начинает открывать xlsx, docx и pdf: читателей объявляют сами функции.
/// </remarks>
public static class OfficeLibrary
{
    /// <summary>Модуль книг Excel.</summary>
    public static IScriptModule Xls { get; } = ScriptModule.FromType(typeof(XlsModule));

    /// <summary>Модуль документов.</summary>
    public static IScriptModule Doc { get; } = ScriptModule.FromType(typeof(DocModule));

    /// <summary>Регистрирует книги и документы в хосте.</summary>
    public static ScriptHost UseOffice(this ScriptHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.Use(Xls).Use(Doc);
    }
}
