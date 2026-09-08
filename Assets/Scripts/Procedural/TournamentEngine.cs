using System;
using System.Collections.Generic;
using UnityEngine;
using Football.Data;

namespace Football.Procedural
{
    public enum TournamentStage
    {
        GroupStage,
        RoundOf16,
        QuarterFinals,
        SemiFinals,
        ThirdPlacePlayoff,
        Final,
        Completed
    }

    [Serializable]
    public class TeamStanding
    {
        public TeamData team;
        public int played = 0;
        public int won = 0;
        public int drawn = 0;
        public int lost = 0;
        public int goalsFor = 0;
        public int goalsAgainst = 0;

        public int GoalDifference => goalsFor - goalsAgainst;
        public int Points => (won * 3) + (drawn * 1);
    }

    [Serializable]
    public class TournamentMatch
    {
        public string matchId;
        public TournamentStage stage;
        public TeamData homeTeam;
        public TeamData awayTeam;
        public int homeScore = 0;
        public int awayScore = 0;
        public bool isCompleted = false;
        public bool decidedOnPenalties = false;
        public int homePenaltyScore = 0;
        public int awayPenaltyScore = 0;

        public TeamData Winner
        {
            get
            {
                if (!isCompleted) return null;
                if (homeScore > awayScore) return homeTeam;
                if (awayScore > homeScore) return awayTeam;
                return homePenaltyScore > awayPenaltyScore ? homeTeam : awayTeam;
            }
        }
    }

    [Serializable]
    public class TournamentGroup
    {
        public string groupName; // "Group A"
        public List<TeamStanding> standings = new List<TeamStanding>();
        public List<TournamentMatch> fixtures = new List<TournamentMatch>();

        public void SortStandings()
        {
            // FIFA World Cup tiebreaker: Points -> Goal Difference -> Goals For
            standings.Sort((a, b) =>
            {
                if (a.Points != b.Points) return b.Points.CompareTo(a.Points);
                if (a.GoalDifference != b.GoalDifference) return b.GoalDifference.CompareTo(a.GoalDifference);
                return b.goalsFor.CompareTo(a.goalsFor);
            });
        }
    }

    public class TournamentEngine : MonoBehaviour
    {
        public static TournamentEngine Instance { get; private set; }

        public TournamentStage currentStage = TournamentStage.GroupStage;
        public List<TournamentGroup> groups = new List<TournamentGroup>();
        public List<TournamentMatch> roundOf16Matches = new List<TournamentMatch>();
        public List<TournamentMatch> quarterMatches = new List<TournamentMatch>();
        public List<TournamentMatch> semiMatches = new List<TournamentMatch>();
        public TournamentMatch thirdPlaceMatch;
        public TournamentMatch finalMatch;
        public TeamData champion;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        /// <summary>
        /// Initializes a full 32-team World Cup with 8 groups (A to H).
        /// </summary>
        public void InitializeWorldCup(List<TeamData> teams32)
        {
            if (teams32 == null || teams32.Count < 32)
            {
                Debug.LogWarning("Tournament requires 32 teams. Generating default international pool.");
                teams32 = GenerateDefault32Teams();
            }

            groups.Clear();
            string[] groupLetters = { "A", "B", "C", "D", "E", "F", "G", "H" };

            int teamIdx = 0;
            for (int g = 0; g < 8; g++)
            {
                var grp = new TournamentGroup { groupName = $"Group {groupLetters[g]}" };
                for (int i = 0; i < 4; i++)
                {
                    grp.standings.Add(new TeamStanding { team = teams32[teamIdx++] });
                }

                // Create round-robin fixtures (6 matches)
                for (int i = 0; i < 4; i++)
                {
                    for (int j = i + 1; j < 4; j++)
                    {
                        grp.fixtures.Add(new TournamentMatch
                        {
                            matchId = $"{grp.groupName}-M{grp.fixtures.Count + 1}",
                            stage = TournamentStage.GroupStage,
                            homeTeam = grp.standings[i].team,
                            awayTeam = grp.standings[j].team
                        });
                    }
                }

                groups.Add(grp);
            }

            currentStage = TournamentStage.GroupStage;
        }

