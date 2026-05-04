using AI.DataStructs.Algebraic;
using AI.ML.Regression;
using System;

namespace AI.ControlSystems.ComplexObjectControl.Base;

/// <summary>
/// Модель обратного процесса, к процессу управления
/// </summary>
[Serializable]
public class ObjectInversModel
{
    /// <summary>
    /// Модель
    /// </summary>
    public IMultyRegression<Vector> MultyRegression;

    /// <summary>
    /// Модель обратного процесса, к процессу управления
    /// </summary>
    public ObjectInversModel() { }



    /// <summary>
    /// Получение управляющего воздействия, способного вызвать нужную реакцию
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public virtual Vector GetControl(Vector state)
    {
        return MultyRegression.Predict(state);
    }

    /// <summary>
    /// Обучение модели
    /// </summary>
    /// <param name="dataset"></param>
    public virtual void Train(ObjModelDataset dataset)
    {
        MultyRegression.Train(dataset.States.ToArray(), dataset.ControlActions.ToArray());
    }

}
