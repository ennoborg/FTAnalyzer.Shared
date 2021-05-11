namespace FTAnalyzer
{
    public class ExportReferrals : IExportReferrals
    {
        readonly Individual Individual;
        readonly Fact Fact;

        public ExportReferrals(Individual ind, Fact f)
        {
            Individual = ind;
            Fact = f;
        }

        public string IndividualID => Individual.IndividualID;
        public string FamilyID => Individual.ReferralFamilyID;
        public string Forenames => Individual.Forenames;
        public string Surname => Individual.Surname;
        public Age Age => Individual.GetAge(Fact.FactDate);
        public bool Include => Individual.IsBloodDirect;

        public string RelationType
        {
            get
            {
                if (Individual.RelationType == Individual.DIRECT)
                    return "Direct Ancestor";
                if (Individual.RelationType == Individual.BLOOD)
                    return "Blood Relation";
                if (Individual.RelationType == Individual.MARRIEDTODB || Individual.RelationType == Individual.MARRIAGE)
                    return "Marriage";
                if (Individual.RelationType == Individual.DESCENDANT)
                    return "Descendant";
                if (Individual.RelationType == Individual.LINKED)
                    return "Linked";
                if (Individual.RelationType == Individual.UNKNOWN)
                    return "Unknown";
                return string.Empty;
            }
        }

        public string ShortCode
        {
            get
            {
                return string.Empty;
            }
        }
    }
}
