// <copyright file="SqlServerEventPersistence.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Tests.Feature
{
    using System;
    using dddlib.Configuration;
    using dddlib.Persistence;
    using dddlib.Persistence.Memory;
    using dddlib.Persistence.Tests.Sdk;
    using dddlib.Runtime;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib [with event sourcing]
    // In order save state
    // I need to be able to persist an aggregate root (in SQL Server)
    public abstract class SqlServerEventPersistence ////: EventPersistenceFeature
    {
    }
}
