using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;
using Football.Locomotion;
using Football.Tactics;
using Football.Presentation;

/// <summary>
/// Professional Player Auto-Switcher (EA Sports FC / FIFA style):
/// 1. Automatically transfers human control to the teammate the ball is traveling towards (pass receiver / loose ball).
/// 2. Switches to the nearest teammate when defending or when a teammate recovers the ball.
/// 3. Moves the floating neon-green overhead indicator and updates the player name and number.
/// 4. Supports manual switching via Q (Keyboard) or LB (Gamepad).
/// </summary>
public class TeamPlayerSwitcher : MonoBehaviour
{
    [Header("Team Configuration")]
    public int humanTeamId = 1;
    public List<PlayerRuntimeState> teamPlayers = new List<PlayerRuntimeState>();
    public PlayerRuntimeState currentActivePlayer;
    public PlayerOverheadMarker overheadMarker;

    [Header("Switching Tuning")]
    public float minSwitchInterval = 0.28f;
    public float receiverAnticipationDistance = 32f;

    private float lastSwitchTime = -10f;
    private Transform intendedReceiver;
    private float intendedReceiverTimer = 0f;

    public static TeamPlayerSwitcher Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnEnable()
    {
        GameEvents.OnPassInitiated += HandlePassInitiated;
    }

    private void OnDisable()
    {
        GameEvents.OnPassInitiated -= HandlePassInitiated;
    }

    private void HandlePassInitiated(int teamId, Transform targetTeammate)
    {
        if (teamId == humanTeamId && targetTeammate != null)
        {
            NotifyPassExecuted(targetTeammate);
        }
    }

    public void RegisterPlayer(PlayerRuntimeState player)
    {
        if (player != null && !teamPlayers.Contains(player))
        {
            teamPlayers.Add(player);
        }
    }

    public void NotifyPassExecuted(Transform targetTeammate)
    {
        if (targetTeammate != null)
        {
            var receiverState = targetTeammate.GetComponent<PlayerRuntimeState>();
            if (receiverState != null && receiverState.teamId == humanTeamId)
            {
                intendedReceiver = targetTeammate;
                intendedReceiverTimer = 2.2f;
                SwitchToPlayer(receiverState);
            }
        }
    }

    private void Update()
    {
        if (teamPlayers.Count == 0) return;

        float dt = Time.deltaTime;
        var ball = FootballBall.Instance;

        // 1. Manual switch key (Q on Keyboard, LB / Left Shoulder on Gamepad)
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        bool manualSwitch = (keyboard != null && keyboard.qKey.wasPressedThisFrame) ||
                            (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame);

        if (manualSwitch && ball != null)
        {
            SwitchToBestPlayerNearBall(ball);
            return;
        }

        // 2. Intended pass receiver countdown
        if (intendedReceiverTimer > 0f)
        {
            intendedReceiverTimer -= dt;
            if (intendedReceiverTimer <= 0f) intendedReceiver = null;
        }

        if (ball == null) return;

        Vector3 ballPos = ball.transform.position;
        Vector3 ballVel = ball.Velocity;
        float ballSpeed = ballVel.magnitude;

        // 3. INSTANT AUTO-SWITCH TO PASS RECEIVER / BALL TARGET:
        // Always checks if the ball is moving towards any teammate (passes, through balls, clearances)
        if (ballSpeed > 1.8f)
        {
            PlayerRuntimeState bestReceiver = FindTeammateBallIsTravelingTowards(ballPos, ballVel);
            if (bestReceiver != null && bestReceiver != currentActivePlayer)
            {
                SwitchToPlayer(bestReceiver);
                return;
            }
        }

        // 4. POSSESSION AUTO-SWITCH:
        // If any outfield teammate has the ball or is in immediate dribbling contact (< 1.6m)
        for (int i = 0; i < teamPlayers.Count; i++)
        {
            var p = teamPlayers[i];
            if (p == null || p.isSentOff) continue;

            // Let goalkeeper hold ball safely in hands and punt it out to outfield teammates
            if (p.attributes != null && p.attributes.position == PlayerPosition.GK && p.isHoldingBallInHands)
            {
                continue;
            }

            if (p.hasBall || Vector3.Distance(p.transform.position, ballPos) < 1.6f)
            {
                if (p != currentActivePlayer)
                {
                    SwitchToPlayer(p);
                    return;
                }
            }
        }

        if (Time.time - lastSwitchTime < minSwitchInterval) return;

        // 5. DEFENDING AUTO-SWITCH:
        // If active player is far from the ball and another teammate is significantly closer
        if (currentActivePlayer != null)
        {
            float activeDist = Vector3.Distance(currentActivePlayer.transform.position, ballPos);
            if (activeDist > 7.0f)
            {
                PlayerRuntimeState nearest = GetNearestTeammateToBall(ballPos);
                if (nearest != null && nearest != currentActivePlayer)
                {
                    float nearestDist = Vector3.Distance(nearest.transform.position, ballPos);
                    if (nearestDist < activeDist - 3.2f)
                    {
                        SwitchToPlayer(nearest);
                    }
                }
            }
        }
    }

