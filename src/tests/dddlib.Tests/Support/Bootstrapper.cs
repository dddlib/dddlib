// <copyright file="Bootstrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    using System;
    using dddlib.Runtime;

    internal class Bootstrapper : IBootstrapper
    {
        public void Bootstrap(IConfiguration configure)
        {
            ////configure.AggregateRoots.ToDoNothingSpecial();

            //////configure.AggregateRoots.ToDispatchEventsUsing(type => new DefaultEventDispatcher(type));

            ////configure.AggregateRoot<Car>()
            ////    ////.ToDoNothingSpecial()
            ////    .ToUseNaturalKey(car => car.Registration)
            ////    .ToReconstituteUsing(() => new Car());

            ////configure.AggregateRoot<Car>()
            ////    .ToUseNaturalKey(car => car.Registration)
            ////    .ToReconstituteUsing(() => new Car())
            ////    ////.ToDoNothingSpecial()
            ////    .ToUseNaturalKey(a => a.Registration);

            ////configure.Entity<Car>().ToUseNaturalKey(car => car.Registration);
        }
    }
}
