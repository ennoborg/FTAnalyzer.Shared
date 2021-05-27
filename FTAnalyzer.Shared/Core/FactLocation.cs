using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using FTAnalyzer.Properties;
using FTAnalyzer.Utilities;
#if !__MACOS__
using System.Web.UI.WebControls;
#endif
namespace FTAnalyzer
{
    public class FactLocation : IComparable<FactLocation>, IComparable
    {
        #region Variables
        // static log4net.ILog log = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public const int UNKNOWN = -1, COUNTRY = 0, REGION = 1, SUBREGION = 2, ADDRESS = 3, PLACE = 4;

        public string OriginalText { get; private set; }
        public string GEDCOMLocation { get; private set; }
        public string SortableLocation { get; private set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public string SubRegion { get; set; }
        public string Address { get => Address1; set { Address1 = value; AddressNoNumerics = FixNumerics(value ?? string.Empty, false); } }
        public string Place { get => Place1; set { Place1 = value; PlaceNoNumerics = FixNumerics(value ?? string.Empty, false); } }
        public string CountryMetaphone { get; private set; }
        public string RegionMetaphone { get; private set; }
        public string SubRegionMetaphone { get; private set; }
        public string AddressMetaphone { get; private set; }
        public string PlaceMetaphone { get; private set; }
        public string AddressNoNumerics { get; private set; }
        public string PlaceNoNumerics { get; private set; }
        public string FuzzyMatch { get; private set; }
        public string FuzzyNoParishMatch { get; private set; }
        public string ParishID { get; internal set; }
        public int Level { get; private set; }
        public Region KnownRegion { get; private set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double LatitudeM { get; set; }
        public double LongitudeM { get; set; }
        public string FoundLocation { get; set; }
        public string FoundResultType { get; set; }
        public int FoundLevel { get; set; }
        public double PixelSize { get; set; }
        public string GEDCOMLoaded => FTAnalyzerCreated ? "No" : "Yes";
        public bool GEDCOMLatLong { get; set; }
        public bool FTAnalyzerCreated
        {
            get => _created;
            set
            {
                _created = value;
                if (!_created)
                    GEDCOMLocation = OriginalText;
            }
        }
        readonly Dictionary<string, Individual> individuals;
        readonly string[] _Parts;
        bool _created;

        static Dictionary<string, string> COUNTRY_TYPOS = new Dictionary<string, string>();
        static Dictionary<string, string> REGION_TYPOS = new Dictionary<string, string>();
        static Dictionary<string, string> REGION_SHIFTS = new Dictionary<string, string>();
        static Dictionary<string, string> FREECEN_LOOKUP = new Dictionary<string, string>();
        static Dictionary<string, Tuple<string, string>> FINDMYPAST_LOOKUP = new Dictionary<string, Tuple<string, string>>();
        static IDictionary<string, FactLocation> LOCATIONS;
        static Dictionary<Tuple<int, string>, string> GOOGLE_FIXES = new Dictionary<Tuple<int, string>, string>();
        static Dictionary<Tuple<int, string>, string> LOCAL_GOOGLE_FIXES;

        static Dictionary<string, string> COUNTRY_SHIFTS = new Dictionary<string, string>();
        static Dictionary<string, string> CITY_ADD_COUNTRY = new Dictionary<string, string>();
        public static FactLocation UNKNOWN_LOCATION;
        public static FactLocation BLANK_LOCATION;
        public static FactLocation TEMP = new FactLocation();
        #endregion

        #region Static Constructor
        static FactLocation()
        {
            ResetLocations();
        }

        public static void LoadConversions(string startPath)
        {
            // load conversions from XML file
            #region Fact Location Fixes
            if (startPath is null) return;
#if __MACOS__
            string filename = Path.Combine(startPath, @"../Resources/FactLocationFixes.xml");
#elif __IOS__
            string filename = Path.Combine(startPath, @"Resources/FactLocationFixes.xml");
#else
            string filename = Path.Combine(startPath, @"Resources\FactLocationFixes.xml");
#endif
            Console.WriteLine($"Loading factlocation fixes from: {filename}");
            if (File.Exists(filename) && !GeneralSettings.Default.SkipFixingLocations) // don't load file if skipping fixing locations
            {
                XmlDocument xmlDoc = new XmlDocument() { XmlResolver = null };
                string xml = File.ReadAllText(filename);
                StringReader sreader = new StringReader(xml);
                using (XmlReader reader = XmlReader.Create(sreader, new XmlReaderSettings() { XmlResolver = null }))
                    xmlDoc.Load(reader);
                foreach (XmlNode n in xmlDoc.SelectNodes("Data/Fixes/CountryTypos/CountryTypo"))
                {
                    string from = n.Attributes["from"].Value;
                    string to = n.Attributes["to"].Value;
                    if (COUNTRY_TYPOS.ContainsKey(from))
                        Console.WriteLine(string.Format("Error duplicate country typos :{0}", from));
                    if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                        COUNTRY_TYPOS.Add(from, to);
                }
                foreach (XmlNode n in xmlDoc.SelectNodes("Data/Fixes/RegionTypos/RegionTypo"))
                {
                    string from = n.Attributes["from"].Value;
                    string to = n.Attributes["to"].Value;
                    if (REGION_TYPOS.ContainsKey(from))
                        Console.WriteLine(string.Format("Error duplicate region typos :{0}", from));
                    if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                        REGION_TYPOS.Add(from, to);
                }
                foreach (XmlNode n in xmlDoc.SelectNodes("Data/Fixes/ChapmanCodes/ChapmanCode"))
                {  // add Chapman code to Region Typos to convert locations with codes to region text strings
                    string chapmanCode = n.Attributes["chapmanCode"].Value;
                    string countyName = n.Attributes["countyName"].Value;
                    if (REGION_TYPOS.ContainsKey(chapmanCode))
                        Console.WriteLine(string.Format("Error duplicate region typos adding ChapmanCode :{0}", chapmanCode));
                    if (!string.IsNullOrEmpty(chapmanCode) && !string.IsNullOrEmpty(countyName))
                        REGION_TYPOS.Add(chapmanCode, countyName);
                }
                foreach (XmlNode n in xmlDoc.SelectNodes("Data/Fixes/DemoteCountries/CountryToRegion"))
                {
                    string from = n.Attributes["region"].Value;
                    string to = n.Attributes["country"].Value;
                    if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                    {
                        if (COUNTRY_SHIFTS.ContainsKey(from))
                            Console.WriteLine(string.Format("Error duplicate country shift :{0}", from));
                        COUNTRY_SHIFTS.Add(from, to);
                    }
                }
                foreach (XmlNode n in xmlDoc.SelectNodes("Data/Fixes/DemoteCountries/CityAddCountry"))
                {
                    string from = n.Attributes["city"].Value;
                    string to = n.Attributes["country"].Value;
                    if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                    {
                        if (CITY_ADD_COUNTRY.ContainsKey(from))
                            Console.WriteLine(string.Format("Error duplicate city add country :{0}", from));
                        if (COUNTRY_SHIFTS.ContainsKey(from)) // also check country shifts for duplicates
                            Console.WriteLine(string.Format("Error duplicate city in country shift :{0}", from));
                        CITY_ADD_COUNTRY.Add(from, to);
                    }
                }
                foreach (XmlNode n in xmlDoc.SelectNodes("Data/Fixes/DemoteRegions/RegionToParish"))
                {
                    string from = n.Attributes["parish"].Value;
                    string to = n.Attributes["region"].Value;
                    if (REGION_SHIFTS.ContainsKey(from))
                        Console.WriteLine(string.Format("Error duplicate region shift :{0}", from));
                    if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                    {
                        REGION_SHIFTS.Add(from, to);
                    }
                }
                foreach (XmlNode n in xmlDoc.SelectNodes("Data/Lookups/FreeCen/Lookup"))
                {
                    string code = n.Attributes["code"].Value;
                    string county = n.Attributes["county"].Value;
                    if (FREECEN_LOOKUP.ContainsKey(county))
                        Console.WriteLine(string.Format("Error duplicate freecen lookup :{0}", county));
                    if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(county))
                        FREECEN_LOOKUP.Add(county, code);
                }
                foreach (XmlNode n in xmlDoc.SelectNodes("Data/Lookups/FindMyPast/Lookup"))
                {
                    string code = n.Attributes["code"].Value;
                    string county = n.Attributes["county"].Value;
                    string country = n.Attributes["country"].Value;
                    if (FINDMYPAST_LOOKUP.ContainsKey(county))
                        Console.WriteLine(string.Format("Error duplicate FindMyPast lookup :{0}", county));
                    if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(county))
                    {
                        Tuple<string, string> result = new Tuple<string, string>(country, code);
                        FINDMYPAST_LOOKUP.Add(county, result);
                    }
                }
                COUNTRY_SHIFTS = COUNTRY_SHIFTS.Concat(CITY_ADD_COUNTRY).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            else
            {
                Console.WriteLine("Failed to find FactLocationFixes.xml File");
            }
            #endregion
        }

        #endregion

        #region Object Constructors
        FactLocation()
        {
            OriginalText = string.Empty;
            GEDCOMLocation = string.Empty;
            FixedLocation = string.Empty;
            SortableLocation = string.Empty;
            Country = string.Empty;
            Region = string.Empty;
            SubRegion = string.Empty;
            Address = string.Empty;
            Place = string.Empty;
            ParishID = null;
            FuzzyMatch = string.Empty;
            individuals = new Dictionary<string, Individual>();
            Latitude = 0;
            Longitude = 0;
            LatitudeM = 0;
            LongitudeM = 0;
            Level = UNKNOWN;
            GEDCOMLatLong = false;
            FoundLocation = string.Empty;
            FoundResultType = string.Empty;
            FoundLevel = -2;
            FTAnalyzerCreated = true; // override when GEDCOM created.
            _Parts = new string[] { Country, Region, SubRegion, Address, Place };
        }

        FactLocation(string location, string latitude, string longitude)
            : this(location)
        {
            Latitude = double.TryParse(latitude, out double temp) ? temp : 0;
            Longitude = double.TryParse(longitude, out temp) ? temp : 0;
        }

        public FactLocation(string location)
            : this()
        {
            if (!string.IsNullOrEmpty(location))
            {
                OriginalText = location;
                // we need to parse the location string from a little injun to a big injun
                int comma = location.LastIndexOf(",", StringComparison.Ordinal);
                if (comma > 0)
                {
                    Country = location.Substring(comma + 1);
                    location = location.Substring(0, comma);
                    comma = location.LastIndexOf(",", comma, StringComparison.Ordinal);
                    if (comma > 0)
                    {
                        Region = location.Substring(comma + 1);
                        location = location.Substring(0, comma);
                        comma = location.LastIndexOf(",", comma, StringComparison.Ordinal);
                        if (comma > 0)
                        {
                            SubRegion = location.Substring(comma + 1);
                            location = location.Substring(0, comma);
                            comma = location.LastIndexOf(",", comma, StringComparison.Ordinal);
                            if (comma > 0)
                            {
                                Address = location.Substring(comma + 1);
                                Place = location.Substring(0, comma);
                                Level = PLACE;
                            }
                            else
                            {
                                Address = location;
                                Level = ADDRESS;
                            }
                        }
                        else
                        {
                            SubRegion = location;
                            Level = SUBREGION;
                        }
                    }
                    else
                    {
                        Region = location;
                        Level = REGION;
                    }
                }
                else
                {
                    Country = location;
                    Level = COUNTRY;
                }
                //string before = $"{SubRegion}, {Region}, {Country}".ToUpper().Trim();
                if (GeneralSettings.Default.SkipFixingLocations)
                { // we aren't fixing locations but we still need to allow for comma space as a valid separator
                    if (Country.Length > 1 && Country[0] == ' ')
                        Country = Country.Substring(1);
                    if (Region.Length > 1 && Region[0] == ' ')
                        Region = Region.Substring(1);
                    if (SubRegion.Length > 1 && SubRegion[0] == ' ')
                        SubRegion = SubRegion.Substring(1);
                    if (Address.Length > 1 && Address[0] == ' ')
                        Address = Address.Substring(1);
                    if (Place.Length > 1 && Place[0] == ' ')
                        Place = Place.Substring(1);
                }
                else
                {
                    TrimLocations();
                    if (!GeneralSettings.Default.AllowEmptyLocations)
                        FixEmptyFields();
                    RemoveDiacritics();
                    FixRegionFullStops();
                    FixCountryFullStops();
                    FixMultipleSpacesAmpersandsCommas();
                    FixUKGBTypos();
                    FixCountryTypos();
                    Country = EnhancedTextInfo.ToTitleCase(FixRegionTypos(Country).ToLower());
                    ShiftCountryToRegion();
                    Region = FixRegionTypos(Region);
                    ShiftRegionToParish();
                    FixDoubleLocations();
                }
                SetFixedLocation();
                SetSortableLocation();
                SetMetaphones();
                KnownRegion = Regions.GetRegion(Region);
                if (!GeneralSettings.Default.SkipFixingLocations)
                    FixCapitalisation();
            }
            _Parts = new string[] { Country, Region, SubRegion, Address, Place };
        }
        #endregion

        #region Static Functions
        bool GecodingMatches(FactLocation temp)
            => Latitude == temp.Latitude && Longitude == temp.Longitude && LatitudeM == temp.LatitudeM && LongitudeM == temp.LongitudeM;

        public bool IsValidLatLong => Latitude >= -90 && Latitude <= 90 && Longitude >= -180 && Longitude <= 180;

        public static List<FactLocation> ExposeFactLocations => LOCATIONS.Values.ToList();

        public static FactLocation LookupLocation(string place)
        {
            LOCATIONS.TryGetValue(place, out FactLocation result);
            if (result is null)
                result = new FactLocation(place);
            return result;
        }

        public static IEnumerable<FactLocation> AllLocations => LOCATIONS.Values;

        public static void ResetLocations()
        {
            COUNTRY_TYPOS = new Dictionary<string, string>();
            REGION_TYPOS = new Dictionary<string, string>();
            COUNTRY_SHIFTS = new Dictionary<string, string>();
            REGION_SHIFTS = new Dictionary<string, string>();
            CITY_ADD_COUNTRY = new Dictionary<string, string>();
            FREECEN_LOOKUP = new Dictionary<string, string>();
            FINDMYPAST_LOOKUP = new Dictionary<string, Tuple<string, string>>();
            LOCATIONS = new Dictionary<string, FactLocation>();
            GOOGLE_FIXES = new Dictionary<Tuple<int, string>, string>();
            LOCAL_GOOGLE_FIXES = new Dictionary<Tuple<int, string>, string>();

            // set unknown location as unknown so it doesn't keep hassling to be searched
            BLANK_LOCATION = new FactLocation(string.Empty, "0.0", "0.0");
            UNKNOWN_LOCATION = new FactLocation("Unknown", "0.0", "0.0");
            LOCATIONS.Add("Unknown", UNKNOWN_LOCATION);
            if (!GeneralSettings.Default.SkipFixingLocations)
                LoadConversions(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location));
        }

