using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EchoProtocol.Player;
using EchoProtocol.Voice;

namespace EchoProtocol.UI
{
    /// <summary>
    /// Pure rules for a floating name tag: how visible it is at a distance and behind walls, and how big it is.
    /// </summary>
    public static class NameTagRules
    {
        /// <summary>Metres within which a tag is fully visible.</summary>
        public const float FullDistance = 18f;

        /// <summary>Metres beyond which a tag is gone.</summary>
        public const float MaxDistance = 35f;

        /// <summary>Metres above the player's centre (the top of a 2 m body, plus a little air).</summary>
        public const float HeightAboveCentre = 1.45f;

        /// <summary>0..1 opacity: full up close, fading out to <see cref="MaxDistance"/>, hidden behind a wall.</summary>
        public static float Alpha(float distance, int wallsInBetween)
        {
            if (wallsInBetween > 0 || distance > MaxDistance) return 0f;
            if (distance <= FullDistance) return 1f;
            return 1f - (distance - FullDistance) / (MaxDistance - FullDistance);
        }

        /// <summary>Scale of the tag: smaller far away, but never too small to read.</summary>
        public static float Scale(float distance) => Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(distance / MaxDistance));
    }

    /// <summary>
    /// Draws every teammate's name above their head, like co-op games do: on the HUD's canvas, following the player,
    /// fading with distance and hidden behind walls (the cave stays scary). It lights up while that player's voice is
    /// heard, so on a recording you can tell who is talking. Your own name is never drawn.
    /// </summary>
    [DisallowMultipleComponent]
    public class NameTagOverlay : MonoBehaviour
    {
        private static readonly Color IdleText = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color TalkingText = new Color(0.45f, 1f, 0.75f, 1f);
        private static readonly Color Backing = new Color(0f, 0f, 0f, 0.5f);

        private class Tag
        {
            public RectTransform Root;
            public Image Background;
            public Text Label;
            public PlayerName Name;
            public ProximityVoice Voice;
        }

        private readonly Dictionary<PlayerController, Tag> _tags = new Dictionary<PlayerController, Tag>();
        private readonly List<PlayerController> _stale = new List<PlayerController>();
        private RectTransform _container;
        private Font _font;

        /// <summary>Tags currently on screen (for tests and debugging).</summary>
        public int VisibleTags { get; private set; }

        /// <summary>Called by the HUD with its canvas root: the tags sit behind every other HUD element.</summary>
        public void Initialise(RectTransform canvasRoot, Font font)
        {
            _font = font;
            var go = new GameObject("NameTags", typeof(RectTransform));
            _container = (RectTransform)go.transform;
            _container.SetParent(canvasRoot, false);
            _container.anchorMin = Vector2.zero;
            _container.anchorMax = Vector2.one;
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;
            _container.SetAsFirstSibling();
        }

        private void LateUpdate()
        {
            if (_container == null) return;

            Camera cam = Camera.main;
            VisibleTags = 0;
            _stale.Clear();
            foreach (var pair in _tags) if (pair.Key == null) _stale.Add(pair.Key);
            foreach (var dead in _stale) RemoveTag(dead);

            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController player = players[i];
                if (player == null || !player.IsSpawned || player.IsOwner) continue;

                Tag tag = GetOrCreate(player);
                if (tag == null) continue;
                UpdateTag(tag, player, cam);
            }
        }

        private Tag GetOrCreate(PlayerController player)
        {
            if (_tags.TryGetValue(player, out Tag existing)) return existing;

            var name = player.GetComponent<PlayerName>();
            if (name == null) return null;

            var root = new GameObject($"Tag_{player.OwnerId}", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(_container, false);
            rect.pivot = new Vector2(0.5f, 0f);

            var background = root.AddComponent<Image>();
            background.color = Backing;
            background.raycastTarget = false;

            var labelObj = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelObj.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelObj.AddComponent<Text>();
            label.font = _font;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            labelObj.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);

            var tag = new Tag { Root = rect, Background = background, Label = label, Name = name, Voice = player.GetComponent<ProximityVoice>() };
            _tags.Add(player, tag);
            return tag;
        }

        private void UpdateTag(Tag tag, PlayerController player, Camera cam)
        {
            Vector3 head = player.transform.position + Vector3.up * NameTagRules.HeightAboveCentre;
            float alpha = 0f;
            Vector3 screen = default;

            if (cam != null)
            {
                screen = cam.WorldToScreenPoint(head);
                if (screen.z > 0.1f)
                {
                    float distance = Vector3.Distance(cam.transform.position, head);
                    int walls = distance <= NameTagRules.MaxDistance ? SoundOcclusion.CountWalls(cam.transform.position, head, player.transform) : 0;
                    alpha = NameTagRules.Alpha(distance, walls);
                    tag.Root.localScale = Vector3.one * NameTagRules.Scale(distance);
                }
            }

            bool show = alpha > 0.01f;
            if (tag.Root.gameObject.activeSelf != show) tag.Root.gameObject.SetActive(show);
            if (!show) return;
            VisibleTags++;

            string text = tag.Name.DisplayName;
            if (tag.Label.text != text)
            {
                tag.Label.text = text;
                tag.Root.sizeDelta = new Vector2(tag.Label.preferredWidth + 22f, 32f);
            }

            bool talking = tag.Voice != null && tag.Voice.IsHeardTalking;
            Color textColor = talking ? TalkingText : IdleText;
            textColor.a = alpha;
            tag.Label.color = textColor;
            Color back = Backing;
            back.a *= alpha;
            tag.Background.color = back;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, null, out Vector2 local);
            tag.Root.anchoredPosition = local;
        }

        private void RemoveTag(PlayerController player)
        {
            if (_tags.TryGetValue(player, out Tag tag) && tag.Root != null) Destroy(tag.Root.gameObject);
            _tags.Remove(player);
        }
    }
}
