﻿using FTAnalyzer.Filters;
using FTAnalyzer.Properties;
using FTAnalyzer.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Numerics;
using System.Collections.Concurrent;
using GeneGenie.Gedcom;

#if __PC__
using FTAnalyzer.Forms.Controls;
#elif __MACOS__ || __IOS__
using FTAnalyzer.ViewControllers;
#endif

namespace FTAnalyzer
{
    public class FamilyTree
    {
        #region Variables
        static FamilyTree instance;

        IList<FactSource> sources;
        IList<Individual> individuals;
        IList<Family> families;
        IList<Tuple<string, Fact>> sharedFacts;
        IDictionary<string, List<Individual>> occupations;
        IDictionary<StandardisedName, StandardisedName> names;
        IDictionary<string, List<Individual>> unknownFactTypes;
        SortableBindingList<IDisplayLooseDeath> looseDeaths;
        SortableBindingList<IDisplayLooseBirth> looseBirths;
        SortableBindingList<IDisplayLooseInfo> looseInfo;
        SortableBindingList<DuplicateIndividual> duplicates;
        ConcurrentBag<DuplicateIndividual> buildDuplicates;
        const int DATA_ERROR_GROUPS = 32;
        BigInteger maxAhnentafel;
        Dictionary<string, Individual> individualLookup;
        string rootIndividualID = string.Empty;
        int SoloFamilies { get; set; }
        int PreMarriageFamilies { get; set; }
        public bool Geocoding { get; set; }
        public List<NonDuplicate> NonDuplicates { get; private set; }
        public string Version { get; set; }
        #endregion

        #region Static Functions

        FamilyTree() => ResetData();

        public static FamilyTree Instance
        {
            get
            {
                if (instance is null)
                    instance = new FamilyTree();
                return instance;
            }
        }

        public bool DocumentLoaded { get; set; }

        public static string ValidFilename(string filename)
        {
            filename = filename ?? string.Empty;
            int pos = filename.IndexOfAny(Path.GetInvalidFileNameChars());
            if (pos == -1)
                return filename;
            var result = new StringBuilder();
            string remainder = filename;
            while (pos != -1)
            {
                result.Append(remainder.Substring(0, pos));
                if (pos == remainder.Length)
                {
                    remainder = string.Empty;
                    pos = -1;
                }
                else
                {
                    remainder = remainder.Substring(pos + 1);
                    pos = remainder.IndexOfAny(Path.GetInvalidFileNameChars());
                }
            }
            result.Append(remainder);
            return result.ToString();
        }
        #endregion

        #region Load Gedcom XML
        public void ResetData()
        {
            DataLoaded = false;
            sources = new List<FactSource>();
            individuals = new List<Individual>();
            families = new List<Family>();
            sharedFacts = new List<Tuple<string, Fact>>();
            occupations = new Dictionary<string, List<Individual>>();
            names = new Dictionary<StandardisedName, StandardisedName>();
            unknownFactTypes = new Dictionary<string, List<Individual>>();
            DataErrorTypes = new List<DataErrorGroup>();
            rootIndividualID = string.Empty;
            SoloFamilies = 0;
            PreMarriageFamilies = 0;
            ResetLooseFacts();
            duplicates = null;
            buildDuplicates = null;
            maxAhnentafel = 0;
            FactLocation.ResetLocations();
            individualLookup = new Dictionary<string, Individual>();
        }

        public void ResetLooseFacts()
        {
            looseBirths = null;
            looseDeaths = null;
            looseInfo = null;
        }

        public void CheckUnknownFactTypes(string factType)
        {
            if (!unknownFactTypes.ContainsKey(factType))
            {
                unknownFactTypes.Add(factType, new List<Individual>());
            }
        }

        public void LoadTreeSources(GedcomDatabase db, IProgress<int> progress, IProgress<string> outputText)
        {
            // First iterate through attributes of root finding all sources
            var list = db.Sources;
            int sourceMax = list.Count == 0 ? 1 : list.Count;
            int counter = 0;
            foreach (var n in list)
            {
                var fs = new FactSource(n);
                sources.Add(fs);
                progress.Report(100 * counter++ / sourceMax);
            }
            outputText.Report($"Loaded {counter} sources.\n");
            progress.Report(100);
        }

        public void LoadTreeIndividuals(GedcomDatabase db, IProgress<int> progress, IProgress<string> outputText)
        {
            // now iterate through child elements of root
            // finding all individuals
            var list = db.Individuals;
            int individualMax = list.Count;
            int counter = 0;
            foreach (var n in list)
            {
                var individual = new Individual(n, outputText);
                if (individual.IndividualID is null)
                    outputText.Report("File has invalid GEDCOM data. Individual found with no ID. Search file for 0 @@ INDI\n");
                else
                {
                    // debugging of individuals - outputText.Report($"Loaded Individual: {individual.ToString()}\n");
                    individuals.Add(individual);
                    if (individualLookup.ContainsKey(individual.IndividualID))
                        outputText.Report($"More than one INDI record found with ID value {individual.IndividualID}\n");
                    else
                        individualLookup.Add(individual.IndividualID, individual);
                    AddOccupations(individual);
                    AddCustomFacts(individual);
                    progress.Report((100 * counter++) / individualMax);
                }
            }
            outputText.Report($"Loaded {counter} individuals.\n");
            progress.Report(100);
        }

        public void LoadTreeFamilies(GedcomDatabase db, IProgress<int> progress, IProgress<string> outputText)
        {
            // now iterate through child elements of root
            // finding all families
            var list = db.Families;
            int familyMax = list.Count == 0 ? 1 : list.Count;
            int counter = 0;
            foreach (var n in list)
            {
                Family family = new Family(n, outputText);
                families.Add(family);
                progress.Report((100 * counter++) / familyMax);
            }
            outputText.Report($"Loaded {counter} families.\n");
            CheckAllIndividualsAreInAFamily(outputText);
            RemoveFamiliesWithNoIndividuals();
            progress.Report(100);
        }

        public void LoadTreeRelationships(GedcomDatabase db, IProgress<int> progress, IProgress<string> outputText)
        {
            if (string.IsNullOrEmpty(rootIndividualID))
                rootIndividualID = individuals[0].IndividualID;
            UpdateRootIndividual(rootIndividualID, progress, outputText); //, true);
            CreateSharedFacts();
            FixIDs();
            SetDataErrorTypes(progress);
            CountUnknownFactTypes(outputText);
            progress.Report(100);
            DataLoaded = true;
            Loading = false;
        }

        public void UpdateRootIndividual(string rootIndividualID, IProgress<int> progress, IProgress<string> outputText) //, bool locationsToFollow = false)
        {
            outputText.Report($"\nCalculating Relationships using {rootIndividualID}: {GetIndividual(rootIndividualID)?.Name} as root person. Please wait\n\n");

            // When the user changes the root individual, no location processing is taking place
            //int locationCount = locationsToFollow ? FactLocation.AllLocations.Count() : 0;
            SetRelations(rootIndividualID, outputText);
            progress?.Report(10);
            SetRelationDescriptions(rootIndividualID);
            outputText.Report(PrintRelationCount());
            progress?.Report(20);
        }

        public void LoadStandardisedNames(string startPath)
        {
            try
            {
                string filename = Path.Combine(startPath, @"Resources\GINAP.txt");
                if (File.Exists(filename))
                    ReadStandardisedNameFile(filename);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load Standardised names error was : {e.Message}");
            }
        }

