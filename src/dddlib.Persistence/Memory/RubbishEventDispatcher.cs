// <copyright file="RubbishEventDispatcher.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using dddlib.Sdk;

    /*  TODO (Cameron): 
        Unseal and make methods virtual.
        Any exceptions? - possibly of type RuntimeException (consider).
        Consider what to do with multiple base classes with different dispatchers.
        Add ability to configure method name.
        Duplicate for application against momento.
        Change to operate on any type, not just AggregateRoot.  */

    /// <summary>
    /// Represents the rubbish event dispatcher.
    /// </summary>
    public sealed class RubbishEventDispatcher : IEventDispatcher
    {
        private static readonly string ApplyMethodName = GetApplyMethodName();

        private readonly Dictionary<Type, List<Action<object, object>>> handlers;

        /// <summary>
        /// Initializes a new instance of the <see cref="RubbishEventDispatcher"/> class.
        /// </summary>
        /// <param name="aggregateType">Type of the aggregate.</param>
        public RubbishEventDispatcher(Type aggregateType)
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
                .Traverse(type => type.BaseType == typeof(object) ? null : new[] { type.BaseType })
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                .Where(method => method.Name.Equals(ApplyMethodName, StringComparison.OrdinalIgnoreCase))
                .Where(method => method.GetParameters().Count() == 1)
                .Where(method => method.DeclaringType != typeof(object))
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
            return "Handle";
        }
    }
}
