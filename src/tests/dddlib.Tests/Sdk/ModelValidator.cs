// <copyright file="ModelValidator.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Sdk
{
    using System;
    using System.Linq;
    using System.Web.Script.Serialization;

    public static class ModelValidator
    {
        private static readonly AggregateRootFactory Factory = new AggregateRootFactory();
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static void HasValidMemento<T>(T aggregate)
            where T : AggregateRoot
        {
            if (aggregate == null)
            {
                throw new ArgumentNullException("aggregate");
            }

            var memento = aggregate.GetMemento();
            if (memento == null)
            {
                throw new Exception("No memento defined!");
            }

            var sameAggregate = Factory.Create<T>(memento, aggregate.Revision, Enumerable.Empty<object>(), "test");
            var sameMemento = sameAggregate.GetMemento();

            var expected = Serializer.Serialize(memento);
            var actual = Serializer.Serialize(sameMemento);

            if (actual != expected)
            {
                throw new Exception("Invalid memento implementation!");
            }
        }
    }
}
