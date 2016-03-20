// <copyright file="IRegistrationService.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    public interface IRegistrationService
    {
        bool ConfirmValid(string registrationNumber);
    }
}