        public static FactLocation BestLocation(IEnumerable<Fact> facts, FactDate when)
        {
            Fact result = BestLocationFact(facts, when, int.MaxValue);
            return result.Location;
        }

        public static Fact BestLocationFact(IEnumerable<Fact> facts, FactDate when, int limit)
        {
            // this returns a Fact for a FactLocation a person was at for a given period
            Fact result = new Fact("Unknown", Fact.UNKNOWN, FactDate.UNKNOWN_DATE, UNKNOWN_LOCATION);
            double minDistance = float.MaxValue;
            double distance;
            foreach (Fact f in facts)
            {
                if (f.FactDate.IsKnown && !string.IsNullOrEmpty(f.Location.OriginalText))
                {  // only deal with known dates and non empty locations
                    if (Fact.RANGED_DATE_FACTS.Contains(f.FactType) && f.FactDate.StartDate.Year != f.FactDate.EndDate.Year) // If fact type is ranged year use least end of range
                    {
                        distance = Math.Min(Math.Abs(f.FactDate.StartDate.Year - when.BestYear), Math.Abs(f.FactDate.EndDate.Year - when.BestYear));
                        distance = Math.Min(distance, Math.Abs(f.FactDate.BestYear - when.BestYear)); // also check mid point to ensure fact is picked up at any point in range
                    }
                    else
                        distance = Math.Abs(f.FactDate.BestYear - when.BestYear);
                    if (distance < limit)
                    {
                        if (distance < minDistance)
                        { // this is a closer date but now check to ensure we aren't overwriting a known country with an unknown one.
                            if (f.Location.IsKnownCountry || (!f.Location.IsKnownCountry && !result.Location.IsKnownCountry))
                            {
                                result = f;
                                minDistance = distance;
                            }
                        }
                    }
                }
            }
            return result;
        }

