using AI.DataStructs.Algebraic;
using AI.ML.Regression;
using System;

namespace AI.ControlSystems.ComplexObjectControl.Base;

/// <summary>
/// Модель для прогнозирование реакции объекта на управляющее воздействие
/// </summary>
[Serializable]
public class ObjectModel
{
    /// <summary>
    /// Модель
    /// </summary>
    public IMultyRegression<Vector> MultyRegression;

    /// <summary>
    /// Модель для прогнозирование реакции объекта на управляющее воздействие
    /// </summary>
    public ObjectModel() { }

    /// <summary>
    /// Прогнозирование реакции объекта на управляющее воздействие
    /// </summary>
    /// <param name="action">Управляющее воздействие</param>
    /// <returns></returns>
    public virtual Vector GetReaction(Vector action)
    {
        return MultyRegression.Predict(action);
    }

    /// <summary>
    /// Обучение модели
    /// </summary>
    /// <param name="dataset"></param>
    public virtual void Train(ObjModelDataset dataset)
    {
        MultyRegression.Train(dataset.ControlActions.ToArray(), dataset.States.ToArray());
    }


}
