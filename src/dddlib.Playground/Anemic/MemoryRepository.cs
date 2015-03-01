namespace dddlib.Playground.Anemic
{
    using System.Collections.Generic;

    public class MemoryRepository
    {
        private readonly Dictionary<string, Car> store = new Dictionary<string, Car>();

        public void Save(Car car)
        {
            this.store[car.Registration.Number] = car;
        }

        public Car Load(string registrationNumber)
        {
            return this.store[registrationNumber];
        }
    }
}
