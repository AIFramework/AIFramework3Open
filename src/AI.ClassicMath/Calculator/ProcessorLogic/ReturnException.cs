using System;

namespace AI.ClassicMath.Calculator.ProcessorLogic;

/// <summary>
/// Исключение для выхода из функции скрипта (return) вместе с её результатом.
/// </summary>
internal class ReturnException : Exception
{
    public object Value { get; }

    public ReturnException(object value) : base("Return statement executed") => Value = value;
}
