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
        internal const int CanvasSortOrder = 521;
        internal const float MinimumWidth = 520f;
        internal const float MinimumHeight = 360f;

        private sealed class RowUi
        {
            internal string Id;
            internal TextMeshProUGUI Title;
            internal TextMeshProUGUI Provider;
            internal TextMeshProUGUI Location;
            internal TextMeshProUGUI Description;
            internal TextMeshProUGUI Progress;
            internal TextMeshProUGUI Reward;
            internal Image ProgressFill;
        }

        private GameObject _root;
        private RectTransform _panel;
        private RectTransform _bodyRoot;
        private RectTransform _collapseChevron;
        private GameObject _resizeGripRoot;
        private bool _collapsed;
        private float _expandedHeight;
        private RectTransform _content;
        private TextMeshProUGUI _zoneLabel;
        private TextMeshProUGUI _refreshLabel;
        private TextMeshProUGUI _footer;
        private RetainedPosition _position;
        private readonly Dictionary<string, RowUi> _rows = new Dictionary<string, RowUi>(StringComparer.OrdinalIgnoreCase);

        private string _structureSignature = string.Empty;
        private string _zone = string.Empty;
        private string _localBoardZone = string.Empty;
        private List<ContractOffer> _localOffers = new List<ContractOffer>();
        private List<ContractOffer> _globalOffers = new List<ContractOffer>();
        private ContractDocument _document;
        private long _localRefreshSeconds;
        private long _globalRefreshSeconds;
        private long _lastRenderedLocalRefreshSeconds = -1L;
        private long _lastRenderedGlobalRefreshSeconds = -1L;
        private string _claimStatus = string.Empty;
        private bool _nativeXpEnabled;
        private Action<string> _accept;
        private Action<string> _abandon;
        private Action<string> _claim;

        internal void Initialize(float storedX, float storedY, float width, float height,
            Action<float, float> persist, Action<float, float> persistSize, Action close, Action reset)
        {
            Dispose();
            float maxWidth = Mathf.Max(1f, Screen.width - 20f);
            float maxHeight = Mathf.Max(1f, Screen.height - 20f);
            width = Mathf.Clamp(width, Mathf.Min(MinimumWidth, maxWidth), maxWidth);
            height = Mathf.Clamp(height, Mathf.Min(MinimumHeight, maxHeight), maxHeight);
            _root = RetainedUiKit.CreateCanvas("ErenshorContractsCanvas", CanvasSortOrder);
            RectTransform canvas = _root.GetComponent<RectTransform>();
            _panel = RetainedUiKit.CreateRect("ContractsPanel", canvas);
            RetainedUiKit.AnchorBottomLeft(_panel, 0f, 0f, width, height);
            RetainedUiKit.AddImage(_panel, RetainedUiKit.Panel);
            _panel.gameObject.AddComponent<CanvasGroup>();
            _bodyRoot = RetainedUiKit.CreateRect("Body", _panel);
            RetainedUiKit.Stretch(_bodyRoot, 0f, 0f, 0f, 0f);
            _expandedHeight = height;
            _collapsed = false;

            BuildHeader(close, reset);

            RectTransform info = RetainedUiKit.CreateRect("Info", _bodyRoot);
            info.anchorMin = new Vector2(0f, 1f); info.anchorMax = new Vector2(1f, 1f); info.pivot = new Vector2(0.5f, 1f);
            info.offsetMin = new Vector2(10f, -66f); info.offsetMax = new Vector2(-10f, -35f);
            TextMeshProUGUI board = RetainedUiKit.AddLabel("Board", info, "CONTRACT BOARD", 11f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            board.rectTransform.anchorMin = Vector2.zero; board.rectTransform.anchorMax = new Vector2(0f, 1f); board.rectTransform.pivot = Vector2.zero;
            board.rectTransform.anchoredPosition = Vector2.zero; board.rectTransform.sizeDelta = new Vector2(112f, 0f);
            _zoneLabel = RetainedUiKit.AddLabel("Zone", info, "", 11f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _zoneLabel.rectTransform.anchorMin = Vector2.zero; _zoneLabel.rectTransform.anchorMax = Vector2.one;
            _zoneLabel.rectTransform.offsetMin = new Vector2(116f, 0f); _zoneLabel.rectTransform.offsetMax = new Vector2(-320f, 0f);
            _refreshLabel = RetainedUiKit.AddLabel("Refresh", info, "", 10f, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            _refreshLabel.color = RetainedUiKit.Muted;
            _refreshLabel.rectTransform.anchorMin = new Vector2(1f, 0f); _refreshLabel.rectTransform.anchorMax = Vector2.one; _refreshLabel.rectTransform.pivot = new Vector2(1f, 0.5f);
            _refreshLabel.rectTransform.anchoredPosition = Vector2.zero; _refreshLabel.rectTransform.sizeDelta = new Vector2(315f, 0f);

            RectTransform viewport;
            RectTransform rawContent;
            ScrollRect scroll = RetainedUiKit.AddScrollRect("ContractsScroll", _bodyRoot, false, true, out viewport, out rawContent);
            RectTransform sr = scroll.GetComponent<RectTransform>();
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(10f, 30f); sr.offsetMax = new Vector2(-10f, -70f);
            _content = RetainedUiKit.AddVerticalContent("ContractRows", viewport, 5f, 2);
            scroll.content = _content;

            _footer = RetainedUiKit.AddLabel("Footer", _bodyRoot, "", 9.5f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            _footer.rectTransform.anchorMin = Vector2.zero; _footer.rectTransform.anchorMax = new Vector2(1f, 0f); _footer.rectTransform.pivot = Vector2.zero;
            _footer.rectTransform.offsetMin = new Vector2(10f, 5f); _footer.rectTransform.offsetMax = new Vector2(-10f, 27f);

            _position = new RetainedPosition(storedX, storedY, 0.5f, 0.5f, persist);
            _position.Resolve(_panel);
            SuiteResizeHandler resize = RetainedUiKit.AddResizeGrip("ResizeGrip", _panel, _panel, 16f, new Vector2(MinimumWidth, MinimumHeight),
                delegate(float w, float h)
                {
                    _expandedHeight = Mathf.Max(MinimumHeight, h);
                    if (persistSize != null) persistSize(w, h);
                });
            _resizeGripRoot = resize == null ? null : resize.gameObject;
            RetainedUiKit.AddFrame(_panel, 1f);
            UpdateCollapseVisual();
            _root.SetActive(false);
        }

        private void BuildHeader(Action close, Action reset)
        {
            RectTransform header = RetainedUiKit.CreateRect("Header", _panel);
            RetainedUiKit.AnchorTopStretch(header, 0f, 0f, 0f, SuiteWindowChromePolicy.HeaderHeight);
            RetainedUiKit.AddImage(header, RetainedUiKit.Header);
            AddCollapseButton(header);
            TextMeshProUGUI title = RetainedUiKit.AddLabel("Title", header, "CONTRACTS", 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            RetainedUiKit.Stretch(title.rectTransform, 40f, 0f, 72f, 0f);
            AddHeaderButton(header, "Reset", "R", -38f, reset);
            AddHeaderButton(header, "Close", "X", -6f, close);
            RetainedUiKit.AddDragSurface("DragSurface", header, _panel, 36f, 72f,
                delegate
                {
                    if (_position == null) return;
                    if (_collapsed) _position.Clamp(_panel);
                    else _position.DragCompleted(_panel);
                });
        }

        internal void Tick(bool visible, string zone, string localBoardZone, List<ContractOffer> localOffers, List<ContractOffer> globalOffers,
            ContractDocument document, long localRefreshSeconds, long globalRefreshSeconds, bool nativeXpEnabled, string claimStatus,
            Action<string> accept, Action<string> abandon, Action<string> claim)
        {
            if (_root == null) return;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;
            bool fitted = RetainedUiKit.FitToScreen(_panel, 10f);
            if (_position != null)
            {
                if (_collapsed) _position.Clamp(_panel);
                else
                {
                    _position.Resolve(_panel);
                    if (fitted) _position.Clamp(_panel);
                }
            }

            _zone = zone ?? string.Empty;
            _localBoardZone = localBoardZone ?? string.Empty;
            _localOffers = localOffers ?? new List<ContractOffer>();
            _globalOffers = globalOffers ?? new List<ContractOffer>();
            _document = document;
            _localRefreshSeconds = Math.Max(0L, localRefreshSeconds);
            _globalRefreshSeconds = Math.Max(0L, globalRefreshSeconds);
            _nativeXpEnabled = nativeXpEnabled;
            _claimStatus = claimStatus ?? string.Empty;
            _accept = accept; _abandon = abandon; _claim = claim;

            if (_collapsed) return;

            if (_zoneLabel != null) _zoneLabel.text = string.IsNullOrWhiteSpace(_zone) ? "No active zone" : _zone;
            if (_refreshLabel != null &&
                (_lastRenderedLocalRefreshSeconds != _localRefreshSeconds ||
                 _lastRenderedGlobalRefreshSeconds != _globalRefreshSeconds))
            {
                _lastRenderedLocalRefreshSeconds = _localRefreshSeconds;
                _lastRenderedGlobalRefreshSeconds = _globalRefreshSeconds;
                _refreshLabel.text =
                    "LOCAL REFRESH  " + ContractCore.FormatRefreshCountdown(_localRefreshSeconds) +
                    "    GLOBAL REFRESH  " + ContractCore.FormatRefreshCountdown(_globalRefreshSeconds);
            }
            if (_footer != null)
            {
                if (!string.IsNullOrWhiteSpace(_claimStatus)) _footer.text = _claimStatus;
                else _footer.text = "Completed: " + (_document == null ? "0" : _document.TotalCompleted.ToString()) + "  |  " + ContractNativeRewardAdapter.CapabilitySummary(_nativeXpEnabled);
            }

            string signature = BuildStructureSignature();
            if (SuiteWindowChromePolicy.ShouldRebuildStructure(_structureSignature, signature))
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
            _panel = null; _bodyRoot = null; _collapseChevron = null; _resizeGripRoot = null;
            _collapsed = false; _expandedHeight = 0f;
            _content = null; _zoneLabel = null; _refreshLabel = null; _footer = null; _position = null;
            _rows.Clear(); _structureSignature = string.Empty;
            _lastRenderedLocalRefreshSeconds = -1L;
            _lastRenderedGlobalRefreshSeconds = -1L;
        }

        private string BuildStructureSignature()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("|CZ:").Append(_zone).Append("|LB:").Append(_localBoardZone);
            AppendOfferSignature(sb, "L", _localOffers);
            AppendOfferSignature(sb, "G", _globalOffers);

            if (_document != null)
            {
                for (int i = 0; i < _document.Active.Count; i++)
                {
                    ContractInstance a = _document.Active[i];
                    if (a == null || IsRepresented(a.OccurrenceId)) continue;
                    sb.Append("|X:").Append(a.OccurrenceId).Append(':');
                    if (ContractCore.HasUnknownRewardOutcome(a)) sb.Append('U');
                    else if (ContractCore.HasRetryableReward(a)) sb.Append('T');
                    else if (ContractCore.HasRewardTransactionStarted(a)) sb.Append('L');
                    else sb.Append(a.IsComplete ? 'R' : 'P');
                }
            }
            return sb.ToString();
        }

        private void AppendOfferSignature(StringBuilder sb, string prefix, List<ContractOffer> offers)
        {
            sb.Append('|').Append(prefix).Append(':').Append(offers.Count);
            for (int i = 0; i < offers.Count; i++)
            {
                ContractOffer o = offers[i];
                if (o == null || o.Template == null) { sb.Append("|null"); continue; }
                sb.Append('|').Append(o.OccurrenceId).Append(':');
                if (o.RewardLocked) sb.Append('U');
                else if (o.RewardRetryable) sb.Append('T');
                else if (o.Claimed) sb.Append('C');
                else if (o.Active == null) sb.Append('A');
                else sb.Append(o.Active.IsComplete ? 'R' : 'P');
            }
        }

        private void RebuildRows()
        {
            RetainedUiKit.ClearChildren(_content);
            _rows.Clear();

            string localOrigin = string.IsNullOrWhiteSpace(_zone) ? "unavailable" : _zone;
            AddSection("LOCAL CONTRACTS  ·  " + localOrigin);
            bool hasExtraLocal = HasUnrepresentedActive(ContractCategory.Local);
            if (_localOffers.Count == 0 && !hasExtraLocal) AddHint("No local contract templates are available for this zone.");
            else
            {
                for (int i = 0; i < _localOffers.Count; i++) BuildOfferRow(_localOffers[i], ContractCategory.Local);
                BuildUnrepresentedActive(ContractCategory.Local);
            }

            AddSection("GLOBAL CONTRACTS");
            bool hasExtraGlobal = HasUnrepresentedActive(ContractCategory.Global);
            if (_globalOffers.Count == 0 && !hasExtraGlobal) AddHint("No verified Global combat target is available yet. Explore another zone to build native enemy knowledge.");
            else
            {
                for (int i = 0; i < _globalOffers.Count; i++) BuildOfferRow(_globalOffers[i], ContractCategory.Global);
                BuildUnrepresentedActive(ContractCategory.Global);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }


        private bool HasUnrepresentedActive(string category)
        {
            if (_document == null) return false;
            string normalized = ContractCategory.Normalize(category);
            for (int i = 0; i < _document.Active.Count; i++)
            {
                ContractInstance active = _document.Active[i];
                if (active == null || IsRepresented(active.OccurrenceId)) continue;
                if (string.Equals(ContractCategory.Normalize(active.Category), normalized, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private void BuildUnrepresentedActive(string category)
        {
            if (_document == null) return;
            string normalized = ContractCategory.Normalize(category);
            for (int i = 0; i < _document.Active.Count; i++)
            {
                ContractInstance active = _document.Active[i];
                if (active == null || IsRepresented(active.OccurrenceId)) continue;
                if (!string.Equals(ContractCategory.Normalize(active.Category), normalized, StringComparison.Ordinal)) continue;
                BuildAdditionalRow(active);
            }
        }

        private void BuildOfferRow(ContractOffer offer, string category)
        {
            if (offer == null || offer.Template == null) return;
            RectTransform box = NewBox("Offer_" + SafeName(offer.OccurrenceId));
            RowUi row = NewRowUi(offer.OccurrenceId, box);
            row.Title.text = offer.Template.Title ?? string.Empty;
            row.Provider.text = ProviderLabel(category, offer.Template.ProviderId);
            row.Location.text = "LOCATION: " + ContractCore.LocationText(offer.Template, _zone);
            row.Description.text = offer.Template.Description ?? string.Empty;
            row.Reward.text = ContractNativeRewardAdapter.DescribeReward(offer.Template.RewardGoldAmount, offer.Template.RewardXpBasisPoints, offer.Template.RewardText, _nativeXpEnabled);

            if (offer.RewardLocked)
            {
                AddStateLine(box, "REWARD OUTCOME LOCKED · retry blocked to prevent a duplicate native reward");
            }
            else if (offer.Claimed)
            {
                AddStateLine(box, "CLAIMED");
            }
            else if (offer.Active != null)
            {
                BuildProgress(box, row);
                RectTransform actions = RetainedUiKit.AddHorizontalRow("Actions", box, 27f, 6f);
                if (offer.Active.IsComplete)
                {
                    bool applied = ContractCore.AllConfiguredRewardsApplied(offer.Active) && ContractCore.HasRewardTransactionStarted(offer.Active);
                    string state = applied ? "REWARD APPLIED" : (offer.RewardRetryable ? "RETRY READY" : "READY TO CLAIM");
                    AddStateLabel(actions, state, applied ? 104f : 104f);
                    string id = offer.Active.OccurrenceId;
                    RetainedUiKit.AddButton("Claim", actions, applied ? "Finalize" : (offer.RewardRetryable ? "Retry" : "Claim"), delegate { Invoke(_claim, id); }, 72f, 25f, false);
                }
                else AddStateLabel(actions, "ACTIVE", 62f);
                if (!ContractCore.HasRewardTransactionStarted(offer.Active))
                {
                    string abandonId = offer.Active.OccurrenceId;
                    RetainedUiKit.AddButton("Abandon", actions, "Abandon", delegate { Invoke(_abandon, abandonId); }, 76f, 25f, true);
                }
            }
            else
            {
                RectTransform actions = RetainedUiKit.AddHorizontalRow("Actions", box, 27f, 6f);
                string id = offer.OccurrenceId;
                AddStateLabel(actions, "AVAILABLE", 74f);
                RetainedUiKit.AddButton("Accept", actions, "Accept", delegate { Invoke(_accept, id); }, 76f, 25f, false);
                AddSmallLabel(actions, ContractCore.TargetText(offer.Template), 210f);
            }
            _rows[offer.OccurrenceId] = row;
        }

        private void BuildAdditionalRow(ContractInstance active)
        {
            RectTransform box = NewBox("Active_" + SafeName(active.OccurrenceId));
            RowUi row = NewRowUi(active.OccurrenceId, box);
            row.Title.text = active.Title ?? string.Empty;
            row.Provider.text = ContractCategory.Normalize(active.Category).ToUpperInvariant();
            row.Location.text = "LOCATION: " + ContractCore.LocationText(active);
            row.Description.text = active.Description ?? string.Empty;
            row.Reward.text = ContractNativeRewardAdapter.DescribeReward(active.RewardGoldAmount, active.RewardXpBasisPoints, active.RewardText, _nativeXpEnabled);
            BuildProgress(box, row);

            if (ContractCore.HasUnknownRewardOutcome(active))
            {
                AddStateLine(box, "REWARD OUTCOME LOCKED · retry blocked to prevent a duplicate native reward");
            }
            else
            {
                RectTransform actions = RetainedUiKit.AddHorizontalRow("Actions", box, 27f, 6f);
                if (active.IsComplete)
                {
                    bool applied = ContractCore.AllConfiguredRewardsApplied(active) && ContractCore.HasRewardTransactionStarted(active);
                    string state = applied ? "REWARD APPLIED" : (ContractCore.HasRetryableReward(active) ? "RETRY READY" : "READY TO CLAIM");
                    AddStateLabel(actions, state, 104f);
                    string claimId = active.OccurrenceId;
                    RetainedUiKit.AddButton("Claim", actions, applied ? "Finalize" : (ContractCore.HasRetryableReward(active) ? "Retry" : "Claim"), delegate { Invoke(_claim, claimId); }, 68f, 24f, false);
                }
                else AddStateLabel(actions, "ACTIVE", 62f);
                if (!ContractCore.HasRewardTransactionStarted(active))
                {
                    string abandonId = active.OccurrenceId;
                    RetainedUiKit.AddButton("Abandon", actions, "Abandon", delegate { Invoke(_abandon, abandonId); }, 76f, 24f, true);
                }
            }
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
            LayoutElement providerLe = row.Provider.gameObject.AddComponent<LayoutElement>(); providerLe.preferredWidth = 150f; providerLe.preferredHeight = 24f;
            row.Location = AddHintLabel(box, "");
            row.Location.color = RetainedUiKit.Edge;
            row.Description = AddBody(box, "");
            row.Reward = AddHintLabel(box, "");
            return row;
        }

        private void BuildProgress(RectTransform box, RowUi row)
        {
            RectTransform p = RetainedUiKit.AddHorizontalRow("Progress", box, 20f, 6f);
            row.Progress = RetainedUiKit.AddLabel("ProgressText", p, "", 10f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LayoutElement tl = row.Progress.gameObject.AddComponent<LayoutElement>(); tl.preferredWidth = 230f; tl.preferredHeight = 20f;
            RectTransform back = RetainedUiKit.CreateRect("ProgressBack", p);
            RetainedUiKit.AddImage(back, new Color(0.02f, 0.06f, 0.07f, 1f));
            LayoutElement bl = back.gameObject.AddComponent<LayoutElement>(); bl.flexibleWidth = 1f; bl.preferredHeight = 14f;
            RectTransform fill = RetainedUiKit.CreateRect("Fill", back);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = Vector2.zero; fill.sizeDelta = Vector2.zero;
            row.ProgressFill = RetainedUiKit.AddImage(fill, RetainedUiKit.Edge);
        }

        private void UpdateRows()
        {
            UpdateOfferProgress(_localOffers);
            UpdateOfferProgress(_globalOffers);
            if (_document != null)
                for (int i = 0; i < _document.Active.Count; i++) UpdateProgress(_document.Active[i]);
        }

        private void UpdateOfferProgress(List<ContractOffer> offers)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                ContractOffer offer = offers[i];
                if (offer == null || offer.Active == null) continue;
                UpdateProgress(offer.Active);
            }
        }

        private void UpdateProgress(ContractInstance active)
        {
            if (active == null) return;
            RowUi row;
            if (!_rows.TryGetValue(active.OccurrenceId, out row) || row == null || row.Progress == null) return;
            row.Progress.text = ContractCore.ProgressText(active);
            float ratio = Mathf.Clamp01(ContractCore.ProgressFraction(active));
            if (row.ProgressFill != null)
            {
                Vector2 max = row.ProgressFill.rectTransform.anchorMax;
                max.x = ratio;
                row.ProgressFill.rectTransform.anchorMax = max;
            }
        }

        private bool IsRepresented(string id)
        {
            return ContainsOffer(_localOffers, id) || ContainsOffer(_globalOffers, id);
        }

        private static bool ContainsOffer(List<ContractOffer> offers, string id)
        {
            for (int i = 0; i < offers.Count; i++)
                if (offers[i] != null && string.Equals(offers[i].OccurrenceId, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private RectTransform NewBox(string name)
        {
            RectTransform box = RetainedUiKit.AddVerticalContent(name, _content, 4f, 6);
            Image image = RetainedUiKit.AddImage(box, new Color(0.02f, 0.12f, 0.15f, 0.78f));
            image.raycastTarget = false;
            LayoutElement le = box.gameObject.AddComponent<LayoutElement>(); le.minHeight = 94f;
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

        private static void AddSmallLabel(Transform parent, string value, float width)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("State", parent, value, 10f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = width; le.preferredHeight = 25f;
        }

        private static void AddStateLabel(Transform parent, string value, float width)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("ContractState", parent, value, 9.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            label.color = RetainedUiKit.Edge;
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = width; le.preferredHeight = 25f;
        }

        private static void AddStateLine(Transform parent, string value)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("ContractState", parent, value, 9.5f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            label.color = RetainedUiKit.Edge;
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>(); le.minHeight = 18f; le.preferredHeight = 20f;
        }

        private void AddCollapseButton(RectTransform header)
        {
            Button button = RetainedUiKit.AddButton("Collapse", header, "", ToggleCollapsed, 28f, 24f, false);
            RectTransform rect = button.GetComponent<RectTransform>();
            LayoutElement layout = rect.GetComponent<LayoutElement>();
            if (layout != null) UnityEngine.Object.DestroyImmediate(layout);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(4f, 0f);
            rect.sizeDelta = new Vector2(28f, 24f);
            _collapseChevron = button.GetComponent<RectTransform>();
            StandaloneLauncherVisual.AddVerticalChevron(_collapseChevron, true);
        }

        private void ToggleCollapsed()
        {
            SetCollapsed(!_collapsed);
        }

        private void SetCollapsed(bool collapsed)
        {
            if (_panel == null || _collapsed == collapsed) return;
            float oldHeight = _panel.rect.height;

            if (collapsed && _expandedHeight < MinimumHeight) _expandedHeight = Mathf.Max(MinimumHeight, oldHeight);
            _collapsed = collapsed;

            float desired = SuiteWindowChromePolicy.ResolveDisplayHeight(_collapsed, _expandedHeight, MinimumHeight);
            if (!_collapsed) desired = Mathf.Min(desired, Mathf.Max(SuiteWindowChromePolicy.CollapsedHeight, Screen.height - 20f));
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, desired);

            Vector2 position = _panel.anchoredPosition;
            position.y = SuiteWindowChromePolicy.PreserveTopBottomY(position.y, oldHeight, desired);
            _panel.anchoredPosition = position;

            if (_bodyRoot != null) _bodyRoot.gameObject.SetActive(!_collapsed);
            if (_resizeGripRoot != null) _resizeGripRoot.SetActive(!_collapsed);
            UpdateCollapseVisual();

            if (_position != null)
            {
                _position.Clamp(_panel);
                if (!_collapsed) _position.DragCompleted(_panel);
            }
        }

        private void UpdateCollapseVisual()
        {
            if (_collapseChevron == null) return;
            for (int i = _collapseChevron.childCount - 1; i >= 0; i--)
                if (_collapseChevron.GetChild(i).name == "Chevron") UnityEngine.Object.Destroy(_collapseChevron.GetChild(i).gameObject);
            // Expanded points up to collapse; collapsed points down to expand.
            StandaloneLauncherVisual.AddVerticalChevron(_collapseChevron, !_collapsed);
        }

        private static void AddHeaderButton(RectTransform header, string name, string label, float right, Action action)
        {
            Button b = RetainedUiKit.AddButton(name, header, label, action, 28f, 24f, false);
            RectTransform r = b.GetComponent<RectTransform>();
            LayoutElement le = r.GetComponent<LayoutElement>(); if (le != null) UnityEngine.Object.DestroyImmediate(le);
            r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f); r.pivot = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(right, 0f); r.sizeDelta = new Vector2(28f, 24f);
        }

        private static string ProviderLabel(string category, string provider)
        {
            string prefix = ContractCategory.Normalize(category).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "builtin", StringComparison.OrdinalIgnoreCase)) return prefix;
            return prefix + " · " + provider.ToUpperInvariant();
        }

        private static void Invoke(Action<string> action, string value) { if (action != null) action(value); }
        private static string SafeName(string value) { return string.IsNullOrEmpty(value) ? "row" : value.Replace("/", "_").Replace("\\", "_").Replace(":", "_"); }
    }
}
