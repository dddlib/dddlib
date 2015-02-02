// <copyright file="DefaultEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;

    /*  TODO (Cameron): 
        Unseal and make methods virtual.
        Any exceptions? - possibly of type RuntimeException (consider).
        Consider what to do with multiple base classes with different dispatchers.
        Add ability to configure method name.
        Duplicate for application against momento.
        Change to operate on any type, not just AggregateRoot.  */

    /// <summary>
    /// Represents the default event dispatcher.
    /// </summary>
    internal sealed class DefaultEventDispatcher : IEventDispatcher
    {
        private static readonly string ApplyMethodName = GetApplyMethodName();

        private readonly Dictionary<Type, List<Action<object, object>>> handlers;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultEventDispatcher"/> class.
        /// </summary>
        /// <param name="aggregateType">Type of the aggregate.</param>
        public DefaultEventDispatcher(Type aggregateType)
        {
            Guard.Against.Null(() => aggregateType);

            this.handlers = GetHandlers(aggregateType);
        }

        /// <summary>
        /// Dispatches the specified event against the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="event">The event.</param>
        public void Dispatch(object target, object @event)
        {
            Guard.Against.Null(() => @event);

            var handlerList = default(List<Action<object, object>>);
            if (this.handlers.TryGetValue(@event.GetType(), out handlerList))
            {
                foreach (var handler in handlerList)
                {
                    handler.Invoke(target, @event);
                }
            }
        }

        private static Dictionary<Type, List<Action<object, object>>> GetHandlers(Type aggregateRootType)
        {
            var handlerMethods = new[] { aggregateRootType }
                .Traverse(type => type.BaseType == typeof(AggregateRoot) ? null : new[] { type.BaseType })
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
                .Where(method => method.Name.Equals(ApplyMethodName, StringComparison.OrdinalIgnoreCase))
                .Where(method => method.GetParameters().Count() == 1)
                .Where(method => method.DeclaringType != typeof(AggregateRoot))
                .Select(methodInfo =>
                    new
                    {
                        Info = methodInfo,
                        ParameterType = methodInfo.GetParameters().First().ParameterType,
                    })
                .ToArray();

            var invalidHandlerMethodTypes = handlerMethods
                .Where(method => !method.ParameterType.IsClass)
                .ToArray();

            var handlers = new Dictionary<Type, List<Action<object, object>>>();

            foreach (var handlerMethod in handlerMethods.Except(invalidHandlerMethodTypes))
            {
                var handler = CreateHandlerDelegate(aggregateRootType, handlerMethod.Info);
                var handlerList = default(List<Action<object, object>>);
                if (!handlers.TryGetValue(handlerMethod.ParameterType, out handlerList))
                {
                    handlerList = new List<Action<object, object>>();
                    handlers.Add(handlerMethod.ParameterType, handlerList);
                }

                handlerList.Add(handler);
            }

            return handlers;
        }

        // LINK (Cameron): http://www.sapiensworks.com/blog/post/2012/04/19/Invoking-A-Private-Method-On-A-Subclass.aspx
        private static Action<object, object> CreateHandlerDelegate(Type declaringType, MethodInfo methodInfo)
        {
            var dynamicMethod = new DynamicMethod(
                string.Empty,
                typeof(void),
                new[] { typeof(object), typeof(object) },
                declaringType.Module,
                true);

            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);          // load this
            il.Emit(OpCodes.Ldarg_1);          // load event
            il.Emit(OpCodes.Call, methodInfo); // call apply method
            il.Emit(OpCodes.Ret);              // return

            return dynamicMethod.CreateDelegate(typeof(Action<object, object>)) as Action<object, object>;
        }

        // LINK (Cameron): http://blog.functionalfun.net/2009/10/getting-methodinfo-of-generic-method.html
        private static string GetApplyMethodName()
        {
            Expression<Action<DefaultEventDispatcher>> expression = aggregate => aggregate.Handle(default(object));
            var lambda = (LambdaExpression)expression;
            var methodCall = (MethodCallExpression)lambda.Body;
            return methodCall.Method.Name;
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "By design.")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "event", Justification = "Also, by design.")]
        private void Handle(object @event)
        {
        }
    }
}
