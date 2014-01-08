// <copyright file="AggregateRoot.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

[module: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "StyleCop.CSharp.NamingRules",
    "SA1300:ElementMustBeginWithUpperCaseLetter",
    Justification = "Noted.")]

namespace dddlib
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Reflection.Emit;

    /// <summary>
    /// Represents an aggregate root.
    /// </summary>
    public abstract class AggregateRoot : Entity, IAggregateRoot
    {
        private static readonly string ApplyMethodName = GetApplyMethodName();

        private Dictionary<Type, List<Action<AggregateRoot, object>>> handlers = new Dictionary<Type, List<Action<AggregateRoot, object>>>();
        private List<object> events = new List<object>();

        private string state;
        private bool isDestroyed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AggregateRoot"/> class.
        /// </summary>
        protected AggregateRoot()
        {
            var handlerMethods = new[] { this.GetType() }
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

            ////var duplicateHandlerMethodTypes = handlerMethods
            ////    .GroupBy(method => method.ParameterType)
            ////    .Where(group => group.Count() > 1)
            ////    .Select(group => group.Key)
            ////    .ToArray();

            ////if (duplicateHandlerMethodTypes.Any())
            ////{
            ////    throw new InvalidOperationException();
            ////}

            // TODO (Cameron): Explore if this can be done external to this class.
            foreach (var handlerMethod in handlerMethods.Except(invalidHandlerMethodTypes))
            {
                var dynamicMethod = new DynamicMethod(
                    string.Empty, 
                    typeof(void), 
                    new[] { typeof(AggregateRoot), typeof(object) }, 
                    this.GetType().Module, 
                    true);

                var il = dynamicMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);                   // load this
                il.Emit(OpCodes.Ldarg_1);                   // load event
                il.Emit(OpCodes.Call, handlerMethod.Info);  // call apply method
                il.Emit(OpCodes.Ret);                       // return

                var handler = dynamicMethod.CreateDelegate(typeof(Action<AggregateRoot, object>)) as Action<AggregateRoot, object>;

                var handlerList = default(List<Action<AggregateRoot, object>>);
                if (!this.handlers.TryGetValue(handlerMethod.ParameterType, out handlerList))
                {
                    handlerList = new List<Action<AggregateRoot, object>>();
                    this.handlers.Add(handlerMethod.ParameterType, handlerList);
                }

                handlerList.Add(handler);
            }
        }

        string IAggregateRoot.State
        {
            get { return this.state; }
        }

        void IAggregateRoot.Initialize(object memento, IEnumerable<object> events, string state)
        {
            Guard.Against.Null(() => events);

            if (memento != null)
            {
                this.SetState(memento);
            }

            foreach (var @event in events)
            {
                this.ApplyChange(@event, false);
            }

            this.state = state;
        }

        object IAggregateRoot.GetMemento()
        {
            return this.GetState();
        }

        IEnumerable<object> IAggregateRoot.GetUncommittedEvents()
        {
            return this.events;
        }

        void IAggregateRoot.CommitEvents(string state)
        {
            this.events.Clear();
            this.state = state;
        }

        /// <summary>
        /// Gets a memento representing the state of the aggregate root.
        /// </summary>
        /// <returns>A memento representing the state of the aggregate root.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Inappropriate.")]
        protected virtual object GetState()
        {
            throw new NotImplementedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The aggregate root of type '{0}' has not been configured to create a memento representing its state.",
                    this.GetType().Name));
        }

        /// <summary>
        /// Sets the state of the aggregate root from the specified memento.
        /// </summary>
        /// <param name="memento">A memento representing the state of the aggregate root.</param>
        protected virtual void SetState(object memento)
        {
            throw new NotImplementedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The aggregate root of type '{0}' has not been configured to apply a memento representing its state.",
                    this.GetType().Name));
        }

        /// <summary>
        /// Ends the lifecycle of this instance of the aggregate root.
        /// </summary>
        protected void EndLifecycle()
        {
            this.isDestroyed = true;
        }

        /// <summary>
        /// Applies the specified change to the aggregate root.
        /// </summary>
        /// <param name="event">The change represented as an event.</param>
        protected void ApplyChange(object @event)
        {
            Guard.Against.Null(() => @event);

            if (this.isDestroyed)
            {
                // TODO (Cameron): Use the natural key and type.
                // maybe: Unable to change Bob because a Person with that name no longer exists.
                // or: cannot change the aggregate of type Person with the identity Bob as this aggregates lifecycle has ended.
                throw new BusinessException("Unable to apply the specified change as this instance of the aggregate root no longer exists.");
            }

            this.ApplyChange(@event, true);
        }

        private static string GetApplyMethodName()
        {
            Expression<Action<AggregateRoot>> expression = aggregate => aggregate.Apply(default(object));
            var lambda = (LambdaExpression)expression;
            var methodCall = (MethodCallExpression)lambda.Body;
            return methodCall.Method.Name;
        }

        // TODO (Cameron): Decide whether to pursue as a protected virtual method.
        [EditorBrowsable(EditorBrowsableState.Never)]
        private void Apply(object @event)
        {
        }

        // LINK (Cameron): http://www.sapiensworks.com/blog/post/2012/04/19/Invoking-A-Private-Method-On-A-Subclass.aspx
        private void ApplyChange(object @event, bool isNew)
        {
            Guard.Against.Null(() => @event);

            if (!@event.GetType().IsClass)
            {
                return;
            }

            var handlerList = default(List<Action<AggregateRoot, object>>); 
            if (this.handlers.TryGetValue(@event.GetType(), out handlerList))
            {
                foreach (var handler in handlerList)
                {
                    handler.Invoke(this, @event);
                }
            }

            if (isNew)
            {
                this.events.Add(@event);
            }
        }
    }
}