        public static void CopyLocationDetails(FactLocation from, FactLocation to)
        {
            if (to is null || from is null) return;
            to.Latitude = from.Latitude;
            to.Longitude = from.Longitude;
            to.LatitudeM = from.LatitudeM;
            to.LongitudeM = from.LongitudeM;
            to.FoundLocation = from.FoundLocation;
            to.FoundResultType = from.FoundResultType;
            to.FoundLevel = from.FoundLevel;
        }
        #endregion

        #region Fix Location string routines
        void TrimLocations()
        {
            // remove extraneous spaces
            Country = Country.Trim();
            Region = Region.Trim();
            SubRegion = SubRegion.Trim();
            Address = Address.Trim();
            Place = Place.Trim();
        }

        void FixEmptyFields()
        {
            if (Country.Length == 0)
            {
                Country = Region;
                Region = SubRegion;
                SubRegion = Address;
                Address = Place;
                Place = string.Empty;
            }
            if (Region.Length == 0)
            {
                Region = SubRegion;
                SubRegion = Address;
                Address = Place;
                Place = string.Empty;
            }
            if (SubRegion.Length == 0)
            {
                SubRegion = Address;
                Address = Place;
                Place = string.Empty;
            }
            if (Address.Length == 0)
            {
                Address = Place;
                Place = string.Empty;
            }
        }

