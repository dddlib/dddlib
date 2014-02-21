// <copyright file="TypeDescriptor.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    /*  TODO (Cameron): 
        This needs fixing one way or another, it is ValueObject-esq. Maybe add some rules in ctor. eg. No event dispatcher for value type. That logic needs to go somewhere.
        DO NOT RENAME! DO NOT CHANGE TO SEPERATE CLASSES. EventDispatcher will be null for an AggregateRoot if runtime mode is Plain.
        Hang on... EventDispatcher should never be null, we might need a null implementation though.
        Create a NullEventDispatcher (internal/private).  */

    using System;
    using System.Collections.Generic;

    internal class TypeDescriptor
    {
        public IEventDispatcher EventDispatcher { get; internal set; }

        public IEqualityComparer<object> EqualityComparer { get; internal set; }

        public Func<object> Factory { get; internal set; }
    }
}
