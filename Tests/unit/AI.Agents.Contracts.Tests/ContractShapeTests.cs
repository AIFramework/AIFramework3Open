using System.Reflection;
using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Форма контрактов: члены required init вместо позиционных записей, необязательное — только null с
/// документированным смыслом, одно понятие — одно место.
/// </summary>
public sealed class ContractShapeTests
{
    private static IEnumerable<Type> DataTypes() =>
        Repository.Contracts.GetExportedTypes().Where(type => !type.IsInterface && !type.IsEnum && !(type.IsAbstract && type.IsSealed));

    /// <summary>
    /// Нет позиционных записей и длинных конструкторов: ни одного открытого конструктора с двумя и более
    /// параметрами и ни одного Deconstruct. Расширение записи полем не ломает двоичную совместимость.
    /// </summary>
    [Fact]
    public void Types_HaveNoPositionalShape()
    {
        var problems = new List<string>();

        foreach (Type type in DataTypes())
        {
            foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                if (constructor.GetParameters().Length > 1)
                    problems.Add($"{type.Name}: открытый конструктор с {constructor.GetParameters().Length} параметрами");
            }

            if (type.GetMethod("Deconstruct", BindingFlags.Public | BindingFlags.Instance) is not null)
                problems.Add($"{type.Name}: есть Deconstruct — позиционная запись");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>
    /// Нет значений по умолчанию, которые молча подменяют выбор: член с init либо required, либо допускает
    /// null (смысл null описан в документации), либо признак bool, где false значит «нет».
    /// </summary>
    [Fact]
    public void OptionalMembers_AreNullableOrFlags()
    {
        var nullability = new NullabilityInfoContext();
        var problems = new List<string>();

        foreach (Type type in DataTypes())
        {
            foreach (PropertyInfo property in ValueComparison.WritableProperties(type))
            {
                // required: без значения запись не создается и не читается из JSON
                if (property.GetCustomAttribute<RequiredMemberAttribute>() is not null)
                    continue;

                Type propertyType = property.PropertyType;
                bool allowed = propertyType == typeof(bool)
                               || Nullable.GetUnderlyingType(propertyType) is not null
                               || !propertyType.IsValueType && nullability.Create(property).WriteState == NullabilityState.Nullable;

                if (!allowed)
                    problems.Add($"{type.Name}.{property.Name}: необязательный член не допускает null");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Срок живет только в бюджете: у входа движка нет поля срока.</summary>
    [Fact]
    public void Deadline_LivesOnlyInBudget()
    {
        Type[] timeTypes = [typeof(DateTimeOffset), typeof(DateTime), typeof(TimeSpan)];

        foreach (PropertyInfo property in typeof(EngineInput).GetProperties())
        {
            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            Assert.DoesNotContain(type, timeTypes);
        }

        Assert.Equal(typeof(DateTimeOffset?), typeof(IRunBudget).GetProperty(nameof(IRunBudget.Deadline))!.PropertyType);
    }

    /// <summary>Код причины остановки один: бюджет и итог движка говорят одним типом.</summary>
    [Fact]
    public void StopCode_IsSharedByBudgetAndResult()
    {
        ParameterInfo stop = typeof(IRunBudget).GetMethod(nameof(IRunBudget.TryStep))!.GetParameters().Single();

        Assert.True(stop.IsOut);
        Assert.Equal(typeof(StopCode), stop.ParameterType.GetElementType());
        Assert.Equal(typeof(StopCode), typeof(EngineResult).GetProperty(nameof(EngineResult.StopCode))!.PropertyType);
    }

    /// <summary>Движок отдает только поток событий: второго канала возврата нет.</summary>
    [Fact]
    public void Engine_ReturnsOnlyEventStream()
    {
        MethodInfo run = typeof(IEngine).GetMethod(nameof(IEngine.RunAsync))!;

        Assert.Equal(typeof(IAsyncEnumerable<EngineEvent>), run.ReturnType);
        Assert.DoesNotContain(run.GetParameters(), parameter => parameter.IsOut || parameter.ParameterType.IsByRef);
        Assert.Equal(typeof(EngineResult), typeof(EngineEvent.Finished).GetProperty(nameof(EngineEvent.Finished.Result))!.PropertyType);
    }

    /// <summary>Порт модели принимает роль, а не имя модели; известные роли на месте.</summary>
    [Fact]
    public void ModelAsk_CarriesRoleNotModelName()
    {
        Assert.Equal(typeof(ModelRole), typeof(ModelAsk).GetProperty(nameof(ModelAsk.Role))!.PropertyType);
        Assert.DoesNotContain(typeof(ModelAsk).GetProperties(), property => property.Name.Contains("Model", StringComparison.Ordinal));

        Assert.Equal(["decide", "write", "judge", "see"],
            new[] { ModelRole.Decide, ModelRole.Write, ModelRole.Judge, ModelRole.See }.Select(role => role.Name));
        Assert.Equal(new ModelRole("decide"), ModelRole.Decide);
        Assert.Throws<ArgumentException>(() => new ModelRole(" "));
    }
}
