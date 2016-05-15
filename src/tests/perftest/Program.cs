// <copyright file="Program.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace perftest
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using dddlib.Persistence.SqlServer;
    using dddlib.Tests.Sdk;
    using HdrHistogram;

    internal class Program
    {
        private static readonly TimeSpan RunPeriod = TimeSpan.FromSeconds(5);

        private readonly string connectionString;

        public Program(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("Running...");
            using (var fixture = new SqlServerFixture())
            {
                var database = new Integration.Database(fixture);
                var program = new Program(fixture.ConnectionString);
                if (args.FirstOrDefault() != null && args.FirstOrDefault().Trim().ToLowerInvariant() == "profile")
                {
                    program.RunForProfiling();
                }
                else
                {
                    program.RunForContinuousIntegration();
                }
            }

            Console.WriteLine("Finished.");
        }

        private void RunForProfiling()
        {
            var iteration = 0;
            var stopwatch = Stopwatch.StartNew();
            do
            {
                ExecuteSqlServerEventStoreTest(++iteration);
            } while (stopwatch.Elapsed < RunPeriod);
        }

        private void RunForContinuousIntegration()
        {
            var histogram1 = new LongHistogram(TimeSpan.TicksPerSecond, 3);
            var histogram2 = new LongHistogram(TimeSpan.TicksPerSecond, 3);

            var iteration = 0;
            var stopwatch = Stopwatch.StartNew();
            do
            {
                histogram1.RecordLatency(() => ExecuteBaselineTest(++iteration));
            } while (stopwatch.Elapsed < RunPeriod);

            iteration = 0;
            stopwatch = Stopwatch.StartNew();
            do
            {
                histogram2.RecordLatency(() => ExecuteSqlServerEventStoreTest(++iteration));
            } while (stopwatch.Elapsed < RunPeriod);

            using (var writer = new StreamWriter("baseline.hgrm"))
            {
                histogram1.OutputPercentileDistribution(writer);
                Console.WriteLine("Written: " + "baseline.hgrm");
            }

            using (var writer = new StreamWriter("SqlServerEventStore.hgrm"))
            {
                histogram2.OutputPercentileDistribution(writer);
                Console.WriteLine("Written: " + "SqlServerEventStore.hgrm");
            }
        }

        ////private static void Setup(string connectionString)
        ////{
        ////    new Baseline.CarRepository(connectionString).Save(new Baseline.Car { Registration = "ABC", });
        ////    new SqlServerEventStoreRepository(connectionString, "dddlib").Save(new SqlServerEventStore.Car("ABC"));
        ////}

        private void ExecuteBaselineTest(int iteration)
        {
            var repository = new Baseline.CarRepository(this.connectionString);

            var car = new Baseline.Car { Registration = string.Concat("T", iteration), };
            repository.Save(car);

            var sameCar = repository.Load(car.Registration);
            sameCar.TotalDistanceDriven += 1;
            repository.Save(sameCar);

            var stillSameCar = repository.Load(car.Registration);
            stillSameCar.IsDestroyed = true;
            repository.Save(stillSameCar);

        }

        private void ExecuteSqlServerEventStoreTest(int iteration)
        {
            var repository = new SqlServerEventStoreRepository(this.connectionString);

            var car = new SqlServerEventStore.Car(string.Concat("T", iteration));
            repository.Save(car);

            var sameCar = repository.Load<SqlServerEventStore.Car>(car.Registration);
            sameCar.Drive(1);
            repository.Save(sameCar);

            var stillSameCar = repository.Load<SqlServerEventStore.Car>(car.Registration);
            stillSameCar.Scrap();
            repository.Save(stillSameCar);
        }

        ////private static void TearDown(string connectionString)
        ////{
        ////    new Baseline.CarRepository(connectionString).Save(new Baseline.Car { Registration = "ABC", IsDestroyed = true });

        ////    var repository = new SqlServerEventStoreRepository(connectionString);
        ////    var car = repository.Load<SqlServerEventStore.Car>("ABC");
        ////    car.Scrap();
        ////    repository.Save(car);
        ////}
    }
}
