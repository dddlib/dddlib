namespace dddlib.Playground.Domain
{
    using System;
    using System.Linq;

    // LINK (Cameron): http://en.wikipedia.org/wiki/Vehicle_registration_plates_of_the_United_Kingdom,_Crown_dependencies_and_overseas_territories
    public class RegistrationB
    {
        public RegistrationB(string areaCode)
        {
            Guard.Against.Null(() => areaCode);

            if (areaCode.Length != 2)
            {
                throw new BusinessException("The area code must consist of two letters which together indicate the local registration office.");
            }

            switch (areaCode[0])
            {
                // NOTE (Cameron): Only Birmingham is supported here...
                case 'b':
                case 'B':
                    if (new[] { "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "O", "P", "R", "S", "T", "U", "V", "W", "X", "Y", }
                        .Contains(areaCode.Substring(1, 1), StringComparer.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    goto default;

                default:
                    throw new BusinessException("The local office identifier of the area code is invalid.");
            }

            this.AreaCode = areaCode;
        }

        public string AreaCode { get; private set; }

        public int AgeIdentifier { get; set; }

        public string RandomLetters { get; set; }
    }
}
