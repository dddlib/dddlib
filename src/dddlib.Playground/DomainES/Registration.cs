namespace dddlib.Playground.DomainES
{
    using System.Collections.Generic;
    using System.Linq;

    // LINK (Cameron): http://en.wikipedia.org/wiki/Vehicle_registration_plates_of_the_United_Kingdom,_Crown_dependencies_and_overseas_territories
    public class Registration : ValueObject<Registration>
    {
        private static readonly Dictionary<char, string> RegistrationAreas = new Dictionary<char, string>
        {
            { 'B', "Birmingham" },
            { 'X', "Personal export" },
        };

        private readonly string registrationArea;
        private readonly int yearRegistered;

        public Registration(string number)
        {
            Guard.Against.Null(() => number);

            var registration = number.ToUpperInvariant().Replace(" ", string.Empty);

            var ageIdentifier = default(int);
            if (registration.Length != 7 || !int.TryParse(registration.Substring(2, 2), out ageIdentifier))
            {
                throw new BusinessException("The registration number must consist of a string in the format 'BD51 SMR'.");
            }

            var areaCode = registration.Substring(0, 2);
            var randomLetters = registration.Substring(4, 3);

            switch (areaCode[0])
            {
                // NOTE (Cameron): Birmingham...
                case 'B':
                    if (new[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', }
                        .Contains(areaCode[1]))
                    {
                        break;
                    }
                    goto default;

                // NOTE (Cameron): Personal export...
                case 'X':
                    if (new[] { 'A', 'B', 'C', 'D', 'E', 'F', }.Contains(areaCode[1]))
                    {
                        break;
                    }
                    goto default;

                default:
                    throw new BusinessException("The local office identifier of the area code is invalid.");
            }

            if (ageIdentifier < 0 || ageIdentifier > 99)
            {
                throw new BusinessException("The age identifier must be an integer between 0 and 99.");
            }

            if (randomLetters.Any(randomLetter => new[] { 'I', 'Q' }.Contains(randomLetter)))
            {
                throw new BusinessException("The random letters must be a three letter sequence excluding the letters 'I' or 'Q'.");
            }

            this.Number = string.Concat(areaCode.ToUpperInvariant(), ageIdentifier.ToString("00"), " ", randomLetters.ToUpperInvariant());
            this.registrationArea = RegistrationAreas[areaCode[0]];
            this.yearRegistered = 2000 + (ageIdentifier > 50 ? ageIdentifier - 50 : ageIdentifier);
        }

        public string Number { get; private set; }

        public string RegistrationArea
        {
            get { return this.registrationArea; }
        }

        public int YearRegistered
        {
            get { return this.yearRegistered; }
        }
    }
}
