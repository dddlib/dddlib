// <copyright file="GlobalSuppressions.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

// LINK (Cameron): https://github.com/dddlib/dddlib/issues/40
// NOTE (Cameron): There is an issue in dddlib with parallelization in the tests. Specifically, the Application creation.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]