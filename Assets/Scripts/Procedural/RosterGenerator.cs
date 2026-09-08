using System;
using System.Collections.Generic;
using UnityEngine;
using Football.Data;

namespace Football.Procedural
{
    public static class RosterGenerator
    {
        private static readonly string[] FirstNamesEurope = { "Lucas", "Julian", "Antoine", "Thomas", "Marco", "David", "Kevin", "Florian", "Bruno", "Bernardo" };
        private static readonly string[] LastNamesEurope = { "Müller", "Dubois", "Silva", "Kroos", "De Bruyne", "Modrić", "Kane", "Griezmann", "Bellingham", "Mbappé" };

        private static readonly string[] FirstNamesAmericas = { "Lionel", "Lautaro", "Enzo", "Vinicius", "Rodrygo", "Federico", "Alexis", "Gabriel", "Neymar", "Julian" };
        private static readonly string[] LastNamesAmericas = { "Messi", "Martinez", "Fernandez", "Junior", "Valverde", "Alvarez", "Casemiro", "Alisson", "Marquinhos", "Diaz" };

        private static readonly string[] FirstNamesAfrica = { "Sadio", "Achraf", "Victor", "Mohamed", "Kalidou", "Yassine", "Thomas", "Wilfred", "Riyad", "Hakim" };
        private static readonly string[] LastNamesAfrica = { "Mané", "Hakimi", "Osimhen", "Salah", "Koulibaly", "Bounou", "Partey", "Ndidi", "Mahrez", "Ziyech" };

        private static readonly string[] FirstNamesAsia = { "Son", "Kaoru", "Takefusa", "Wataru", "Daichi", "Hwang", "Kim", "Takumi", "Mehdi", "Sardar" };
        private static readonly string[] LastNamesAsia = { "Heung-min", "Mitoma", "Kubo", "Endo", "Kamada", "Hee-chan", "Min-jae", "Minamino", "Taremi", "Azmoun" };

        public static TeamData GenerateNationalTeam(string name, string code, string confederation, Color primary, Color secondary, FormationType formation)
        {
            var team = new TeamData
            {
                teamId = code,
                teamName = name,
                shortCode = code,
                confederation = confederation,
                homePrimaryColor = primary,
                homeSecondaryColor = secondary,
                defaultFormation = formation
            };

            // Generate 11 Starters
            var slots = FormationData.GetFormationSlots(formation);
            int[] standardNumbers = { 1, 3, 4, 5, 2, 6, 8, 7, 11, 10, 9 };

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                int num = (i < standardNumbers.Length) ? standardNumbers[i] : (i + 1);
                var player = GeneratePlayer(confederation, slot.position, num);
                team.startingEleven.Add(player);
            }

            // Generate 7 Substitutes
            PlayerPosition[] subPositions = { PlayerPosition.GK, PlayerPosition.CB, PlayerPosition.LB, PlayerPosition.CM, PlayerPosition.CAM, PlayerPosition.LW, PlayerPosition.ST };
            for (int i = 0; i < subPositions.Length; i++)
            {
                var player = GeneratePlayer(confederation, subPositions[i], 12 + i);
                team.substitutes.Add(player);
            }

            return team;
        }

        public static PlayerAttributes GeneratePlayer(string region, PlayerPosition pos, int jerseyNum)
        {
            var p = new PlayerAttributes
            {
                jerseyNumber = jerseyNum,
                position = pos,
                preferredFoot = (UnityEngine.Random.value > 0.25f) ? Footedness.Right : Footedness.Left
            };

            // Pick regional name
            string first = "Player", last = jerseyNum.ToString();
            if (region == "CONMEBOL")
            {
                first = FirstNamesAmericas[UnityEngine.Random.Range(0, FirstNamesAmericas.Length)];
                last = LastNamesAmericas[UnityEngine.Random.Range(0, LastNamesAmericas.Length)];
            }
            else if (region == "CAF")
            {
                first = FirstNamesAfrica[UnityEngine.Random.Range(0, FirstNamesAfrica.Length)];
                last = LastNamesAfrica[UnityEngine.Random.Range(0, LastNamesAfrica.Length)];
            }
            else if (region == "AFC")
            {
                first = FirstNamesAsia[UnityEngine.Random.Range(0, FirstNamesAsia.Length)];
                last = LastNamesAsia[UnityEngine.Random.Range(0, LastNamesAsia.Length)];
            }
            else
            {
                first = FirstNamesEurope[UnityEngine.Random.Range(0, FirstNamesEurope.Length)];
                last = LastNamesEurope[UnityEngine.Random.Range(0, LastNamesEurope.Length)];
            }
            p.playerName = $"{first} {last}";

            // Tune positional attributes
            int baseRating = UnityEngine.Random.Range(74, 88);
            ApplyPositionalModifiers(p, pos, baseRating);

            return p;
        }

        private static void ApplyPositionalModifiers(PlayerAttributes p, PlayerPosition pos, int baseRating)
        {
            int Variance(int bias) => Mathf.Clamp(baseRating + bias + UnityEngine.Random.Range(-4, 5), 50, 99);

            switch (pos)
            {
                case PlayerPosition.GK:
                    p.gkDiving = Variance(6);
                    p.gkReflexes = Variance(7);
                    p.gkHandling = Variance(4);
                    p.gkPositioning = Variance(5);
                    p.gkKicking = Variance(0);
                    p.sprintSpeed = Variance(-20);
                    break;

                case PlayerPosition.CB:
                    p.standingTackle = Variance(8);
                    p.slidingTackle = Variance(6);
                    p.marking = Variance(7);
                    p.interceptions = Variance(6);
                    p.strength = Variance(8);
                    p.jumping = Variance(6);
                    p.sprintSpeed = Variance(-5);
                    p.finishing = Variance(-30);
                    break;

                case PlayerPosition.LB:
                case PlayerPosition.RB:
                case PlayerPosition.LWB:
                case PlayerPosition.RWB:
                    p.sprintSpeed = Variance(10);
                    p.acceleration = Variance(8);
                    p.stamina = Variance(10);
                    p.crossing = Variance(7);
                    p.standingTackle = Variance(4);
                    break;

                case PlayerPosition.CDM:
                    p.interceptions = Variance(8);
                    p.standingTackle = Variance(7);
                    p.shortPassing = Variance(6);
                    p.stamina = Variance(8);
                    p.strength = Variance(6);
                    break;

                case PlayerPosition.CM:
                case PlayerPosition.LM:
                case PlayerPosition.RM:
                    p.shortPassing = Variance(8);
                    p.longPassing = Variance(7);
                    p.vision = Variance(7);
                    p.ballControl = Variance(6);
                    p.stamina = Variance(6);
                    break;

                case PlayerPosition.CAM:
                case PlayerPosition.LAM:
                case PlayerPosition.RAM:
                    p.vision = Variance(9);
                    p.shortPassing = Variance(7);
                    p.ballControl = Variance(8);
                    p.agility = Variance(7);
                    p.longShots = Variance(6);
                    break;

                case PlayerPosition.LW:
                case PlayerPosition.RW:
                    p.sprintSpeed = Variance(12);
                    p.acceleration = Variance(12);
                    p.agility = Variance(10);
                    p.dribbling = Variance(9);
                    p.crossing = Variance(6);
                    p.finishing = Variance(4);
                    break;

                case PlayerPosition.ST:
                    p.finishing = Variance(12);
                    p.shotPower = Variance(9);
                    p.composure = Variance(8);
                    p.heading = Variance(7);
                    p.ballControl = Variance(6);
                    p.sprintSpeed = Variance(5);
                    break;
            }
        }
    }
}
