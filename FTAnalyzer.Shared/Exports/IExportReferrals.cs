namespace FTAnalyzer
{
    public interface IExportReferrals
    {
        string Forenames  { get; }
        string Surname { get; }
        Age Age { get; }
        string RelationType { get; }
    }
}