        void ReadStandardisedNameFile(string filename)
        {
            using (StreamReader reader = new StreamReader(filename))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    string[] values = line.Split(',');
                    if (line.IndexOf(",", StringComparison.Ordinal) > 0 && (values[0] == "1" || values[0] == "2"))
                    {
                        StandardisedName original = new StandardisedName(values[0] == "2", values[2]);
                        StandardisedName standardised = new StandardisedName(values[1] == "2", values[3]);
                        names.Add(original, standardised);
                    }
                }
            }
        }

        public string GetStandardisedName(bool IsMale, string name)
        {
            StandardisedName gIn = new StandardisedName(IsMale, name);
            names.TryGetValue(gIn, out StandardisedName gOut);
            return gOut is null ? name : gOut.Name;
        }

        void RemoveFamiliesWithNoIndividuals() => (families as List<Family>).RemoveAll(x => x.FamilySize == 0);

        void CountUnknownFactTypes(IProgress<string> outputText)
        {
            if (unknownFactTypes.Count > 0 && !GeneralSettings.Default.IgnoreFactTypeWarnings)
            {
                outputText.Report("\nThe following unknown/custom fact types were reported.\nNB. This isn't an error if you deliberately created these fact types.\nThis is simply highlighting the types so you can check for any possible errors/duplicate types.\n");
                foreach (string tag in unknownFactTypes.Keys)
                {
                    int count = unknownFactTypes[tag].Count;
                    bool ignore = DatabaseHelper.IgnoreCustomFact(tag);
                    if (count > 0 && !ignore)
                        outputText.Report($"\nFound {count} facts of unknown/custom fact type {tag}");
                }
                outputText.Report("\n");
            }
        }

        void CreateSharedFacts()
        {
            foreach (Tuple<string, Fact> t in sharedFacts)
            {
                Individual ind = GetIndividual(t.Item1);
                Fact fact = t.Item2;
                if (ind != null && !ind.Facts.ContainsFact(fact))
                    ind.AddFact(fact);
            }
        }

        void AddOccupations(Individual individual)
        {
            List<string> jobs = new List<string>();
            foreach (Fact f in individual.GetFacts(Fact.OCCUPATION))
            {
                if (!jobs.ContainsString(f.Comment))
                {
                    if (!occupations.TryGetValue(f.Comment, out List<Individual> workers))
                    {
                        workers = new List<Individual>();
                        occupations.Add(f.Comment, workers);
                    }
                    workers.Add(individual);
                    jobs.Add(f.Comment);
                }
            }
        }
        void AddCustomFacts(Individual individual)
        {
            foreach (string factType in unknownFactTypes.Keys)
            {
                if (individual.AllFacts.Any(x => x.FactTypeDescription == factType))
                    unknownFactTypes[factType].Add(individual);
            }
        }

        void CheckAllIndividualsAreInAFamily(IProgress<string> outputText)
        {
            foreach (Family f in families)
            {
                if (f.Husband != null)
                {
                    f.Husband.Infamily = true;
                    f.Husband.ReferralFamilyID = f.FamilyID;
                }
                if (f.Wife != null)
                {
                    f.Wife.Infamily = true;
                    f.Wife.ReferralFamilyID = f.FamilyID;
                }
                foreach (Individual c in f.Children)
                {
                    c.Infamily = true;
                    c.ReferralFamilyID = f.FamilyID;
                    c.HasParents = f.Husband != null || f.Wife != null;
                    c.HasOnlyOneParent = (f.Husband != null && f.Wife is null) || (f.Husband is null && f.Wife != null);
                }
            }
            foreach (Individual ind in individuals)
            {
                if (!ind.IsInFamily)
                    families.Add(new Family(ind, NextSoloFamily));
            }
            if (SoloFamilies > 0)
                outputText.Report($"Added {SoloFamilies} lone individuals as single families.\n");
        }
        #endregion

        #region Properties

        public bool Loading { get; private set; }
        public bool DataLoaded { get; private set; }

        public IEnumerable<Family> AllFamilies => families;

        public IEnumerable<Individual> AllIndividuals => individuals;

        public IEnumerable<FactSource> AllSources => sources;

        public IEnumerable<IDisplayDataError> AllDataErrors => DataErrorTypes.SelectMany(dg => dg.Errors);

        public int IndividualCount => individuals.Count;

        public List<Individual> DeadOrAlive => individuals.Filter(x => x.DeathDate.IsKnown && x.IsFlaggedAsLiving).ToList();

        public string NextSoloFamily { get { return $"SF{++SoloFamilies}"; } }

        public string NextPreMarriageFamily { get { return $"PM{++PreMarriageFamilies}"; } }
        #endregion

        #region Property Functions

        public IEnumerable<Individual> GetAllRelationsOfType(int relationType) => individuals.Filter(ind => ind.RelationType == relationType);

        public IEnumerable<Individual> GetUncertifiedFacts(string factType, int relationType)
        {
            return individuals.Filter(ind =>
            {
                if (ind.RelationType == relationType)
                {
                    Fact f = ind.GetPreferredFact(factType);
                    return (f != null && !f.CertificatePresent);
                }
                return false;
            });
        }

        public FactSource GetSource(string sourceID) => sources.FirstOrDefault(s => s.SourceID == sourceID);

        public Individual GetIndividual(string individualID)
        {
            //            return individuals.FirstOrDefault(i => i.IndividualID == individualID);
            if (string.IsNullOrEmpty(individualID))
                return null;
            individualLookup.TryGetValue(individualID, out Individual person);
            while (individualID.StartsWith("I0", StringComparison.Ordinal) && person is null)
            {
                if (individualID.Length >= 2) individualID = $"I{individualID.Substring(2)}";
                individualLookup.TryGetValue(individualID, out person);
            }
            return person;
        }

        public Family GetFamily(string familyID) => families.FirstOrDefault(f => f.FamilyID == familyID);

        public void AddSharedFact(string individual, Fact fact) => sharedFacts.Add(new Tuple<string, Fact>(individual, fact));

        public IEnumerable<Individual> GetIndividualsAtLocation(FactLocation loc, int level) => individuals.Filter(i => i.IsAtLocation(loc, level));

        public IEnumerable<Family> GetFamiliesAtLocation(FactLocation loc, int level) => families.Filter(f => f.IsAtLocation(loc, level));

        public int CountPeopleAtLocation(FactLocation loc, int level) => individuals.Filter(i => i.IsAtLocation(loc, level)).Count() + families.Filter(f => f.IsAtLocation(loc, level)).Count();

        public List<string> GetSurnamesAtLocation(FactLocation loc) { return GetSurnamesAtLocation(loc, FactLocation.SUBREGION); }
        public List<string> GetSurnamesAtLocation(FactLocation loc, int level)
        {
            List<string> result = new List<string>();
            foreach (Individual i in individuals)
            {
                if (!result.ContainsString(i.Surname) && i.IsAtLocation(loc, level))
                    result.Add(i.Surname);
            }
            result.Sort();
            return result;
        }

        void FixIDs()
        {
            int indLen = individuals.Count.ToString().Length;
            foreach (Individual ind in individuals)
            {
                ind.FixIndividualID(indLen);
                // If the individual id has been changed, the lookup needs to be updated
                if (!individualLookup.ContainsKey(ind.IndividualID))
                    individualLookup.Add(ind.IndividualID, ind);
            }
            int famLen = families.Count.ToString().Length;
            foreach (Family f in families)
                f.FixFamilyID(famLen);
            int sourceLen = sources.Count.ToString().Length;
            foreach (FactSource s in sources)
                s.FixSourceID(sourceLen);
        }

        public void SetFullNames()
        {
            foreach (Individual ind in individuals)
                ind.SetFullName();
        }
        #endregion

        #region Loose Info
        public SortableBindingList<IDisplayLooseInfo> LooseInfo()
        {
            if (looseInfo != null)
                return looseInfo;
            if (looseBirths is null)
                LooseBirths();
            if (looseDeaths is null)
                LooseDeaths();
            SortableBindingList<IDisplayLooseInfo> result = new SortableBindingList<IDisplayLooseInfo>();
            try
            {
                foreach (Individual ind in looseBirths)
                    result.Add(ind as IDisplayLooseInfo);
                foreach (Individual ind in looseDeaths)
                    result.Add(ind as IDisplayLooseInfo);
            }
            catch (Exception ex)
            {
                throw new LooseDataException($"Problem calculating Loose Info. Error was {ex.Message}");
            }
            looseInfo = result;
            return result;
        }
        #endregion

        #region Loose Births

        public SortableBindingList<IDisplayLooseBirth> LooseBirths()
        {
            if (looseBirths != null)
                return looseBirths;
            SortableBindingList<IDisplayLooseBirth> result = new SortableBindingList<IDisplayLooseBirth>();
            try
            {
                foreach (Individual ind in individuals)
                    CheckLooseBirth(ind, result);
            }
            catch (Exception ex)
            {
                throw new LooseDataException($"Problem calculating Loose Births. Error was {ex.Message}");
            }
            looseBirths = result;
            return result;
        }

        void CheckLooseBirth(Individual indiv, SortableBindingList<IDisplayLooseBirth> result = null)
        {
            FactDate birthDate = indiv.BirthDate;
            FactDate toAdd = null;
            if (birthDate.EndDate.Year - birthDate.StartDate.Year > 1)
            {
                FactDate baseDate = BaseLivingDate(indiv);
                DateTime minStart = baseDate.StartDate;
                DateTime minEnd = baseDate.EndDate;
                if (birthDate.EndDate != FactDate.MAXDATE && birthDate.EndDate > minEnd)
                {   // makes sure we use birth date end in event we have not enough facts
                    minEnd = birthDate.EndDate;
                    // don't think we should set the start date as max years before end as end may be wide range into future whereas start was calculated from facts
                    //if (minStart != FactDate.MINDATE && minEnd.Year > minStart.Year + FactDate.MAXYEARS)
                    //    minStart = CreateDate(minEnd.Year - FactDate.MAXYEARS, minStart.Month, minStart.Day); // min end mustn't be more than max years after start
                }
                foreach (Family fam in indiv.FamiliesAsSpouse)
                {
                    FactDate marriageDate = fam.GetPreferredFactDate(Fact.MARRIAGE);
                    if (marriageDate.StartDate.Year > GeneralSettings.Default.MinParentalAge && !marriageDate.IsLongYearSpan)
                    {  // set maximum birthdate as X years before earliest marriage
                        DateTime preMarriage = CreateDate(marriageDate.StartDate.Year - GeneralSettings.Default.MinParentalAge, 12, 31);
                        if (preMarriage < minEnd && preMarriage >= minStart)
                            minEnd = preMarriage;
                    }
                    if (fam.Children.Count > 0)
                    {   // must be at least X years old at birth of child
                        List<Individual> childrenNoAFT =
                            fam.Children.Filter(child => child.BirthDate.EndDate != FactDate.MAXDATE && !child.BirthDate.IsLongYearSpan).ToList();
                        if (childrenNoAFT.Count > 0)
                        {
                            int minChildYear = childrenNoAFT.Min(child => child.BirthDate.EndDate).Year;
                            DateTime minChild = CreateDate(minChildYear - GeneralSettings.Default.MinParentalAge, 12, 31);
                            if (minChild < minEnd && minChild >= minStart)
                                minEnd = minChild;
                        }
                        List<Individual> childrenNoBEF =
                            fam.Children.Filter(child => child.BirthDate.StartDate != FactDate.MINDATE && !child.BirthDate.IsLongYearSpan).ToList();
                        if (childrenNoBEF.Count > 0)
                        {
                            int maxChildYear = childrenNoBEF.Max(child => child.BirthDate.StartDate).Year;
                            DateTime maxChild;
                            if (indiv.IsMale) // for males check that not over 100 when oldest child is born
                                maxChild = CreateDate(maxChildYear - 100, 1, 1);
                            else // for females check that not over 60 when oldest child is born
                                maxChild = CreateDate(maxChildYear - 60, 1, 1);
                            if (maxChild > minStart)
                                minStart = maxChild;
                        }
                    }
                    Individual spouse = fam.Spouse(indiv);
                    if (spouse != null && spouse.DeathDate.IsKnown)
                    {
                        DateTime maxMarried = CreateDate(spouse.DeathEnd.Year - GeneralSettings.Default.MinParentalAge, 12, 31);
                        if (maxMarried < minEnd && maxMarried >= minStart)
                            minEnd = maxMarried;
                    }
                }
                foreach (ParentalRelationship parents in indiv.FamiliesAsChild)
                {  // check min date at least X years after parent born and no later than parent dies
                    Family fam = parents.Family;
                    if (fam.Husband != null)
                    {
                        if (fam.Husband.BirthDate.IsKnown && fam.Husband.BirthDate.StartDate != FactDate.MINDATE)
                            if (fam.Husband.BirthDate.StartDate.TryAddYears(GeneralSettings.Default.MinParentalAge) > minStart)
                                minStart = CreateDate(fam.Husband.BirthDate.StartDate.Year + GeneralSettings.Default.MinParentalAge, 1, 1);
                        if (fam.Husband.DeathDate.IsKnown && fam.Husband.DeathDate.EndDate != FactDate.MAXDATE)
                            if (fam.Husband.DeathDate.EndDate.Year != FactDate.MAXDATE.Year && fam.Husband.DeathDate.EndDate.AddMonths(9) < minEnd)
                                minEnd = CreateDate(fam.Husband.DeathDate.EndDate.AddMonths(9).Year, 1, 1);
                    }
                    if (fam.Wife != null)
                    {
                        if (fam.Wife.BirthDate.IsKnown && fam.Wife.BirthDate.StartDate != FactDate.MINDATE)
                            if (fam.Wife.BirthDate.StartDate.TryAddYears(GeneralSettings.Default.MinParentalAge) > minStart)
                                minStart = CreateDate(fam.Wife.BirthDate.StartDate.Year + GeneralSettings.Default.MinParentalAge, 1, 1);
                        if (fam.Wife.DeathDate.IsKnown && fam.Wife.DeathDate.EndDate != FactDate.MAXDATE)
                            if (fam.Wife.DeathDate.EndDate.Year != FactDate.MAXDATE.Year && fam.Wife.DeathDate.EndDate < minEnd)
                                minEnd = CreateDate(fam.Wife.DeathDate.EndDate.Year, 1, 1);
                    }
                }
                if (birthDate.EndDate <= minEnd && birthDate.EndDate != FactDate.MAXDATE)
                {  // check for BEF XXXX types that are prevalent in my tree
                    if (birthDate.StartDate == FactDate.MINDATE && birthDate.EndDate.TryAddYears(1) <= minEnd)
                        minEnd = birthDate.EndDate.TryAddYears(1);
                    else
                        minEnd = birthDate.EndDate;
                }
                if (birthDate.StartDate > minStart)
                    minStart = birthDate.StartDate;
                // force min & max years with odd dates to be min & max dates
                if (minEnd.Year == FactDate.MAXDATE.Year && minEnd != FactDate.MAXDATE)
                    minEnd = FactDate.MAXDATE;
                if (minStart.Year == 1 && minStart != FactDate.MINDATE)
                    minStart = FactDate.MINDATE;
                if (minEnd.Month == 1 && minEnd.Day == 1 && birthDate.EndDate.Month == 12 && birthDate.EndDate.Day == 31)
                    minEnd = minEnd.TryAddYears(1).AddDays(-1); // year has rounded to 1st Jan when was upper year.
                baseDate = new FactDate(minStart, minEnd);
                if (birthDate != baseDate)
                    toAdd = baseDate;
            }
            if (toAdd != null && toAdd != birthDate && toAdd.DistanceSquared(birthDate) > 1)
            {
                // we have a date to change and its not the same 
                // range as the existing death date
                Fact looseBirth = new Fact(indiv.IndividualID, Fact.LOOSEBIRTH, toAdd, FactLocation.UNKNOWN_LOCATION);
                indiv.AddFact(looseBirth);
                if (result != null)
                    result.Add(indiv);
            }
        }

        DateTime CreateDate(int year, int month, int day)
        {
            if (year > DateTime.MaxValue.Year)
                year = DateTime.MaxValue.Year;
            if (year < 1)
                year = 1;
            if (month > 12)
                month = 12;
            if (month < 1)
                month = 1;
            if (month == 2 && day == 29 & !DateTime.IsLeapYear(year))
                day = 28;
            return new DateTime(year, month, day);
        }

        FactDate BaseLivingDate(Individual indiv)
        {
            DateTime mindate = FactDate.MAXDATE;
            DateTime maxdate = GetMaxLivingDate(indiv, Fact.LOOSE_BIRTH_FACTS);
            DateTime startdate = maxdate.Year < FactDate.MAXYEARS ? FactDate.MINDATE : CreateDate(maxdate.Year - FactDate.MAXYEARS, 1, 1);
            foreach (Fact f in indiv.AllFacts)
            {
                if (Fact.LOOSE_BIRTH_FACTS.Contains(f.FactType))
                {
                    if (f.FactDate.IsKnown && (!Fact.IGNORE_LONG_RANGE.Contains(f.FactType) || !f.FactDate.IsLongYearSpan))
                    {  // don't consider long year span marriage or children facts
                        if (f.FactDate.StartDate != FactDate.MINDATE && f.FactDate.StartDate < mindate)
                            mindate = f.FactDate.StartDate;
                        if (f.FactDate.EndDate != FactDate.MAXDATE && f.FactDate.EndDate < mindate) //copes with BEF dates
                            mindate = f.FactDate.EndDate;
                    }
                }
            }
            if (startdate.Year != 1 && startdate.Year != FactDate.MAXDATE.Year && startdate < mindate)
                return new FactDate(startdate, mindate);
            if (mindate.Year != 1 && mindate.Year != FactDate.MAXDATE.Year && mindate <= maxdate)
                return new FactDate(mindate, maxdate);
            return FactDate.UNKNOWN_DATE;
        }

        #endregion

        #region Loose Deaths

        public SortableBindingList<IDisplayLooseDeath> LooseDeaths()
        {
            if (looseDeaths != null)
                return looseDeaths;
            SortableBindingList<IDisplayLooseDeath> result = new SortableBindingList<IDisplayLooseDeath>();
            try
            {
                foreach (Individual ind in individuals)
                    CheckLooseDeath(ind, result);
            }
            catch (Exception ex)
            {
                throw new LooseDataException($"Problem calculating Loose Deaths. Error was {ex.Message}");
            }
            looseDeaths = result;
            return result;
        }

        void CheckLooseDeath(Individual indiv, SortableBindingList<IDisplayLooseDeath> result = null)
        {
            FactDate deathDate = indiv.DeathDate;
            FactDate toAdd = null;
            if (deathDate.EndDate.Year - deathDate.StartDate.Year > 1)
            {
                DateTime maxLiving = GetMaxLivingDate(indiv, Fact.LOOSE_DEATH_FACTS);
                DateTime minDeath = GetMinDeathDate(indiv);
                if (minDeath != FactDate.MAXDATE)
                {   // we don't have a minimum death date so can't proceed - individual may still be alive
                    if (maxLiving > deathDate.StartDate)
                    {
                        // the starting death date is before the last alive date
                        // so add to the list of loose deaths
                        if (minDeath < deathDate.EndDate)
                            toAdd = new FactDate(maxLiving, minDeath);
                        else if (deathDate.DateType == FactDate.FactDateType.BEF && minDeath != FactDate.MAXDATE
                              && deathDate.EndDate != FactDate.MAXDATE
                              && deathDate.EndDate.TryAddYears(1) == minDeath)
                            toAdd = new FactDate(maxLiving, minDeath);
                        else
                            toAdd = new FactDate(maxLiving, deathDate.EndDate);
                    }
                    else if (minDeath < deathDate.EndDate)
                    {
                        // earliest death date before current latest death
                        // or they were two BEF dates 
                        // so add to the list of loose deaths
                        toAdd = new FactDate(deathDate.StartDate, minDeath);
                    }
                }
            }
            if (toAdd != null && toAdd != deathDate && toAdd.DistanceSquared(deathDate) > 1)
            {
                // we have a date to change and its not the same 
                // range as the existing death date
                Fact looseDeath = new Fact(indiv.IndividualID, Fact.LOOSEDEATH, toAdd, FactLocation.UNKNOWN_LOCATION);
                indiv.AddFact(looseDeath);
                if (result != null)
                    result.Add(indiv);
            }
        }

        DateTime GetMaxLivingDate(Individual indiv, ISet<string> factTypes)
        {
            DateTime maxdate = FactDate.MINDATE;
            // having got the families the individual is a parent of
            // get the max startdate of the birthdate of the youngest child
            // this then is the minimum point they were alive
            // subtract 9 months for a male
            bool childDate = false;
            foreach (Family fam in indiv.FamiliesAsSpouse)
            {
                FactDate marriageDate = fam.GetPreferredFactDate(Fact.MARRIAGE);
                if (marriageDate.StartDate > maxdate && !marriageDate.IsLongYearSpan)
                    maxdate = marriageDate.StartDate;
                List<Individual> childrenNoLongSpan = fam.Children.Filter(child => !child.BirthDate.IsLongYearSpan).ToList<Individual>();
                if (childrenNoLongSpan.Count > 0)
                {
                    DateTime maxChildBirthDate = childrenNoLongSpan.Max(child => child.BirthDate.StartDate);
                    if (maxChildBirthDate > maxdate)
                    {
                        maxdate = maxChildBirthDate;
                        childDate = true;
                    }
                }
            }
            if (childDate && indiv.IsMale && maxdate > FactDate.MINDATE.AddMonths(9))
            {
                // set to 9 months before birth if indiv is a father 
                // and we have changed maxdate from the MINDATE default
                // and the date is derived from a child not a marriage
                maxdate = maxdate.AddMonths(-9);
                // now set to Jan 1 of that year 9 months before birth to prevent 
                // very exact 9 months before dates
                maxdate = CreateDate(maxdate.Year, 1, 1);
            }
            // Check max date on all facts of facttype but don't consider long year span marriage or children facts
            foreach (Fact f in indiv.AllFacts)
                if (factTypes.Contains(f.FactType) && f.FactDate.StartDate > maxdate && (!Fact.IGNORE_LONG_RANGE.Contains(f.FactType) || !f.FactDate.IsLongYearSpan))
                    maxdate = f.FactDate.StartDate;
            // at this point we have the maximum point a person was alive
            // based on their oldest child and last living fact record and marriage date
            return maxdate;
        }

        DateTime GetMinDeathDate(Individual indiv)
        {
            FactDate deathDate = indiv.DeathDate;
            FactDate.FactDateType deathDateType = deathDate.DateType;
            FactDate.FactDateType birthDateType = indiv.BirthDate.DateType;
            DateTime minDeath = FactDate.MAXDATE;
            if (indiv.BirthDate.IsKnown && indiv.BirthDate.EndDate.Year < 9999) // filter out births where no year specified
            {
                minDeath = CreateDate(indiv.BirthDate.EndDate.Year + FactDate.MAXYEARS, 12, 31);
                if (birthDateType == FactDate.FactDateType.BEF)
                    minDeath = minDeath.TryAddYears(1);
                if (minDeath > FactDate.NOW) // 110 years after birth is after todays date so we set to ignore
                    minDeath = FactDate.MAXDATE;
            }
            FactDate burialDate = indiv.GetPreferredFactDate(Fact.BURIAL);
            if (burialDate.EndDate < minDeath)
                minDeath = burialDate.EndDate;
            if (minDeath <= deathDate.EndDate)
                return minDeath;
            if (deathDateType == FactDate.FactDateType.BEF && minDeath != FactDate.MAXDATE)
                return minDeath;
            return deathDate.EndDate;
        }

        //TODO Check Loose Marriage
        //void CheckLooseMarriage(Individual ind)
        //{

        //}

        #endregion

        #region Relationship Functions
        void ClearRelations()
        {
            foreach (Individual i in individuals)
            {
                i.RelationType = Individual.UNKNOWN;
                i.BudgieCode = string.Empty;
                i.Ahnentafel = 0;
                i.CommonAncestor = null;
                i.RelationToRoot = string.Empty;
            }
        }

        void AddToQueue(Queue<Individual> queue, IEnumerable<Individual> list)
        {
            foreach (Individual i in list)
                queue.Enqueue(i);
        }

        void AddDirectParentsToQueue(Individual indiv, Queue<Individual> queue, IProgress<string> outputText)
        {
            foreach (ParentalRelationship parents in indiv.FamiliesAsChild)
            {
                Family family = parents.Family;
                // add parents to queue
                if (family.Husband != null && parents.IsNaturalFather && indiv.RelationType == Individual.DIRECT)
                {
                    BigInteger newAhnentafel = indiv.Ahnentafel * 2;
                    if (family.Husband.RelationType != Individual.UNKNOWN && family.Husband.Ahnentafel != newAhnentafel)
                        AlreadyDirect(family.Husband, newAhnentafel, outputText);
                    else
                    {
                        family.Husband.Ahnentafel = newAhnentafel;
                        if (family.Husband.Ahnentafel > maxAhnentafel)
                            maxAhnentafel = family.Husband.Ahnentafel;
                        queue.Enqueue(family.Husband); // add to directs queue only if natural father of direct
                    }
                }

                if (family.Wife != null && parents.IsNaturalMother && indiv.RelationType == Individual.DIRECT)
                {
                    BigInteger newAhnentafel = indiv.Ahnentafel * 2 + 1;
                    if (family.Wife.RelationType != Individual.UNKNOWN && family.Wife.Ahnentafel != newAhnentafel)
                        AlreadyDirect(family.Wife, newAhnentafel, outputText);
                    else
                    {
                        family.Wife.Ahnentafel = newAhnentafel;
                        if (family.Wife.Ahnentafel > maxAhnentafel)
                            maxAhnentafel = family.Wife.Ahnentafel;
                        queue.Enqueue(family.Wife); // add to directs queue only if natural mother of direct
                    }
                }
            }
        }

        void AlreadyDirect(Individual parent, BigInteger newAhnentafel, IProgress<string> outputText)
        {
            if (GeneralSettings.Default.ShowMultiAncestors)
            {
                // Hmm interesting a direct parent who is already a direct
                //string currentRelationship = Relationship.CalculateRelationship(RootPerson, parent);
                string currentLine = Relationship.AhnentafelToString(parent.Ahnentafel);
                string newLine = Relationship.AhnentafelToString(newAhnentafel);
                if (parent.Ahnentafel > newAhnentafel)
                    parent.Ahnentafel = newAhnentafel; // set to lower line if new direct
                if (outputText != null)
                {
                    outputText.Report($"{parent.Name} detected as a direct ancestor more than once as:\n");
                    outputText.Report($"{currentLine} and as:\n");
                    outputText.Report($"{newLine}\n\n");
                }
            }
        }

        void AddParentsToQueue(Individual indiv, Queue<Individual> queue)
        {
            foreach (ParentalRelationship parents in indiv.FamiliesAsChild)
            {
                Family family = parents.Family;
                // add parents to queue
                if (family?.Husband?.RelationType == Individual.UNKNOWN)
                    queue.Enqueue(family.Husband);
                if (family?.Wife?.RelationType == Individual.UNKNOWN)
                    queue.Enqueue(family.Wife);
            }
        }

        //void AddChildrenToQueue(Individual indiv, Queue<Individual> queue, bool isRootPerson)
        //{
        //    IEnumerable<Family> parentFamilies = indiv.FamiliesAsSpouse;
        //    foreach (Family family in parentFamilies)
        //    {
        //        foreach (Individual child in family.Children)
        //        {
        //            // add child to queue
        //            if (child.RelationType == Individual.BLOOD || child.RelationType == Individual.UNKNOWN)
        //            {
        //                child.RelationType = Individual.BLOOD;
        //                child.Ahnentafel = isRootPerson ? indiv.Ahnentafel - 2 : indiv.Ahnentafel - 1;
        //                child.BudgieCode = $"-{child.Ahnentafel.ToString().PadLeft(2, '0')}c";
        //                queue.Enqueue(child);
        //            }
        //        }
        //        family.SetBudgieCode(indiv, 2);
        //    }
        //}

        public Individual RootPerson { get; set; }

        public void SetRelations(string startID, IProgress<string> outputText)
        {
            ClearRelations();
            RootPerson = GetIndividual(startID);
            if (RootPerson is null)
            {
                startID = individuals[0].IndividualID;
                RootPerson = GetIndividual(startID);
                if (RootPerson is null)
                    throw new NotFoundException("Unable to find a Root Person in the file");
            }
            Individual ind = RootPerson;
            ind.RelationType = Individual.DIRECT;
            ind.Ahnentafel = 1;
            maxAhnentafel = 1;
            var queue = new Queue<Individual>();
            queue.Enqueue(RootPerson);
            while (queue.Count > 0)
            {
                // now take an item from the queue
                ind = queue.Dequeue();
                // set them as a direct relation
                ind.RelationType = Individual.DIRECT;
                ind.CommonAncestor = new CommonAncestor(ind, 0, false); // set direct as common ancestor
                AddDirectParentsToQueue(ind, queue, outputText);
            }
            int lenAhnentafel = maxAhnentafel.ToString().Length;
            // we have now added all direct ancestors
            IEnumerable<Individual> directs = GetAllRelationsOfType(Individual.DIRECT);
            // add all direct ancestors budgie codes
            foreach (Individual i in directs)
                i.BudgieCode = (i.Ahnentafel).ToString().PadLeft(lenAhnentafel, '0') + "d";
            AddToQueue(queue, directs);
            while (queue.Count > 0)
            {
                // get the next person
                ind = queue.Dequeue();
                var parentFamilies = ind.FamiliesAsSpouse;
                foreach (Family family in parentFamilies)
                {
                    // if the spouse of a direct ancestor is not a direct
                    // ancestor then they are only related by marriage
                    family.SetSpouseRelation(ind, Individual.MARRIEDTODB);
                    // all children of direct ancestors and blood relations
                    // are blood relations
                    family.SetChildRelation(queue, Individual.BLOOD);
                    family.SetChildrenCommonRelation(ind, ind.CommonAncestor);
                    family.SetBudgieCode(ind, lenAhnentafel);
                }
            }
            // we have now set all direct ancestors and all blood relations
            // now is to loop through the marriage relations
            IEnumerable<Individual> marriedDBs = GetAllRelationsOfType(Individual.MARRIEDTODB);
            AddToQueue(queue, marriedDBs);
            while (queue.Count > 0)
            {
                // get the next person
                ind = queue.Dequeue();
                // first only process this individual if they are related by marriage or still unknown
                int relationship = ind.RelationType;
                if (relationship == Individual.MARRIAGE ||
                    relationship == Individual.MARRIEDTODB ||
                    relationship == Individual.UNKNOWN)
                {
                    // set this individual to be related by marriage
                    if (relationship == Individual.UNKNOWN)
                        ind.RelationType = Individual.MARRIAGE;
                    AddParentsToQueue(ind, queue);
                    IEnumerable<Family> parentFamilies = ind.FamiliesAsSpouse;
                    foreach (Family family in parentFamilies)
                    {
                        family.SetSpouseRelation(ind, Individual.MARRIAGE);
                        // children of relatives by marriage that we haven't previously 
                        // identified are also relatives by marriage
                        family.SetChildRelation(queue, Individual.MARRIAGE);
                    }
                }
            }
            // now anyone linked is set
            bool keepLooping = true;
            while (keepLooping)
            {
                keepLooping = false;
                IEnumerable<Family> families = AllFamilies.Where(f => f.HasUnknownRelations && f.HasLinkedRelations);
                foreach (Family f in families)
                {
                    foreach (Individual i in f.Members)
                    {
                        if (i.RelationType == Individual.UNKNOWN)
                        {
                            i.RelationType = Individual.LINKED;
                            keepLooping = true; // keep going if we set an individual
                        }
                    }
                }
            }
        }

        void SetRelationDescriptions(string startID)
        {
            IEnumerable<Individual> directs = GetAllRelationsOfType(Individual.DIRECT);
            IEnumerable<Individual> blood = GetAllRelationsOfType(Individual.BLOOD);
            IEnumerable<Individual> married = GetAllRelationsOfType(Individual.MARRIEDTODB);
            Individual rootPerson = GetIndividual(startID);
            foreach (Individual i in directs)
                i.RelationToRoot = Relationship.CalculateRelationship(rootPerson, i);
            foreach (Individual i in blood)
                i.RelationToRoot = Relationship.CalculateRelationship(rootPerson, i);
            foreach (Individual i in married)
            {
                foreach (Family f in i.FamiliesAsSpouse)
                {
                    if (i.RelationToRoot is null && f.Spouse(i) != null && f.Spouse(i).IsBloodDirect)
                    {
                        string relation = f.MaritalStatus != Family.MARRIED ? "partner" : i.IsMale ? "husband" : "wife";
                        i.RelationToRoot = $"{relation} of {f.Spouse(i).RelationToRoot}";
                        break;
                    }
                }
            }
        }

        public string PrintRelationCount()
        {
            StringBuilder sb = new StringBuilder();
            int[] relations = new int[Individual.UNSET + 1];
            foreach (Individual i in individuals)
                relations[i.RelationType]++;
            sb.Append($"Direct Ancestors: {relations[Individual.DIRECT]}\n");
            sb.Append($"Descendants: {relations[Individual.DESCENDANT]}\n");
            sb.Append($"Blood Relations: {relations[Individual.BLOOD]}\n");
            sb.Append($"Married to Blood or Direct Relation: {relations[Individual.MARRIEDTODB]}\n");
            sb.Append($"Related by Marriage: {relations[Individual.MARRIAGE]}\n");
            sb.Append($"Linked through Marriages: {relations[Individual.LINKED]}\n");
            sb.Append($"Unknown relation: {relations[Individual.UNKNOWN]}\n");
            if (relations[Individual.UNSET] > 0)
                sb.Append($"Failed to set relationship: {relations[Individual.UNSET]}\n");
            sb.Append('\n');
            return sb.ToString();
        }

        public IEnumerable<Individual> DirectLineIndividuals => AllIndividuals.Filter(i => i.RelationType == Individual.DIRECT || i.RelationType == Individual.DESCENDANT);

        #endregion

        #region Displays
        public SortableBindingList<IDisplayIndividual> AllDisplayIndividuals
        {
            get
            {
                SortableBindingList<IDisplayIndividual> result = new SortableBindingList<IDisplayIndividual>();
                foreach (IDisplayIndividual i in individuals)
                    result.Add(i);
                return result;
            }
        }

        public SortableBindingList<IDisplayFamily> AllDisplayFamilies
        {
            get
            {
                SortableBindingList<IDisplayFamily> result = new SortableBindingList<IDisplayFamily>();
                foreach (IDisplayFamily f in families)
                    result.Add(f);
                return result;
            }
        }

        public static SortableBindingList<IDisplayFact> GetSourceDisplayFacts(FactSource source)
        {
            SortableBindingList<IDisplayFact> result = new SortableBindingList<IDisplayFact>();
            foreach (Fact f in source.Facts)
            {
                if (f.Individual != null)
                {
                    DisplayFact df = new DisplayFact(f.Individual, f);
                    if (!result.ContainsFact(df))
                        result.Add(df);
                }
                else
                {
                    if (f.Family != null && f.Family.Husband != null)
                    {
                        DisplayFact df = new DisplayFact(f.Family.Husband, f);
                        if (!result.ContainsFact(df))
                            result.Add(df);
                    }
                    if (f.Family != null && f.Family.Wife != null)
                    {
                        DisplayFact df = new DisplayFact(f.Family.Wife, f);
                        if (!result.ContainsFact(df))
                            result.Add(df);
                    }
                }
            }
            return result;
        }

        public SortableBindingList<IDisplaySource> AllDisplaySources
        {
            get
            {
                var result = new SortableBindingList<IDisplaySource>();
                foreach (IDisplaySource s in sources)
                    result.Add(s);
                return result;
            }
        }

        public SortableBindingList<IDisplayOccupation> AllDisplayOccupations
        {
            get
            {
                var result = new SortableBindingList<IDisplayOccupation>();
                foreach (string occ in occupations.Keys)
                    result.Add(new DisplayOccupation(occ, occupations[occ].Count));
                return result;
            }
        }
        public SortableBindingList<IDisplayCustomFact> AllCustomFacts
        {
            get
            {
                var result = new SortableBindingList<IDisplayCustomFact>();
                foreach (string facttype in unknownFactTypes.Keys)
                {
                    bool ignore = DatabaseHelper.IgnoreCustomFact(facttype);
                    var customFact = new DisplayCustomFact(facttype, unknownFactTypes[facttype].Count, ignore);
                    result.Add(customFact);
                }
                return result;
            }
        }

        public SortableBindingList<IDisplayFact> AllDisplayFacts
        {
            get
            {
                SortableBindingList<IDisplayFact> result = new SortableBindingList<IDisplayFact>();

                foreach (Individual ind in individuals)
                {
                    foreach (Fact f in ind.PersonalFacts)
                        result.Add(new DisplayFact(ind, f));
                    foreach (Family fam in ind.FamiliesAsSpouse)
                        foreach (Fact famfact in fam.Facts)
                            result.Add(new DisplayFact(ind, famfact));
                }
                return result;
            }
        }

        public SortableBindingList<Individual> AllWorkers(string job) => new SortableBindingList<Individual>(occupations[job]);

        public SortableBindingList<Individual> AllCustomFactIndividuals(string factType) =>
            new SortableBindingList<Individual>(unknownFactTypes[factType]);

        public SortableBindingList<IDisplayFamily> PossiblyMissingChildFamilies
        {
            get
            {
                SortableBindingList<IDisplayFamily> result = new SortableBindingList<IDisplayFamily>();
                foreach (Family fam in families)
                    if (fam.EldestChild != null && fam.MarriageDate.IsKnown && fam.EldestChild.BirthDate.IsKnown &&
                      !fam.EldestChild.BirthDate.IsLongYearSpan && fam.EldestChild.BirthDate.BestYear > fam.MarriageDate.BestYear + 3)
                        result.Add(fam);
                return result;
            }
        }

        public SortableBindingList<IDisplayFamily> SingleFamilies
        {
            get
            {
                SortableBindingList<IDisplayFamily> result = new SortableBindingList<IDisplayFamily>();
                foreach (Family fam in families)
                    if (fam.FamilyType != Family.SOLOINDIVIDUAL && (fam.Husband is null || fam.Wife is null))
                        result.Add(fam);
                return result;
            }
        }

        public SortableBindingList<IDisplayIndividual> AgedOver99
        {
            get
            {
                SortableBindingList<IDisplayIndividual> result = new SortableBindingList<IDisplayIndividual>();
                foreach (Individual ind in individuals)
                {
                    int age = ind.GetMaxAge(FactDate.TODAY);
                    Console.WriteLine($"\nName: {ind.Name}: b.{ind.BirthDate} d.{ind.DeathDate} max age={age}");
                    if (ind.DeathDate.IsUnknown && age >= 99)
                        result.Add(ind);
                }
                return result;
            }
        }

        public List<IDisplayColourBMD> ColourBMD(RelationTypes relType, string surname, ComboBoxFamily family)
        {
            Predicate<Individual> filter;
            if (family is null)
            {
                filter = relType.BuildFilter<Individual>(x => x.RelationType);
                if (surname.Length > 0)
                {
                    Predicate<Individual> surnameFilter = FilterUtils.StringFilter<Individual>(x => x.Surname, surname);
                    filter = FilterUtils.AndFilter(filter, surnameFilter);
                }
            }
            else
                filter = x => family.Members.Contains(x);
            return individuals.Filter(filter).ToList<IDisplayColourBMD>();
        }

        public List<IDisplayMissingData> MissingData(RelationTypes relType, string surname, ComboBoxFamily family)
        {
            Predicate<Individual> filter;
            if (family is null)
            {
                filter = relType.BuildFilter<Individual>(x => x.RelationType);
                if (surname.Length > 0)
                {
                    Predicate<Individual> surnameFilter = FilterUtils.StringFilter<Individual>(x => x.Surname, surname);
                    filter = FilterUtils.AndFilter(filter, surnameFilter);
                }
            }
            else
                filter = x => family.Members.Contains(x);
            return individuals.Filter(filter).ToList<IDisplayMissingData>();
        }
        #endregion

        #region Data Errors

        void SetDataErrorTypes(IProgress<int> progress)
        {
            int catchCount = 0;
            int totalRecords = (individuals.Count + families.Count) / 40 + 1; //only count for 40% of progressbar
            int record = 0;
            DataErrorTypes = new List<DataErrorGroup>();
            List<DataError>[] errors = new List<DataError>[DATA_ERROR_GROUPS];
            for (int i = 0; i < DATA_ERROR_GROUPS; i++)
                errors[i] = new List<DataError>();
            // calculate error lists
            #region Individual Fact Errors
            foreach (Individual ind in AllIndividuals)
            {
                progress.Report(20 + (record++ / totalRecords));
                try
                {
                    if (ind.BaptismDate is object && ind.BaptismDate.IsKnown)
                    {
                        if (ind.BirthDate.IsAfter(ind.BaptismDate))
                        {   // if birthdate after baptism and not an approx date
                            if (ind.Birth != ColourValues.BMDColours.APPROX_DATE)
                                errors[(int)Dataerror.BIRTH_AFTER_BAPTISM].Add(new DataError((int)Dataerror.BIRTH_AFTER_BAPTISM, ind, $"Baptised/Christened {ind.BaptismDate} before born {ind.BirthDate}"));
                            else
                            {   // if it is an approx birthdate only show as error if 4 months after birthdate to fudge for quarter days
                                if (ind.BirthDate.SubtractMonths(4).IsAfter(ind.BaptismDate))
                                    errors[(int)Dataerror.BIRTH_AFTER_BAPTISM].Add(new DataError((int)Dataerror.BIRTH_AFTER_BAPTISM, ind, $"Baptised/Christened {ind.BaptismDate} before born {ind.BirthDate}"));
                            }
                        }
                    }
                    #region Death facts
                    if (ind.DeathDate.IsKnown)
                    {
                        if (ind.BirthDate.IsAfter(ind.DeathDate))
                            errors[(int)Dataerror.BIRTH_AFTER_DEATH].Add(new DataError((int)Dataerror.BIRTH_AFTER_DEATH, ind, $"Died {ind.DeathDate} before born {ind.BirthDate}"));
                        if (ind.BurialDate != null && ind.BirthDate.IsAfter(ind.BurialDate))
                            errors[(int)Dataerror.BIRTH_AFTER_DEATH].Add(new DataError((int)Dataerror.BIRTH_AFTER_DEATH, ind, $"Buried {ind.BurialDate} before born {ind.BirthDate}"));
                        if (ind.BurialDate != null && ind.BurialDate.IsBefore(ind.DeathDate) && !ind.BurialDate.Overlaps(ind.DeathDate))
                            errors[(int)Dataerror.BURIAL_BEFORE_DEATH].Add(new DataError((int)Dataerror.BURIAL_BEFORE_DEATH, ind, $"Buried {ind.BurialDate} before died {ind.DeathDate}"));
                        int minAge = ind.GetMinAge(ind.DeathDate);
                        if (minAge > FactDate.MAXYEARS)
                            errors[(int)Dataerror.AGED_MORE_THAN_110].Add(new DataError((int)Dataerror.AGED_MORE_THAN_110, ind, $"Aged over {FactDate.MAXYEARS} before died {ind.DeathDate}"));
                        if (ind.IsFlaggedAsLiving)
                            errors[(int)Dataerror.LIVING_WITH_DEATH_DATE].Add(new DataError((int)Dataerror.LIVING_WITH_DEATH_DATE, ind, $"Flagged as living but has death date of {ind.DeathDate}"));
                    }
                    #endregion
                    #region Error facts
                    foreach (Fact f in ind.ErrorFacts)
                    {
                        bool added = false;
                        if (f.FactErrorNumber != 0)
                        {
                            errors[f.FactErrorNumber].Add(
                                new DataError(f.FactErrorNumber, ind, f.FactErrorMessage));
                            added = true;
                        }
                        if (f.FactErrorLevel == Fact.FactError.WARNINGALLOW && f.FactType == Fact.RESIDENCE)
                        {
                            errors[(int)Dataerror.RESIDENCE_CENSUS_DATE].Add(
                                    new DataError((int)Dataerror.RESIDENCE_CENSUS_DATE, f.FactErrorLevel, ind, f.FactErrorMessage));
                            added = true;
                        }
                        if (!added)
                            errors[(int)Dataerror.FACT_ERROR].Add(new DataError((int)Dataerror.FACT_ERROR, f.FactErrorLevel, ind, f.FactErrorMessage));
                    }
                    #endregion
                    #region All Facts
                    foreach (Fact f in ind.AllFacts)
                    {
                        if (f.FactDate.IsAfter(FactDate.TODAY))
                            errors[(int)Dataerror.FACT_IN_FUTURE].Add(
                                new DataError((int)Dataerror.FACT_IN_FUTURE, ind, $"{f} is in the future."));
                        if (FactBeforeBirth(ind, f))
                            errors[(int)Dataerror.FACTS_BEFORE_BIRTH].Add(
                                new DataError((int)Dataerror.FACTS_BEFORE_BIRTH, ind, f.FactErrorMessage));
                        if (FactAfterDeath(ind, f))
                            errors[(int)Dataerror.FACTS_AFTER_DEATH].Add(
                                new DataError((int)Dataerror.FACTS_AFTER_DEATH, ind, f.FactErrorMessage));
                        if (!GeneralSettings.Default.IgnoreFactTypeWarnings)
                        {
                            foreach (string tag in unknownFactTypes.Keys)
                            {
                                if (f.FactTypeDescription == tag)
                                {
                                    errors[(int)Dataerror.UNKNOWN_FACT_TYPE].Add(
                                        new DataError((int)Dataerror.UNKNOWN_FACT_TYPE, Fact.FactError.QUESTIONABLE,
                                            ind, $"Unknown/Custom fact type {f.FactTypeDescription} recorded"));
                                }
                            }
                        }
                    }
                    #region Duplicate Fact Check
                    var dup = ind.AllFileFacts.GroupBy(x => x.EqualHash).Where(g => g.Count() > 1).Select(y => y.Key).ToList();
                    var dupList = new List<Fact>();
                    foreach (string dfs in dup)
                    {
                        var df = ind.AllFacts.First(x => x.EqualHash.Equals(dfs));
                        if (df != null)
                        {
                            dupList.Add(df);
                            errors[(int)Dataerror.DUPLICATE_FACT].Add(
                                            new DataError((int)Dataerror.DUPLICATE_FACT, Fact.FactError.ERROR,
                                                ind, $"Duplicated {df.FactTypeDescription} fact recorded"));
                        }
                    }
                    var possDuplicates = ind.AllFileFacts.GroupBy(x => x.PossiblyEqualHash).Where(g => g.Count() > 1).Select(y => y.Key).ToList();
                    foreach (string pd in possDuplicates)
                    {
                        var pdf = ind.AllFacts.First(x => x.PossiblyEqualHash.Equals(pd));
                        if (pdf != null && !dupList.ContainsFact(pdf))
                        {
                            errors[(int)Dataerror.POSSIBLE_DUPLICATE_FACT].Add(
                                            new DataError((int)Dataerror.POSSIBLE_DUPLICATE_FACT, Fact.FactError.QUESTIONABLE,
                                                ind, $"Possibly duplicated {pdf.FactTypeDescription} fact recorded"));
                        }
                    }
                    #endregion
                    #endregion
                    #region Parents Facts
                    foreach (ParentalRelationship parents in ind.FamiliesAsChild)
                    {
                        Family asChild = parents.Family;
                        Individual father = asChild.Husband;
                        if (father != null && ind.BirthDate.StartDate.Year != 1 && parents.IsNaturalFather)
                        {
                            int minAge = father.GetMinAge(ind.BirthDate);
                            int maxAge = father.GetMaxAge(ind.BirthDate);
                            if (minAge > 90)
                                errors[(int)Dataerror.BIRTH_AFTER_FATHER_90].Add(new DataError((int)Dataerror.BIRTH_AFTER_FATHER_90, ind, $"Father {father.Name} born {father.BirthDate} is more than 90 yrs old when individual was born"));
                            if (maxAge < 13)
                                errors[(int)Dataerror.BIRTH_BEFORE_FATHER_13].Add(new DataError((int)Dataerror.BIRTH_BEFORE_FATHER_13, ind, $"Father {father.Name} born {father.BirthDate} is less than 13 yrs old when individual was born"));
                            if (father.DeathDate.IsKnown && ind.BirthDate.IsKnown)
                            {
                                FactDate conception = ind.BirthDate.SubtractMonths(9);
                                if (father.DeathDate.IsBefore(conception))
                                    errors[(int)Dataerror.BIRTH_AFTER_FATHER_DEATH].Add(new DataError((int)Dataerror.BIRTH_AFTER_FATHER_DEATH, ind, $"Father {father.Name} died {father.DeathDate} more than 9 months before individual was born"));
                            }
                        }
                        Individual mother = asChild.Wife;
                        if (mother != null && ind.BirthDate.StartDate.Year != 1 && parents.IsNaturalMother)
                        {
                            int minAge = mother.GetMinAge(ind.BirthDate);
                            int maxAge = mother.GetMaxAge(ind.BirthDate);
                            if (minAge > 60)
                                errors[(int)Dataerror.BIRTH_AFTER_MOTHER_60].Add(new DataError((int)Dataerror.BIRTH_AFTER_MOTHER_60, ind, $"Mother {mother.Name} born {mother.BirthDate} is more than 60 yrs old when individual was born"));
                            if (maxAge < 13)
                                errors[(int)Dataerror.BIRTH_BEFORE_MOTHER_13].Add(new DataError((int)Dataerror.BIRTH_BEFORE_MOTHER_13, ind, $"Mother {mother.Name} born {mother.BirthDate} is less than 13 yrs old when individual was born"));
                            if (mother.DeathDate.IsKnown && mother.DeathDate.IsBefore(ind.BirthDate))
                                errors[(int)Dataerror.BIRTH_AFTER_MOTHER_DEATH].Add(new DataError((int)Dataerror.BIRTH_AFTER_MOTHER_DEATH, ind, $"Mother {mother.Name} died {mother.DeathDate} which is before individual was born"));
                        }
                    }
                    List<Individual> womansChildren = new List<Individual>();
                    foreach (Family asParent in ind.FamiliesAsSpouse)
                    {
                        Individual spouse = asParent.Spouse(ind);
                        if (asParent.MarriageDate != null && spouse != null)
                        {
                            if (ind.DeathDate != null && asParent.MarriageDate.IsAfter(ind.DeathDate))
                                errors[(int)Dataerror.MARRIAGE_AFTER_DEATH].Add(new DataError((int)Dataerror.MARRIAGE_AFTER_DEATH, ind, $"Marriage to {spouse.Name} in {asParent.MarriageDate} is after individual died on {ind.DeathDate}"));
                            if (spouse.DeathDate != null && asParent.MarriageDate.IsAfter(spouse.DeathDate))
                                errors[(int)Dataerror.MARRIAGE_AFTER_SPOUSE_DEAD].Add(new DataError((int)Dataerror.MARRIAGE_AFTER_SPOUSE_DEAD, ind, $"Marriage to {spouse.Name} in {asParent.MarriageDate} is after spouse died {spouse.DeathDate}"));
                            int maxAge = ind.GetMaxAge(asParent.MarriageDate);
                            if (maxAge < 13 && ind.BirthDate.IsAfter(FactDate.MARRIAGE_LESS_THAN_13))
                                errors[(int)Dataerror.MARRIAGE_BEFORE_13].Add(new DataError((int)Dataerror.MARRIAGE_BEFORE_13, ind, $"Marriage to {spouse.Name} in {asParent.MarriageDate} is before individual was 13 years old"));
                            maxAge = spouse.GetMaxAge(asParent.MarriageDate);
                            if (maxAge < 13 && spouse.BirthDate.IsAfter(FactDate.MARRIAGE_LESS_THAN_13))
                                errors[(int)Dataerror.MARRIAGE_BEFORE_SPOUSE_13].Add(new DataError((int)Dataerror.MARRIAGE_BEFORE_SPOUSE_13, ind, $"Marriage to {spouse.Name} in {asParent.MarriageDate} is before spouse born {spouse.BirthDate} was 13 years old"));
                            if (ind.Surname == spouse.Surname)
                            {
                                Individual wifesFather = ind.IsMale ? spouse.NaturalFather : ind.NaturalFather;
                                Individual husband = ind.IsMale ? ind : spouse;
                                if (husband.Surname != wifesFather?.Surname) // if couple have same surname and wife is different from her natural father then likely error
                                    errors[(int)Dataerror.SAME_SURNAME_COUPLE].Add(new DataError((int)Dataerror.SAME_SURNAME_COUPLE, ind, $"Spouse {spouse.Name} has same surname. Usually due to wife incorrectly recorded with married instead of maiden name."));
                            }
                            //if (ind.FirstMarriage != null && ind.FirstMarriage.MarriageDate != null)
                            //{
                            //    if (asParent.MarriageDate.isAfter(ind.FirstMarriage.MarriageDate))
                            //    {  // we have a later marriage now see if first marriage spouse is still alive

                            //    }
                            //}
                        }
                        if (!ind.IsMale) // for females as parent in family check children
                        {
                            womansChildren.AddRange(asParent.Children.Where(c => c.IsNaturalChildOf(ind)));
                        }
                    }
                    womansChildren = womansChildren.Distinct().ToList(); // eliminate duplicate children
                    if (womansChildren.Count > 1) // only bother checking if we have two or more children.
                    {
                        womansChildren.Sort(new BirthDateComparer());
                        FactDate previousBirth = ind.BirthDate;  // set start date to womans birth date.
                        foreach (Individual child in womansChildren)
                        {
                            if (child.IsBirthKnown)
                            {
                                double daysDiff = child.BirthDate.DaysDifference(previousBirth);
                                if (daysDiff >= 10 && daysDiff <= 168)
                                    errors[(int)Dataerror.SIBLING_TOO_SOON].Add(new DataError((int)Dataerror.SIBLING_TOO_SOON, Fact.FactError.ERROR, child, $"Child {child.Name} of {ind.Name} born too soon, only {daysDiff} days after sibling."));
                                if (daysDiff > 168 && daysDiff < 365)
                                    errors[(int)Dataerror.SIBLING_PROB_TOO_SOON].Add(new DataError((int)Dataerror.SIBLING_PROB_TOO_SOON, Fact.FactError.QUESTIONABLE, ind, $"Child {child.Name} of {ind.Name} born very soon after sibling, only {daysDiff} days later."));
                            }
                        }
                    }
                    #endregion
                }
#if __MACOS__ || __IOS__
                catch (Exception)
                {
                    catchCount++;
                }
#else
                catch (Exception e)
                {
                    if (catchCount == 0) // prevent multiple displays of the same error - usually resource icon load failures
                    {
                        ErrorHandler.Show("FTA_0001", e);
                        catchCount++;
                    }
                }
#endif
            }
            #endregion
            #region Family Fact Errors
            catchCount = 0;
            foreach (Family fam in AllFamilies)
            {
                progress.Report(20 + (record++ / totalRecords));
                try
                {
                    foreach (Fact f in fam.Facts)
                    {
                        if (f.FactErrorLevel == Fact.FactError.ERROR)
                        {
                            if (f.FactType == Fact.CHILDREN1911)
                                errors[(int)Dataerror.CHILDRENSTATUS_TOTAL_MISMATCH].Add(
                                    new DataError((int)Dataerror.CHILDRENSTATUS_TOTAL_MISMATCH, fam, f.FactErrorMessage));
                            else
                                errors[(int)Dataerror.FACT_ERROR].Add(
                                    new DataError((int)Dataerror.FACT_ERROR, fam, f.FactErrorMessage));
                        }
                    }
                }
                catch (Exception)
                {
                    if (catchCount == 0) // prevent multiple displays of the same error - usually resource icon load failures
                        catchCount++;
                }
            }
            #endregion

            for (int i = 0; i < DATA_ERROR_GROUPS; i++)
                DataErrorTypes.Add(new DataErrorGroup(i, errors[i]));
        }

        public IList<DataErrorGroup> DataErrorTypes { get; private set; }

        public static bool FactBeforeBirth(Individual ind, Fact f)
        {
            if (ind is null || f is null) return false;
            if (f.FactType != Fact.BIRTH & f.FactType != Fact.BIRTH_CALC && Fact.LOOSE_BIRTH_FACTS.Contains(f.FactType) && f.FactDate.IsBefore(ind.BirthDate))
            {
                if (f.FactType == Fact.CHRISTENING || f.FactType == Fact.BAPTISM)
                {  //due to possible late birth abt qtr reporting use 3 month fudge factor for bapm/chr
                    if (f.FactDate.IsBefore(ind.BirthDate.SubtractMonths(4)))
                        return true;
                }
                else
                    return true;
            }
            return false;
        }

        public static bool FactAfterDeath(Individual ind, Fact f) => Fact.LOOSE_DEATH_FACTS.Contains(f.FactType) && f.FactDate.IsAfter(ind.DeathDate);
        public enum Dataerror
        {
            BIRTH_AFTER_BAPTISM = 0, BIRTH_AFTER_DEATH = 1, BIRTH_AFTER_FATHER_90 = 2, BIRTH_AFTER_MOTHER_60 = 3, BIRTH_AFTER_MOTHER_DEATH = 4,
            BIRTH_AFTER_FATHER_DEATH = 5, BIRTH_BEFORE_FATHER_13 = 6, BIRTH_BEFORE_MOTHER_13 = 7, BURIAL_BEFORE_DEATH = 8,
            AGED_MORE_THAN_110 = 9, FACTS_BEFORE_BIRTH = 10, FACTS_AFTER_DEATH = 11, MARRIAGE_AFTER_DEATH = 12,
            MARRIAGE_AFTER_SPOUSE_DEAD = 13, MARRIAGE_BEFORE_13 = 14, MARRIAGE_BEFORE_SPOUSE_13 = 15, LOST_COUSINS_NON_CENSUS = 16,
            LOST_COUSINS_NOT_SUPPORTED_YEAR = 17, RESIDENCE_CENSUS_DATE = 18, CENSUS_COVERAGE = 19, FACT_ERROR = 20,
            UNKNOWN_FACT_TYPE = 21, LIVING_WITH_DEATH_DATE = 22, CHILDRENSTATUS_TOTAL_MISMATCH = 23, DUPLICATE_FACT = 24,
            POSSIBLE_DUPLICATE_FACT = 25, NATREG1939_INEXACT_BIRTHDATE = 26, MALE_WIFE_FEMALE_HUSBAND = 27,
            SAME_SURNAME_COUPLE = 28, SIBLING_TOO_SOON = 29, SIBLING_PROB_TOO_SOON = 30, FACT_IN_FUTURE = 31
        };

        #endregion

        #region Census Searching

        public static string ProviderName(int censusProvider)
        {
            switch (censusProvider)
            {
                case 0:
                    return "Ancestry";
                case 1:
                    return "FindMyPast";
                case 2:
                    return "FreeCen";
                case 3:
                    return "FamilySearch";
                case 4:
                    return "ScotlandsPeople";
                default:
                    return "FamilySearch";
            }
        }

        #endregion

        #region Birth/Marriage/Death Searching

        public enum SearchType { BIRTH = 0, MARRIAGE = 1, DEATH = 2 };

        static string RecordCountry(SearchType st, Individual individual, FactDate factdate)
        {
            string record_country = Countries.UNKNOWN_COUNTRY;
            if (Countries.IsKnownCountry(individual.BirthLocation.Country))
                record_country = individual.BirthLocation.Country;
            if (st.Equals(SearchType.DEATH) && Countries.IsKnownCountry(individual.DeathLocation.Country))
                record_country = individual.DeathLocation.Country;
            return record_country;
        }

        static string GetSurname(SearchType st, Individual individual, bool ancestry)
        {
            string surname = string.Empty;
            if (individual is null) return surname;
            if (individual.Surname != "?" && individual.Surname.ToUpper() != Individual.UNKNOWN_NAME)
                surname = individual.Surname;
            if (st.Equals(SearchType.DEATH) && individual.MarriedName != "?" && individual.MarriedName.ToUpper() != Individual.UNKNOWN_NAME && individual.MarriedName != individual.Surname)
                surname = ancestry ? $"{surname} {individual.MarriedName}" : individual.MarriedName; // for ancestry combine names for others sites just use marriedName if death search
            surname = surname.Trim();
            return surname;
        }

        #endregion

        #region Relationship Groups
        public static List<Individual> GetFamily(Individual startIndividiual)
        {
            List<Individual> results = new List<Individual>();
            if (startIndividiual is object) // checks not null
            {
                foreach (Family f in startIndividiual.FamiliesAsSpouse)
                {
                    foreach (Individual i in f.Members)
                        results.Add(i);
                }
                foreach (ParentalRelationship pr in startIndividiual.FamiliesAsChild)
                {
                    foreach (Individual i in pr.Family.Members)
                        results.Add(i);
                }
            }
            return results;
        }

        public static List<Individual> GetAncestors(Individual startIndividual)
        {
            List<Individual> results = new List<Individual>();
            Queue<Individual> queue = new Queue<Individual>();
            results.Add(startIndividual);
            queue.Enqueue(startIndividual);
            while (queue.Count > 0)
            {
                Individual ind = queue.Dequeue();
                foreach (ParentalRelationship parents in ind.FamiliesAsChild)
                {
                    if (parents.IsNaturalFather)
                    {
                        queue.Enqueue(parents.Father);
                        results.Add(parents.Father);
                    }
                    if (parents.IsNaturalMother)
                    {
                        queue.Enqueue(parents.Mother);
                        results.Add(parents.Mother);
                    }
                }
            }
            return results;
        }

        public static List<Individual> GetDescendants(Individual startIndividual)
        {
            List<Individual> results = new List<Individual>();
            Dictionary<string, Individual> processed = new Dictionary<string, Individual>();
            Queue<Individual> queue = new Queue<Individual>();
            results.Add(startIndividual);
            queue.Enqueue(startIndividual);
            while (queue.Count > 0)
            {
                Individual parent = queue.Dequeue();
                processed.Add(parent.IndividualID, parent);
                foreach (Family f in parent.FamiliesAsSpouse)
                {
                    Individual spouse = f.Spouse(parent);
                    if (spouse != null && !processed.ContainsKey(spouse.IndividualID))
                    {
                        queue.Enqueue(spouse);
                        results.Add(spouse);
                    }
                    foreach (Individual child in f.Children)
                    {
                        // we have a child and we have a parent check if natural child
                        if (!processed.ContainsKey(child.IndividualID) && child.IsNaturalChildOf(parent))
                        {
                            queue.Enqueue(child);
                            results.Add(child);
                        }
                    }
                }
            }
            return results;
        }

        public static List<Individual> GetAllRelations(Individual ind) => GetFamily(ind).Union(GetAncestors(ind).Union(GetDescendants(ind))).ToList();
        #endregion

        #region Duplicates Processing
        long totalComparisons;
        long maxComparisons;
        long duplicatesFound;
        int currentPercentage;

        public async Task<SortableBindingList<IDisplayDuplicateIndividual>> GenerateDuplicatesList(int value, IProgress<int> progress, IProgress<string> progressText, IProgress<int> maximum, CancellationToken ct)
        {
            if (duplicates != null)
            {
                maximum.Report(MaxDuplicateScore());
                return BuildDuplicateList(value, progress, progressText); // we have already processed the duplicates since the file was loaded
            }
            var groups = individuals.Where(x => x.Name != Individual.UNKNOWN_NAME).GroupBy(x => x.SurnameMetaphone).Select(x => x.ToList()).ToList();
            int numgroups = groups.Count;
            progress.Report(0);
            totalComparisons = 0;
            maxComparisons = groups.Sum(x => x.Count * (x.Count - 1L) / 2);
            currentPercentage = 0;
            duplicatesFound = 0;
            buildDuplicates = new ConcurrentBag<DuplicateIndividual>();
            var tasks = new List<Task>();
            try
            {
                foreach (var group in groups)
                {
                    var task = Task.Run(() => IdentifyDuplicates(group, ct), ct);
                    tasks.Add(task);
                }
                var progressTask = Task.Run(() => ProgressReporter(progress, progressText, ct), ct);
                tasks.Add(progressTask);
                await Task.WhenAll(tasks).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                progress.Report(0); // if user cancels then simply clear progress do not throw away work done
            }
            catch (Exception e)
            {
                UIHelpers.ShowMessage($"Duplicate report encountered a problem. Message was: {e.Message}");
            }
            try
            {
                duplicates = new SortableBindingList<DuplicateIndividual>(buildDuplicates.ToList());
                maximum.Report(MaxDuplicateScore());
                DeserializeNonDuplicates();
                return BuildDuplicateList(value, progress, progressText);
            }
            catch (Exception e)
            {
                UIHelpers.ShowMessage($"Duplicate report encountered a problem. Message was: {e.Message}");
            }
            return null;
        }

        int MaxDuplicateScore()
        {
            int score = 0;
            foreach (DuplicateIndividual dup in buildDuplicates)
            {
                if (dup != null && dup.Score > score)
                    score = dup.Score;
            }
            return score;
        }

        void IdentifyDuplicates(IList<Individual> list, CancellationToken ct)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var indA = list[i];
                for (var j = i + 1; j < list.Count; j++)
                {
                    var indB = list[j];
                    if ((indA.ForenameMetaphone.Equals(indB.ForenameMetaphone) || indA.StandardisedName.Equals(indB.StandardisedName)) &&
                       indA.BirthDate.DistanceSquared(indB.BirthDate) < 5)
                    {
                        var test = new DuplicateIndividual(indA, indB);
                        if (test.Score > 0)
                        {
                            buildDuplicates.Add(test);
                            Interlocked.Increment(ref duplicatesFound);
                        }
                    }
                    Interlocked.Increment(ref totalComparisons);
                    if (ct.IsCancellationRequested)
                        return;
                }
            }
        }

        void ProgressReporter(IProgress<int> progress, IProgress<string> progressText, CancellationToken ct)
        {
            while (totalComparisons < maxComparisons && currentPercentage < 100)
            {
                Task.Delay(1000);
                ct.ThrowIfCancellationRequested();
                var val = (int)(100 * totalComparisons / maxComparisons);
                if (val > currentPercentage)
                {
                    currentPercentage = val;
                    if (val < 100)
                        progressText.Report($"Done {totalComparisons:N0} of {maxComparisons:N0} - {val}%\nFound {duplicatesFound:N0} possible duplicates");
                    else
                        progressText.Report($"Completed {duplicatesFound:N0} possible duplicates found. Preparing display.");
                    progress.Report(val);
                }
            }
        }

        public SortableBindingList<IDisplayDuplicateIndividual> BuildDuplicateList(int minScore, IProgress<int> progress, IProgress<string> progressText)
        {
            var select = new SortableBindingList<IDisplayDuplicateIndividual>();
            long numDuplicates = duplicates.Count;
            long numProcessed = 0;
            currentPercentage = 0;
            progress.Report(0);
            progressText.Report("Preparing Display");
            if (NonDuplicates is null)
                DeserializeNonDuplicates();
            foreach (DuplicateIndividual dup in duplicates)
            {
                if (dup.Score >= minScore)
                {
                    var dispDup = new DisplayDuplicateIndividual(dup);
                    var toCheck = new NonDuplicate(dispDup);
                    dispDup.IgnoreNonDuplicate = NonDuplicates.ContainsDuplicate(toCheck);
                    if (!(GeneralSettings.Default.HideIgnoredDuplicates && dispDup.IgnoreNonDuplicate))
                        select.Add(dispDup);
                }
                numProcessed++;
                if (numProcessed % 20 == 0)
                {
                    var val = (int)(100 * numProcessed / numDuplicates);
                    if (val > currentPercentage)
                    {
                        currentPercentage = val;
                        progressText.Report($"Preparing Display. {numProcessed:N0} of {numDuplicates:N0} - {val}%");
                        progress.Report(val);
                    }
                }
            }
            progressText.Report("Prepared records. Sorting - Please wait");
            return select;
        }

        public void SerializeNonDuplicates()
        {
            try
            {
                IFormatter formatter = new BinaryFormatter();
                string file = Path.Combine(GeneralSettings.Default.SavePath, "NonDuplicates.xml");
                using (Stream stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    formatter.Serialize(stream, NonDuplicates);
                }
            }
            catch
            {
                // log.Error($"Error {e.Message} writing NonDuplicates.xml");
            }
        }

        public void DeserializeNonDuplicates()
        {
            // log.Debug("FamilyTree.DeserializeNonDuplicates");
            try
            {
                IFormatter formatter = new BinaryFormatter();
                string file = Path.Combine(GeneralSettings.Default.SavePath, "NonDuplicates.xml");
                if (File.Exists(file))
                {
                    using (Stream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        NonDuplicates = (List<NonDuplicate>)formatter.Deserialize(stream);
                    }
                }
                else
                    NonDuplicates = new List<NonDuplicate>();
            }
            catch
            {
                //  log.Error("Error " + e.Message + " reading NonDuplicates.xml");
                NonDuplicates = new List<NonDuplicate>();
            }
        }
        #endregion

        #region Report Issues
        public static void WriteUnrecognisedReferencesFile(IEnumerable<string> unrecognisedResults, IEnumerable<string> missingResults, IEnumerable<string> notesResults, string filename)
        {
            using (StreamWriter output = new StreamWriter(new FileStream(filename, FileMode.Create, FileAccess.Write), Encoding.UTF8))
            {
                int count = 0;
                output.WriteLine("Note the counts on the loading page may not match the counts in the file as duplicates not written out each time\n");
                if (unrecognisedResults.Any())
                {
                    output.WriteLine("Census fact details where a Census reference was expected but went unrecognised");
                    unrecognisedResults = unrecognisedResults.OrderBy(x => x.ToString());
                    foreach (string line in unrecognisedResults)
                        output.WriteLine($"{++count}: {line}");
                }
                if (missingResults.Any())
                {
                    count = 0;
                    output.WriteLine("\n\nCensus fact details where a Census Reference was missing or not detected");
                    missingResults = missingResults.OrderBy(x => x.ToString());
                    foreach (string line in missingResults)
                        output.WriteLine($"{++count}: {line}");
                }
                if (notesResults.Any())
                {
                    count = 0;
                    output.WriteLine("\n\nNotes with no census recognised references\nThese are usually NOT census references and are included in case there are some that got missed");
                    notesResults = notesResults.OrderBy(x => x.ToString());
                    foreach (string line in notesResults)
                        output.WriteLine($"{++count}: {line}");
                }
            }
        }

        #endregion
    }
}