        void RemoveDiacritics()
        {
            Country = EnhancedTextInfo.RemoveDiacritics(Country);
            Region = EnhancedTextInfo.RemoveDiacritics(Region);
            SubRegion = EnhancedTextInfo.RemoveDiacritics(SubRegion);
            Address = EnhancedTextInfo.RemoveDiacritics(Address);
            Place = EnhancedTextInfo.RemoveDiacritics(Place);
        }

        void FixCapitalisation()
        {
            if (Country.Length > 1)
                Country = char.ToUpper(Country[0]) + Country.Substring(1);
            if (Region.Length > 1)
                Region = char.ToUpper(Region[0]) + Region.Substring(1);
            if (SubRegion.Length > 1)
                SubRegion = char.ToUpper(SubRegion[0]) + SubRegion.Substring(1);
            if (Address.Length > 1)
                Address = char.ToUpper(Address[0]) + Address.Substring(1);
            if (Place.Length > 1)
                Place = char.ToUpper(Place[0]) + Place.Substring(1);
        }

        void FixRegionFullStops() => Region = Region.Replace(".", " ").Trim();

        void FixCountryFullStops() => Country = Country.Replace(".", " ").Trim();

        void FixMultipleSpacesAmpersandsCommas()
        {
            while (Country.IndexOf("  ", StringComparison.Ordinal) != -1)
                Country = Country.Replace("  ", " ");
            while (Region.IndexOf("  ", StringComparison.Ordinal) != -1)
                Region = Region.Replace("  ", " ");
            while (SubRegion.IndexOf("  ", StringComparison.Ordinal) != -1)
                SubRegion = SubRegion.Replace("  ", " ");
            while (Address.IndexOf("  ", StringComparison.Ordinal) != -1)
                Address = Address.Replace("  ", " ");
            while (Place.IndexOf("  ", StringComparison.Ordinal) != -1)
                Place = Place.Replace("  ", " ");
            Country = Country.Replace("&", "and").Replace(",", "").Trim();
            Region = Region.Replace("&", "and").Replace(",", "").Trim();
            SubRegion = SubRegion.Replace("&", "and").Replace(",", "").Trim();
            Address = Address.Replace("&", "and").Replace(",", "").Trim();
            Place = Place.Replace("&", "and").Replace(",", "").Trim();
        }