    private PlayerRuntimeState FindTeammateBallIsTravelingTowards(Vector3 ballPos, Vector3 ballVel)
    {
        Vector3 ballDir = ballVel.normalized;
        ballDir.y = 0;
        if (ballDir.sqrMagnitude < 0.01f) return null;
        ballDir.Normalize();

        PlayerRuntimeState bestTeammate = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < teamPlayers.Count; i++)
        {
            var p = teamPlayers[i];
            if (p == null || p.isSentOff) continue;

            Vector3 toPlayer = p.transform.position - ballPos;
            toPlayer.y = 0;
            float dist = toPlayer.magnitude;

            if (dist > receiverAnticipationDistance || dist < 0.5f) continue;

            // Dot product evaluates alignment with ball trajectory
            float dot = Vector3.Dot(toPlayer.normalized, ballDir);
            if (dot > 0.35f) // Forward cone along trajectory
            {
                // Prioritize alignment with trajectory and proximity to interception
                float score = (dot * 50f) - (dist * 0.75f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTeammate = p;
                }
            }
        }

        return bestTeammate;
    }

    private PlayerRuntimeState GetNearestTeammateToBall(Vector3 ballPos)
    {
        PlayerRuntimeState nearest = null;
        float minD = float.MaxValue;

        for (int i = 0; i < teamPlayers.Count; i++)
        {
            var p = teamPlayers[i];
            if (p == null || p.isSentOff) continue;

            float d = Vector3.Distance(p.transform.position, ballPos);
            if (d < minD)
            {
                minD = d;
                nearest = p;
            }
        }
        return nearest;
    }

    private void SwitchToBestPlayerNearBall(FootballBall ball)
    {
        var nearest = GetNearestTeammateToBall(ball.transform.position);
        if (nearest != null) SwitchToPlayer(nearest);
    }

    public void SwitchToPlayer(PlayerRuntimeState newPlayer)
    {
        if (newPlayer == null || newPlayer == currentActivePlayer) return;

        // 1. Deactivate previous active player: transfer to AI
        if (currentActivePlayer != null)
        {
            var prevInput = currentActivePlayer.GetComponent<FootballInputHandler>();
            if (prevInput != null) prevInput.isHumanControlled = false;

            var prevAI = currentActivePlayer.GetComponent<FootballAIPlayer>();
            if (prevAI != null) prevAI.enabled = true;
        }

        // 2. Activate new player: take over from AI
        currentActivePlayer = newPlayer;

        var nextInput = newPlayer.GetComponent<FootballInputHandler>();
        if (nextInput == null) nextInput = newPlayer.gameObject.AddComponent<FootballInputHandler>();
        nextInput.isHumanControlled = true;

        var nextAI = newPlayer.GetComponent<FootballAIPlayer>();
        if (nextAI != null) nextAI.enabled = false;

        lastSwitchTime = Time.time;

        // 3. Move EA FC overhead indicator & update name tag
        string displayName = (newPlayer.attributes != null && !string.IsNullOrEmpty(newPlayer.attributes.playerName))
            ? $"{newPlayer.jerseyNumber} {newPlayer.attributes.playerName.ToUpper()}"
            : $"#{newPlayer.jerseyNumber}";

        if (overheadMarker == null) overheadMarker = FindFirstObjectByType<PlayerOverheadMarker>();
        if (overheadMarker != null)
        {
            overheadMarker.SwitchTarget(newPlayer.transform, displayName);
        }
    }
}
