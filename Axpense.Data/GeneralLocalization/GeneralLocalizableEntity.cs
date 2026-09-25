using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Threading;

namespace Axpense.Data.GeneralLocalization
{
    public class GeneralLocalizableEntity
    {
        public string Localize(string textAr, string textEN)
        {
            CultureInfo CultureInfo = Thread.CurrentThread.CurrentCulture;
            if (CultureInfo.TwoLetterISOLanguageName.ToLower().Equals("ar"))
                return textAr;
            return textEN;
        }
    }
}