        void FixUKGBTypos()
        {
            if (Country == "UK" || Country == "GB")
            {
                if (Region == "Scotland" || Region == "England" || Region == "Wales")
                {
                    Country = Region;
                    Region = SubRegion;
                    SubRegion = Address;
                    Address = Place;
                    Place = string.Empty;
                }
            }
        }

        void FixCountryTypos()
        {
            COUNTRY_TYPOS.TryGetValue(Country, out string result);
            if (!string.IsNullOrEmpty(result))
                Country = result;
            else
            {
                string fixCase = EnhancedTextInfo.ToTitleCase(Country.ToLower());
                COUNTRY_TYPOS.TryGetValue(fixCase, out result);
                if (!string.IsNullOrEmpty(result))
                    Country = result;
            }
        }

        string FixRegionTypos(string toFix)
        {
            if (Country == Countries.AUSTRALIA && toFix.Equals("WA"))
                return "Western Australia"; // fix for WA = Washington
            REGION_TYPOS.TryGetValue(toFix, out string result);
            if (!string.IsNullOrEmpty(result))
                return result;
            string fixCase = EnhancedTextInfo.ToTitleCase(toFix.ToLower());
            REGION_TYPOS.TryGetValue(fixCase, out result);
            return !string.IsNullOrEmpty(result) ? result : toFix;
        }

