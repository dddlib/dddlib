namespace dddlib.Persistence.Playground
{
    using System;
    using System.Collections.Generic;
    using dddlib.Configuration;
    using dddlib.Persistence.NEventStore;
    using global::NEventStore;
    //using global::NEventStore.Dispatcher;
    //using global::NEventStore.Persistence.Sql.SqlDialects;

    internal static class MainProgram
    {
        private static void Main()
        {
            using (var store = WireupEventStore())
            {
                var eventStore = new EventStore(store);
                var repository = new EventStoreRepository(new Memory.MemoryIdentityMap(), eventStore);

                var car = new Car("abc");
                repository.Save(car);
                var car2 = repository.Load<Car>("abc");

                var data = new List<string>();
                var view = new CarView(data);
                eventStore.ReplayEventsTo(view);
            }
        }

        private static IStoreEvents WireupEventStore()
        {
            return global::NEventStore.Wireup.Init()
                .UsingInMemoryPersistence()
                //.UsingSqlPersistence("EventStore") // Connection string is in app.config
                //.WithDialect(new MsSqlDialect())
                .EnlistInAmbientTransaction()
                .InitializeStorageEngine()
                .UsingJsonSerialization()
                //.DispatchTo(new DelegateMessageDispatcher(DispatchCommit))
                .Build();
        }

        internal class Car : AggregateRoot
        {
            internal Car()
            {
            }

            public Car(string registration)
            {
                this.Apply(new NewCar { Registration = registration });
            }

            [NaturalKey]
            public string Registration { get; private set; }

            private void Handle(NewCar @event)
            {
                this.Registration = @event.Registration;
            }
        }

        internal class NewCar
        {
            public string Registration { get; set; }
        }

        public class CarView(private List<string> data)
        {
            public void Handle(NewCar @event)
            {
                this.data.Add(@event.Registration);
            }
        }

        private class Bootstrapper : IBootstrapper
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.AggregateRoot<Car>().ToReconstituteUsing(() => new Car());
            }
        }
    }
}