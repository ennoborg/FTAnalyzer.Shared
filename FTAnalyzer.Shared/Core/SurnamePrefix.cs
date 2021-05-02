namespace FTAnalyzer
{
    public class SurnamePrefix
    {
        // NOTE: we do no return the prefix string found, that string is not needed. We simply return the offset the base name starts at.
        // NOTE: the order of strings in these lists here is important! A preifx like "van der " has to come before "van " or the code will never match it.
        // NOTE: the inclusion (and sometimes omisson) of the space after the prefix is significant, and simplifies the code
        static string[] NamePrefixesA =
        {
            "aan den ",
            "aan der ",
            "aan het ",
            "aan 't ",
            "aan t ",

            "auf dem ",
            "auf den ",
            "auf der ",
            "aus dem ",
            "aus den ",
            "aus der ",
        };

        static string[] NamePrefixesB =
        {
            "boven d' ",

            "bij den ",
            "bij het ",
            "bij de ",
            "bij 't ",
            "bij t ",
        };

        static string[] NamePrefixesD =
        {
            "de van der ",
            "de van ",

            "de die le ",
            "de la ",
            "de le ",
            "del",
            "de l'", //  no space following
            "den ",
            "der ",
            "des ",
            "da",
            "du",
            "d'", // no space following
            "d ",
        };

        static string[] NamePrefixesE =
        {
            "el ",
        };

        static string[] NamePrefixesH =
        {
            "het ",
        };

        static string[] NamePrefixesI =
        {
            "in der ",
            "in den ",
            "in de ",
            "in het ",
            "in 't ",
            "in t ",
        };

        static string[] NamePrefixesO =
        {
            "over den ",
            "over de ",
            "over het ",
            "over 't ",
            "over t ",
            "over ",


            "onder den ",
            "onder de ",
            "onder het ",
            "onder 't ",
            "onder t ",
            "onder ",

            "op gen ",
            "op den ",
            "op de ",
            "op het ",
            "op ten ",
            "op 't ",
            "op t ",
            "op ",
        };

        static string[] NamePrefixesT =
        {
            "ter ",
            "te ",
            "ter ",
            "ten ",
            "thoe ",
            "tho ",
            "toe ",
            "to ",
            "tot ",
        };

        static string[] NamePrefixesU =
        {
            "uijt den ",
            "uijt de ",
            "uijt te de ",
            "uijt ten ",
            "uijt 't ",
            "uijt t ",
            "uijt ",

            "uij den ",
            "uit de ",
            "uit te de ",
            "uit ten ",
            "uit 't ",
            "uit t ",
            "uit ",
        };

        static string[] NamePrefixesV =
        {
            "van der ",
            "van den ",
            "van de ",
            "van het ",
            "van 't ",
            "van t ",
            "van la ",
            "van ter ",

            "van van de ",
            "van der ",
            "van den ",
            "van ter ",
            "van gen ",
            "van de l'",//  no space following
            "van de ",

            "van ",

            "von dem ",
            "von den ",
            "von der ",

            "voor den ",
            "voor de ",
            "voor in 't ",
            "voor ",

            "vor der ",
            "vor ",
            "v. d.",    // abbreviations
            "v.d.",
        };

        static string[] NamePrefixesZ =
        {
            "zum ",
            "zur ",
            "zu "
        };

        static string[] NamePrefixesApostrophe =
        {
            "'s ",
            "'t ",
        };
        static string[] NamePrefixesAAccentGrave =
        {
            "à ",
        };
        static string[] NamePrefixesAAccentAcute =
        {
            "á ",
        };


        static int BasenameStart(in string[] NamePrefixes, in string Surname)
        {
            int PrefixFound = 0;

            for (int i = 0; i < NamePrefixes.Length; i++)
            {
                if (Surname.StartsWith(NamePrefixes[i]))
                {
                    PrefixFound = NamePrefixes[i].Length;
                    break;
                };
            };

            return PrefixFound;
        }



        // BaseName is Surname without the prefix
        // note: we do <em>not</em> create a lowercasecopy of the surname string; we demand that prefixes should be correctly lowercased already. That's not a bug, but a feature.
        public static int BasenameStart(in string Surname)
        {
            int PrefixFound = 0;

            if (2 < Surname.Length)
            {
                char chFirst = Surname[0];

                switch (chFirst)
                {
                    case '\\': PrefixFound = BasenameStart(NamePrefixesApostrophe, Surname); break;

                    case 'a': PrefixFound = BasenameStart(NamePrefixesA, Surname); break;
                    case 'b': PrefixFound = BasenameStart(NamePrefixesB, Surname); break;
                    case 'd': PrefixFound = BasenameStart(NamePrefixesD, Surname); break;
                    case 'e': PrefixFound = BasenameStart(NamePrefixesE, Surname); break;

                    case 'h': PrefixFound = BasenameStart(NamePrefixesH, Surname); break;
                    case 'i': PrefixFound = BasenameStart(NamePrefixesI, Surname); break;
                    case 'o': PrefixFound = BasenameStart(NamePrefixesO, Surname); break;
                    case 't': PrefixFound = BasenameStart(NamePrefixesT, Surname); break;

                    case 'u': PrefixFound = BasenameStart(NamePrefixesU, Surname); break;
                    case 'v': PrefixFound = BasenameStart(NamePrefixesV, Surname); break;

                    case 'à': PrefixFound = BasenameStart(NamePrefixesAAccentGrave, Surname); break;
                    case 'á': PrefixFound = BasenameStart(NamePrefixesAAccentAcute, Surname); break;
                }
            };
            return PrefixFound;
        }

    }
}