        void ShiftCountryToRegion()
        {
            COUNTRY_SHIFTS.TryGetValue(Country, out string result);
            if (string.IsNullOrEmpty(result))
            {
                string fixCase = EnhancedTextInfo.ToTitleCase(Country.ToLower());
                COUNTRY_SHIFTS.TryGetValue(fixCase, out result);
            }
            if (!string.IsNullOrEmpty(result))
            {
                Place = (Place + " " + Address).Trim();
                Address = SubRegion;
                SubRegion = Region;
                Region = Country;
                Country = result;
                if (Level < PLACE) Level++; // we have moved up a level
            }
        }

        void ShiftRegionToParish()
        {
            if (!Countries.IsUnitedKingdom(Country))
                return; // don't shift regions if not UK
            REGION_SHIFTS.TryGetValue(Region, out string result);
            if (string.IsNullOrEmpty(result))
            {
                string fixCase = EnhancedTextInfo.ToTitleCase(Region.ToLower());
                REGION_TYPOS.TryGetValue(fixCase, out result);
            }
            if (!string.IsNullOrEmpty(result))
            {
                Place = (Place + " " + Address).Trim();
                Address = SubRegion;
                SubRegion = Region;
                Region = result;
                if (Level < PLACE) Level++; // we have moved up a level
            }
        }

        void FixDoubleLocations()
        {
            if (Country.Equals(Region))
            {
                Region = SubRegion;
                SubRegion = Address;
                Address = Place;
                Place = string.Empty;
            }
            if (Region.Equals(SubRegion))
            {
                SubRegion = Address;
                Address = Place;
                Place = string.Empty;
            }
        }

        void SetFixedLocation()
        {
            FixedLocation = Country;
            if (Region.Length > 0 || GeneralSettings.Default.AllowEmptyLocations)
                FixedLocation = Region + ", " + FixedLocation;
            if (SubRegion.Length > 0 || GeneralSettings.Default.AllowEmptyLocations)
                FixedLocation = SubRegion + ", " + FixedLocation;
            if (Address.Length > 0 || GeneralSettings.Default.AllowEmptyLocations)
                FixedLocation = Address + ", " + FixedLocation;
            if (Place.Length > 0)
                FixedLocation = Place + ", " + FixedLocation;
            FixedLocation = TrimLeadingCommas(FixedLocation);
        }

        void SetSortableLocation()
        {
            SortableLocation = Country;
            if (Region.Length > 0 || GeneralSettings.Default.AllowEmptyLocations)
                SortableLocation = $"{SortableLocation}, {Region}";
            if (SubRegion.Length > 0 || GeneralSettings.Default.AllowEmptyLocations)
                SortableLocation = $"{SortableLocation}, {SubRegion}";
            if (Address.Length > 0 || GeneralSettings.Default.AllowEmptyLocations)
                SortableLocation = $"{SortableLocation}, {Address}";
            if (Place.Length > 0)
                SortableLocation = $"{SortableLocation}, {Place}";
            SortableLocation = TrimLeadingCommas(SortableLocation);
        }

        void SetMetaphones()
        {
            DoubleMetaphone meta = new DoubleMetaphone(Country);
            CountryMetaphone = meta.PrimaryKey;
            meta = new DoubleMetaphone(Region);
            RegionMetaphone = meta.PrimaryKey;
            meta = new DoubleMetaphone(SubRegion);
            SubRegionMetaphone = meta.PrimaryKey;
            meta = new DoubleMetaphone(Address);
            AddressMetaphone = meta.PrimaryKey;
            meta = new DoubleMetaphone(Place);
            PlaceMetaphone = meta.PrimaryKey;
            FuzzyMatch = $"{AddressMetaphone}:{SubRegionMetaphone}:{RegionMetaphone}:{CountryMetaphone}";
            FuzzyNoParishMatch = $"{AddressMetaphone}:{RegionMetaphone}:{CountryMetaphone}";
        }

