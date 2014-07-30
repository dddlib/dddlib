namespace dddlib.Playground.Anemic
{
    public class Playground
    {
        public void Play()
        {
            var registration1 = new Registration { Number = "abc" };
            var registration2 = new Registration { Number = "BD47aax" };
            var registration3 = new Registration { Number = "bd47 AAx" };

            var areEqual1 = registration1 == registration2;
            var areEqual2 = registration2 == registration3;
        }
    }
}
