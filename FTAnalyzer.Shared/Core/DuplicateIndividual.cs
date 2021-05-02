namespace FTAnalyzer
{
    using System.Diagnostics;
    public class DuplicateIndividual
    {
        public Individual IndividualA { get; private set; }
        public Individual IndividualB { get; private set; }
        public int Score { get; private set; }

/* do not use this expensive constructor, always use the one that accpets the already calculated matching score as an extra parameter
        // DONE: changed parameters to in parameters
        public DuplicateIndividual(in Individual a, in Individual b)
        {
            IndividualA = a;
            IndividualB = b;
            _Score = Matching.Score(IndividualA, IndividualB);
        }
*/
        public DuplicateIndividual(in Individual a, in Individual b, int score)
        {
            IndividualA = a;
            IndividualB = b;
            Score = score;
        }

        public override bool Equals(object obj)
        {
            if (obj is DuplicateIndividual)
                return (IndividualA.Equals(((DuplicateIndividual)obj).IndividualA) && IndividualB.Equals(((DuplicateIndividual)obj).IndividualB))
                    || (IndividualA.Equals(((DuplicateIndividual)obj).IndividualB) && IndividualB.Equals(((DuplicateIndividual)obj).IndividualA));
            return false;
        }
        public override int GetHashCode() => base.GetHashCode();

        public int CompareTo( DuplicateIndividual dupOther )
        {
            int iVal = this.Score - dupOther.Score;

            return iVal;
        }
    }


    // TODO: get rid of memory-hogging DuplicateIndividual
    // class still exists merely to limit code changes made to a few files

    // DONE: split class DuplicateIndividual into DuplicateIndivdual and Matching
    // Matching is class with static functions to calculate matching score
    // but without requiring a  costdly constructor that copies two Individual objects (roughly 1024 bytes)
    // Score() is the only public member function now, the rest are private.

    public class Matching
    {
        // DONE: commented out SharedParents() ; SharedParents() really is a Twin check, <em>and shoulod be used as such</em> (and when shown in a the main list, twins should arguably rank lower, not higher)
        // DONE: commented out SharedChildren(); this check makes no sense; if connected to the same children, one pair are probably step parents. What the app needs is separarate data consistency check on multiple parents.
        // DONE" kept DifferentParentsPenalty(); lowering the ranking of otherwise identical identical records with different parents makes sense.
        static public int Score(in Individual indLeft, in Individual indRight)
        {
            int iScore = 0;
            iScore += NameScore(indLeft, indRight);

            iScore += ScoreDates(indLeft.BirthDate, indRight.BirthDate);
            iScore += ScoreDates(indLeft.DeathDate, indRight.DeathDate);

            iScore += LocationScore(indLeft, indRight);
            //          iScore += SharedParents(indLeft, indRight);
            //          iScore += SharedChildren(indLeft, indRight);
            iScore += DifferentParentsPenalty(indLeft, indRight);

            return iScore;
        }


        // NOTE: this routine deliberately takes a simplistc approach; if any parent matches, it returns true
        // notice use of return to break out of inner and outer loop
        static public bool IsTwins(in Individual indLeft, in Individual indRight)
        {
            foreach (ParentalRelationship parentLeft in indLeft.FamiliesAsChild)
            {
                foreach (ParentalRelationship parentRight in indRight.FamiliesAsChild)
                {
                    if (parentLeft.Father == parentRight.Father) return true;
                    if (parentLeft.Mother == parentRight.Mother) return true;
                };
            };
            return false;
        }

        // DONE: changed parameters to in parameters
        // DONE: stop using and comparing to string value "UNKNOWN". leave empty names empty and check for 0 == inidA.Surname.Length
        static private int NameScore(in Individual indA, in Individual indB)
        {
            int iScore = 0;

            if (null == indA) return iScore;
            if (null == indB) return iScore;

            if (0 == indA.Surname.Length) return iScore;
            if (0 == indB.Forename.Length) return iScore;

            if (indA.Surname.Equals(indB.Surname)) iScore += 20;
            if (indA.Forename.Equals(indB.Forename)) iScore += 20;

            return iScore;
        }

        // DONE now has in parameters and returns the score, instead of requiring a costly DuplicateIndivual object
        // DONE: optimisation applied: strings can only be identical if their metaphones are identical
        static private int LocationScore( in Individual indLeft, in Individual indRight )
        {
            int iScore = 0 ;

            if (indLeft.BirthLocation.IsBlank || indRight.BirthLocation.IsBlank) return iScore ;


            if (indLeft.BirthLocation.CountryMetaphone.Equals(indRight.BirthLocation.CountryMetaphone))
            {
                iScore += 5;
                if (indLeft.BirthLocation.Country.Equals(indRight.BirthLocation.Country)) iScore += 10;
                if (indLeft.BirthLocation.Equals(indRight.BirthLocation)) iScore += 75;
            };

            if (indLeft.BirthLocation.RegionMetaphone.Equals(indRight.BirthLocation.RegionMetaphone))
            {
                iScore += 5;
                if (indLeft.BirthLocation.Region.Equals(indRight.BirthLocation.Region)) iScore += 10;
            };

            if (indLeft.BirthLocation.SubRegionMetaphone.Equals(indRight.BirthLocation.SubRegionMetaphone))
            {
                iScore += 10;
                if (indLeft.BirthLocation.SubRegion.Equals(indRight.BirthLocation.SubRegion))iScore += 20;
            };

            if (indLeft.BirthLocation.AddressMetaphone.Equals(indRight.BirthLocation.AddressMetaphone))
            {
                iScore += 20;
                if (indLeft.BirthLocation.Address.Equals(indRight.BirthLocation.Address)) iScore += 40;
            };

            if (indLeft.BirthLocation.PlaceMetaphone.Equals(indRight.BirthLocation.PlaceMetaphone))
            {
                iScore += 20;
                if (indLeft.BirthLocation.Place.Equals(indRight.BirthLocation.Place)) iScore += 40;
            };

            if (indLeft.BirthLocation.IsKnownCountry && indRight.BirthLocation.IsKnownCountry &&
                !indLeft.BirthLocation.Country.Equals(indRight.BirthLocation.Country))
               iScore -= 250;

            return iScore;
        }


        /* old version. replaced with version using integers and DistanceSquared() instead of Dinstance()
            void ScoreDates(FactDate dateA, FactDate dateB)
            {
                if (dateA.IsKnown && dateB.IsKnown)
                {
                    double distance = dateA.Distance(dateB);
                    if (dateA.Equals(dateB))
                        Score += 50;
                    else if (distance <= .25)
                        Score += 50;
                    else if (distance <= .5)
                        Score += 20;
                    else if (distance <= 1)
                        Score += 10;
                    else if (distance <= 2)
                        Score += 5;
                    else if (distance > 5 && distance < 20)
                        Score -= (int)(distance * distance);
                    else
                        Score = -10000;  // distance is too big so set score to large negative
                    if (dateA.IsExact && dateB.IsExact)
                        Score += 100;
                }
            }
        */

        /*     // -in=between version that already uses integers, but still uses Math.Sqrt()
               static private int ScoreDates(in FactDate dateA, in FactDate dateB)
               {
                   int iScore = 0;

                   if (dateA.IsKnown && dateB.IsKnown)
                   {
                       long distanceSquared = dateA.DistanceSquared(dateB);
                       distanceSquared *= 16;
                       long distance4 = (long)System.Math.Sqrt(distanceSquared); // dinstance times 4

                       if (dateA.Equals(dateB))
                           iScore += 50;
                       else if (distance4 <= 1)
                           iScore += 50;
                       else if (distance4 <= 2)
                           iScore += 20;
                       else if (distance4 <= 4)
                           iScore += 10;
                       else if (distance4 <= 8)
                           iScore += 5;
                       else if (distance4 > 20 && distance4 < 80)
                           iScore -= (int)distanceSquared;
                       else
                           iScore = -10000;  // distance is too big so set score to large negative
                       if (dateA.IsExact && dateB.IsExact)
                           iScore += 100;
                   }

                   return iScore;
               }
       */

        // DONE: BUG not copied from original source: no score for difference 3,4 and 5 (well, actually setting Score to -10000)
        // DONE: changed parameters to in parameters
        // DONE: returns a score instead of directly affecting an object value
        // DONE: got rid of floating point comparisons by multiplying all values by 4
        // DONE: rewritting using distanceSquared(), without calculating distance; no more Math.Sqrt()
        // DONE: no longer taking square root first, and recalcuting the (floor of the) squoare later
        // DONE: restructred to reduce number of comparisions, and not even calculating distanceSquared if dates are equal
        // TOPD: speedup by using julian day number and JDDinstanceSquared()
        // TODO: take advantage of the increased day instead of month resoluton
        // TODO: take advantage of the increased resolution of the square versus square root
        // TODO: the last condition in this code , (1 <= distanceSquared),  shows why you really need to use day differene, not month diifference; right now, it will never be true

        static private int ScoreDates(in FactDate dateA, in FactDate dateB)
        {
            int iScore = 0;

            if (dateA.IsKnown && dateB.IsKnown)
            {
                if (dateA.IsExact && dateB.IsExact)
                { 
                    iScore += 100;
                };

                if (dateA.Equals(dateB))
                {
                    iScore += 50;
                }
                else
                {
                    long distanceSquared = dateA.DistanceSquared(dateB);

                    for (; ; )
                    {
                        if (400 < distanceSquared)
                        {
                            iScore = -1000;
                            break;
                        };

                        if (25 < distanceSquared)
                        {
                            iScore = -(int)distanceSquared;
                            break;
                        };

                        Debug.Assert(25 >= distanceSquared); // no overflow on calculating distance4Squared
                        int distance4Squared = (int)distanceSquared * 16;

                        if  (1 > distance4Squared) { iScore += 50; break; };
                        if  (4 > distance4Squared) { iScore += 20; break; };
                        if (16 > distance4Squared) { iScore += 10; break; };
                        if (64 > distance4Squared) { iScore +=  5; break; };

                        break;
                    };
                };

            }

            return iScore;
        }

        static private int SharedParents(in Individual indLeft, in Individual indRight)
        {
            int score = 0;
            foreach (ParentalRelationship parentLeft in indLeft.FamiliesAsChild)
            {
                foreach (ParentalRelationship parentRight in indRight.FamiliesAsChild)
                {
                    if (parentLeft.Father == parentRight.Father)
                        score += 50;
                    else
                        score += NameScore(parentLeft.Father, parentRight.Father);
                    if (parentLeft.Mother == parentRight.Mother)
                        score += 50;
                    else
                        score += NameScore(parentLeft.Mother, parentRight.Mother);
                }
            }
            return score;
        }

        // DONE: using in parameteers
        static private int DifferentParentsPenalty(in Individual indLeft, in Individual indRight)
        {
            int iScore = 0;

            if (indLeft.FamiliesAsChild.Count == 1 && indRight.FamiliesAsChild.Count == 1)
            { // both individuals have parents if none of them are shared parents apply a heavy penalty
                if (indLeft.FamiliesAsChild[0].Father != null && indLeft.FamiliesAsChild[0].Mother != null &&
                    indRight.FamiliesAsChild[0].Father != null && indRight.FamiliesAsChild[0].Mother != null &&
                    !indLeft.FamiliesAsChild[0].Father.Equals(indRight.FamiliesAsChild[0].Father) &&
                    !indLeft.FamiliesAsChild[0].Mother.Equals(indRight.FamiliesAsChild[0].Mother))
                    iScore = -500;
            }
            else if (indLeft.FamiliesAsChild.Count > 0 && indRight.FamiliesAsChild.Count > 0)
            {
                if (0 == SharedParents( indLeft, indRight ))
                    iScore = -250;
            }
            return iScore;
        }

        // DONE: using in parameteers
        static private int SharedChildren( in Individual indLeft, in Individual indRight)
        {
            int score = 0;
            foreach (Family familyLeft in indLeft.FamiliesAsSpouse)
            {
                foreach (Family familyRight in indRight.FamiliesAsSpouse)
                {
                    foreach (Individual indRightChild in familyRight.Children)
                        if (familyLeft.Children.Contains(indRightChild))
                            score += 50;
                }
            }
            return score;
        }

    }
}
