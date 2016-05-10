// <copyright file="Program.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace perftest
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using dddlib.Persistence.SqlServer;
    using HdrHistogram;

    internal class Program
    {
        private static readonly TimeSpan RunPeriod = TimeSpan.FromSeconds(10);

        private readonly string connectionString;

        public Program(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public static void Main(string[] args)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["SqlDatabase"].ConnectionString;

            Console.WriteLine("Running...");
            Setup(connectionString);

            var program = new Program(connectionString);
            if (args.FirstOrDefault() != null && args.FirstOrDefault().Trim().ToLowerInvariant() == "profile")
            {
                program.RunForProfiling();
            }
            else
            {
                program.RunForContinuousIntegration();
            }

            TearDown(connectionString);
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

        private static void Setup(string connectionString)
        {
            new Baseline.CarRepository(connectionString).Save(new Baseline.Car { Registration = "ABC", });
            new SqlServerEventStoreRepository(connectionString).Save(new SqlServerEventStore.Car("ABC"));
        }

        private void ExecuteBaselineTest(int iteration)
        {
            var repository = new Baseline.CarRepository(this.connectionString);
            var car = repository.Load("ABC");
            car.TotalDistanceDriven += 1;
            repository.Save(car);
        }

        private void ExecuteSqlServerEventStoreTest(int iteration)
        {
            var repository = new SqlServerEventStoreRepository(this.connectionString);
            var car = repository.Load<SqlServerEventStore.Car>("ABC");
            car.Drive(1);
            repository.Save(car);
        }

        private static void TearDown(string connectionString)
        {
            new Baseline.CarRepository(connectionString).Save(new Baseline.Car { Registration = "ABC", IsDestroyed = true });

            var repository = new SqlServerEventStoreRepository(connectionString);
            var car = repository.Load<SqlServerEventStore.Car>("ABC");
            car.Scrap();
            repository.Save(car);
        }
    }
}
