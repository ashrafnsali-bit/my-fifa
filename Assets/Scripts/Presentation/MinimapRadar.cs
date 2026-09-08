using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Football.Core;
using Football.Locomotion;
using Football.PhysicsEngine;

namespace Football.Presentation
{
    public class MinimapRadar : MonoBehaviour
    {
        [Header("Radar Canvas Rect")]
        public RectTransform radarPitchRect;

        [Header("Blip Prefabs/Templates")]
        public Image homeBlipTemplate;
        public Image awayBlipTemplate;
        public Image ballBlip;

        private List<PlayerRuntimeState> allPlayers = new List<PlayerRuntimeState>();
        private List<RectTransform> activeBlips = new List<RectTransform>();

        private void Start()
        {
            InitializeBlips();
        }

        private void InitializeBlips()
        {
            if (radarPitchRect == null) return;

            // Find all active players on pitch
            allPlayers.AddRange(FindObjectsOfType<PlayerRuntimeState>());

            if (homeBlipTemplate != null) homeBlipTemplate.gameObject.SetActive(false);
            if (awayBlipTemplate != null) awayBlipTemplate.gameObject.SetActive(false);

            for (int i = 0; i < allPlayers.Count; i++)
            {
                var p = allPlayers[i];
                var template = (p.teamId == 1) ? homeBlipTemplate : awayBlipTemplate;
                if (template != null)
                {
                    var blip = Instantiate(template, radarPitchRect);
                    blip.gameObject.SetActive(true);
                    activeBlips.Add(blip.rectTransform);
                }
            }
        }

        private void LateUpdate()
        {
            if (radarPitchRect == null) return;

            float radarWidth = radarPitchRect.rect.width;
            float radarHeight = radarPitchRect.rect.height;

            // Update player blips
            for (int i = 0; i < allPlayers.Count && i < activeBlips.Count; i++)
            {
                var p = allPlayers[i];
                var blipRect = activeBlips[i];

                if (p == null || p.isSentOff)
                {
                    blipRect.gameObject.SetActive(false);
                    continue;
                }

                blipRect.gameObject.SetActive(true);
                Vector2 normPos = WorldToNormalizedPitch(p.transform.position);
                blipRect.anchoredPosition = new Vector2(normPos.x * radarWidth * 0.5f, normPos.y * radarHeight * 0.5f);
            }

            // Update ball blip
            if (ballBlip != null && FootballBall.Instance != null)
            {
                Vector2 ballNorm = WorldToNormalizedPitch(FootballBall.Instance.transform.position);
                ballBlip.rectTransform.anchoredPosition = new Vector2(ballNorm.x * radarWidth * 0.5f, ballNorm.y * radarHeight * 0.5f);
            }
        }

        private Vector2 WorldToNormalizedPitch(Vector3 worldPos)
        {
            float normX = Mathf.Clamp(worldPos.x / PitchConstants.HalfWidth, -1f, 1f);
            float normZ = Mathf.Clamp(worldPos.z / PitchConstants.HalfLength, -1f, 1f);
            return new Vector2(normX, normZ);
        }
    }
}
