using System.Reflection;
using System.Text.Json;
using AI.Agents.Contracts.Memory;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Контракт памяти (<see cref="IMemoryPort"/>): один маленький порт, один набор операций записи, исход с
/// причиной у каждой записи, сбой статусом.
/// </summary>
public sealed class MemoryContractTests
{
    private static readonly Type[] MemoryTypes = Repository.Contracts.GetExportedTypes()
        .Where(type => type.Namespace == "AI.Agents.Contracts.Memory")
        .ToArray();

    /// <summary>Порт ровно из двух методов: записать и показать. Сбой приходит статусом в итоге, а не вторым каналом.</summary>
    [Fact]
    public void Port_HasOnlyWriteAndView()
    {
        MethodInfo[] methods = typeof(IMemoryPort).GetMethods();

        Assert.Equal(["ViewAsync", "WriteAsync"], methods.Select(method => method.Name).Order());

        MethodInfo write = typeof(IMemoryPort).GetMethod(nameof(IMemoryPort.WriteAsync))!;
        MethodInfo view = typeof(IMemoryPort).GetMethod(nameof(IMemoryPort.ViewAsync))!;

        Assert.Equal(typeof(Task<MemoryWriteResult>), write.ReturnType);
        Assert.Equal([typeof(MemoryWrite), typeof(CancellationToken)], write.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(typeof(Task<MemoryView>), view.ReturnType);
        Assert.Equal([typeof(MemoryAsk), typeof(CancellationToken)], view.GetParameters().Select(p => p.ParameterType));

        Assert.Equal(typeof(MemoryStatus), typeof(MemoryWriteResult).GetProperty(nameof(MemoryWriteResult.Status))!.PropertyType);
        Assert.Equal(typeof(MemoryStatus), typeof(MemoryView).GetProperty(nameof(MemoryView.Status))!.PropertyType);
    }

    /// <summary>Наборы значений — решения владельца; новое значение требует нового решения.</summary>
    [Fact]
    public void Enums_HaveAgreedValues()
    {
        Assert.Equal(["Unknown", "Add", "Revise", "Confirm", "Retire"], Enum.GetNames<MemoryOp>());
        Assert.Equal(["Unknown", "Said", "Document", "Kind", "Derived"], Enum.GetNames<ValueSource>());
        Assert.Equal(["Unknown", "Written", "Revised", "Confirmed", "Proposed", "Archived", "Rejected"], Enum.GetNames<MemoryVerdict>());
        Assert.Equal(["Unknown", "Ok", "Partial", "Failed"], Enum.GetNames<MemoryStatus>());
    }

    /// <summary>Имена, занятые во фреймворке (OwnershipTests), в контракте памяти не используются.</summary>
    [Fact]
    public void Memory_DoesNotUseTakenNames()
    {
        string[] taken = ["IAgentMemory", "Origin", "MemoryEntry", "IUserMemory"];

        Assert.DoesNotContain(MemoryTypes, type => taken.Contains(type.Name));
    }

    /// <summary>Область ставит хост: она лежит в запросах порта, а не в факте или эпизоде, которые предлагает модель.</summary>
    [Fact]
    public void Scope_LivesOnlyInRequests()
    {
        Type[] holders = MemoryTypes
            .Where(type => type.GetProperties().Any(property => property.PropertyType == typeof(MemoryScope)))
            .OrderBy(type => type.Name)
            .ToArray();

        Assert.Equal([typeof(MemoryAsk), typeof(MemoryWrite)], holders);
    }

    /// <summary>Объект памяти: устойчивый идентификатор, имя и не больше одного вида.</summary>
    [Fact]
    public void Object_HasIdNameAndSingleKind()
    {
        Assert.Equal(typeof(string), typeof(MemoryObject).GetProperty(nameof(MemoryObject.Kind))!.PropertyType);
        Assert.Equal(["Id", "Kind", "Name"], typeof(MemoryObject).GetProperties().Select(property => property.Name).Order());
    }

    /// <summary>Запись в том виде, в каком ее шлет хост, читается без настроек: перечисления именами, даты ISO.</summary>
    [Fact]
    public void Write_ReadsFromHostJson()
    {
        const string json = """
            {
              "Scope": { "Owner": "user-7", "Chat": "chat-42" },
              "Episode": { "Id": "turn-118", "Actor": "User", "At": "2026-11-21T10:30:00+03:00" },
              "Changes": [
                {
                  "Op": "Revise",
                  "Target": "fact-7",
                  "Fact": {
                    "Subject": { "Id": "obj-1", "Name": "Владелец", "Kind": "kind:person" },
                    "Property": "mem:drives",
                    "Value": "Тигуан",
                    "Source": "Said",
                    "ValidFrom": "2026-10-01",
                    "Quote": "теперь езжу на «Тигуане»"
                  }
                },
                { "Op": "Retire", "Target": "fact-5", "Reason": "Устарело" }
              ],
              "Withdraw": [ "turn-90" ]
            }
            """;

        MemoryWrite write = JsonSerializer.Deserialize<MemoryWrite>(json)!;

        Assert.Null(write.Scope.Project);
        Assert.Equal(MemoryActor.User, write.Episode.Actor);
        Assert.Equal(MemoryOp.Revise, write.Changes![0].Op);
        Assert.Equal(new DateOnly(2026, 10, 1), write.Changes[0].Fact!.ValidFrom);
        Assert.Null(write.Changes[0].Fact!.ValidTo);
        Assert.Null(write.Changes[1].Fact);
        Assert.Equal(["turn-90"], write.Withdraw!);
    }

    /// <summary>Без обязательного поля запись не читается: исход без вердикта и срез без статуса невозможны.</summary>
    [Fact]
    public void Read_MissingRequired_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MemoryOutcome>(
            """{"Fact":{"Subject":{"Id":"o","Name":"n"},"Property":"p","Value":"v","Source":"Said"}}"""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MemoryView>(
            """{"Profile":[],"Focus":[],"Others":[],"Trace":[]}"""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MemoryObject>("""{"Name":"Тигуан"}"""));
    }
}