        public static string ReplaceString(string str, string oldValue, string newValue, StringComparison comparison)
        {
            StringBuilder sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, comparison);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

        public string GoogleFixed
        {
            get
            {
                // first check the multifixes
                string result = FixedLocation;
                foreach (KeyValuePair<Tuple<int, string>, string> fix in LOCAL_GOOGLE_FIXES)
                {
                    if (fix.Key.Item1 == UNKNOWN)
                        result = ReplaceString(result, fix.Key.Item2, fix.Value, StringComparison.OrdinalIgnoreCase);
                }
                if (result != FixedLocation)
                    return result;

                foreach (KeyValuePair<Tuple<int, string>, string> fix in GOOGLE_FIXES)
                {
                    if (fix.Key.Item1 == UNKNOWN)
                        result = ReplaceString(result, fix.Key.Item2, fix.Value, StringComparison.OrdinalIgnoreCase);
                }
                if (result != FixedLocation)
                    return result;

                // now check the individual part fixes
                LOCAL_GOOGLE_FIXES.TryGetValue(new Tuple<int, string>(COUNTRY, Country.ToUpperInvariant()), out string countryFix);
                if (countryFix is null)
                {
                    GOOGLE_FIXES.TryGetValue(new Tuple<int, string>(COUNTRY, Country.ToUpperInvariant()), out countryFix);
                    if (countryFix is null)
                        countryFix = Country;
                }
                LOCAL_GOOGLE_FIXES.TryGetValue(new Tuple<int, string>(REGION, Region.ToUpperInvariant()), out string regionFix);
                if (regionFix is null)
                {
                    GOOGLE_FIXES.TryGetValue(new Tuple<int, string>(REGION, Region.ToUpperInvariant()), out regionFix);
                    if (regionFix is null)
                        regionFix = Region;
                }
                LOCAL_GOOGLE_FIXES.TryGetValue(new Tuple<int, string>(SUBREGION, SubRegion.ToUpperInvariant()), out string subRegionFix);
                if (subRegionFix is null)
                {
                    GOOGLE_FIXES.TryGetValue(new Tuple<int, string>(SUBREGION, SubRegion.ToUpperInvariant()), out subRegionFix);
                    if (subRegionFix is null)
                        subRegionFix = SubRegion;
                }
                result = countryFix;
                if (!string.IsNullOrEmpty(regionFix) || GeneralSettings.Default.AllowEmptyLocations)
                    result = regionFix + ", " + result;
                if (!string.IsNullOrEmpty(subRegionFix) || GeneralSettings.Default.AllowEmptyLocations)
                    result = subRegionFix + ", " + result;
                if (!string.IsNullOrEmpty(Address) || GeneralSettings.Default.AllowEmptyLocations)
                    result = Address + ", " + result;
                if (!string.IsNullOrEmpty(Place))
                    result = Place + ", " + result;
                return TrimLeadingCommas(result);
            }
        }

        string TrimLeadingCommas(string toChange)
        {
            while (toChange.StartsWith(", ", StringComparison.Ordinal))
                toChange = toChange.Substring(2);
            return toChange.Trim();
        }

        #endregion
        #region Properties

        public string[] GetParts() => (string[])_Parts.Clone();

        public string AddressNumeric => FixNumerics(Address, true);

        public string PlaceNumeric => FixNumerics(Place, true);

        public bool IsKnownCountry => Countries.IsKnownCountry(Country);

        public bool IsUnitedKingdom => Countries.IsUnitedKingdom(Country);

        public bool IsEnglandWales => Countries.IsEnglandWales(Country);

        public static int LocationsCount => AllLocations.Count() - 1;

        public static int GEDCOMLocationsCount => AllLocations.Count(l => !l.FTAnalyzerCreated);

        public string FreeCenCountyCode
        {
            get
            {
                FREECEN_LOOKUP.TryGetValue(Region, out string result);
                if (result is null)
                    result = "all";
                return result;
            }
        }

