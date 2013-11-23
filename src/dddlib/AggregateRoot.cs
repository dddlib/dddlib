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
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Represents an aggregate root.
    /// </summary>
    public abstract class AggregateRoot : Entity
    {
        private Dictionary<Type, Action<object>> handlers = new Dictionary<Type, Action<object>>();
        private List<object> changes = new List<object>();

        private bool exists;

        internal string State { get; private set; }

        internal void Initialize(object memento, IEnumerable<object> events, string state)
        {
            Guard.Against.Null(() => events);

            // NOTE (Cameron): We have to initialize everything here because this gets called following FormatterServices.GetUninitializedObject().
            this.handlers = new Dictionary<Type, Action<object>>();
            this.changes = new List<object>();
            this.exists = true;

            this.DynamicWireUpHandlers();

            if (memento != null)
            {
                this.LoadStateFromMemento(memento);
            }

            foreach (var @event in events)
            {
                this.ApplyChange(@event, false);
            }

            this.State = state;
        }

        ////internal IEnumerable<object> GetUncommittedChanges()
        ////{
        ////    return this.changes;
        ////}

        ////internal void MarkChangesAsCommitted(string state)
        ////{
        ////    this.state = state;
        ////    this.changes.Clear();
        ////}

        /// <summary>
        /// Creates a memento representing the state of the aggregate root.
        /// </summary>
        /// <returns>The memento representing the state of the aggregate root.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Inappropriate.")]
        protected internal virtual object CreateMemento()
        {
            return null;
        }

        /// <summary>
        /// Loads the state from a memento of the aggregate root.
        /// </summary>
        /// <param name="memento">The memento of the aggregate root.</param>
        protected internal virtual void LoadStateFromMemento(object memento)
        {
        }

        /// <summary>
        /// Applies the specified change to the aggregate root.
        /// </summary>
        /// <param name="event">The change as an event.</param>
        /// <exception cref="dddlib.BusinessException">Thrown if the aggregate root no longer exists.</exception>
        protected internal void ApplyChange(object @event)
        {
            if (!this.exists)
            {
                throw new BusinessException("Unable to apply the specified change as the aggregate root no longer exists.");
            }

            this.ApplyChange(@event, true);
        }

        private void ApplyChange(object @event, bool isNew)
        {
            // TODO (Cameron): Fix.
            if (@event.GetType().Name.Contains("DeleteEvent"))
            {
                this.exists = false;
            }

            if (this.handlers.ContainsKey(@event.GetType()))
            {
                this.handlers[@event.GetType()].Invoke(@event);
            }

            if (isNew)
            {
                this.changes.Add(@event);
            }
        }

        private void DynamicWireUpHandlers()
        {
            // NOTE (Cameron): We need to traverse the type hierarchy because the handler methods have private scope.
            var typeHierarchy = new[] { this.GetType() }
                .Traverse(type => type.BaseType == typeof(AggregateRoot) ? null : new[] { type.BaseType });

            // TODO (Cameron): Cache per type.
            foreach (var type in typeHierarchy)
            {
                var handlerMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(method => method.Name == "Apply");

                foreach (var method in handlerMethods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Count() == 1)
                    {
                        var delegateType = typeof(Action<>).MakeGenericType(parameters.First().ParameterType);
                        var methodDelegate = Delegate.CreateDelegate(delegateType, this, method);
                        var registerMethod = typeof(AggregateRoot)
                            .GetMethod("RegisterLocalHandler", BindingFlags.Instance | BindingFlags.NonPublic)
                            .MakeGenericMethod(parameters.First().ParameterType);

                        registerMethod.Invoke(this, new[] { methodDelegate });
                    }
                }
            }
        }

        ////private void RegisterLocalHandler<T>(Action<T> handler) where T : class
        ////{
        ////    this.handlers.Add(typeof(T), DelegateAdjuster.CastArgument<Event, T>(message => handler(message)));
        ////}

        ////public static class DelegateAdjuster
        ////{
        ////    public static Action<TBase> CastArgument<TBase, TDerived>(Expression<Action<TDerived>> source)
        ////        where TDerived : TBase
        ////    {
        ////        if (typeof(TDerived) == typeof(TBase))
        ////        {
        ////            return (Action<TBase>)((Delegate)source.Compile());
        ////        }

        ////        var sourceParameter = Expression.Parameter(typeof(TBase), "source");
        ////        var result = Expression.Lambda<Action<TBase>>(
        ////            Expression.Invoke(
        ////                source,
        ////                Expression.Convert(sourceParameter, typeof(TDerived))),
        ////            sourceParameter);

        ////        return result.Compile();
        ////    }
        ////}
    }
}
