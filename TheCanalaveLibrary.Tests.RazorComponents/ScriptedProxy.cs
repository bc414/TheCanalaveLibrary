using System.Reflection;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// A service stand-in for a page test that needs only a few members of a wide interface: each scripted
/// member answers by name, and any other call throws, so a page reaching for something the test did not
/// expect fails loudly. Same idea as <see cref="ChapterEditorPageTakedownTests.UnexpectedCall"/>, with
/// answers. Create one with <see cref="Create{T}"/>.
/// </summary>
public class ScriptedProxy : DispatchProxy
{
    private readonly Dictionary<string, Func<object?[], object?>> _handlers = [];

    public static T Create<T>(out ScriptedProxy script) where T : class
    {
        T service = Create<T, ScriptedProxy>();
        script = (ScriptedProxy)(object)service;
        return service;
    }

    /// <summary>Scripts <paramref name="method"/> (every overload of that name).</summary>
    public ScriptedProxy On(string method, Func<object?[], object?> handler)
    {
        _handlers[method] = handler;
        return this;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        _handlers.TryGetValue(targetMethod!.Name, out Func<object?[], object?>? handler)
            ? handler(args ?? [])
            : throw new NotSupportedException($"{targetMethod.Name} is not scripted in this test.");
}
