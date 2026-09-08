using NUnit.Framework;
using System.Collections.Generic;
using Football.Data;
using Football.Procedural;

namespace Football.Tests
{
    public class TournamentEngineTests
    {
        [Test]
        public void WorldCupInitialization_CreatesEightGroupsOfFour()
        {
            var teams = TournamentEngine.GenerateDefault32Teams();
            Assert.AreEqual(32, teams.Count, "Pool must contain exactly 32 teams.");

            var go = new UnityEngine.GameObject("TournamentTest");
            var engine = go.AddComponent<TournamentEngine>();

            engine.InitializeWorldCup(teams);

            Assert.AreEqual(8, engine.groups.Count, "Tournament must have 8 groups (A to H).");
            foreach (var grp in engine.groups)
            {
                Assert.AreEqual(4, grp.standings.Count, "Each group must have 4 teams.");
                Assert.AreEqual(6, grp.fixtures.Count, "Each group must have 6 round-robin fixtures.");
            }

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void GroupStandings_SortsByPointsAndGoalDifference()
        {
            var grp = new TournamentGroup { groupName = "Group Test" };

            var teamA = new TeamData { teamName = "Team A" };
            var teamB = new TeamData { teamName = "Team B" };
            var teamC = new TeamData { teamName = "Team C" };
            var teamD = new TeamData { teamName = "Team D" };

            var standingA = new TeamStanding { team = teamA, won = 2, drawn = 1, goalsFor = 6, goalsAgainst = 2 }; // 7 pts, +4 GD
            var standingB = new TeamStanding { team = teamB, won = 2, drawn = 1, goalsFor = 4, goalsAgainst = 1 }; // 7 pts, +3 GD
            var standingC = new TeamStanding { team = teamC, won = 1, drawn = 0, goalsFor = 3, goalsAgainst = 5 }; // 3 pts, -2 GD
            var standingD = new TeamStanding { team = teamD, won = 0, drawn = 0, goalsFor = 1, goalsAgainst = 6 }; // 0 pts, -5 GD

            grp.standings.Add(standingD);
            grp.standings.Add(standingB);
            grp.standings.Add(standingA);
            grp.standings.Add(standingC);

            grp.SortStandings();

            Assert.AreEqual("Team A", grp.standings[0].team.teamName, "Team A must finish 1st (+4 GD).");
            Assert.AreEqual("Team B", grp.standings[1].team.teamName, "Team B must finish 2nd (+3 GD).");
            Assert.AreEqual("Team C", grp.standings[2].team.teamName, "Team C must finish 3rd (3 pts).");
            Assert.AreEqual("Team D", grp.standings[3].team.teamName, "Team D must finish 4th (0 pts).");
        }
    }
}
