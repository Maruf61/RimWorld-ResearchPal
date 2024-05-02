// ResearchNode.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static ResearchPal.Constants;

namespace ResearchPal
{
    public enum Painter
    {
        Tree = 0,
        Queue = 1,
        Drag = 2
    }

    public class ResearchNode : Node
    {
        private static readonly Dictionary<ResearchProjectDef, bool> _buildingPresentCache =
            new Dictionary<ResearchProjectDef, bool>();

        private static readonly Dictionary<ResearchProjectDef, List<ThingDef>> _missingFacilitiesCache =
            new Dictionary<ResearchProjectDef, List<ThingDef>>();

        public ResearchProjectDef Research;

        public ResearchNode(ResearchProjectDef research)
        {
            Research = research;

            // initialize position at vanilla y position, leave x at zero - we'll determine this ourselves
            _pos = new Vector2(0, research.researchViewY + 1);
        }

        public bool isMatched = false;

        private bool _available = false;

        private List<Def> _unlocks;

        private Painter _currentPainter;

        public bool PainterIs(Painter p)
        {
            return p == _currentPainter;
        }

        private List<Def> Unlocks()
        {
            if (_unlocks == null)
            {
                // _unlocks = Research.GetUnlockDefsAndDescs();
                _unlocks = Research.GetUnlockDefs();
            }

            return _unlocks;
        }

        private void UpdateAvailable()
        {
            _available = GetAvailable();
        }

        private HighlightReasonSet _highlightReasons = new HighlightReasonSet();

        public override bool Highlighted()
        {
            return _highlightReasons.Highlighted();
        }

        public void Highlight(Highlighting.Reason r)
        {
            _highlightReasons.Highlight(r);
        }

        public bool HighlightedAs(Highlighting.Reason r)
        {
            return _highlightReasons.HighlightedAs(r);
        }

        public List<ResearchNode> Parents
        {
            get
            {
                return InNodes.OfType<ResearchNode>()
                    .Concat(
                        InNodes.OfType<DummyNode>().SelectMany(dn => dn.Parent))
                    .ToList();
            }
        }

        private Color HighlightColor()
        {
            return Highlighting.Color(
                _highlightReasons.Current(), Research.techLevel);
        }

        public bool Unhighlight(Highlighting.Reason r)
        {
            return _highlightReasons.Unhighlight(r);
        }

        public IEnumerable<Highlighting.Reason> HighlightReasons()
        {
            return _highlightReasons.Reasons();
        }

        public override Color Color
        {
            get
            {
                if (Completed() && (!IsUnmatchedInSearch() || Highlighted()))
                {
                    return Assets.ColorCompleted[Research.techLevel];
                }

                if (Highlighted())
                {
                    return HighlightColor();
                }

                if (IsUnmatchedInSearch())
                {
                    return Assets.ColorUnmatched[Research.techLevel];
                }

                if (Available())
                {
                    return Assets.ColorCompleted[Research.techLevel];
                }

                return Assets.ColorUnavailable[Research.techLevel];
            }
        }

        public bool IsUnmatchedInSearch()
        {
            // return highlightReasons.Contains(HL.Reason.SearchUnmatched);
            return MainTabWindow_ResearchTree.Instance.SearchActive() && !isMatched;
        }

        public bool IsMatchedInSearch()
        {
            // return highlightReasons.Contains(HL.Reason.SearchMatched);
            return MainTabWindow_ResearchTree.Instance.SearchActive() && isMatched;
        }

