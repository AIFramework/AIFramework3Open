using AI.Insights;

namespace AI.Solvers.Chem.Qsar;

/// <summary>Разбор качества QSAR-модели.</summary>
public sealed partial class QsarModel : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        QsarQuality q = Quality;
        double overfit = q.R2 - q.Q2;
        int descriptors = DescriptorNames.Count;
        bool predictive = q.Q2 >= 0.5;
        bool overfitted = overfit > 0.3;
        string property = string.IsNullOrEmpty(Property) ? "свойство" : Property;

        return new InterpretationBuilder($"QSAR-модель: {property}")
            .Summary($"Модель на {descriptors} дескрипторах описывает обучающую выборку с R² = "
                + $"{Fmt.Num(q.R2, 3)} и предсказывает при перекрёстной проверке с Q² = {Fmt.Num(q.Q2, 3)}. "
                + $"Ошибка на обучении RMSE = {Fmt.Num(q.Rmse, 4)}, при проверке — {Fmt.Num(q.RmseCv, 4)}. "
                + (predictive
                    ? "По принятому в QSAR порогу Q² > 0.5 модель обладает предсказательной силой."
                    : "По принятому в QSAR порогу Q² > 0.5 предсказательная сила не подтверждена."))
            .Metric("R²", Fmt.Num(q.R2, 4), null, "доля объяснённой дисперсии на обучающей выборке",
                q.R2 >= 0.8 ? MetricQuality.Good : q.R2 >= 0.6 ? MetricQuality.Neutral : MetricQuality.Warning)
            .Metric("Q²", Fmt.Num(q.Q2, 4), null,
                "то же при перекрёстной проверке — единственная из двух величин, говорящая о предсказании",
                q.Q2 >= 0.6 ? MetricQuality.Good : predictive ? MetricQuality.Neutral : MetricQuality.Critical)
            .Metric("R² − Q²", Fmt.Num(overfit, 4), null,
                "разрыв между подгонкой и предсказанием; выше 0.3 — признак переобучения",
                overfitted ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("RMSE", Fmt.Num(q.Rmse, 5), null, "среднеквадратичная ошибка на обучении")
            .Metric("RMSE_cv", Fmt.Num(q.RmseCv, 5), null, "то же при перекрёстной проверке")
            .Metric("MAE", Fmt.Num(q.Mae, 5), null, "средняя абсолютная ошибка: устойчивее к выбросам")
            .Metric("Дескрипторов", descriptors, null, "число признаков в модели", MetricQuality.Unknown, 0)
            .Metric("Порог рычага", Fmt.Num(LeverageThreshold, 4), null,
                "структуры с бо́льшим рычагом лежат вне области применимости")
            .FindingIf(overfitted,
                $"Разрыв R² − Q² = {Fmt.Num(overfit, 2)} указывает на переобучение: модель описывает "
                + "обучающую выборку заметно лучше, чем предсказывает новые структуры.")
            .FindingIf(!predictive,
                $"Q² = {Fmt.Num(q.Q2, 3)} ниже 0.5. Высокий R² при этом ничего не спасает: подогнать "
                + "выборку набором дескрипторов можно почти всегда, предсказывать — нет.")
            .FindingIf(predictive && !overfitted,
                "Показатели согласованы: подгонка и перекрёстная проверка дают близкое качество, "
                + "и модель пригодна для скрининга структур внутри области применимости.")
            .FindingIf(q.RmseCv > 1.5 * q.Rmse && q.Rmse > 0,
                "Ошибка при проверке заметно выше ошибки на обучении — тот же вывод, что и по разрыву "
                + "R² и Q², но выраженный в единицах свойства.")
            .Warning("Область применимости ограничена рычагом: предсказание для структуры с рычагом выше "
                + $"{Fmt.Num(LeverageThreshold, 3)} — экстраполяция, и заявленная точность на неё не распространяется.")
            .Warning("Дескрипторы описывают структуру, а не механизм. Корреляция со свойством не означает "
                + "причинной связи и может исчезнуть на ином химическом классе.")
            .Warning("Перекрёстная проверка выполнена на той же выборке, на которой отбирались дескрипторы: "
                + "если отбор шёл по всей выборке, Q² оптимистичен.")
            .Recommendation("Проверять принадлежность каждой новой структуры области применимости "
                + "перед тем, как пользоваться предсказанием.")
            .RecommendationIf(overfitted || !predictive,
                "Сократить число дескрипторов либо расширить выборку: на короткой выборке лишние признаки "
                + "поднимают R², не улучшая Q².")
            .Recommendation("Подтвердить модель на внешнем тестовом наборе, не участвовавшем ни в обучении, "
                + "ни в отборе признаков.")
            .Build();
    }
}
