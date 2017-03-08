// <copyright file="Program.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace perftest
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using dddlib.Persistence;
    using dddlib.Persistence.SqlServer;
    using dddlib.Tests.Sdk;
    using global::NEventStore;
    using HdrHistogram;

    internal class Program
    {
        private static readonly TimeSpan RunPeriod = TimeSpan.FromSeconds(60);

        private readonly Baseline.CarRepository baselineRepository;
        private readonly IEventStoreRepository eventStoreRepository;
        private readonly IEventStoreRepository nEventStoreRepository;

        public Program(
            Baseline.CarRepository baselineRepository,
            IEventStoreRepository eventStoreRepository,
            IEventStoreRepository nEventStoreRepository)
        {
            this.baselineRepository = baselineRepository;
            this.eventStoreRepository = eventStoreRepository;
            this.nEventStoreRepository = nEventStoreRepository;
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("Running...");
            using (var fixture = new SqlServerFixture())
            {
                var database = new Integration.Database(fixture);

                using (var store = GetNEventStore(database.ConnectionString))
                {
                    var baselineRepository = new Baseline.CarRepository(database.ConnectionString);
                    var eventStoreRepository = new SqlServerEventStoreRepository(database.ConnectionString, "dddlib");
                    var nEventStoreRepository = new dddlib.Persistence.NEventStore.NEventStoreRepository(database.ConnectionString, "NEventStore", store);

                    var program = new Program(baselineRepository, eventStoreRepository, nEventStoreRepository);

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
                ////Console.ReadKey();
            }
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
            var histogram1 = new LongHistogram(TimeSpan.TicksPerMinute, 2);
            var histogram2 = new LongHistogram(TimeSpan.TicksPerMinute, 2);
            var histogram3 = new LongHistogram(TimeSpan.TicksPerMinute, 2);

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

            iteration = 0;
            stopwatch = Stopwatch.StartNew();
            do
            {
                histogram3.RecordLatency(() => ExecuteNEventStoreTest(++iteration));
            } while (stopwatch.Elapsed < RunPeriod);

            using (var writer = new StreamWriter("baseline.hgrm"))
            {
                histogram1.OutputPercentileDistribution(writer);
                Console.WriteLine("Written: " + "baseline.hgrm");
            }

            using (var writer = new StreamWriter("dddlib.hgrm"))
            {
                histogram2.OutputPercentileDistribution(writer);
                Console.WriteLine("Written: " + "dddlib.hgrm");
            }

            using (var writer = new StreamWriter("NEventStore.hgrm"))
            {
                histogram3.OutputPercentileDistribution(writer);
                Console.WriteLine("Written: " + "NEventStore.hgrm");
            }
        }

        private void ExecuteBaselineTest(int iteration)
        {
            var car = new Baseline.Car { Registration = string.Concat("T", iteration), };
            this.baselineRepository.Save(car);

            var sameCar = this.baselineRepository.Load(car.Registration);
            sameCar.TotalDistanceDriven += 1;
            this.baselineRepository.Save(sameCar);

            var stillSameCar = this.baselineRepository.Load(car.Registration);
            stillSameCar.IsDestroyed = true;
            this.baselineRepository.Save(stillSameCar);

        }

        private void ExecuteSqlServerEventStoreTest(int iteration)
        {
            var car = new SqlServerEventStore.Car(string.Concat("T", iteration));
            this.eventStoreRepository.Save(car);

            var sameCar = this.eventStoreRepository.Load<SqlServerEventStore.Car>(car.Registration);
            sameCar.Drive(1);
            this.eventStoreRepository.Save(sameCar);

            var stillSameCar = this.eventStoreRepository.Load<SqlServerEventStore.Car>(car.Registration);
            stillSameCar.Scrap();
            this.eventStoreRepository.Save(stillSameCar);
        }

        private void ExecuteNEventStoreTest(int iteration)
        {
            var car = new SqlServerEventStore.Car(string.Concat("T", iteration));
            this.nEventStoreRepository.Save(car);

            var sameCar = this.nEventStoreRepository.Load<SqlServerEventStore.Car>(car.Registration);
            sameCar.Drive(1);
            this.nEventStoreRepository.Save(sameCar);

            var stillSameCar = this.nEventStoreRepository.Load<SqlServerEventStore.Car>(car.Registration);
            stillSameCar.Scrap();
            this.nEventStoreRepository.Save(stillSameCar);
        }

        private static NEventStore.IStoreEvents GetNEventStore(string connectionString)
        {
            return Wireup.Init()
                //.LogToOutputWindow()
                .UsingSqlPersistence("SqlDatabase", "System.Data.SqlClient", connectionString)
                    .WithDialect(new global::NEventStore.Persistence.Sql.SqlDialects.MsSqlDialect())
                    .InitializeStorageEngine()
                    .UsingJsonSerialization()
                .Build();
        }
    }
}
