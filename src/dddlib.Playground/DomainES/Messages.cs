namespace dddlib.Playground.DomainES
{
    public class CarEnteredIntoSystem
    {
        public string RegistrationNumber { get; set; }
    }

    public class CarPassedThroughSpeedTrap
    {
        public string RegistrationNumber { get; set; }

        public int Speed { get; set; }
    }
}