        /// <summary>
        /// Simulates or resolves a group match outcome and updates group standings table.
        /// </summary>
        public void RecordGroupMatchResult(TournamentGroup group, TournamentMatch match, int homeScore, int awayScore)
        {
            match.homeScore = homeScore;
            match.awayScore = awayScore;
            match.isCompleted = true;

            var homeStanding = group.standings.Find(s => s.team == match.homeTeam);
            var awayStanding = group.standings.Find(s => s.team == match.awayTeam);

            if (homeStanding != null && awayStanding != null)
            {
                homeStanding.played++;
                awayStanding.played++;

                homeStanding.goalsFor += homeScore;
                homeStanding.goalsAgainst += awayScore;

                awayStanding.goalsFor += awayScore;
                awayStanding.goalsAgainst += homeScore;

                if (homeScore > awayScore)
                {
                    homeStanding.won++;
                    awayStanding.lost++;
                }
                else if (awayScore > homeScore)
                {
                    awayStanding.won++;
                    homeStanding.lost++;
                }
                else
                {
                    homeStanding.drawn++;
                    awayStanding.drawn++;
                }

                group.SortStandings();
            }
        }

        /// <summary>
        /// Populates Round of 16 bracket once all group matches finish:
        /// Winner Group A vs Runner-up Group B, Winner Group C vs Runner-up Group D, etc.
        /// </summary>
        public void ProgressToRoundOf16()
        {
            roundOf16Matches.Clear();

            for (int i = 0; i < 8; i += 2)
            {
                var grp1 = groups[i];
                var grp2 = groups[i + 1];

                grp1.SortStandings();
                grp2.SortStandings();

                // Match 1: 1st Grp1 vs 2nd Grp2
                roundOf16Matches.Add(new TournamentMatch
                {
                    matchId = $"R16-{roundOf16Matches.Count + 1}",
                    stage = TournamentStage.RoundOf16,
                    homeTeam = grp1.standings[0].team,
                    awayTeam = grp2.standings[1].team
                });

                // Match 2: 1st Grp2 vs 2nd Grp1
                roundOf16Matches.Add(new TournamentMatch
                {
                    matchId = $"R16-{roundOf16Matches.Count + 1}",
                    stage = TournamentStage.RoundOf16,
                    homeTeam = grp2.standings[0].team,
                    awayTeam = grp1.standings[1].team
                });
            }

            currentStage = TournamentStage.RoundOf16;
        }

        /// <summary>
        /// Fast-simulates a match between two teams based on relative overall team ratings.
        /// </summary>
        public static (int homeScore, int awayScore) SimulateMatchScore(TeamData home, TeamData away)
        {
            float homeOvr = home != null ? home.GetAverageOverall() : 75f;
            float awayOvr = away != null ? away.GetAverageOverall() : 75f;

            float diff = (homeOvr - awayOvr) / 10f; // Each 10 OVR difference favors team
            int homeGoals = Mathf.Max(0, Mathf.RoundToInt(UnityEngine.Random.Range(0f, 3.2f) + diff * 0.6f));
            int awayGoals = Mathf.Max(0, Mathf.RoundToInt(UnityEngine.Random.Range(0f, 3.2f) - diff * 0.6f));

            return (homeGoals, awayGoals);
        }

        public static List<TeamData> GenerateDefault32Teams()
        {
            var list = new List<TeamData>();
            string[] names = {
                "Argentina", "France", "Brazil", "England", "Spain", "Germany", "Portugal", "Netherlands",
                "Italy", "Belgium", "Croatia", "Uruguay", "Japan", "Senegal", "Morocco", "USA",
                "Mexico", "Denmark", "Switzerland", "Colombia", "South Korea", "Nigeria", "Australia", "Canada",
                "Poland", "Sweden", "Serbia", "Ecuador", "Cameroon", "Ghana", "Saudi Arabia", "Qatar"
            };
            string[] codes = {
                "ARG", "FRA", "BRA", "ENG", "ESP", "GER", "POR", "NED",
                "ITA", "BEL", "CRO", "URU", "JPN", "SEN", "MAR", "USA",
                "MEX", "DEN", "SUI", "COL", "KOR", "NGA", "AUS", "CAN",
                "POL", "SWE", "SRB", "ECU", "CMR", "GHA", "KSA", "QAT"
            };
            string[] confeds = {
                "CONMEBOL", "UEFA", "CONMEBOL", "UEFA", "UEFA", "UEFA", "UEFA", "UEFA",
                "UEFA", "UEFA", "UEFA", "CONMEBOL", "AFC", "CAF", "CAF", "CONCACAF",
                "CONCACAF", "UEFA", "UEFA", "CONMEBOL", "AFC", "CAF", "AFC", "CONCACAF",
                "UEFA", "UEFA", "UEFA", "CONMEBOL", "CAF", "CAF", "AFC", "AFC"
            };

            for (int i = 0; i < 32; i++)
            {
                Color c1 = Color.HSVToRGB((i * 0.031f) % 1.0f, 0.7f, 0.9f);
                Color c2 = Color.white;
                list.Add(RosterGenerator.GenerateNationalTeam(names[i], codes[i], confeds[i], c1, c2, FormationType.Formation_4_3_3));
            }

            return list;
        }
    }
}
