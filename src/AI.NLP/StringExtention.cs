using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AI.NLP;

/// <summary>
/// Методы расширения для строк
/// </summary>
[Serializable]
public static class StringExtention
{
    /// <summary>
    /// Объединение строк
    /// </summary>
    /// <param name="strings">Массив строк</param>
    /// <param name="sep">Разделитель</param>
    public static string Concatinate(this string[] strings, string sep = "\n")
    {
        if (strings == null || strings.Length == 0)
            return string.Empty;

        return string.Join(sep, strings);
    }


    /// <summary>
    /// Разделение по строке
    /// </summary>
    /// <param name="text">Текст</param>
    /// <param name="strSpliter">Строка-разделитель</param>
    public static string[] Split(this string text, string strSpliter)
    {
        return text.Split(new[] { strSpliter }, StringSplitOptions.None);
    }

    /// <summary>
    /// Удаление подстрок
    /// </summary>
    /// <param name="text">Текст</param>
    /// <param name="delStrs">Подстроки, которые будут удалены</param>
    public static string Remove(this string text, string[] delStrs)
    {
        if (text == null || delStrs == null || delStrs.Length == 0)
            return text;

        string ret = text;
        for (int i = 0; i < delStrs.Length; i++)
        {
            string s = delStrs[i];
            if (!string.IsNullOrEmpty(s))
                ret = ret.Replace(s, string.Empty);
        }

        return ret;
    }

    /// <summary>
    /// Замена с использованием регулярных выражений
    /// </summary>
    /// <param name="text">Текст</param>
    /// <param name="pattern">Патерн для замены</param>
    /// <param name="new_string">На что заменить патерн</param>
    public static string ReReplace(this string text, string pattern, string new_string)
    {
        return Regex.Replace(text, pattern, new_string);
    }

    /// <summary>
    /// Преобразование с использованием регулярных выражений
    /// </summary>
    /// <param name="text">Текст</param>
    /// <param name="pattern">Патерн для преобразования</param>
    /// <param name="transformer">Функция преобразования текста совпадающего с патерном</param>
    public static string ReTransform(this string text, string pattern, Func<string, string> transformer)
    {
        return Regex.Replace(text, pattern, x => transformer(x.Value));
    }


    /// <summary>
    /// Находит разность между строками, например "привет" - "ве" = "прит".
    /// Удаляет первое вхождение <paramref name="text2"/> из <paramref name="text1"/>.
    /// </summary>
    /// <param name="text1">Строка из которой вычитаем</param>
    /// <param name="text2">Строка которую вычитаем</param>
    public static string Diff(this string text1, string text2)
    {
        if (text1 == null) throw new ArgumentNullException(nameof(text1));
        if (text2 == null) throw new ArgumentNullException(nameof(text2));

        int len = text1.Length - text2.Length;
        if (len < 0) throw new Exception("Вычитаемое больше уменьшаемого");

        if (text2.Length == 0)
            return text1;

        int idx = text1.IndexOf(text2, StringComparison.Ordinal);
        if (idx < 0) throw new Exception("Уменьшаемое не содержит вычитаемое");

        // Соединяем префикс и суффикс без копирования через StringBuilder
        var sb = new StringBuilder(len);
        if (idx > 0) _ = sb.Append(text1, 0, idx);
        int tailStart = idx + text2.Length;
        if (tailStart < text1.Length) _ = sb.Append(text1, tailStart, text1.Length - tailStart);
        return sb.ToString();
    }
}
