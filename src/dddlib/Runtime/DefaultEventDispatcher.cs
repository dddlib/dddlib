// <copyright file="DefaultEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;

    /// <summary>
    /// Represents the default event dispatcher.
    /// </summary>
    //// TODO (Cameron): Make public and allow to be configured?
    public sealed class DefaultEventDispatcher : IEventDispatcher
    {
        private static readonly string ApplyMethodName = GetApplyMethodName();

        private readonly Dictionary<Type, List<Action<AggregateRoot, object>>> handlers;

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
        /// Dispatches the specified event against the specified aggregate.
        /// </summary>
        /// <param name="aggregate">The aggregate.</param>
        /// <param name="event">The event.</param>
        public void Dispatch(AggregateRoot aggregate, object @event)
        {
            var handlerList = default(List<Action<AggregateRoot, object>>);
            if (this.handlers.TryGetValue(@event.GetType(), out handlerList))
            {
                foreach (var handler in handlerList)
                {
                    handler.Invoke(aggregate, @event);
                }
            }
        }

        private static Dictionary<Type, List<Action<AggregateRoot, object>>> GetHandlers(Type aggregateRootType)
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

            var handlers = new Dictionary<Type, List<Action<AggregateRoot, object>>>();

            foreach (var handlerMethod in handlerMethods.Except(invalidHandlerMethodTypes))
            {
                var handler = CreateHandlerDelegate(aggregateRootType, handlerMethod.Info);
                var handlerList = default(List<Action<AggregateRoot, object>>);
                if (!handlers.TryGetValue(handlerMethod.ParameterType, out handlerList))
                {
                    handlerList = new List<Action<AggregateRoot, object>>();
                    handlers.Add(handlerMethod.ParameterType, handlerList);
                }

                handlerList.Add(handler);
            }

            return handlers;
        }

        private static Action<AggregateRoot, object> CreateHandlerDelegate(Type declaringType, MethodInfo methodInfo)
        {
            var dynamicMethod = new DynamicMethod(
                string.Empty,
                typeof(void),
                new[] { typeof(AggregateRoot), typeof(object) },
                declaringType.Module,
                true);

            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);          // load this
            il.Emit(OpCodes.Ldarg_1);          // load event
            il.Emit(OpCodes.Call, methodInfo); // call apply method
            il.Emit(OpCodes.Ret);              // return

            return dynamicMethod.CreateDelegate(typeof(Action<AggregateRoot, object>)) as Action<AggregateRoot, object>;
        }

        private static string GetApplyMethodName()
        {
            Expression<Action<DefaultEventDispatcher>> expression = aggregate => aggregate.Handle(default(object));
            var lambda = (LambdaExpression)expression;
            var methodCall = (MethodCallExpression)lambda.Body;
            return methodCall.Method.Name;
        }

        private void Handle(object @event)
        {
        }
    }
}
