// <copyright file="Bootstrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests
{
    using Configuration;

    public class Bootstrapper : IBootstrapper
    {
        public void Bootstrap(IConfiguration configure)
        {
            configure.ValueObject<Bug.Bug0128.OtherSubject>().ToUseEqualityComparer(new Bug.Bug0128.OtherSubject.EqualityComparer());
        }
    }
}
