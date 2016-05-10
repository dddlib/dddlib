// <copyright file="Car.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace perftest.Baseline
{
    // NOTE (Cameron): In it's most basic form for persistence, Car is just data.
    public class Car
    {
        public string Registration { get; set; }

        public int TotalDistanceDriven { get; set; }

        public bool IsDestroyed { get; set; }
    }
}
