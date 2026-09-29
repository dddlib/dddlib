using System.Linq.Expressions;
using System.Reflection;
using dddlib.Runtime;

namespace dddlib.Sdk;

/// <summary>
/// Dispatches events to non-public single-parameter methods named <c>Handle</c> (case-insensitive) whose parameter
/// type exactly matches the runtime type of the event. Handlers are discovered by reflection once per type and
/// invoked through compiled delegates.
/// </summary>
public sealed class DefaultEventDispatcher : IEventDispatcher
{
    private const string DefaultMethodName = "Handle";

    private readonly Dictionary<Type, Action<object, object>[]> handlers;

    public DefaultEventDispatcher(Type type)
        : this(type, DefaultMethodName, BindingFlags.Instance | BindingFlags.NonPublic)
    {
    }

    public DefaultEventDispatcher(Type type, string methodName, BindingFlags bindingFlags)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrEmpty(methodName);

        if (!IsValidIdentifier(methodName))
        {
            throw new ArgumentException("The specified target method name is not a valid identifier.", nameof(methodName));
        }

        this.handlers = GetHandlers(type, methodName, bindingFlags);
    }

    public void Dispatch(object target, object @event)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(@event);

        if (!this.handlers.TryGetValue(@event.GetType(), out var handlerList))
        {
            return;
        }

        foreach (var handler in handlerList)
        {
            handler(target, @event);
        }
    }

    private static bool IsValidIdentifier(string name) =>
        (char.IsLetter(name[0]) || name[0] == '_') && name.All(static c => char.IsLetterOrDigit(c) || c == '_');

    private static Dictionary<Type, Action<object, object>[]> GetHandlers(Type type, string methodName, BindingFlags bindingFlags)
    {
        var handlerMethods = type.GetTypeHierarchyUntil(typeof(object))
            .SelectMany(t => t.GetMethods(bindingFlags | BindingFlags.DeclaredOnly))
            .Where(method => method.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .Where(method => !method.IsGenericMethodDefinition)
            .Select(method => (Method: method, Parameters: method.GetParameters()))
            .Where(handler => handler.Parameters.Length == 1)
            .Select(handler => (handler.Method, ParameterType: handler.Parameters[0].ParameterType))
            .Where(handler => handler.ParameterType.IsClass)
            .ToArray();

        return handlerMethods
            .GroupBy(handler => handler.ParameterType)
            .ToDictionary(
                group => group.Key,
                group => group.Select(handler => CreateHandlerDelegate(handler.Method, handler.ParameterType)).ToArray());
    }

    private static Action<object, object> CreateHandlerDelegate(MethodInfo method, Type parameterType)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var @event = Expression.Parameter(typeof(object), "event");
        var call = Expression.Call(
            Expression.Convert(target, method.DeclaringType!),
            method,
            Expression.Convert(@event, parameterType));

        return Expression.Lambda<Action<object, object>>(call, target, @event).Compile();
    }
}
