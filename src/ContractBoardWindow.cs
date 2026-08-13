using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErenshorContracts
{
    internal sealed class ContractBoardWindow
    {
        private sealed class RowUi
        {
            internal string Id;
            internal TextMeshProUGUI Title;
            internal TextMeshProUGUI Provider;
            internal TextMeshProUGUI Description;
            internal TextMeshProUGUI Progress;
            internal TextMeshProUGUI Reward;
            internal Image ProgressFill;
        }

        private GameObject _root;
        private RectTransform _panel;
        private RectTransform _content;
        private TextMeshProUGUI _zoneLabel;
        private TextMeshProUGUI _dateLabel;
        private TextMeshProUGUI _footer;
        private RetainedPosition _position;
        private readonly Dictionary<string, RowUi> _rows = new Dictionary<string, RowUi>(StringComparer.OrdinalIgnoreCase);

        private string _structureSignature = string.Empty;
        private string _zone = string.Empty;
        private List<ContractOffer> _offers = new List<ContractOffer>();
        private ContractDocument _document;
        private Action<string> _accept;
        private Action<string> _abandon;
        private Action<string> _claim;

        internal void Initialize(float storedX, float storedY, float width, float height,
            Action<float, float> persist, Action<float, float> persistSize, Action close, Action reset)
        {
            Dispose();
            width = Mathf.Clamp(width, 520f, Mathf.Max(520f, Screen.width - 20f));
            height = Mathf.Clamp(height, 360f, Mathf.Max(360f, Screen.height - 20f));
            _root = RetainedUiKit.CreateCanvas("ErenshorContractsCanvas", 521);
            RectTransform canvas = _root.GetComponent<RectTransform>();
            _panel = RetainedUiKit.CreateRect("ContractsPanel", canvas);
            RetainedUiKit.AnchorBottomLeft(_panel, 0f, 0f, width, height);
            RetainedUiKit.AddImage(_panel, RetainedUiKit.Panel);
            _panel.gameObject.AddComponent<CanvasGroup>();

            BuildHeader(close, reset);

            RectTransform info = RetainedUiKit.CreateRect("Info", _panel);
            info.anchorMin = new Vector2(0f, 1f); info.anchorMax = new Vector2(1f, 1f); info.pivot = new Vector2(0.5f, 1f);
            info.offsetMin = new Vector2(10f, -66f); info.offsetMax = new Vector2(-10f, -35f);
            TextMeshProUGUI local = RetainedUiKit.AddLabel("LocalBoard", info, "LOCAL BOARD", 11f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            local.rectTransform.anchorMin = Vector2.zero; local.rectTransform.anchorMax = new Vector2(0f, 1f); local.rectTransform.pivot = Vector2.zero;
            local.rectTransform.anchoredPosition = Vector2.zero; local.rectTransform.sizeDelta = new Vector2(92f, 0f);
            _zoneLabel = RetainedUiKit.AddLabel("Zone", info, "", 11f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _zoneLabel.rectTransform.anchorMin = Vector2.zero; _zoneLabel.rectTransform.anchorMax = Vector2.one;
            _zoneLabel.rectTransform.offsetMin = new Vector2(96f, 0f); _zoneLabel.rectTransform.offsetMax = new Vector2(-90f, 0f);
            _dateLabel = RetainedUiKit.AddLabel("Date", info, "", 10f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            _dateLabel.rectTransform.anchorMin = new Vector2(1f, 0f); _dateLabel.rectTransform.anchorMax = Vector2.one; _dateLabel.rectTransform.pivot = new Vector2(1f, 0.5f);
            _dateLabel.rectTransform.anchoredPosition = Vector2.zero; _dateLabel.rectTransform.sizeDelta = new Vector2(86f, 0f);

            RectTransform viewport;
            RectTransform rawContent;
            ScrollRect scroll = RetainedUiKit.AddScrollRect("ContractsScroll", _panel, false, true, out viewport, out rawContent);
            RectTransform sr = scroll.GetComponent<RectTransform>();
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(10f, 30f); sr.offsetMax = new Vector2(-10f, -70f);
            _content = RetainedUiKit.AddVerticalContent("ContractRows", viewport, 7f, 2);
            scroll.content = _content;

            _footer = RetainedUiKit.AddLabel("Footer", _panel,
                "Preview: no native XP/gold/item rewards are granted yet.", 10f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _footer.rectTransform.anchorMin = Vector2.zero; _footer.rectTransform.anchorMax = new Vector2(1f, 0f); _footer.rectTransform.pivot = Vector2.zero;
            _footer.rectTransform.offsetMin = new Vector2(10f, 5f); _footer.rectTransform.offsetMax = new Vector2(-10f, 27f);

            _position = new RetainedPosition(storedX, storedY, 0.5f, 0.5f, persist);
            _position.Resolve(_panel);
            RetainedUiKit.AddResizeGrip("ResizeGrip", _panel, _panel, 16f, new Vector2(520f, 360f), persistSize);
            _root.SetActive(false);
        }

        private void BuildHeader(Action close, Action reset)
        {
            RectTransform header = RetainedUiKit.CreateRect("Header", _panel);
            RetainedUiKit.AnchorTopStretch(header, 0f, 0f, 0f, 32f);
            RetainedUiKit.AddImage(header, RetainedUiKit.Header);
            TextMeshProUGUI title = RetainedUiKit.AddLabel("Title", header, "ERENSHOR CONTRACTS", 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            RetainedUiKit.Stretch(title.rectTransform, 10f, 0f, 72f, 0f);
            AddHeaderButton(header, "Reset", "R", -38f, reset);
            AddHeaderButton(header, "Close", "X", -6f, close);
            RetainedUiKit.AddDragSurface("DragSurface", header, _panel, 72f,
                delegate { if (_position != null) _position.DragCompleted(_panel); });
        }

        internal void Tick(bool visible, string zone, List<ContractOffer> offers, ContractDocument document,
            Action<string> accept, Action<string> abandon, Action<string> claim)
        {
            if (_root == null) return;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;
            if (_position != null) _position.Resolve(_panel);

            _zone = zone ?? string.Empty;
            _offers = offers ?? new List<ContractOffer>();
            _document = document;
            _accept = accept; _abandon = abandon; _claim = claim;
            if (_zoneLabel != null) _zoneLabel.text = string.IsNullOrWhiteSpace(_zone) ? "No active zone" : _zone;
            if (_dateLabel != null) _dateLabel.text = DateTime.Now.ToString("yyyy-MM-dd");
            if (_footer != null) _footer.text = "Preview: no native XP/gold/item rewards.  Completed: " + (_document == null ? "0" : _document.TotalCompleted.ToString());

            string signature = BuildStructureSignature();
            if (!string.Equals(signature, _structureSignature, StringComparison.Ordinal))
            {
                _structureSignature = signature;
                RebuildRows();
            }
            UpdateRows();
        }

        internal void ResetPosition() { if (_position != null) _position.Reset(_panel); }

        internal void ResetTransientState()
        {
            _structureSignature = string.Empty;
            _rows.Clear();
        }

        internal void Dispose()
        {
            SuiteDragHandler.ForceReleaseIfOwned();
            RetainedUiKit.DestroyRoot(ref _root);
            _panel = null; _content = null; _zoneLabel = null; _dateLabel = null; _footer = null; _position = null;
            _rows.Clear(); _structureSignature = string.Empty;
        }

        private string BuildStructureSignature()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_offers.Count);
            for (int i = 0; i < _offers.Count; i++)
            {
                ContractOffer o = _offers[i];
                if (o == null || o.Template == null) { sb.Append("|null"); continue; }
                sb.Append('|').Append(o.OccurrenceId).Append(':');
                if (o.Claimed) sb.Append('C');
                else if (o.Active == null) sb.Append('A');
                else sb.Append(o.Active.IsComplete ? 'R' : 'P');
            }

            if (_document != null)
            {
                for (int i = 0; i < _document.Active.Count; i++)
                {
                    ContractInstance a = _document.Active[i];
                    if (a == null || IsRepresented(a.OccurrenceId)) continue;
                    sb.Append("|X:").Append(a.OccurrenceId).Append(':').Append(a.IsComplete ? 'R' : 'P');
                }
            }
            return sb.ToString();
        }

        private void RebuildRows()
        {
            RetainedUiKit.ClearChildren(_content);
            _rows.Clear();
            AddSection("TODAY'S CONTRACTS");
            if (_offers.Count == 0)
            {
                AddHint("No contract templates are available for this scene yet.");
            }
            else
            {
                for (int i = 0; i < _offers.Count; i++) BuildOfferRow(_offers[i]);
            }

            bool addedOther = false;
            if (_document != null)
            {
                for (int i = 0; i < _document.Active.Count; i++)
                {
                    ContractInstance active = _document.Active[i];
                    if (active == null || IsRepresented(active.OccurrenceId)) continue;
                    if (!addedOther) { AddSection("OTHER ACTIVE CONTRACTS"); addedOther = true; }
                    BuildAdditionalRow(active);
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private void BuildOfferRow(ContractOffer offer)
        {
            if (offer == null || offer.Template == null) return;
            RectTransform box = NewBox("Offer_" + SafeName(offer.OccurrenceId));
            RowUi row = NewRowUi(offer.OccurrenceId, box);
            row.Title.text = offer.Template.Title ?? string.Empty;
            row.Provider.text = string.Equals(offer.Template.ProviderId, "builtin", StringComparison.OrdinalIgnoreCase)
                ? "LOCAL" : (offer.Template.ProviderId ?? string.Empty).ToUpperInvariant();
            row.Description.text = offer.Template.Description ?? string.Empty;
            row.Reward.text = offer.Template.RewardText ?? string.Empty;

            if (offer.Claimed)
            {
                AddHintTo(box, "Completed today");
            }
            else if (offer.Active != null)
            {
                BuildProgress(box, row);
                RectTransform actions = RetainedUiKit.AddHorizontalRow("Actions", box, 27f, 6f);
                if (offer.Active.IsComplete)
                {
                    string id = offer.Active.OccurrenceId;
                    RetainedUiKit.AddButton("Claim", actions, "Claim completion", delegate { Invoke(_claim, id); }, 124f, 25f, false);
                }
                else AddSmallLabel(actions, "Active", 54f);
                string abandonId = offer.Active.OccurrenceId;
                RetainedUiKit.AddButton("Abandon", actions, "Abandon", delegate { Invoke(_abandon, abandonId); }, 76f, 25f, true);
            }
            else
            {
                RectTransform actions = RetainedUiKit.AddHorizontalRow("Actions", box, 27f, 6f);
                string id = offer.OccurrenceId;
                RetainedUiKit.AddButton("Accept", actions, "Accept", delegate { Invoke(_accept, id); }, 86f, 25f, false);
                AddSmallLabel(actions, "Target: " + offer.Template.Target.ToString(), 92f);
            }
            _rows[offer.OccurrenceId] = row;
        }

        private void BuildAdditionalRow(ContractInstance active)
        {
            RectTransform box = NewBox("Active_" + SafeName(active.OccurrenceId));
            RowUi row = NewRowUi(active.OccurrenceId, box);
            row.Title.text = active.Title ?? string.Empty;
            row.Provider.text = active.OriginZone ?? string.Empty;
            row.Description.text = active.Description ?? string.Empty;
            row.Reward.text = active.RewardText ?? string.Empty;
            BuildProgress(box, row);
            RectTransform actions = RetainedUiKit.AddHorizontalRow("Actions", box, 27f, 6f);
            if (active.IsComplete)
            {
                string claimId = active.OccurrenceId;
                RetainedUiKit.AddButton("Claim", actions, "Claim", delegate { Invoke(_claim, claimId); }, 68f, 24f, false);
            }
            string abandonId = active.OccurrenceId;
            RetainedUiKit.AddButton("Abandon", actions, "Abandon", delegate { Invoke(_abandon, abandonId); }, 76f, 24f, true);
            _rows[active.OccurrenceId] = row;
        }

        private RowUi NewRowUi(string id, RectTransform box)
        {
            RowUi row = new RowUi();
            row.Id = id;
            RectTransform titleRow = RetainedUiKit.AddHorizontalRow("TitleRow", box, 24f, 6f);
            row.Title = RetainedUiKit.AddLabel("Title", titleRow, "", 12f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LayoutElement titleLe = row.Title.gameObject.AddComponent<LayoutElement>(); titleLe.flexibleWidth = 1f; titleLe.preferredHeight = 24f;
            row.Provider = RetainedUiKit.AddLabel("Provider", titleRow, "", 9f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            LayoutElement providerLe = row.Provider.gameObject.AddComponent<LayoutElement>(); providerLe.preferredWidth = 110f; providerLe.preferredHeight = 24f;
            row.Description = AddBody(box, "");
            row.Reward = AddHintLabel(box, "");
            return row;
        }

        private void BuildProgress(RectTransform box, RowUi row)
        {
            RectTransform p = RetainedUiKit.AddHorizontalRow("Progress", box, 20f, 6f);
            row.Progress = RetainedUiKit.AddLabel("ProgressText", p, "", 10f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LayoutElement tl = row.Progress.gameObject.AddComponent<LayoutElement>(); tl.preferredWidth = 88f; tl.preferredHeight = 20f;
            RectTransform back = RetainedUiKit.CreateRect("ProgressBack", p);
            RetainedUiKit.AddImage(back, new Color(0.02f, 0.06f, 0.07f, 1f));
            LayoutElement bl = back.gameObject.AddComponent<LayoutElement>(); bl.flexibleWidth = 1f; bl.preferredHeight = 14f;
            RectTransform fill = RetainedUiKit.CreateRect("Fill", back);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = Vector2.zero; fill.sizeDelta = Vector2.zero;
            row.ProgressFill = RetainedUiKit.AddImage(fill, RetainedUiKit.Edge);
        }

        private void UpdateRows()
        {
            for (int i = 0; i < _offers.Count; i++)
            {
                ContractOffer offer = _offers[i];
                if (offer == null || offer.Active == null) continue;
                UpdateProgress(offer.Active);
            }
            if (_document != null)
            {
                for (int i = 0; i < _document.Active.Count; i++) UpdateProgress(_document.Active[i]);
            }
        }

        private void UpdateProgress(ContractInstance active)
        {
            if (active == null) return;
            RowUi row;
            if (!_rows.TryGetValue(active.OccurrenceId, out row) || row == null || row.Progress == null) return;
            row.Progress.text = ContractCore.ProgressText(active);
            float ratio = active.Target <= 0 ? 0f : Mathf.Clamp01((float)active.Progress / (float)active.Target);
            if (row.ProgressFill != null)
            {
                Vector2 max = row.ProgressFill.rectTransform.anchorMax;
                max.x = ratio;
                row.ProgressFill.rectTransform.anchorMax = max;
            }
        }

        private bool IsRepresented(string id)
        {
            for (int i = 0; i < _offers.Count; i++)
                if (_offers[i] != null && string.Equals(_offers[i].OccurrenceId, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private RectTransform NewBox(string name)
        {
            RectTransform box = RetainedUiKit.AddVerticalContent(name, _content, 4f, 7);
            Image image = RetainedUiKit.AddImage(box, new Color(0.02f, 0.12f, 0.15f, 0.96f));
            image.raycastTarget = false;
            LayoutElement le = box.gameObject.AddComponent<LayoutElement>(); le.minHeight = 78f;
            return box;
        }

        private void AddSection(string value)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("Section", _content, value, 11f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 24f;
        }

        private void AddHint(string value)
        {
            TextMeshProUGUI label = AddHintLabel(_content, value);
            LayoutElement le = label.GetComponent<LayoutElement>(); if (le != null) le.minHeight = 28f;
        }

        private static TextMeshProUGUI AddBody(Transform parent, string value)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("Body", parent, value, 10.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>(); le.minHeight = 20f; le.preferredHeight = 32f;
            return label;
        }

        private static TextMeshProUGUI AddHintLabel(Transform parent, string value)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("Hint", parent, value, 9.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            label.color = RetainedUiKit.Muted;
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>(); le.minHeight = 18f; le.preferredHeight = 24f;
            return label;
        }

        private static void AddHintTo(Transform parent, string value) { AddHintLabel(parent, value); }

        private static void AddSmallLabel(Transform parent, string value, float width)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("State", parent, value, 10f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = width; le.preferredHeight = 25f;
        }

        private static void AddHeaderButton(RectTransform header, string name, string label, float right, Action action)
        {
            Button b = RetainedUiKit.AddButton(name, header, label, action, 28f, 24f, false);
            RectTransform r = b.GetComponent<RectTransform>();
            LayoutElement le = r.GetComponent<LayoutElement>(); if (le != null) UnityEngine.Object.DestroyImmediate(le);
            r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f); r.pivot = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(right, 0f); r.sizeDelta = new Vector2(28f, 24f);
        }

        private static void Invoke(Action<string> action, string value) { if (action != null) action(value); }
        private static string SafeName(string value) { return string.IsNullOrEmpty(value) ? "row" : value.Replace("/", "_").Replace("\\", "_").Replace(":", "_"); }
    }
}
