using FTAnalyzer.Exports;
using FTAnalyzer.Properties;
using FTAnalyzer.Utilities;
using GeneGenie.Gedcom;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using static FTAnalyzer.ColourValues;

namespace FTAnalyzer
{
    public class Individual : IComparable<Individual>,
        IDisplayIndividual, IDisplayLooseDeath, IDisplayLooseBirth,
        IDisplayColourBMD, IDisplayMissingData, IDisplayLooseInfo,
        IJsonIndividual
    {
        // edefine relation type from direct ancestor to related by marriage and 
        // MARRIAGEDB ie: married to a direct or blood relation
        public const int UNKNOWN = 1, DIRECT = 2, DESCENDANT = 4, BLOOD = 8, MARRIEDTODB = 16, MARRIAGE = 32, LINKED = 64, UNSET = 128;
        public const string UNKNOWN_NAME = "UNKNOWN";

        string _forenames;
        string _fullname;
        int _relationType;
        List<Fact> _allfacts;
        List<Fact> _allFileFacts;
        readonly DoubleMetaphone surnameMetaphone;
        readonly DoubleMetaphone forenameMetaphone;
        readonly Dictionary<string, Fact> preferredFacts;
        public string Notes { get; private set; }
        public string StandardisedName { get; private set; }
        public bool HasParents { get; set; }
        public bool HasOnlyOneParent { get; set; }
        public bool Infamily { get; set; }
        public bool IsFlaggedAsLiving { get; private set; }
        public BigInteger Ahnentafel { get; set; }
        public string BudgieCode { get; set; }
        public string RelationToRoot { get; set; }
        public string Title { get; private set; }
        public string Suffix { get; private set; }
        public string FamilySearchID { get; private set; }
        public decimal RelationSort { get; set; }
        public CommonAncestor CommonAncestor { get; set; }
        public IList<Fact> Facts { get; private set; }
        public string Alias { get; set; }
        public IList<FactLocation> Locations { get; }

        private readonly GedcomIndividualRecord _indi;

        #region Constructors
        Individual()
        {
            _indi = null;
            _forenames = string.Empty;
            Surname = string.Empty;
            forenameMetaphone = new DoubleMetaphone();
            surnameMetaphone = new DoubleMetaphone();
            MarriedName = string.Empty;
            StandardisedName = string.Empty;
            FamilySearchID = string.Empty;
            IsFlaggedAsLiving = false;
            Alias = string.Empty;
            Title = string.Empty;
            Suffix = string.Empty;
            Ahnentafel = 0;
            BudgieCode = string.Empty;
            _relationType = UNSET;
            RelationToRoot = string.Empty;
            CommonAncestor = null;
            Infamily = false;
            Notes = string.Empty;
            HasParents = false;
            HasOnlyOneParent = false;
            ReferralFamilyID = string.Empty;
            Facts = new List<Fact>();
            ErrorFacts = new List<Fact>();
            Locations = new List<FactLocation>();
            FamiliesAsChild = new List<ParentalRelationship>();
            FamiliesAsSpouse = new List<Family>();
            preferredFacts = new Dictionary<string, Fact>();
            _allfacts = null;
            _allFileFacts = null;
        }

        public Individual(GedcomIndividualRecord node, IProgress<string> outputText)
            : this()
        {
            _indi = node;
            Name = node.Names[0].Name;
            forenameMetaphone = new DoubleMetaphone(Forename);
            surnameMetaphone = new DoubleMetaphone(Surname);
            Notes = _indi.Notes.ToString();
            StandardisedName = FamilyTree.Instance.GetStandardisedName(IsMale, Forename);
            Fact nameFact = new Fact(IndividualID, Fact.INDI, FactDate.UNKNOWN_DATE, FactLocation.BLANK_LOCATION, Name, true, true, this);
            AddFact(nameFact);

            var events = node.Events;

            foreach (var e in events)
            {
                FactDate factDate;
                FactLocation factLocation;

                try
                {
                    factDate = new FactDate(e.Date?.DateString);
                }
                catch
                {
                    factDate = FactDate.UNKNOWN_DATE;
                }
                try
                {
                    factLocation = new FactLocation(e.Place?.Name);
                }
                catch
                {
                    factLocation = FactLocation.BLANK_LOCATION;
                }

                AddFact(new Fact(IndividualID, e.GedcomTag, factDate, factLocation));
            }
        }

        internal Individual(Individual i)
        {
            if (i != null)
            {
                IndividualID = i.IndividualID;
                _forenames = i._forenames;
                Surname = i.Surname;
                forenameMetaphone = i.forenameMetaphone;
                surnameMetaphone = i.surnameMetaphone;
                MarriedName = i.MarriedName;
                StandardisedName = i.StandardisedName;
                _fullname = i._fullname;
                SortedName = i.SortedName;
                IsFlaggedAsLiving = i.IsFlaggedAsLiving;
                Alias = i.Alias;
                Ahnentafel = i.Ahnentafel;
                BudgieCode = i.BudgieCode;
                _relationType = i._relationType;
                RelationToRoot = i.RelationToRoot;
                FamilySearchID = i.FamilySearchID;
                Infamily = i.Infamily;
                Notes = i.Notes;
                HasParents = i.HasParents;
                HasOnlyOneParent = i.HasOnlyOneParent;
                ReferralFamilyID = i.ReferralFamilyID;
                CommonAncestor = i.CommonAncestor;
                Facts = new List<Fact>(i.Facts);
                ErrorFacts = new List<Fact>(i.ErrorFacts);
                Locations = new List<FactLocation>(i.Locations);
                FamiliesAsChild = new List<ParentalRelationship>(i.FamiliesAsChild);
                FamiliesAsSpouse = new List<Family>(i.FamiliesAsSpouse);
                preferredFacts = new Dictionary<string, Fact>(i.preferredFacts);
            }
        }
        #endregion

        #region Properties

        public bool HasRangedBirthDate => BirthDate.DateType == FactDate.FactDateType.BET && BirthDate.StartDate.Year != BirthDate.EndDate.Year;

        public bool HasLostCousinsFact
        {
            get
            {
                foreach (Fact f in AllFacts)
                    if (f.FactType == Fact.LOSTCOUSINS || f.FactType == Fact.LC_FTA)
                        return true;
                return false;
            }
        }

        public int RelationType
        {
            get => _relationType;
            set
            {
                if (_relationType == UNKNOWN || _relationType > value)
                    _relationType = value;
            }
        }

        public bool IsBloodDirect => _relationType == BLOOD || _relationType == DIRECT || _relationType == DESCENDANT || _relationType == MARRIEDTODB;

        public bool HasNotes => Notes.Length > 0;
        public string HasNotesMac => HasNotes ? "Yes" : "No";

        public string Relation
        {
            get
            {
                switch (_relationType)
                {
                    case DIRECT: return Ahnentafel == 1 ? "Root Person" : "Direct Ancestor";
                    case BLOOD: return "Blood Relation";
                    case MARRIAGE: return "By Marriage";
                    case MARRIEDTODB: return "Marr to Direct/Blood";
                    case DESCENDANT: return "Descendant";
                    case LINKED: return "Linked by Marriages";
                    default: return "Unknown";
                }
            }
        }

        public IList<Fact> PersonalFacts => Facts;

        IList<Fact> FamilyFacts
        {
            get
            {
                var familyfacts = new List<Fact>();
                foreach (Family f in FamiliesAsSpouse)
                    familyfacts.AddRange(f.Facts);
                return familyfacts;
            }
        }

        public IList<Fact> ErrorFacts { get; }

        int Factcount { get; set; }
        public IList<Fact> AllFacts
        {
            get
            {
                int currentFactCount = Facts.Count + FamilyFacts.Count;
                if (_allfacts is null || currentFactCount != Factcount)
                {
                    _allfacts = new List<Fact>();
                    _allfacts.AddRange(PersonalFacts);
                    _allfacts.AddRange(FamilyFacts);
                    _allFileFacts = _allfacts.Where(x => !x.Created).ToList();
                    Factcount = _allfacts.Count;
                }
                return _allfacts;
            }
        }

        public IList<Fact> AllFileFacts => _allFileFacts;

        public string Gender
        {
            get => _indi.SexChar;
        }

        public bool GenderMatches(Individual that) => Gender == that.Gender || Gender == "U" || that.Gender == "U";

        public string SortedName { get; private set; }

        public string Name
        {
            get => _fullname;
            private set
            {
                string name = value;
                int startPos = name.IndexOf("/", StringComparison.Ordinal), endPos = name.LastIndexOf("/", StringComparison.Ordinal);
                if (startPos >= 0 && endPos > startPos)
                {
                    Surname = name.Substring(startPos + 1, endPos - startPos - 1);
                    _forenames = startPos == 0 ? UNKNOWN_NAME : name.Substring(0, startPos).Trim();
                }
                else
                {
                    Surname = UNKNOWN_NAME;
                    _forenames = name;
                }
                if (string.IsNullOrEmpty(Surname) || Surname.ToLower() == "mnu" || Surname.ToLower() == "lnu" || Surname == "[--?--]" || Surname.ToLower() == "unk" ||
                  ((Surname[0] == '.' || Surname[0] == '?' || Surname[0] == '_') && Surname.Distinct().Count() == 1)) // if all chars are same and is . ? or _
                    Surname = UNKNOWN_NAME;
                if (GeneralSettings.Default.TreatFemaleSurnamesAsUnknown && !IsMale && Surname.StartsWith("(", StringComparison.Ordinal) && Surname.EndsWith(")", StringComparison.Ordinal))
                    Surname = UNKNOWN_NAME;
                if (string.IsNullOrEmpty(_forenames) || _forenames.ToLower() == "unk" || _forenames == "[--?--]" ||
                  ((_forenames[0] == '.' || _forenames[0] == '?' || _forenames[0] == '_') && _forenames.Distinct().Count() == 1))
                    _forenames = UNKNOWN_NAME;
                MarriedName = Surname;
                _fullname = SetFullName();
                SortedName = $"{_forenames} {Surname}".Trim();
            }
        }

        public string SetFullName()
        {
            return GeneralSettings.Default.ShowAliasInName && Alias.Length > 0
                ? $"{_forenames}  '{Alias}' {Surname}".Trim()
                : $"{_forenames} {Surname}".Trim();
        }

        public string Forename
        {
            get
            {
                if (_forenames is null)
                    return string.Empty;
                int pos = _forenames.IndexOf(" ", StringComparison.Ordinal);
                return pos > 0 ? _forenames.Substring(0, pos) : _forenames;
            }
        }

        public string OtherNames
        {
            get
            {
                if (_forenames is null)
                    return string.Empty;
                int pos = _forenames.IndexOf(" ", StringComparison.Ordinal);
                return pos > 0 ? _forenames.Substring(pos).Trim() : string.Empty;
            }
        }

        public string ForenameMetaphone => forenameMetaphone.PrimaryKey;

        public string Forenames => GeneralSettings.Default.ShowAliasInName && Alias.Length > 0 ? $"{_forenames} '{Alias}' " : _forenames;

        public string Surname { get; private set; }

        public string SurnameMetaphone => surnameMetaphone.PrimaryKey;

        public string MarriedName { get; set; }

        public Fact BirthFact
        {
            get
            {
                Fact f = GetPreferredFact(Fact.BIRTH);
                if (f != null)
                    return f;
                f = GetPreferredFact(Fact.BIRTH_CALC);
                if (GeneralSettings.Default.UseBaptismDates)
                {
                    if (f != null)
                        return f;
                    f = GetPreferredFact(Fact.BAPTISM);
                    if (f != null)
                        return f;
                    f = GetPreferredFact(Fact.CHRISTENING);
                }
                return f;
            }
        }

        public FactDate BirthDate => BirthFact is null ? FactDate.UNKNOWN_DATE : BirthFact.FactDate;

        public DateTime BirthStart => BirthDate.StartDate != FactDate.MINDATE ? BirthDate.StartDate : BirthDate.EndDate;
        public DateTime BirthEnd => BirthDate.StartDate != FactDate.MAXDATE ? BirthDate.EndDate : BirthDate.StartDate;

        public FactLocation BirthLocation => (BirthFact is null) ? FactLocation.BLANK_LOCATION : BirthFact.Location;

        public Fact DeathFact
        {
            get
            {
                Fact f = GetPreferredFact(Fact.DEATH);
                if (GeneralSettings.Default.UseBurialDates)
                {
                    if (f != null)
                        return f;
                    f = GetPreferredFact(Fact.BURIAL);
                    if (f != null)
                        return f;
                    f = GetPreferredFact(Fact.CREMATION);
                }
                return f;
            }
        }

        public FactDate DeathDate => DeathFact is null ? FactDate.UNKNOWN_DATE : DeathFact.FactDate;

        public DateTime DeathStart => DeathDate.StartDate != FactDate.MINDATE ? DeathDate.StartDate : DeathDate.EndDate;
        public DateTime DeathEnd => DeathDate.EndDate != FactDate.MAXDATE ? DeathDate.EndDate : DeathDate.StartDate;

        public FactLocation DeathLocation => DeathFact is null ? FactLocation.BLANK_LOCATION : DeathFact.Location;

        public FactDate BaptismDate
        {
            get
            {
                Fact f = GetPreferredFact(Fact.BAPTISM);
                if (f is null)
                    f = GetPreferredFact(Fact.CHRISTENING);
                return f?.FactDate;
            }
        }

        public FactDate BurialDate
        {
            get
            {
                Fact f = GetPreferredFact(Fact.BURIAL);
                if (f is null)
                    f = GetPreferredFact(Fact.CREMATION);
                return f?.FactDate;
            }
        }

        public string Occupation
        {
            get
            {
                Fact occupation = GetPreferredFact(Fact.OCCUPATION);
                return occupation is null ? string.Empty : occupation.Comment;
            }
        }

        int MaxAgeAtDeath => DeathDate.EndDate > FactDate.NOW ? GetAge(FactDate.NOW).MaxAge : GetAge(DeathDate).MaxAge;

        public Age LifeSpan => GetAge(FactDate.TODAY);

        public FactDate LooseBirthDate
        {
            get
            {
                Fact loose = GetPreferredFact(Fact.LOOSEBIRTH);
                return loose is null ? FactDate.UNKNOWN_DATE : loose.FactDate;
            }
        }

        public string LooseBirth
        {
            get
            {
                FactDate fd = LooseBirthDate;
                return (fd.StartDate > fd.EndDate) ? "Alive facts after death, check data errors tab and children's births" : fd.ToString();
            }
        }

        public FactDate LooseDeathDate
        {
            get
            {
                Fact loose = GetPreferredFact(Fact.LOOSEDEATH);
                return loose is null ? FactDate.UNKNOWN_DATE : loose.FactDate;
            }
        }

        public string LooseDeath
        {
            get
            {
                FactDate fd = LooseDeathDate;
                return (fd.StartDate > fd.EndDate) ? "Alive facts after death, check data errors tab and children's births" : fd.ToString();
            }
        }

        public string IndividualRef => $"{IndividualID}: {Name}";

        public string ServiceNumber
        {
            get
            {
                Fact service = GetPreferredFact(Fact.SERVICE_NUMBER);
                return service is null ? string.Empty : service.Comment;
            }
        }

        public bool BirthdayEffect
        {
            get
            {
                if (BirthDate.IsExact && DeathDate.IsExact)
                {
                    DateTime amendedDeath;
                    try
                    {
                        if (DeathDate.StartDate.Month == 2 && DeathDate.StartDate.Day == 29)
                            amendedDeath = new DateTime(BirthDate.StartDate.Year, 2, 28); // fix issue with 29th February death dates
                        else
                            amendedDeath = new DateTime(BirthDate.StartDate.Year, DeathDate.StartDate.Month, DeathDate.StartDate.Day); // set death date to be same year as birth
                        var diff = Math.Abs((amendedDeath - BirthDate.StartDate).Days);
                        Console.WriteLine($"Processed Individual: {IndividualID}: {Name}, Diff:{diff}, Birth: {BirthDate.StartDate.ToShortDateString()} Death: {DeathDate.StartDate.ToShortDateString()}");
                        if (diff > 180)
                        {
                            if (BirthDate.StartDate.Month < 7)
                                amendedDeath = amendedDeath.AddYears(-1);
                            else
                                amendedDeath = amendedDeath.AddYears(1);
                            diff = Math.Abs((amendedDeath - BirthDate.StartDate).Days);
                        }
                        return diff < 16;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        Console.WriteLine($"PROBLEM Individual: {IndividualID}: {Name}");
                        return false;
                    }
                }
                return false;
            }
        }

        public string BirthMonth => BirthDate.IsExact ? BirthDate.StartDate.ToString("MM : MMMM", CultureInfo.InvariantCulture) : "00 : Unknown";

        public IList<Family> FamiliesAsSpouse { get; }

        public IList<ParentalRelationship> FamiliesAsChild { get; }

        public string FamilyIDsAsParent
        {
            get
            {
                string result = string.Empty;
                foreach (Family f in FamiliesAsSpouse)
                    result += f.FamilyID + ",";
                return result.Length == 0 ? result : result.Substring(0, result.Length - 1);
            }
        }

        public string FamilyIDsAsChild
        {
            get
            {
                string result = string.Empty;
                foreach (ParentalRelationship pr in FamiliesAsChild)
                    result += pr.Family.FamilyID + ",";
                return result.Length == 0 ? result : result.Substring(0, result.Length - 1);
            }
        }

        public bool IsNaturalChildOf(Individual parent)
        {
            if (parent is null) return false;
            foreach (ParentalRelationship pr in FamiliesAsChild)
            {
                if (pr.Family != null)
                    return (pr.IsNaturalFather && parent.IsMale && parent.Equals(pr.Father)) ||
                           (pr.IsNaturalMother && !parent.IsMale && parent.Equals(pr.Mother));
            }
            return false;
        }

        public Individual NaturalFather
        {
            get
            {
                foreach (ParentalRelationship pr in FamiliesAsChild)
                {
                    if (pr.Family != null && pr.Father != null && pr.IsNaturalFather)
                        return pr.Father;
                }
                return null;
            }
        }

        public int FactCount(string factType) => Facts.Count(f => f.FactType == factType && f.FactErrorLevel == Fact.FactError.GOOD);

        public int ErrorFactCount(string factType, Fact.FactError errorLevel) => ErrorFacts.Count(f => f.FactType == factType && f.FactErrorLevel == errorLevel);

        public string MarriageDates
        {
            get
            {
                string output = string.Empty;
                foreach (Family f in FamiliesAsSpouse)
                    if (!string.IsNullOrEmpty(f.MarriageDate?.ToString()))
                        output += $"{f.MarriageDate}; ";
                return output.Length > 0 ? output.Substring(0, output.Length - 2) : output; // remove trailing ;
            }
        }

        public string MarriageLocations
        {
            get
            {
                string output = string.Empty;
                foreach (Family f in FamiliesAsSpouse)
                    if (!string.IsNullOrEmpty(f.MarriageLocation))
                        output += $"{f.MarriageLocation}; ";
                return output.Length > 0 ? output.Substring(0, output.Length - 2) : output; // remove trailing ;
            }
        }

        public int MarriageCount => FamiliesAsSpouse.Count;

        public int ChildrenCount => FamiliesAsSpouse.Sum(x => x.Children.Count);

        #endregion

        #region Boolean Tests

        public bool IsMale => Gender.Equals("M");

        public bool IsInFamily => Infamily;

        public bool IsMarried(FactDate fd)
        {
            if (IsSingleAtDeath)
                return false;
            return FamiliesAsSpouse.Any(f =>
            {
                FactDate marriage = f.GetPreferredFactDate(Fact.MARRIAGE);
                return (marriage != null && marriage.IsBefore(fd));
            });
        }

        public bool HasMilitaryFacts => Facts.Any(f => f.FactType == Fact.MILITARY || f.FactType == Fact.SERVICE_NUMBER);

        public bool IsAlive(FactDate when) => IsBorn(when) && !IsDeceased(when);

        public bool IsBorn(FactDate when) => !BirthDate.IsKnown || BirthDate.StartsOnOrBefore(when); // assume born if birthdate is unknown

        public bool IsDeceased(FactDate when) => DeathDate.IsKnown && DeathDate.IsBefore(when);

        public bool IsSingleAtDeath => GetPreferredFact(Fact.UNMARRIED) != null || MaxAgeAtDeath < 16 || LifeSpan.MaxAge < 16;

        public bool IsBirthKnown => BirthDate.IsKnown && BirthDate.IsExact;

        public bool IsDeathKnown => DeathDate.IsKnown && DeathDate.IsExact;

        public bool IsPossiblyAlive(FactDate when)
        {
            if (when is null || when.IsUnknown) return true;
            if (BirthDate.StartDate <= when.EndDate && DeathDate.EndDate >= when.StartDate) return true;
            if (DeathDate.IsUnknown)
            {
                // if unknown death add 110 years to Enddate
                var death = BirthDate.AddEndDateYears(110);
                if (BirthDate.StartDate <= when.EndDate && death.EndDate >= when.StartDate) return true;
            }
            return false;
        }

        #endregion

        #region Age Functions

        public Age GetAge(FactDate when) => new Age(this, when);

        public Age GetAge(FactDate when, string factType) => (factType == Fact.BIRTH || factType == Fact.PARENT) ? Age.BIRTH : new Age(this, when);

        public Age GetAge(DateTime when)
        {
            string now = FactDate.Format(FactDate.FULL, when);
            return GetAge(new FactDate(now));
        }

        public int GetMaxAge(FactDate when) => GetAge(when).MaxAge;

        public int GetMinAge(FactDate when) => GetAge(when).MinAge;

        public int GetMaxAge(DateTime when)
        {
            string now = FactDate.Format(FactDate.FULL, when);
            return GetMaxAge(new FactDate(now));
        }

        public int GetMinAge(DateTime when)
        {
            string now = FactDate.Format(FactDate.FULL, when);
            return GetMinAge(new FactDate(now));
        }

        #endregion
        #region Fact Functions

        public void AddFact(Fact fact)
        {
            if (fact is null)
                return;
            if (FamilyTree.FactBeforeBirth(this, fact))
                fact.SetError((int)FamilyTree.Dataerror.FACTS_BEFORE_BIRTH, Fact.FactError.ERROR,
                    $"{fact.FactTypeDescription} fact recorded: {fact.FactDate} before individual was born");
            if (FamilyTree.FactAfterDeath(this, fact))
                fact.SetError((int)FamilyTree.Dataerror.FACTS_AFTER_DEATH, Fact.FactError.ERROR,
                    $"{fact.FactTypeDescription} fact recorded: {fact.FactDate} after individual died");

            switch (fact.FactErrorLevel)
            {
                case Fact.FactError.GOOD:
                    AddGoodFact(fact);
                    break;
                case Fact.FactError.WARNINGALLOW:
                    AddGoodFact(fact);
                    ErrorFacts.Add(fact);
                    break;
                case Fact.FactError.WARNINGIGNORE:
                case Fact.FactError.ERROR:
                    ErrorFacts.Add(fact);
                    break;
            }
        }

        void AddGoodFact(Fact fact)
        {
            Facts.Add(fact);
            if (fact.Preferred && !preferredFacts.ContainsKey(fact.FactType))
                preferredFacts.Add(fact.FactType, fact);
            AddLocation(fact);
        }

        public string LCSurname => ValidLostCousinsString(Surname, false);
        public string LCForename => ValidLostCousinsString(Forename, false);
        public string LCOtherNames => ValidLostCousinsString(OtherNames, true);

        string ValidLostCousinsString(string input, bool allowspace)
        {
            StringBuilder output = new StringBuilder();
            input = RemoveQuoted(input);
            foreach (char c in input)
            {
                if (c == '-' || c == '\'' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                    output.Append(c);
                if (allowspace && c == ' ')
                    output.Append(c);
            }
            var result = output.ToString().Replace("--", "-").Replace("--", "-").Replace("--", "-");
            return result == "-" ? UNKNOWN_NAME : result;
        }

        string RemoveQuoted(string input)
        {
            string output = input.Replace("UNKNOWN", "");
            int startptr = input.IndexOf('\'');
            if (startptr == -1) startptr = input.IndexOf('\"');
            if (startptr != -1)
            {
                int endptr = input.IndexOf('\'', startptr);
                if (endptr == -1) endptr = input.IndexOf('\"', startptr);
                output = (startptr < input.Length ? input.Substring(0, startptr) : string.Empty) + (endptr < input.Length ? input.Substring(endptr) : string.Empty);
            }
            output = output.Replace("--", "").Replace('\'', ' ').Replace('\"', ' ').Replace("  ", " ").Replace("  ", " ");
            return output.TrimEnd('-').Trim();
        }


        void AddLocation(Fact fact)
        {
            FactLocation loc = fact.Location;
            if (loc != null && !Locations.ContainsLocation(loc))
            {
                Locations.Add(loc);
                loc.AddIndividual(this);
            }
        }

        public Fact GetPreferredFact(string factType) => preferredFacts.ContainsKey(factType) ? preferredFacts[factType] : Facts.FirstOrDefault(f => f.FactType == factType);

        public FactDate GetPreferredFactDate(string factType)
        {
            Fact f = GetPreferredFact(factType);
            return (f is null || f.FactDate is null) ? FactDate.UNKNOWN_DATE : f.FactDate;
        }

        // Returns all facts of the given type.
        public IEnumerable<Fact> GetFacts(string factType) => Facts.Where(f => f.FactType == factType);

        public string SurnameAtDate(FactDate date)
        {
            string name = Surname;
            if (!IsMale)
            {
                foreach (Family marriage in FamiliesAsSpouse.OrderBy(f => f.MarriageDate))
                {
                    if ((marriage.MarriageDate.Equals(date) || marriage.MarriageDate.IsBefore(date)) && marriage.Husband != null && marriage.Husband.Surname != "UNKNOWN")
                        name = marriage.Husband.Surname;
                }
            }
            return name;
        }

        public void QuestionGender(Family family, bool pHusband)
        {
            if (family is null) return;
            string description;
            if (Gender.Equals("U"))
            {
                string spouse = pHusband ? "husband" : "wife";
                description = $"Unknown gender but appears as a {spouse} in family {family.FamilyRef} check gender setting";
            }
            else
            {
                if (IsMale)
                    description = $"Male but appears as a wife in family {family.FamilyRef} check family for swapped husband and wife";
                else
                    description = $"Female but appears as husband in family {family.FamilyRef} check family for swapped husband and wife";
            }
            var gender = new Fact(family.FamilyID, Fact.GENDER, FactDate.UNKNOWN_DATE, null, description, true, true);
            gender.SetError((int)FamilyTree.Dataerror.MALE_WIFE_FEMALE_HUSBAND, Fact.FactError.ERROR, description);
            AddFact(gender);
        }
        #endregion

        #region Location functions

        public bool IsAtLocation(FactLocation loc, int level)
        {
            foreach (Fact f in AllFacts)
            {
                if (f.Location.Equals(loc, level))
                    return true;
            }
            return false;
        }
        #endregion

        readonly FactComparer factComparer = new FactComparer();

        public int DuplicateLCFacts
        {
            get
            {
                IEnumerable<Fact> lcFacts = AllFacts.Where(f => f.FactType == Fact.LOSTCOUSINS || f.FactType == Fact.LC_FTA);
                int distinctFacts = lcFacts.Distinct(factComparer).Count();
                return LostCousinsFacts - distinctFacts;
            }
        }

        public int LostCousinsFacts => Facts.Count(f => f.FactType == Fact.LOSTCOUSINS || f.FactType == Fact.LC_FTA);

        public string ReferralFamilyID { get; set; }

        public void FixIndividualID(int length)
        {
            try
            {
                IndividualID = IndividualID.Substring(0, 1) + IndividualID.Substring(1).PadLeft(length, '0');
            }
            catch (ArgumentOutOfRangeException)
            {  // don't error if Individual isn't of type Ixxxx
            }
        }

        #region Colour BMD Values

        public BMDColours Birth => BirthDate.DateStatus(false);

        public BMDColours BaptChri
        {
            get
            {
                FactDate baptism = GetPreferredFactDate(Fact.BAPTISM);
                FactDate christening = GetPreferredFactDate(Fact.CHRISTENING);
                BMDColours baptismStatus = baptism.DateStatus(true);
                BMDColours christeningStatus = christening.DateStatus(true);
                if (baptismStatus.Equals(BMDColours.EMPTY))
                    return christeningStatus;
                if (christeningStatus.Equals(BMDColours.EMPTY))
                    return baptismStatus;
                return (int)baptismStatus < (int)christeningStatus ? baptismStatus : christeningStatus;
            }
        }

        BMDColours CheckMarriageStatus(Family fam)
        {
            // individual is a member of a family as parent so check family status
            if ((IndividualID == fam.HusbandID && fam.Wife is null) ||
                (IndividualID == fam.WifeID && fam.Husband is null))
            {
                return fam.Children.Count > 0 ?
                      BMDColours.NO_PARTNER // no partner but has children
                    : BMDColours.EMPTY; // solo individual so no marriage
            }
            if (fam.GetPreferredFact(Fact.MARRIAGE) is null)
                return BMDColours.NO_MARRIAGE; // has a partner but no marriage fact
            return fam.MarriageDate.DateStatus(false); // has a partner and a marriage so return date status
        }

        public BMDColours Marriage1
        {
            get
            {
                Family fam = Marriages(0);
                if (fam is null)
                {
                    if (MaxAgeAtDeath > 16 && GetPreferredFact(Fact.UNMARRIED) is null)
                        return BMDColours.NO_SPOUSE; // of marrying age but hasn't a partner or unmarried
                    return BMDColours.EMPTY;
                }
                return CheckMarriageStatus(fam);
            }
        }

        public BMDColours Marriage2
        {
            get
            {
                Family fam = Marriages(1);
                return fam is null ? BMDColours.EMPTY : CheckMarriageStatus(fam);
            }
        }

        public BMDColours Marriage3
        {
            get
            {
                Family fam = Marriages(2);
                return fam is null ? 0 : CheckMarriageStatus(fam);
            }
        }

        public string FirstMarriage => MarriageString(0);

        public string SecondMarriage => MarriageString(1);

        public string ThirdMarriage => MarriageString(2);

        public FactDate FirstMarriageDate
        {
            get
            {
                Family fam = Marriages(0);
                return fam is null ? FactDate.UNKNOWN_DATE : Marriages(0).MarriageDate;
            }
        }

        public FactDate SecondMarriageDate
        {
            get
            {
                Family fam = Marriages(1);
                return fam is null ? FactDate.UNKNOWN_DATE : Marriages(1).MarriageDate;
            }
        }

        public FactDate ThirdMarriageDate
        {
            get
            {
                Family fam = Marriages(2);
                return fam is null ? FactDate.UNKNOWN_DATE : Marriages(2).MarriageDate;
            }
        }

        public Individual FirstSpouse
        {
            get
            {
                Family fam = Marriages(0);
                return fam?.Spouse(this);
            }
        }

        public Individual SecondSpouse
        {
            get
            {
                Family fam = Marriages(1);
                return fam?.Spouse(this);
            }
        }

        public Individual ThirdSpouse
        {
            get
            {
                Family fam = Marriages(2);
                return fam?.Spouse(this);
            }
        }

        public BMDColours Death
        {
            get
            {
                if (IsFlaggedAsLiving)
                    return BMDColours.ISLIVING;
                if (DeathDate.IsUnknown && GetMaxAge(FactDate.TODAY) < FactDate.MAXYEARS)
                    return GetMaxAge(FactDate.TODAY) < 90 ? BMDColours.EMPTY : BMDColours.OVER90;
                return DeathDate.DateStatus(false);
            }
        }

        public BMDColours CremBuri
        {
            get
            {
                FactDate cremation = GetPreferredFactDate(Fact.CREMATION);
                FactDate burial = GetPreferredFactDate(Fact.BURIAL);
                BMDColours cremationStatus = cremation.DateStatus(true);
                BMDColours burialStatus = burial.DateStatus(true);
                if (cremationStatus.Equals(BMDColours.EMPTY))
                    return burialStatus;
                if (burialStatus.Equals(BMDColours.EMPTY))
                    return cremationStatus;
                return (int)cremationStatus < (int)burialStatus ? cremationStatus : burialStatus;
            }
        }

        #endregion

        public float Score
        {
            get { return 0.0f; }
            // TODO Add scoring mechanism
        }

        public int FactsCount => Facts.Count;

        public int SourcesCount => Facts.SelectMany(f => f.Sources).Distinct().Count();

        public bool IsLivingError => IsFlaggedAsLiving && DeathDate.IsKnown;

        public string IndividualID
        {
            get
            {
                if (_indi != null)
                    return _indi.XRefID;
                else
                    return null;
            }
            set
            {
                if (_indi != null)
                    _indi.XRefID = value;
            }
        }

        Family Marriages(int number)
        {
            if (number < FamiliesAsSpouse.Count)
            {
                Family f = FamiliesAsSpouse.OrderBy(d => d.MarriageDate).ElementAt(number);
                return f;
            }
            return null;
        }

        string MarriageString(int number)
        {
            Family marriage = Marriages(number);
            if (marriage is null)
                return string.Empty;
            if (IndividualID == marriage.HusbandID && marriage.Wife != null)
                return $"To {marriage.Wife.Name}: {marriage}";
            if (IndividualID == marriage.WifeID && marriage.Husband != null)
                return $"To {marriage.Husband.Name}: {marriage}";
            return $"Married: {marriage}";
        }

        #region Basic Class Functions
        public override bool Equals(object obj)
        {
            return obj is Individual individual && IndividualID == individual.IndividualID;
        }

        public override int GetHashCode() => base.GetHashCode();

        public override string ToString() => $"{IndividualID}: {Name} b.{BirthDate}";

        public int CompareTo(Individual that)
        {
            // Individuals are naturally ordered by surname, then forenames,
            // then date of birth.
            if (that is null)
                return -1;
            int res = string.Compare(Surname, that.Surname, StringComparison.CurrentCulture);
            if (res == 0)
            {
                res = string.Compare(_forenames, that._forenames, StringComparison.Ordinal);
                if (res == 0)
                {
                    FactDate d1 = BirthDate;
                    FactDate d2 = that.BirthDate;
                    res = d1.CompareTo(d2);
                }
            }
            return res;
        }

        IComparer<IDisplayIndividual> IColumnComparer<IDisplayIndividual>.GetComparer(string columnName, bool ascending)
        {
            switch (columnName)
            {
                case "IndividualID": return CompareComparableProperty<IDisplayIndividual>(i => i.IndividualID, ascending);
                case "Forenames": return new NameComparer<IDisplayIndividual>(ascending, true);
                case "Surname": return new NameComparer<IDisplayIndividual>(ascending, false);
                case "Gender": return CompareComparableProperty<IDisplayIndividual>(i => i.Gender, ascending);
                case "BirthDate": return CompareComparableProperty<IDisplayIndividual>(i => i.BirthDate, ascending);
                case "BirthLocation": return CompareComparableProperty<IDisplayIndividual>(i => i.BirthLocation, ascending);
                case "DeathDate": return CompareComparableProperty<IDisplayIndividual>(i => i.DeathDate, ascending);
                case "DeathLocation": return CompareComparableProperty<IDisplayIndividual>(i => i.DeathLocation, ascending);
                case "Occupation": return CompareComparableProperty<IDisplayIndividual>(i => i.Occupation, ascending);
                case "Relation": return CompareComparableProperty<IDisplayIndividual>(i => i.Relation, ascending);
                case "RelationToRoot": return CompareComparableProperty<IDisplayIndividual>(i => i.RelationToRoot, ascending);
                case "FamilySearchID": return CompareComparableProperty<IDisplayIndividual>(i => i.FamilySearchID, ascending);
                case "MarriageCount": return CompareComparableProperty<IDisplayIndividual>(i => i.MarriageCount, ascending);
                case "ChildrenCount": return CompareComparableProperty<IDisplayIndividual>(i => i.ChildrenCount, ascending);
                case "BudgieCode": return CompareComparableProperty<IDisplayIndividual>(i => i.BudgieCode, ascending);
                case "Ahnentafel": return CompareComparableProperty<IDisplayIndividual>(i => i.Ahnentafel, ascending);
                case "Notes": return CompareComparableProperty<IDisplayIndividual>(i => i.Notes, ascending);
                default: return null;
            }
        }

        IComparer<IDisplayColourBMD> IColumnComparer<IDisplayColourBMD>.GetComparer(string columnName, bool ascending)
        {
            switch (columnName)
            {
                case "IndividualID": return CompareComparableProperty<IDisplayColourBMD>(i => i.IndividualID, ascending);
                case "Forenames": return new NameComparer<IDisplayColourBMD>(ascending, true);
                case "Surname": return new NameComparer<IDisplayColourBMD>(ascending, false);
                case "Relation": return CompareComparableProperty<IDisplayColourBMD>(i => i.Relation, ascending);
                case "RelationToRoot": return CompareComparableProperty<IDisplayColourBMD>(i => i.RelationToRoot, ascending);
                case "Birth": return CompareComparableProperty<IDisplayColourBMD>(i => (int)i.Birth, ascending);
                case "Baptism": return CompareComparableProperty<IDisplayColourBMD>(i => (int)i.BaptChri, ascending);
                case "Marriage 1": return CompareComparableProperty<IDisplayColourBMD>(i => (int)i.Marriage1, ascending);
                case "Marriage 2": return CompareComparableProperty<IDisplayColourBMD>(i => (int)i.Marriage2, ascending);
                case "Marriage 3": return CompareComparableProperty<IDisplayColourBMD>(i => (int)i.Marriage3, ascending);
                case "Death": return CompareComparableProperty<IDisplayColourBMD>(i => (int)i.Death, ascending);
                case "Burial": return CompareComparableProperty<IDisplayColourBMD>(i => (int)i.CremBuri, ascending);
                case "BirthDate": return CompareComparableProperty<IDisplayColourBMD>(i => i.BirthDate, ascending);
                case "DeathDate": return CompareComparableProperty<IDisplayColourBMD>(i => i.DeathDate, ascending);
                case "First Marriage": return CompareComparableProperty<IDisplayColourBMD>(i => i.FirstMarriage, ascending);
                case "Second Marriage": return CompareComparableProperty<IDisplayColourBMD>(i => i.SecondMarriage, ascending);
                case "Third Marriage": return CompareComparableProperty<IDisplayColourBMD>(i => i.ThirdMarriage, ascending);
                case "BirthLocation": return CompareComparableProperty<IDisplayColourBMD>(i => i.BirthLocation, ascending);
                case "DeathLocation": return CompareComparableProperty<IDisplayColourBMD>(i => i.DeathLocation, ascending);
                case "Ahnentafel": return CompareComparableProperty<IDisplayColourBMD>(i => i.Ahnentafel, ascending);
                default: return null;
            }
        }

        IComparer<IDisplayLooseBirth> IColumnComparer<IDisplayLooseBirth>.GetComparer(string columnName, bool ascending)
        {
            switch (columnName)
            {
                case "IndividualID": return CompareComparableProperty<IDisplayLooseBirth>(i => i.IndividualID, ascending);
                case "Forenames": return new NameComparer<IDisplayLooseBirth>(ascending, true);
                case "Surname": return new NameComparer<IDisplayLooseBirth>(ascending, false);
                case "BirthDate": return CompareComparableProperty<IDisplayLooseBirth>(i => i.BirthDate, ascending);
                case "BirthLocation": return CompareComparableProperty<IDisplayLooseBirth>(i => i.BirthLocation, ascending);
                case "LooseBirth": return CompareComparableProperty<IDisplayLooseBirth>(i => i.LooseBirthDate, ascending);
                default: return null;
            }
        }

        IComparer<IDisplayLooseDeath> IColumnComparer<IDisplayLooseDeath>.GetComparer(string columnName, bool ascending)
        {
            switch (columnName)
            {
                case "IndividualID": return CompareComparableProperty<IDisplayLooseDeath>(i => i.IndividualID, ascending);
                case "Forenames": return new NameComparer<IDisplayLooseDeath>(ascending, true);
                case "Surname": return new NameComparer<IDisplayLooseDeath>(ascending, false);
                case "BirthDate": return CompareComparableProperty<IDisplayLooseDeath>(i => i.DeathDate, ascending);
                case "BirthLocation": return CompareComparableProperty<IDisplayLooseDeath>(i => i.DeathLocation, ascending);
                case "DeathDate": return CompareComparableProperty<IDisplayLooseDeath>(i => i.DeathDate, ascending);
                case "DeathLocation": return CompareComparableProperty<IDisplayLooseDeath>(i => i.DeathLocation, ascending);
                case "LooseDeath": return CompareComparableProperty<IDisplayLooseDeath>(i => i.LooseDeathDate, ascending);
                default: return null;
            }
        }

        IComparer<IDisplayLooseInfo> IColumnComparer<IDisplayLooseInfo>.GetComparer(string columnName, bool ascending)
        {
            switch (columnName)
            {
                case "IndividualID": return CompareComparableProperty<IDisplayLooseInfo>(i => i.IndividualID, ascending);
                case "Forenames": return new NameComparer<IDisplayLooseInfo>(ascending, true);
                case "Surname": return new NameComparer<IDisplayLooseInfo>(ascending, false);
                case "BirthDate": return CompareComparableProperty<IDisplayLooseInfo>(i => i.BirthDate, ascending);
                case "BirthLocation": return CompareComparableProperty<IDisplayLooseInfo>(i => i.BirthLocation, ascending);
                case "DeathDate": return CompareComparableProperty<IDisplayLooseInfo>(i => i.DeathDate, ascending);
                case "DeathLocation": return CompareComparableProperty<IDisplayLooseInfo>(i => i.DeathLocation, ascending);
                case "LooseBirth": return CompareComparableProperty<IDisplayLooseInfo>(i => i.LooseDeathDate, ascending);
                case "LooseDeath": return CompareComparableProperty<IDisplayLooseInfo>(i => i.LooseDeathDate, ascending);
                default: return null;
            }
        }

        Comparer<T> CompareComparableProperty<T>(Func<Individual, IComparable> accessor, bool ascending)
        {
            return Comparer<T>.Create((x, y) =>
            {
                var a = accessor(x as Individual);
                var b = accessor(y as Individual);
                int result = a.CompareTo(b);
                return ascending ? result : -result;
            });
        }
        #endregion
    }
}
