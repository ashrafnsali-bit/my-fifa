using UnityEngine;
using Football.Core;
using Football.Data;

namespace Football.Locomotion
{
    public enum MovementState
    {
        Idle,
        Jog,
        Sprint,
        Dribbling,
        Tackling,
        Kicking,
        Fouled,
        HoldingBallHands
    }

    /// <summary>
    /// Tracks dynamic runtime states for a single player instance on the pitch.
    /// Manages stamina decay/recovery, card status, and current movement phase.
    /// </summary>
    public class PlayerRuntimeState : MonoBehaviour
    {
        [Header("Player Identity")]
        public int teamId = 1;
        public int jerseyNumber = 10;
        public PlayerAttributes attributes;

        [Header("Stamina Mechanics")]
        [Range(0f, 100f)] public float currentStamina = 100f;
        public const float MaxStamina = 100f;
        public float sprintDrainRate = 12.0f; // stamina points lost per second sprinting
        public float recoveryRate = 7.0f;     // stamina points recovered per second jogging/standing

        [Header("State Status")]
        public MovementState currentState = MovementState.Idle;
        public bool hasBall = false;
        public bool isHoldingBallInHands = false;
        public CardType cardStatus = CardType.None;
        public bool isSentOff = false;

        public float SpeedMultiplier
        {
            get
            {
                // Fatigue penalty below 25% stamina
                if (currentStamina < 25f)
                {
                    return 0.75f + (currentStamina / 100f);
                }
                return 1.0f;
            }
        }

        private void Awake()
        {
            if (attributes == null)
            {
                attributes = new PlayerAttributes();
            }
        }

        public void ConsumeStamina(float dt)
        {
            currentStamina = Mathf.Max(0f, currentStamina - sprintDrainRate * dt);
        }

        public void RecoverStamina(float dt)
        {
            currentStamina = Mathf.Min(MaxStamina, currentStamina + recoveryRate * dt);
        }

        public void ApplyCard(CardType card)
        {
            if (card == CardType.Yellow)
            {
                if (cardStatus == CardType.Yellow)
                {
                    cardStatus = CardType.Red;
                    isSentOff = true;
                    GameEvents.TriggerCardIssued(teamId, jerseyNumber, CardType.Red);
                }
                else
                {
                    cardStatus = CardType.Yellow;
                    GameEvents.TriggerCardIssued(teamId, jerseyNumber, CardType.Yellow);
                }
            }
            else if (card == CardType.Red)
            {
                cardStatus = CardType.Red;
                isSentOff = true;
                GameEvents.TriggerCardIssued(teamId, jerseyNumber, CardType.Red);
            }
        }
    }
}
