using System.Collections.Generic;
using System.Linq;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Drag-and-drop sort of the hierarchy of controls (tablet briefing). Cards start shuffled in a pile;
    // the learner drags each onto a ranked slot (1 = most effective). Scoring is deterministic (Core).
    public sealed class HierarchyBoard : MonoBehaviour
    {
        private readonly List<RectTransform> slots = new List<RectTransform>();
        private readonly Dictionary<RectTransform, DragCard> placed = new Dictionary<RectTransform, DragCard>();
        private RectTransform pile;

        public static HierarchyBoard Build(RectTransform parent, Font font, System.Action<IReadOnlyList<string>> onSubmit)
        {
            var go = new GameObject("HierarchyBoard", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 430;
            var board = go.AddComponent<HierarchyBoard>();
            var rt = (RectTransform)go.transform;

            for (var i = 0; i < 5; i++)
            {
                var slot = Panel(rt, $"Slot{i + 1}", new Vector2(0, 1), new Vector2(0.52f, 1), new Vector2(0, -8 - i * 62), 56, new Color(1, 1, 1, 0.06f));
                Text(slot, $"{i + 1}", font, 18, new Color(1f, .78f, .1f), TextAnchor.MiddleLeft, 8);
                board.slots.Add(slot);
            }
            board.pile = Panel(rt, "Pile", new Vector2(0.55f, 1), new Vector2(1, 1), new Vector2(0, -8), 5 * 62 - 6, new Color(0, 0, 0, 0));
            var names = HierarchyOrdering.Correct.OrderBy(n => (n.GetHashCode() * 7919) & 0xffff).ToList(); // stable shuffle
            for (var i = 0; i < names.Count; i++)
            {
                var card = Panel(board.pile, names[i], new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -i * 62), 56, new Color(.19f, .26f, .27f));
                Text(card, names[i], font, 22, Color.white, TextAnchor.MiddleCenter, 0);
                card.gameObject.AddComponent<CanvasGroup>();
                card.gameObject.AddComponent<DragCard>().Init(board, names[i]);
            }
            var check = Panel(rt, "Check", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), 50, new Color(1f, .78f, .1f));
            check.pivot = new Vector2(0.5f, 0);
            Text(check, "Check order", font, 22, new Color(.08f, .08f, .08f), TextAnchor.MiddleCenter, 0);
            check.gameObject.AddComponent<Button>().onClick.AddListener(() => onSubmit(board.Order()));
            return board;
        }

        public IReadOnlyList<string> Order() => slots.Select(s => placed.TryGetValue(s, out var c) ? c.Label : "").ToList();

        // Test hook: place cards programmatically in the given order.
        public void PlaceAll(IReadOnlyList<string> order)
        {
            var cards = GetComponentsInChildren<DragCard>(true);
            for (var i = 0; i < slots.Count && i < order.Count; i++)
                Drop(cards.First(c => c.Label == order[i]), slots[i]);
        }

        internal void DropAt(DragCard card, Vector2 screenPoint, Camera cam)
        {
            var slot = slots.FirstOrDefault(s => RectTransformUtility.RectangleContainsScreenPoint(s, screenPoint, cam));
            if (slot == null) { card.ReturnHome(); return; }
            Drop(card, slot);
        }

        private void Drop(DragCard card, RectTransform slot)
        {
            foreach (var kv in placed.Where(kv => kv.Value == card).ToList()) placed.Remove(kv.Key); // leaving its old slot
            if (placed.TryGetValue(slot, out var other)) other.ReturnHome();                      // swap back the occupant
            placed[slot] = card;
            card.SnapTo(slot);
        }

        internal RectTransform Pile => pile;

        static RectTransform Panel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, float height, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(0, height);
            go.GetComponent<Image>().color = color;
            return rt;
        }

        static void Text(RectTransform parent, string value, Font font, int size, Color color, TextAnchor anchor, float pad)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, 0); rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>(); t.text = value; t.font = font; t.fontSize = size; t.color = color; t.alignment = anchor; t.raycastTarget = false;
        }
    }

    public sealed class DragCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private HierarchyBoard board;
        private RectTransform rt;
        private Vector2 homePos;
        public string Label { get; private set; }

        public void Init(HierarchyBoard owner, string label)
        {
            board = owner; Label = label; rt = (RectTransform)transform; homePos = rt.anchoredPosition;
        }

        public void OnBeginDrag(PointerEventData e) { GetComponent<CanvasGroup>().blocksRaycasts = false; transform.SetAsLastSibling(); }

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rt.parent, e.position, e.pressEventCamera, out var local);
            rt.localPosition = local;
        }

        public void OnEndDrag(PointerEventData e)
        {
            GetComponent<CanvasGroup>().blocksRaycasts = true;
            board.DropAt(this, e.position, e.pressEventCamera);
        }

        public void SnapTo(RectTransform slot)
        {
            rt.SetParent(slot, false);
            rt.anchorMin = new Vector2(0.12f, 0); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.offsetMin = new Vector2(rt.offsetMin.x, 3); rt.offsetMax = new Vector2(-3, -3);
            GetComponent<Image>().color = new Color(.23f, .34f, .36f);
        }

        public void ReturnHome()
        {
            rt.SetParent(board.Pile, false);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = homePos; rt.sizeDelta = new Vector2(0, 56);
            GetComponent<Image>().color = new Color(.19f, .26f, .27f);
        }
    }
}
