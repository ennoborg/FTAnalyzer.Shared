using FTAnalyzer.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FTAnalyzer
{
    public class Fact
    {
        public const string ADOPTION = "ADOP", ADULT_CHRISTENING = "CHRA", AFN = "AFN", ALIAS = "ALIA", ANNULMENT = "ANUL",
                BAPTISM = "BAPM", BAPTISM_LDS = "BAPL", BAR_MITZVAH = "BARM", BAS_MITZVAH = "BASM", BIRTH = "BIRT",
                BIRTH_CALC = "_BIRTHCALC", BLESSING = "BLES", BURIAL = "BURI", CASTE = "CAST", CAUSE_OF_DEATH = "CAUS",
                CHANGE = "CHAN", CHILDREN1911 = "CHILDREN1911", CHRISTENING = "CHR",
                CIRCUMCISION = "_CIRC", CONFIRMATION = "CONF", CONFIRMATION_LDS = "CONL", CREMATION = "CREM",
                CUSTOM_ATTRIBUTE = "_ATTR", CUSTOM_EVENT = "EVEN", CUSTOM_FACT = "FACT", DEATH = "DEAT", DEGREE = "_DEG", 
                DESTINATION = "_DEST", DIVORCE = "DIV", DIVORCE_FILED = "DIVF", DNA = "_DNA", EDUCATION = "EDUC", ELECTION = "_ELEC",
                EMAIL = "EMAIL", EMIGRATION = "EMIG", EMPLOYMENT = "_EMPLOY", ENDOWMENT_LDS = "ENDL", ENGAGEMENT = "ENGA",
                EXCOMMUNICATION = "_EXCM", FIRST_COMMUNION = "FCOM", FUNERAL = "_FUN", GENDER = "SEX", GRADUATION = "GRAD",
                HEIGHT = "_HEIG", IMMIGRATION = "IMMI", INITIATORY_LDS = "_INIT", INDI = "INDI", LEGATEE = "LEGA", MARRIAGE = "MARR",
                MARRIAGE_BANN = "MARB", MARR_CONTRACT = "MARC", MARR_LICENSE = "MARL", MARR_SETTLEMENT = "MARS",
                MEDICAL_CONDITION = "_MDCL", MILITARY = "_MILT", MISSION_LDS = "_MISN", NAME = "NAME",
                NAMESAKE = "_NAMS", NATIONALITY = "NATI", NATURALIZATION = "NATU", NAT_ID_NO = "IDNO", NUM_CHILDREN = "NCHI",
                NUM_MARRIAGE = "NMR", OCCUPATION = "OCCU", ORDINATION = "ORDN", ORDINANCE = "_ORDI", ORIGIN = "_ORIG",
                PHONE = "PHON", PHYSICAL_DESC = "DSCR", PROBATE = "PROB", PROPERTY = "PROP", REFERENCE = "REFN",
                RELIGION = "RELI", RESIDENCE = "RESI", RETIREMENT = "RETI", SEALED_TO_PARENTS = "SLGC",
                SEALED_TO_SPOUSE = "SLGS", SEPARATION = "_SEPR", SERVICE_NUMBER = "_MILTID", SOCIAL_SECURITY = "SSN", TITLE = "TITL",
                UNKNOWN = "UNKN", WEIGHT = "_WEIG", WILL = "WILL", HASHTAG = "_HASHTAG", OBITUARY = "OBIT";

        public const string ARRIVAL = "*ARRI", CHILDLESS = "*CHILD", CHILDREN = "*CHILDREN", CONTACT = "*CONT", DEPARTURE = "*DEPT",
                FAMILYSEARCH = "*IGI", FAMILYSEARCH_ID = "FSID", LC_FTA = "*LOST_FTA", LOOSEBIRTH = "*LOOSEB", RACE = "RACE",
                LOOSEDEATH = "*LOOSED", LOSTCOUSINS = "*LOST", MISSING = "*MISSING", PARENT = "*PARENT", REPORT = "*REPORT",
                UNMARRIED = "*UNMAR", WEBSITE = "*WEBSITE", WITNESS = "*WITNE", WORLD_EVENT = "*WORLD_EVENT";

        public const string ANCESTRY_DEATH_CAUSE = "_DCAUSE";

        public static ISet<string> LOOSE_BIRTH_FACTS = new HashSet<string>(new string[] {
            CHRISTENING, BAPTISM, RESIDENCE, WITNESS, EMIGRATION, IMMIGRATION, ARRIVAL, DEPARTURE, 
            EDUCATION, DEGREE, ADOPTION, BAR_MITZVAH, BAS_MITZVAH, ADULT_CHRISTENING, CONFIRMATION, 
            FIRST_COMMUNION, ORDINATION, NATURALIZATION, GRADUATION, RETIREMENT, LOSTCOUSINS, 
            LC_FTA, MARR_CONTRACT, MARR_LICENSE, MARR_SETTLEMENT, MARRIAGE, MARRIAGE_BANN, DEATH, 
            CREMATION, BURIAL, BIRTH_CALC
                    });

        public static ISet<string> LOOSE_DEATH_FACTS = new HashSet<string>(new string[] {
            RESIDENCE, WITNESS, EMIGRATION, IMMIGRATION, ARRIVAL, DEPARTURE, EDUCATION,
            DEGREE, ADOPTION, BAR_MITZVAH, BAS_MITZVAH, ADULT_CHRISTENING, CONFIRMATION, FIRST_COMMUNION,
            ORDINATION, NATURALIZATION, GRADUATION, RETIREMENT, LOSTCOUSINS, LC_FTA
                    });

        public static ISet<string> RANGED_DATE_FACTS = new HashSet<string>(new string[] {
            EDUCATION, OCCUPATION, RESIDENCE, RETIREMENT, MILITARY, ELECTION, DEGREE, EMPLOYMENT, MEDICAL_CONDITION
                    });

        public static ISet<string> IGNORE_LONG_RANGE = new HashSet<string>(new string[] {
            MARRIAGE, CHILDREN
                    });

        public static ISet<string> CREATED_FACTS = new HashSet<string>(new string[] {
            CHILDREN, PARENT, BIRTH_CALC, LC_FTA
                    });

        public static readonly Dictionary<string, string> NON_STANDARD_FACTS = new Dictionary<string,string>();
        static readonly Dictionary<string, string> CUSTOM_TAGS = new Dictionary<string, string>();
        static readonly HashSet<string> COMMENT_FACTS = new HashSet<string>();

        static Fact()
        {
            // special tags
            CUSTOM_TAGS.Add("IGI SEARCH", FAMILYSEARCH);
            CUSTOM_TAGS.Add("CHILDLESS", CHILDLESS);
            CUSTOM_TAGS.Add("CONTACT", CONTACT);
            CUSTOM_TAGS.Add("WITNESS", WITNESS);
            CUSTOM_TAGS.Add("WITNESSES", WITNESS);
            CUSTOM_TAGS.Add("WITN: WITNESS", WITNESS);
            CUSTOM_TAGS.Add("UNMARRIED", UNMARRIED);
            CUSTOM_TAGS.Add("FRIENDS", UNMARRIED);
            CUSTOM_TAGS.Add("PARTNERS", UNMARRIED);
            CUSTOM_TAGS.Add("UNKNOWN", UNKNOWN);
            CUSTOM_TAGS.Add("UNKNOWN-BEGIN", UNKNOWN);
            CUSTOM_TAGS.Add("ARRIVAL", ARRIVAL);
            CUSTOM_TAGS.Add("DEPARTURE", DEPARTURE);
            CUSTOM_TAGS.Add("RECORD CHANGE", CHANGE);
            CUSTOM_TAGS.Add("*CHNG", CHANGE);
            CUSTOM_TAGS.Add("LOST COUSINS", LOSTCOUSINS);
            CUSTOM_TAGS.Add("LOSTCOUSINS", LOSTCOUSINS);
            CUSTOM_TAGS.Add("DIED SINGLE", UNMARRIED);
            CUSTOM_TAGS.Add("MISSING", MISSING);
            CUSTOM_TAGS.Add("CHILDREN STATUS", CHILDREN1911);
            CUSTOM_TAGS.Add("CHILDREN1911", CHILDREN1911);
            CUSTOM_TAGS.Add("WEBSITE", WEBSITE);
            CUSTOM_TAGS.Add("_TAG1", HASHTAG);
            CUSTOM_TAGS.Add("_TAG2", HASHTAG);
            CUSTOM_TAGS.Add("_TAG3", HASHTAG);
            CUSTOM_TAGS.Add("_TAG4", HASHTAG);
            CUSTOM_TAGS.Add("_TAG5", HASHTAG);
            CUSTOM_TAGS.Add("_TAG6", HASHTAG);
            CUSTOM_TAGS.Add("_TAG7", HASHTAG);
            CUSTOM_TAGS.Add("_TAG8", HASHTAG);
            CUSTOM_TAGS.Add("_TAG9", HASHTAG);

            // convert custom tags to normal tags
            CUSTOM_TAGS.Add("BAPTISED", BAPTISM);
            CUSTOM_TAGS.Add("BIRTH REG", BIRTH);
            CUSTOM_TAGS.Add("BIRTH", BIRTH);
            CUSTOM_TAGS.Add("ALTERNATE BIRTH", BIRTH);
            CUSTOM_TAGS.Add("ALTERNATIVE BIRTH", BIRTH);
            CUSTOM_TAGS.Add("BIRTH CERTIFICATE", BIRTH);
            CUSTOM_TAGS.Add("BIRTH CERT", BIRTH);
            CUSTOM_TAGS.Add("MARRIAGE REG", MARRIAGE);
            CUSTOM_TAGS.Add("MARRIAGE", MARRIAGE);
            CUSTOM_TAGS.Add("MARRIAGE CERTIFICATE", MARRIAGE);
            CUSTOM_TAGS.Add("MARRIAGE CERT", MARRIAGE);
            CUSTOM_TAGS.Add("ALTERNATE MARRIAGE", BIRTH);
            CUSTOM_TAGS.Add("ALTERNATIVE MARRIAGE", BIRTH);
            CUSTOM_TAGS.Add("SAME SEX MARRIAGE", MARRIAGE);
            CUSTOM_TAGS.Add("CIVIL", MARRIAGE);
            CUSTOM_TAGS.Add("CIVIL PARTNER", MARRIAGE);
            CUSTOM_TAGS.Add("CIVIL PARTNERSHIP", MARRIAGE);
            CUSTOM_TAGS.Add("DEATH REG", DEATH);
            CUSTOM_TAGS.Add("DEATH", DEATH);
            CUSTOM_TAGS.Add("ALTERNATE DEATH", DEATH);
            CUSTOM_TAGS.Add("ALTERNATIVE DEATH", DEATH);
            CUSTOM_TAGS.Add("DEATH CERTIFICATE", DEATH);
            CUSTOM_TAGS.Add("DEATH CERT", DEATH);
            CUSTOM_TAGS.Add("DEATH NOTICE", DEATH);
            CUSTOM_TAGS.Add("DEATH NOTICE REF", DEATH);
            CUSTOM_TAGS.Add("CHRISTENING", CHRISTENING);
            CUSTOM_TAGS.Add("CHRISTENED", CHRISTENING);
            CUSTOM_TAGS.Add("BURIAL", BURIAL);
            CUSTOM_TAGS.Add("FUNERAL", BURIAL);
            CUSTOM_TAGS.Add("CREMATION", CREMATION);
            CUSTOM_TAGS.Add("CREMATED", CREMATION);
            CUSTOM_TAGS.Add("PROBATE", PROBATE);
            CUSTOM_TAGS.Add("GRANT OF PROBATE", PROBATE);
            CUSTOM_TAGS.Add("PROBATE DATE", PROBATE);
            CUSTOM_TAGS.Add("RESIDENCE", RESIDENCE);
            CUSTOM_TAGS.Add("DIVORCED", DIVORCE);
            CUSTOM_TAGS.Add("OCCUPATION", OCCUPATION);
            CUSTOM_TAGS.Add("NATURALIZATION", NATURALIZATION);
            CUSTOM_TAGS.Add("NATURALISATION", NATURALIZATION);
            CUSTOM_TAGS.Add("CURRENT RESIDENCE", RESIDENCE);
            CUSTOM_TAGS.Add("HIGH SCHOOL", EDUCATION);
            CUSTOM_TAGS.Add("COLLEGE", EDUCATION);
            CUSTOM_TAGS.Add("TERTIARY EDUCATION", EDUCATION);
            CUSTOM_TAGS.Add("UNIVERSITY", EDUCATION);
            CUSTOM_TAGS.Add("DIPLOMA", EDUCATION);
            CUSTOM_TAGS.Add("SCHL: SCHOOL ATTENDANCE", EDUCATION);
            CUSTOM_TAGS.Add("EMPL: EMPLOYMENT", EMPLOYMENT);
            CUSTOM_TAGS.Add("MARL: MARRIAGE LICENCE", MARR_LICENSE);
            CUSTOM_TAGS.Add("FUNL: FUNERAL", FUNERAL);
            CUSTOM_TAGS.Add("CAUSE OF DEATH (FACTS PAGE)", CAUSE_OF_DEATH);
            CUSTOM_TAGS.Add("LTOG: LIVED TOGETHER (UNMARRIED)", UNMARRIED);
            CUSTOM_TAGS.Add("ILLNESS", MEDICAL_CONDITION);
            
            // Legacy 8 default fact types
            CUSTOM_TAGS.Add("ALT. BIRTH", BIRTH);
            CUSTOM_TAGS.Add("ALT. CHRISTENING", CHRISTENING);
            CUSTOM_TAGS.Add("ALT. DEATH", DEATH);
            CUSTOM_TAGS.Add("ALT. BURIAL", BURIAL);
            CUSTOM_TAGS.Add("ALT. MARRIAGE", MARRIAGE);
            CUSTOM_TAGS.Add("DIVORCE FILING", DIVORCE_FILED);
            CUSTOM_TAGS.Add("DEGREE", DEGREE);
            CUSTOM_TAGS.Add("ELECTION", ELECTION);
            CUSTOM_TAGS.Add("EMPLOYMENT", EMPLOYMENT);
            CUSTOM_TAGS.Add("MARRIAGE LICENCE", MARR_LICENSE);
            CUSTOM_TAGS.Add("MARRIAGE LICENSE", MARR_LICENSE);
            CUSTOM_TAGS.Add("MARRIAGE CONTRACT", MARR_CONTRACT);
            CUSTOM_TAGS.Add("MEDICAL", MEDICAL_CONDITION);
            CUSTOM_TAGS.Add("MILITARY", MILITARY);
            CUSTOM_TAGS.Add("MILITARY SERVICE", MILITARY);
            CUSTOM_TAGS.Add("MILITARY ENLISTMENT", MILITARY);
            CUSTOM_TAGS.Add("MILITARY DISCHARGE", MILITARY);
            CUSTOM_TAGS.Add("MILITARY AWARD", MILITARY);
            CUSTOM_TAGS.Add("PROPERTY", PROPERTY);

            // Convert non standard fact types to standard ones
            NON_STANDARD_FACTS.Add(ANCESTRY_DEATH_CAUSE, CAUSE_OF_DEATH);

            // Create list of Comment facts treat text as comment rather than location
            COMMENT_FACTS.Add(AFN);
            COMMENT_FACTS.Add(ALIAS);
            COMMENT_FACTS.Add(CASTE);
            COMMENT_FACTS.Add(CAUSE_OF_DEATH);
            COMMENT_FACTS.Add(CHILDLESS);
            COMMENT_FACTS.Add(CHILDREN);
            COMMENT_FACTS.Add(CHILDREN1911);
            COMMENT_FACTS.Add(DESTINATION);
            COMMENT_FACTS.Add(FAMILYSEARCH);
            COMMENT_FACTS.Add(HASHTAG);
            COMMENT_FACTS.Add(HEIGHT);
            COMMENT_FACTS.Add(MISSING);
            COMMENT_FACTS.Add(NAME);
            COMMENT_FACTS.Add(NAMESAKE);
            COMMENT_FACTS.Add(NATIONALITY);
            COMMENT_FACTS.Add(OBITUARY);
            COMMENT_FACTS.Add(PARENT);
            COMMENT_FACTS.Add(RACE);
            COMMENT_FACTS.Add(REFERENCE);
            COMMENT_FACTS.Add(RELIGION);
            COMMENT_FACTS.Add(SOCIAL_SECURITY);
            COMMENT_FACTS.Add(TITLE);
            COMMENT_FACTS.Add(UNKNOWN);
            COMMENT_FACTS.Add(UNMARRIED);
            COMMENT_FACTS.Add(WEIGHT);
            COMMENT_FACTS.Add(WILL);
            COMMENT_FACTS.Add(WITNESS);
        }

        internal static string GetFactTypeDescription(string factType)
        {
            switch (factType)
            {
                case ADOPTION: return "Adoption";
                case ADULT_CHRISTENING: return "Adult christening";
                case AFN: return "Ancestral File Number";
                case ALIAS: return "Also known as";
                case ANNULMENT: return "Annulment";
                case ARRIVAL: return "Arrival";
                case BAPTISM: return "Baptism";
                case BAPTISM_LDS: return "Baptism (LDS)";
                case BAR_MITZVAH: return "Bar mitzvah";
                case BAS_MITZVAH: return "Bas mitzvah";
                case BIRTH: return "Birth";
                case BIRTH_CALC: return "Birth (Calc from Age)";
                case BLESSING: return "Blessing";
                case BURIAL: return "Burial";
                case CASTE: return "Caste";
                case CAUSE_OF_DEATH: return "Cause of Death";
                case CHANGE: return "Record change";
                case CHILDLESS: return "Childless";
                case CHILDREN1911: return "Children Status";
                case CHILDREN: return "Child Born";
                case CHRISTENING: return "Christening";
                case CIRCUMCISION: return "Circumcision";
                case CONFIRMATION: return "Confirmation";
                case CONFIRMATION_LDS: return "Confirmation (LDS)";
                case CONTACT: return "Contact";
                case CREMATION: return "Cremation";
                case CUSTOM_ATTRIBUTE: return "Custom Attribute";
                case CUSTOM_EVENT: return "Event";
                case CUSTOM_FACT: return "Custom Fact";
                case DEATH: return "Death";
                case DEGREE: return "Degree";
                case DEPARTURE: return "Departure";
                case DESTINATION: return "Destination";
                case DIVORCE: return "Divorce";
                case DIVORCE_FILED: return "Divorce filed";
                case DNA: return "DNA Markers";
                case EDUCATION: return "Education";
                case ELECTION: return "Election";
                case EMAIL: return "Email Address";
                case EMIGRATION: return "Emigration";
                case EMPLOYMENT: return "Employment";
                case ENDOWMENT_LDS: return "Endowment (LDS)";
                case ENGAGEMENT: return "Engagement";
                case EXCOMMUNICATION: return "Excommunication";
                case FAMILYSEARCH: return "FamilySearch";
                case FAMILYSEARCH_ID: return "FamilySearch ID";
                case FIRST_COMMUNION: return "First communion";
                case FUNERAL: return "Funeral";
                case GENDER: return "Gender";
                case GRADUATION: return "Graduation";
                case HASHTAG: return "Hashtag";
                case HEIGHT: return "Height";
                case IMMIGRATION: return "Immigration";
                case INDI: return "Name";
                case INITIATORY_LDS: return "Initiatory (LDS)";
                case LC_FTA: return "Lost Cousins (FTAnalyzer)";
                case LEGATEE: return "Legatee";
                case LOOSEBIRTH: return "Loose birth";
                case LOOSEDEATH: return "Loose death";
                case LOSTCOUSINS: return "Lost Cousins";
                case MARRIAGE: return "Marriage";
                case MARRIAGE_BANN: return "Marriage banns";
                case MARR_CONTRACT: return "Marriage contract";
                case MARR_LICENSE: return "Marriage license";
                case MARR_SETTLEMENT: return "Marriage settlement";
                case MEDICAL_CONDITION: return "Medical condition";
                case MILITARY: return "Military service";
                case MISSING: return "Missing";
                case MISSION_LDS: return "Mission (LDS)";
                case NAME: return "Alternate Name";
                case NAMESAKE: return "Namesake";
                case NATIONALITY: return "Nationality";
                case NATURALIZATION: return "Naturalization";
                case NAT_ID_NO: return "National identity no.";
                case NUM_CHILDREN: return "Number of children";
                case NUM_MARRIAGE: return "Number of marriages";
                case OBITUARY: return "Obituary";
                case OCCUPATION: return "Occupation";
                case ORDINATION: return "Ordination";
                case ORDINANCE: return "Ordinance";
                case PARENT: return "Parental Info";
                case PHONE: return "Phone";
                case PHYSICAL_DESC: return "Physical description";
                case PROBATE: return "Probate";
                case PROPERTY: return "Property";
                case RACE: return "Race";
                case REFERENCE: return "Reference ID";
                case RELIGION: return "Religion";
                case REPORT: return "Fact Report";
                case RESIDENCE: return "Residence";
                case RETIREMENT: return "Retirement";
                case SEALED_TO_PARENTS: return "Sealed to Parents (LDS)";
                case SEALED_TO_SPOUSE: return "Sealed to Spouse (LDS)";
                case SEPARATION: return "Separation";
                case SERVICE_NUMBER: return "Military service number";
                case SOCIAL_SECURITY: return "Social Security number";
                case TITLE: return "Title";
                case UNKNOWN: return "UNKNOWN";
                case UNMARRIED: return "Unmarried";
                case WEIGHT: return "Weight";
                case WILL: return "Will";
                case WITNESS: return "Witness";
                case WORLD_EVENT: return "World Event";
                case "": return "UNKNOWN";
                default: return EnhancedTextInfo.ToTitleCase(factType);
            }
        }

        public enum FactError { GOOD = 0, WARNINGALLOW = 1, WARNINGIGNORE = 2, ERROR = 3, QUESTIONABLE = 4, IGNORE = 5 };

        #region Constructors

        Fact(string reference, bool preferred)
        {
            FactType = string.Empty;
            FactDate = FactDate.UNKNOWN_DATE;
            Comment = string.Empty;
            Place = string.Empty;
            Location = FactLocation.BLANK_LOCATION;
            Sources = new List<FactSource>();
            CertificatePresent = false;
            FactErrorLevel = FactError.GOOD;
            FactErrorMessage = string.Empty;
            FactErrorNumber = 0;
            GedcomAge = null;
            Created = false;
            Tag = string.Empty;
            Preferred = preferred;
            Reference = reference;
            SourcePages = new List<string>();
        }

        const string CHILDREN_STATUS_PATTERN1 = @"(\d{1,2}) Total ?,? ?(\d{1,2}) (Alive|Living) ?,? ?(\d{1,2}) Dead";
        const string CHILDREN_STATUS_PATTERN2 = @"Total:? (\d{1,2}) ?,? ?(Alive|Living):? (\d{1,2}) ?,? ?Dead:? (\d{1,2})";
        public readonly static Regex regexChildren1 = new Regex(CHILDREN_STATUS_PATTERN1, RegexOptions.Compiled);
        public readonly static Regex regexChildren2 = new Regex(CHILDREN_STATUS_PATTERN2, RegexOptions.Compiled);

        public Fact(string factRef, string factType, FactDate date, FactLocation loc, string comment = "", bool preferred = true, bool createdByFTA = false, Individual ind = null)
            : this(factRef, preferred)
        {
            FactType = factType;
            FactDate = date ?? FactDate.UNKNOWN_DATE;
            Comment = comment;
            Created = createdByFTA;
            Place = string.Empty;
            Location = loc;
            Individual = ind;
        }

        #endregion

        #region Properties

        string Reference { get; set; }
        string Tag { get; set; }
        public Age GedcomAge { get; private set; }
        public bool Created { get; protected set; }
        public bool Preferred { get; private set; }
        public FactLocation Location { get; private set; }
        public string Place { get; private set; }
        public string Comment { get; set; }
        public FactDate FactDate { get; private set; }
        public string FactType { get; private set; }
        public int FactErrorNumber { get; private set; }
        public FactError FactErrorLevel { get; private set; }
        public string FactErrorMessage { get; private set; }
        public Individual Individual { get; private set; }
        public Family Family { get; private set; }
        public string FactTypeDescription => (FactType == UNKNOWN && Tag.Length > 0) ? Tag : GetFactTypeDescription(FactType);

        public bool IsMarriageFact =>  
            FactType == MARR_CONTRACT || FactType == MARR_LICENSE || 
            FactType == MARR_SETTLEMENT || FactType == MARRIAGE || FactType == MARRIAGE_BANN;

        public static bool IsAlwaysLoadFact(string factType)
        {
            if (factType == LOSTCOUSINS || factType == LC_FTA || factType == CUSTOM_EVENT || factType == CUSTOM_FACT) return true;
            return false;
        }

        public string DateString
        {
            get { return FactDate is null ? string.Empty : FactDate.DateString; }
        }

        public string SourceList
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (FactSource s in Sources.OrderBy(s => s.ToString()))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(s.ToString());
                }
                return sb.ToString();
            }
        }

        public IList<FactSource> Sources { get; private set; }

        public int SourcesCount => Sources.Count;

        public List<string> SourcePages { get; private set; }

        public string Country => Location is null ? "UNKNOWN" : Location.Country;

        public bool CertificatePresent { get; private set; }

        #endregion

        public static string ReverseLocation(string location) => string.Join(",", location.Split(',').Reverse());

        public void ChangeNonStandardFactType(string factType) => FactType = factType;

        public void SetError(int number, FactError level, string message)
        {
            FactErrorNumber = number;
            FactErrorLevel = level;
            FactErrorMessage = message;
        }

        public void UpdateFactDate(FactDate date)
        {
            if (FactDate.IsUnknown && date != null && date.IsKnown)
                FactDate = date;
        }

        string UnknownFactHash => FactType == UNKNOWN ? Tag : string.Empty;

        string FamilyFactHash => Family is null ? string.Empty : Family.FamilyID;
        
        public string PossiblyEqualHash => FactType + UnknownFactHash + FamilyFactHash +  FactDate + IsMarriageFact;

        public string EqualHash => FactType + UnknownFactHash + FamilyFactHash + FactDate + Location + Comment + IsMarriageFact;

        public override string ToString() => 
            FactTypeDescription + ": " + FactDate + (Location.ToString().Length > 0 ? " at " + Location : string.Empty) + (Comment.Length > 0 ? "  (" + Comment + ")" : string.Empty);
    }
}