        public bool HighlightInEdge(ResearchNode from)
        {
            foreach (var r1 in HighlightReasons())
            {
                foreach (var r2 in from.HighlightReasons())
                {
                    if (Highlighting.Similar(r1, r2))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public override Color InEdgeColor(ResearchNode from)
        {
            if (HighlightInEdge(from))
                return Assets.NormalHighlightColor;
            if (MainTabWindow_ResearchTree.Instance.SearchActive())
            {
                return Assets.ColorUnmatched[Research.techLevel];
            }

            if (Completed())
                return Assets.ColorEdgeCompleted[Research.techLevel];
            if (Available())
                return Assets.ColorAvailable[Research.techLevel];
            return Assets.ColorUnavailable[Research.techLevel];
        }

        public List<ResearchNode> Children
        {
            get
            {
                return OutNodes.OfType<ResearchNode>()
                    .Concat(
                        OutNodes.OfType<DummyNode>().SelectMany(dn => dn.Child))
                    .ToList();
            }
        }

        public override string Label => Research.LabelCap;

        public static bool BuildingPresent(ResearchNode node)
        {
            var research = node.Research;
            if (DebugSettings.godMode && Prefs.DevMode)
                return true;

            // try get from cache
            bool result;
            if (_buildingPresentCache.TryGetValue(research, out result))
                return result;

            // do the work manually
            if (research.requiredResearchBuilding == null)
            {
                result = true;
            }
            else
            {
                result = Find.Maps.SelectMany(map => map.listerBuildings.allBuildingsColonist)
                    .OfType<Building_ResearchBench>()
                    .Any(b => research.CanBeResearchedAt(b, true));
            }

            if (result)
            {
                result = node.MissingPrerequisites().All(BuildingPresent);
            }

            // update cache
            _buildingPresentCache.Add(research, result);
            return result;
        }

        public static bool TechprintAvailable(ResearchProjectDef research)
        {
            return research.TechprintRequirementMet;
        }

        public static void ClearCaches()
        {
            _buildingPresentCache.Clear();
            _missingFacilitiesCache.Clear();
        }

        public int Matches(string query)
        {
            var culture = CultureInfo.CurrentUICulture;
            query = query.ToLower(culture);

            if (Research.LabelCap.RawText.ToLower(culture).Contains(query))
                return 1;
            if (Unlocks().Any(
                    unlock => unlock.LabelCap.RawText.ToLower(culture).Contains(query)))
                return 2;
            if ((Research.modContentPack?.Name.ToLower(culture) ?? "").Contains(query))
            {
                return 3;
            }

            if (Settings.searchByDescription)
            {
                if (Research.description.ToLower(culture).Contains(query))
                    return 4;
            }

            return 0;
        }

        public static List<ThingDef> MissingFacilities(ResearchProjectDef research)
        {
            // try get from cache
            List<ThingDef> missing;
            if (_missingFacilitiesCache.TryGetValue(research, out missing))
                return missing;

            // get list of all researches required before this
            var thisAndPrerequisites = research.Ancestors().Where(rpd => !rpd.IsFinished).ToList();
            thisAndPrerequisites.Add(research);

            // get list of all available research benches
            var availableBenches = Find.Maps.SelectMany(map => map.listerBuildings.allBuildingsColonist)
                .OfType<Building_ResearchBench>();
            var availableBenchDefs = availableBenches.Select(b => b.def).Distinct();
            missing = new List<ThingDef>();

            // check each for prerequisites
            // TODO: We should really build this list recursively so we can re-use results for prerequisites.
            foreach (var rpd in thisAndPrerequisites)
            {
                if (rpd.requiredResearchBuilding == null)
                    continue;

                if (!availableBenchDefs.Contains(rpd.requiredResearchBuilding))
                    missing.Add(rpd.requiredResearchBuilding);

                if (rpd.requiredResearchFacilities.NullOrEmpty())
                    continue;

                foreach (var facility in rpd.requiredResearchFacilities)
                    if (!availableBenches.Any(b => b.HasFacility(facility)))
                        missing.Add(facility);
            }

            // add to cache
            missing = missing.Distinct().ToList();
            _missingFacilitiesCache.Add(research, missing);
            return missing;
        }

        public bool BuildingPresent()
        {
            return BuildingPresent(this);
        }

        public bool TechprintAvailable()
        {
            return TechprintAvailable(Research);
        }

        public override int DefaultPriority()
        {
            return InEdges.Count() + OutEdges.Count();
        }

        public override int LayoutUpperPriority()
        {
            return InEdges.Count();
        }

        public override int LayoutLowerPriority()
        {
            return OutEdges.Count();
        }

        public override int CompareTieBreaker(Node that)
        {
            if (that is DummyNode)
            {
                return -1;
            }

            var n = that as ResearchNode;
            var c1 = Research.techLevel.CompareTo(n.Research.techLevel);
            if (c1 != 0) return c1;
            return (Research.modContentPack?.Name ?? "").CompareTo(n.Research.modContentPack?.Name ?? "");
        }

        private void DrawProgressBarImpl(Rect rect)
        {
            GUI.color = Assets.ColorAvailable[Research.techLevel];
            rect.xMin += Research.ProgressPercent * rect.width;
            GUI.DrawTexture(rect, BaseContent.WhiteTex);
        }

        private void DrawBackground(bool mouseOver)
        {
            // researches that are completed or could be started immediately, and that have the required building(s) available
            GUI.color = Color;

            if (mouseOver)
                GUI.DrawTexture(Rect, Assets.ButtonActive);
            else
                GUI.DrawTexture(Rect, Assets.Button);
        }

        private void DrawProgressBar()
        {
            // grey out center to create a progress bar effect, completely greying out research not started.
            if (IsMatchedInSearch()
                || !IsUnmatchedInSearch() && _available
                || Highlighted())
            {
                var progressBarRect = Rect.ContractedBy(3f);
                DrawProgressBarImpl(progressBarRect);
            }
        }

        private void HandleTooltips()
        {
            if (PainterIs(Painter.Drag))
            {
                return;
            }

            Text.WordWrap = true;


            if (!Settings.disableShortcutManual)
            {
                TooltipHandler.TipRegion(Rect, ShortcutManualTooltip, Research.GetHashCode() + 2);
            }

            // attach description and further info to a tooltip
            if (!TechprintAvailable())
            {
                TooltipHandler.TipRegion(Rect,
                    "InsufficientTechprintsApplied".Translate(Research.TechprintsApplied, Research.TechprintCount));
                // ResourceBank.String.MissingTechprints(Research.TechprintsApplied, Research.techprintCount));
            }

            if (!BuildingPresent())
            {
                TooltipHandler.TipRegion(Rect,
                    ResourceBank.String.MissingFacilities(
                        string.Join(", ",
                            MissingFacilities().Select(td => td.LabelCap)
                                .ToArray())));
            }

            if (!PassCustomUnlockRequirements(Research)
                || CompatibilityHooks.PassCustomUnlockRequirements(Research))
            {
                var prompts = CustomUnlockRequirementPrompts(Research);
                prompts.AddRange(CompatibilityHooks.CustomUnlockRequirementPrompts(Research));
                foreach (var prompt in prompts)
                {
                    TooltipHandler.TipRegion(Rect, prompt);
                }
            }

            if (Research.techLevel > Faction.OfPlayer.def.techLevel)
            {
                TooltipHandler.TipRegion(
                    Rect, TechLevelTooLowTooltip, Research.GetHashCode() + 3);
            }

            TooltipHandler.TipRegion(
                Rect, GetResearchTooltipString, Research.GetHashCode());

            if (Settings.progressTooltip && ProgressWorthDisplaying() && !Research.IsFinished)
            {
                TooltipHandler.TipRegion(Rect, string.Format("Progress: {0}", ProgressString()));
            }
        }

        private string ShortcutManualTooltip()
        {
            if (Event.current.shift)
            {
                StringBuilder builder = new StringBuilder();
                if (PainterIs(Painter.Queue))
                {
                    builder.AppendLine(ResourceBank.String.LClickRemoveFromQueue);
                }
                else
                {
                    if (Available())
                    {
                        builder.AppendLine(ResourceBank.String.LClickReplaceQueue);
                        builder.AppendLine(ResourceBank.String.SLClickAddToQueue);
                        builder.AppendLine(ResourceBank.String.ALClickAddToQueue);
                    }

                    if (DebugSettings.godMode)
                    {
                        builder.AppendLine(ResourceBank.String.CLClickDebugInstant);
                    }
                }

                if (Available())
                {
                    builder.AppendLine(ResourceBank.String.Drag);
                }

                builder.AppendLine(ResourceBank.String.RClickHighlight);
                builder.AppendLine(ResourceBank.String.RClickIcon);
                return builder.ToString();
            }
            else
            {
                return ResourceBank.String.ShiftForShortcutManual;
            }
        }

        private string TechLevelTooLowTooltip()
        {
            var techlevel = Faction.OfPlayer.def.techLevel;
            return ResourceBank.String.TechLevelTooLow(
                techlevel, Research.CostFactor(techlevel), (int)Research.baseCost);
        }

        private IEnumerable<ResearchProjectDef> OtherLockedPrerequisites(
            IEnumerable<ResearchProjectDef> ps)
        {
            if (ps == null)
            {
                return new List<ResearchProjectDef>();
            }

            return ps.Where(p => !p.IsFinished && p != Research);
        }

        private string OtherPrereqTooltip(List<ResearchProjectDef> ps)
        {
            if (ps.NullOrEmpty())
            {
                return "";
            }

            return ResourceBank.String.OtherPrerequisites(
                String.Join(", ", ps.Distinct().Select(p => p.LabelCap)));
        }

        private string UnlockItemTooltip(Def def)
        {
            string unlockTooltip = "";
            string otherPrereqTooltip = "";
            if (def is TerrainDef)
            {
                unlockTooltip += ResourceBank.String.AllowsBuildingX(def.LabelCap);
                otherPrereqTooltip +=
                    OtherPrereqTooltip(OtherLockedPrerequisites(
                        (def as TerrainDef).researchPrerequisites).ToList());
            }
            else if (def is RecipeDef)
            {
                unlockTooltip += ResourceBank.String.AllowsCraftingX(def.LabelCap);
                otherPrereqTooltip +=
                    OtherPrereqTooltip(OtherLockedPrerequisites(
                        (def as RecipeDef).researchPrerequisites).ToList());
            }
            else if (def is ThingDef)
            {
                ThingDef thing = def as ThingDef;
                List<ResearchProjectDef> plantPrerequisites =
                    thing.plant?.sowResearchPrerequisites ?? new List<ResearchProjectDef>();
                if (plantPrerequisites.Contains(Research))
                {
                    unlockTooltip += ResourceBank.String.AllowsPlantingX(def.LabelCap);
                    otherPrereqTooltip +=
                        OtherPrereqTooltip(
                            OtherLockedPrerequisites(plantPrerequisites).ToList());
                }
                else
                {
                    unlockTooltip += ResourceBank.String.AllowsBuildingX(def.LabelCap);
                    OtherPrereqTooltip(OtherLockedPrerequisites(
                        (def as BuildableDef).researchPrerequisites).ToList());
                }
            }
            else
            {
                unlockTooltip += ResourceBank.String.AllowGeneralX(def.LabelCap);
            }

            string tooltip = otherPrereqTooltip == ""
                ? unlockTooltip
                : unlockTooltip + "\n\n" + otherPrereqTooltip;
            return tooltip;
        }

        private FloatMenu MakeInfoMenuFromDefs(IEnumerable<Def> defs)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (var def in defs)
            {
                Texture2D icon = def.IconTexture();
                Dialog_InfoCard.Hyperlink hyperlink = new Dialog_InfoCard.Hyperlink(def);

                options.Add(new FloatMenuOption(
                    def.label, () => hyperlink.ActivateHyperlink(), icon, def.IconColor(),
                    MenuOptionPriority.Default,
                    rect => TooltipHandler.TipRegion(
                        rect, () => UnlockItemTooltip(def),
                        def.GetHashCode() + Research.GetHashCode())));
            }

            return new FloatMenu(options);
        }

        private void IconActions(bool draw)
        {
            // handle only right click
            if (!draw && !(Event.current.type == EventType.MouseDown && Event.current.button == 1))
            {
                return;
            }

            var unlocks = Unlocks();
            for (var i = 0; i < unlocks.Count; ++i)
            {
                var iconRect = new Rect(
                    IconsRect.xMax - (i + 1) * (IconSize.x + 4f),
                    IconsRect.yMin + (IconsRect.height - IconSize.y) / 2f,
                    IconSize.x,
                    IconSize.y);

                if (iconRect.xMin - IconSize.x < IconsRect.xMin
                    && i + 1 < unlocks.Count)
                {
                    // stop the loop if we're about to overflow and have 2 or more unlocks yet to print.
                    iconRect.x = IconsRect.x + 4f;

                    if (draw)
                    {
                        GUI.DrawTexture(iconRect, Assets.MoreIcon, ScaleMode.ScaleToFit);
                        if (!PainterIs(Painter.Drag))
                        {
                            var tip = string.Join(
                                "\n",
                                unlocks.GetRange(i, unlocks.Count - i).Select(p => p.LabelCap).ToArray());
                            TooltipHandler.TipRegion(iconRect, tip);
                        }
                    }
                    else if
                        (!draw && Mouse.IsOver(iconRect)
                               && Find.WindowStack.FloatMenu == null)
                    {
                        var floatMenu = MakeInfoMenuFromDefs(unlocks.Skip(i));
                        Find.WindowStack.Add(floatMenu);
                        Event.current.Use();
                    }

                    break;
                }

                var def = unlocks[i];

                if (draw)
                {
                    def.DrawColouredIcon(iconRect);
                    if (!PainterIs(Painter.Drag))
                    {
                        TooltipHandler.TipRegion(
                            iconRect, () => UnlockItemTooltip(def),
                            def.GetHashCode() + Research.GetHashCode());
                    }
                }
                else if (Mouse.IsOver(iconRect))
                {
                    Dialog_InfoCard.Hyperlink link = new Dialog_InfoCard.Hyperlink(def);
                    link.ActivateHyperlink();
                    Event.current.Use();
                    break;
                }
            }
        }

        private void DrawNodeDetailMode(bool mouseOver)
        {
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            Text.Font = _largeLabel ? GameFont.Tiny : GameFont.Small;
            Widgets.Label(LabelRect, Research.LabelCap);

            GUI.DrawTexture(
                CostIconRect,
                !Completed() && !Available() ? Assets.Lock : Assets.ResearchIcon,
                ScaleMode.ScaleToFit);

            Color savedColor = GUI.color;
            Color numberColor;
            float numberToDraw;
            if (Settings.alwaysDisplayProgress && ProgressWorthDisplaying() || SwitchToProgress())
            {
                if (Research.IsFinished)
                {
                    numberColor = Color.cyan;
                    numberToDraw = 0;
                }
                else
                {
                    numberToDraw = Research.CostApparent - Research.ProgressApparent;
                    numberColor = Color.green;
                }
            }
            else
            {
                numberToDraw = Research.CostApparent;
                numberColor = savedColor;
            }

            if (IsUnmatchedInSearch() && (!Highlighted()))
            {
                numberColor = Color.gray;
            }

            GUI.color = numberColor;
            Text.Anchor = TextAnchor.UpperRight;

            Text.Font = NumericalFont(numberToDraw);
            Widgets.Label(CostLabelRect, numberToDraw.ToStringByStyle(ToStringStyle.Integer));
            GUI.color = savedColor;

            IconActions(true);
        }

        private string ProgressString()
        {
            return string.Format("{0} / {1}",
                Research.ProgressApparent.ToStringByStyle(ToStringStyle.Integer),
                Research.CostApparent.ToStringByStyle(ToStringStyle.Integer));
        }

        private bool ProgressWorthDisplaying()
        {
            return Research.ProgressApparent > 0;
        }

        private void DrawNodeZoomedOut(bool mouseOver)
        {
            string textToDraw;
            if (SwitchToProgress() && !Research.IsFinished)
            {
                textToDraw = ProgressString();
            }
            else
            {
                textToDraw = Research.LabelCap;
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            // Text.Font     = ChooseFont(textToDraw, Rect, GameFont.Medium, true);
            // Text.WordWrap = Text.Font == GameFont.Medium ? false : true;
            Text.Font = GameFont.Medium;
            Text.WordWrap = true;
            Widgets.Label(Rect, textToDraw);
        }

        bool ShouldGreyOutText()
        {
            return !
                ((Completed() || Available())
                 && (Highlighted() || !IsUnmatchedInSearch()));
        }

        private void DrawNode(bool detailedMode, bool mouseOver)
        {
            if (Research.knowledgeCategory != null)
            {
                var re = new Rect(Rect.x - 2, Rect.y - 2, Rect.width + 4, Rect.height + 4);
                GUI.DrawTexture(re,
                    Research.knowledgeCategory == KnowledgeCategoryDefOf.Basic
                        ? Assets.OrangeBackground
                        : Assets.RedBackground);
            }

            HandleTooltips();

            DrawBackground(mouseOver);
            DrawProgressBar();


            // draw the research label
            if (ShouldGreyOutText())
                GUI.color = Color.grey;
            else
                GUI.color = Color.white;

            if (detailedMode)
            {
                DrawNodeDetailMode(mouseOver);
            }
            else
            {
                DrawNodeZoomedOut(mouseOver);
            }

            Text.WordWrap = true;
        }


        public static GameFont NumericalFont(float number)
        {
            return number >= 1000000 ? GameFont.Tiny : GameFont.Small;
        }

        public static bool RightClick(Rect rect)
        {
            return Input.GetMouseButton(1) && Mouse.IsOver(rect);
        }

        public bool MouseOver()
        {
            return Mouse.IsOver(Rect);
        }

        public bool SwitchToProgress()
        {
            return Tree.DisplayProgressState && ProgressWorthDisplaying();
        }

        public bool MouseOver(Vector2 mousePos)
        {
            return Rect.Contains(mousePos);
        }

        private void HandleDragging(bool mouseOver)
        {
            var evt = Event.current;
            if (!mouseOver || Event.current.shift || Event.current.alt)
            {
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && Available())
            {
                MainTabWindow_ResearchTree.Instance.StartDragging(this, _currentPainter);
                if (PainterIs(Painter.Queue))
                {
                    if (this.Research.knowledgeCategory != null)
                    {
                        AnomalyQueue.NotifyNodeDraggedS();
                    }
                    else
                    {
                        Queue.NotifyNodeDraggedS();
                    }
                }

                Highlight(Highlighting.Reason.Focused);
                Event.current.Use();
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0 && PainterIs(Painter.Tree))
            {
                var tab = MainTabWindow_ResearchTree.Instance;

                if (tab.DraggedNode() == this && tab.DraggingTime() < Constants.DraggingClickDelay)
                {
                    LeftClick();
                    tab.StopDragging();
                    Event.current.Use();
                }
            }
        }

        public void NotifyDraggingRelease()
        {
            Unhighlight(Highlighting.Reason.Focused);
        }

        public bool IsDragged()
        {
            return MainTabWindow_ResearchTree.Instance.DraggedNode() == this;
        }

        private bool DetailMode()
        {
            return PainterIs(Painter.Queue)
                   || PainterIs(Painter.Drag)
                   || MainTabWindow_ResearchTree.Instance.ZoomLevel < DetailedModeZoomLevelCutoff;
        }

        // bool prevOver = false;

        /// <summary>
        ///     Draw the node, including interactions.
        /// </summary>
        public override void Draw(Rect visibleRect, Painter painter)
        {
            _currentPainter = painter;
            // Call site should ensure this condition
            // if (!IsVisible(visibleRect)) {
            //     return;
            // }
            var mouseOver = Mouse.IsOver(Rect);

            if (Event.current.type == EventType.Repaint)
            {
                UpdateAvailable();
                DrawNode(DetailMode(), mouseOver);
            }


            // Tool tip debugging fail attempt
            // var curOver = Mouse.IsOver(Rect);
            // if (prevOver && !curOver) {
            //     var w = Find.WindowStack.GetWindowAt(UI.GUIToScreenPoint(Event.current.mousePosition)); 
            //     var w2 = Find.WindowStack[Find.WindowStack.Count - 1];
            //     var ms = UI.GUIToScreenPoint(Event.current.mousePosition);
            //     // var w = Find.WindowStack[Find.WindowStack.Count - 1];
            //     // bool curOver = Rect.Contains(Event.current.mousePosition) && ! (b1 && ! b2 || (b3 || b4));
            //     Log.Message("LEAVING {0}, immediate: {1}, rect: {2}, {7} pos: {9} => {3} => {10},  rect2 : {4}, {8} w: {5} h: {6}",
            //         Research.label,
            //         w is ImmediateWindow, w?.windowRect, ms, w2.windowRect,
            //         UI.screenWidth, UI.screenHeight, w.windowRect.Contains(ms),
            //         w2.windowRect.Contains(ms), Event.current.mousePosition,
            //         GenUI.GetMouseAttachedWindowPos(w2.windowRect.width, w2.windowRect.height));
            // }
            // prevOver = curOver;
            if (PainterIs(Painter.Drag))
            {
                return;
            }

            if (DetailMode())
            {
                IconActions(false);
            }

            HandleDragging(mouseOver);

            // if clicked and not yet finished, queue up this research and all prereqs.
            if (!MainTabWindow_ResearchTree.Instance.DraggingNode()
                && Widgets.ButtonInvisible(Rect))
            {
                if (Event.current.button == 0)
                {
                    LeftClick();
                }
                else if (Event.current.button == 1)
                {
                    RightClick();
                }
            }
        }

        public bool LeftClick()
        {
            if (Completed() || !Available())
            {
                return false;
            }

            bool isAnomaly = ModLister.AnomalyInstalled && this.Research.knowledgeCategory != null;

            if (!isAnomaly)
            {
                if (DebugSettings.godMode && Event.current.control)
                {
                    Queue.FinishS(this);
                    Messages.Message(ResourceBank.String.FinishedResearch(Research.LabelCap),
                        MessageTypeDefOf.SilentInput, false);
                    Queue.Notify_InstantFinished();
                }
                else if (!Queue.ContainsS(this))
                {
                    if (Event.current.shift)
                    {
                        Queue.AppendS(this);
                    }
                    else if (Event.current.alt)
                    {
                        Queue.PrependS(this);
                    }
                    else
                    {
                        Queue.ReplaceS(this);
                    }
                }
                else
                {
                    Queue.RemoveS(this);
                }
            }
            else
            {
                if (DebugSettings.godMode && Event.current.control)
                {
                    AnomalyQueue.FinishS(this);
                    Messages.Message(ResourceBank.String.FinishedResearch(Research.LabelCap),
                        MessageTypeDefOf.SilentInput, false);
                    AnomalyQueue.Notify_InstantFinished();
                }
                else if (!AnomalyQueue.ContainsS(this))
                {
                    if (Event.current.shift)
                    {
                        AnomalyQueue.AppendS(this);
                    }
                    else if (Event.current.alt)
                    {
                        AnomalyQueue.PrependS(this);
                    }
                    else
                    {
                        AnomalyQueue.ReplaceS(this);
                    }
                }
                else
                {
                    AnomalyQueue.RemoveS(this);
                }
            }

            return true;
        }


        private bool RightClick()
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            Tree.HandleFixedHighlight(this);
            if (_currentPainter == Painter.Queue)
            {
                MainTabWindow_ResearchTree.Instance.CenterOn(this);
            }

            return true;
        }

        // inc means "inclusive"
        public List<ResearchNode> MissingPrerequisitesInc()
        {
            List<ResearchNode> result = new List<ResearchNode>();
            MissingPrerequitesRec(result);
            return result;
        }

        public List<ResearchNode> MissingPrerequisites()
        {
            List<ResearchNode> result = new List<ResearchNode>();
            if (!Research.PrerequisitesCompleted)
            {
                foreach (var n in DirectPrerequisites().Where(n => !n.Research.IsFinished))
                {
                    n.MissingPrerequitesRec(result);
                }
            }

            return result;
        }

        public IEnumerable<ResearchNode> DirectPrerequisites()
        {
            return InEdges.Select(e => e.InResearch());
        }

        public IEnumerable<ResearchNode> DirectChildren()
        {
            return OutEdges.Select(e => e.OutResearch());
        }


        private void MissingPrerequitesRec(List<ResearchNode> acc)
        {
            if (acc.Contains(this))
            {
                return;
            }

            if (!Research.PrerequisitesCompleted)
            {
                foreach (var n in DirectPrerequisites().Where(n => !n.Research.IsFinished))
                {
                    n.MissingPrerequitesRec(acc);
                }
            }

            acc.Add(this);
        }

        public bool Completed()
        {
            return Research.IsFinished;
        }

        // deprecated
        // patch `CompatibilityHooks.PassCustomUnlockRequirements` instead
        public static bool PassCustomUnlockRequirements(ResearchProjectDef p)
        {
            return true;
        }

        // deprecated
        // patch `CompatibilityHooks.CustomUnlockRequirementPrompts` instead
        public static List<string>
            CustomUnlockRequirementPrompts(ResearchProjectDef p)
        {
            return new List<string>();
        }

        public bool GetAvailable()
        {
            return !Research.IsFinished &&
                   (DebugSettings.godMode
                    || (BuildingPresent()
                        && TechprintAvailable()
                        && MainTabWindow_ResearchTree.AllowedTechlevel(
                            Research.techLevel)
                        && PassCustomUnlockRequirements(Research)
                        && CompatibilityHooks.PassCustomUnlockRequirements(
                            Research)));
        }

        public bool Available()
        {
            return _available;
        }

        public List<ThingDef> MissingFacilities()
        {
            return MissingFacilities(Research);
        }

        /// <summary>
        ///     Creates text version of research description and additional unlocks/prereqs/etc sections.
        /// </summary>
        /// <returns>string description</returns>
        private string GetResearchTooltipString()
        {
            var isAnomaly = this.Research.knowledgeCategory != null;
            // start with the descripton
            var text = new StringBuilder();
            text.AppendLine(Research.description);
            text.AppendLine();
            text.Append(ResourceBank.String.TechLevelOfResearch
                        + (!isAnomaly
                            ? Research.techLevel.ToStringHuman().CapitalizeFirst()
                            : Research.knowledgeCategory + " Anomaly"));

            return text.ToString();
        }

        public void DrawAt(
            Vector2 pos, Rect visibleRect, Painter painter, bool deferRectReset = false)
        {
            SetRects(pos);
            if (IsVisible(visibleRect))
            {
                Draw(visibleRect, painter);
            }

            if (!deferRectReset)
            {
                SetRects();
            }
        }
    }
}