        public Tuple<string, string> FindMyPastCountyCode
        {
            get
            {
                FINDMYPAST_LOOKUP.TryGetValue(Region, out Tuple<string, string> result);
                return result;
            }
        }

        public bool IsBlank => Country.Length == 0;
        public bool IsKnown => this != BLANK_LOCATION && this != UNKNOWN_LOCATION;

        public string FixedLocation { get; set; }
        public string Address1 { get; set; }
        public string Place1 { get; set; }
        #endregion

        #region General Functions
        public void AddIndividual(Individual ind)
        {
            if (ind != null && !individuals.ContainsKey(ind.IndividualID))
                individuals.Add(ind.IndividualID, ind);
        }

        public IList<string> Surnames
        {
            get
            {
                HashSet<string> names = new HashSet<string>();
                foreach (Individual i in individuals.Values)
                    names.Add(i.Surname);
                List<string> result = names.ToList();
                result.Sort();
                return result;
            }
        }

        static readonly Regex numericFix = new Regex("\\d+[A-Za-z]?", RegexOptions.Compiled);

        string FixNumerics(string addressField, bool returnNumber)
        {
            int pos = addressField.IndexOf(" ", StringComparison.Ordinal);
            if (pos > 0 & pos < addressField.Length)
            {
                string number = addressField.Substring(0, pos);
                string name = addressField.Substring(pos + 1);
                Match matcher = numericFix.Match(number);
                if (matcher.Success)
                    return returnNumber ? name + " - " + number : name;
            }
            return addressField;
        }
        #endregion

        public bool IsWithinUKBounds => Longitude >= -7.974074 && Longitude <= 1.879409 && Latitude >= 49.814376 && Latitude <= 60.970872;

        //public string OSGridMapReference
        //{
        //    get
        //    {
        //        if (IsWithinUKBounds)
        //        {
        //            //var latLong = new LatitudeLongitude(Latitude, Longitude);

        //            //var cartesian = GeoUK.Convert.ToCartesian(new Wgs84(), latLong);
        //            //var bngCartesian = Transform.Etrs89ToOsgb36(cartesian);
        //            //var bngEN = GeoUK.Convert.ToEastingNorthing(new Airy1830(), new BritishNationalGrid(), bngCartesian);

        //            //// Convert to Osgb36 coordinates by creating a new object passing  
        //            //// in the EastingNorthing object to the constructor.
        //            //var osgb36EN = new Osgb36(bngEN);
        //            //return osgb36EN.MapReference;
        //        }
        //        return string.Empty;
        //    }
        //}

        #region Overrides

        public int CompareTo(object that) => CompareTo(that as FactLocation);

        public int CompareTo(FactLocation that) => CompareTo(that, PLACE);

        public virtual int CompareTo(FactLocation that, int level)
        {
            int res = string.Compare(Country, that.Country, StringComparison.Ordinal);
            if (res == 0 && level > COUNTRY)
            {
                res = string.Compare(Region, that.Region, StringComparison.Ordinal);
                if (res == 0 && level > REGION)
                {
                    res = string.Compare(SubRegion, that.SubRegion, StringComparison.Ordinal);
                    if (res == 0 && level > SUBREGION)
                    {
                        res = string.Compare(AddressNumeric, that.AddressNumeric, StringComparison.Ordinal);
                        if (res == 0 && level > ADDRESS)
                        {
                            res = string.Compare(PlaceNumeric, that.PlaceNumeric, StringComparison.Ordinal);
                        }
                    }
                }
            }
            return res;
        }

        public override string ToString() => GeneralSettings.Default.SkipFixingLocations ? OriginalText : FixedLocation; //return location;

        public override bool Equals(object obj)
        {
            return obj is FactLocation location && CompareTo(location) == 0;
        }

        public static bool operator ==(FactLocation a, FactLocation b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            // If one is null, but not both, return false.
            if ((a is null) || (b is null))
                return false;
            return a.Equals(b);
        }

        public static bool operator !=(FactLocation a, FactLocation b) => !(a == b);

        public bool Equals(FactLocation that, int level) => CompareTo(that, level) == 0;

        public override int GetHashCode() => base.GetHashCode();

        #endregion
    }
}