// Queue.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using System;
using static ResearchPal.Assets;
using static ResearchPal.Constants;

namespace ResearchPal
{
    public class AnomalyQueue : WorldComponent
    {
        private static AnomalyQueue _instance;
        private readonly List<ResearchNode> _queue = new List<ResearchNode>();
        private List<ResearchProjectDef> _saveableQueue;

        private static UndoStateHandler<ResearchNode[]> undoState
            = new UndoStateHandler<ResearchNode[]>();

        public AnomalyQueue(World world) : base(world)
        {
            _instance = this;
        }

        /// <summary>
        ///     Removes and returns the first node in the queue.
        /// </summary>
        /// <returns></returns>

        public static int NumQueued => _instance._queue.Count - 1;

        public static void DrawLabels(Rect visibleRect)
        {
            Profiler.Start("AnomalyQueue.DrawLabels");
            var i = 1;
            foreach (var node in _instance._queue.Where(x =>
                         x.Research.knowledgeCategory == KnowledgeCategoryDefOf.Basic))
            {
                if (node.IsVisible(visibleRect))
                {
                    var main = ColorCompleted[node.Research.techLevel];
                    var background = i > 1 ? ColorUnavailable[node.Research.techLevel] : main;
                    DrawLabel(node.QueueRect, main, background, i);
                }

                i++;
            }


            var i2 = 1;
            foreach (var node in _instance._queue.Where(x =>
                         x.Research.knowledgeCategory == KnowledgeCategoryDefOf.Advanced))
            {
                if (node.IsVisible(visibleRect))
                {
                    var main = ColorCompleted[node.Research.techLevel];
                    var background = i2 > 1 ? ColorUnavailable[node.Research.techLevel] : main;
                    DrawLabel(node.QueueRect, main, background, i2);
                }

                i2++;
            }

            Profiler.End();
        }

        public static void DrawLabel(Rect canvas, Color main, Color background, int label)
        {
            // draw coloured tag
            GUI.color = main;
            GUI.DrawTexture(canvas, CircleFill);

            // if this is not first in line, grey out centre of tag
            if (background != main)
            {
                GUI.color = background;
                GUI.DrawTexture(canvas.ContractedBy(2f), CircleFill);
            }

            // draw queue number
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(canvas, label.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // Require the input to be topologically ordered
        private void UnsafeConcat(IEnumerable<ResearchNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (!_queue.Contains(n))
                {
                    _queue.Add(n);
                }
            }
        }

        // Require the input to be topologically ordered
        private void UnsafeConcatFront(IEnumerable<ResearchNode> nodes)
        {
            UnsafeInsert(nodes, 0);
        }

        private void UnsafeInsert(IEnumerable<ResearchNode> nodes, int pos)
        {
            int i = pos;
            foreach (var n in nodes)
            {
                if (_queue.IndexOf(n, 0, pos) != -1)
                {
                    continue;
                }

                _queue.Remove(n);
                _queue.Insert(i++, n);
            }
        }

        static public bool CantResearch(ResearchNode node)
        {
            return !node.GetAvailable();
        }

        private void UnsafeAppend(ResearchNode node)
        {
            UnsafeConcat(node.MissingPrerequisitesInc());
            UpdateCurrentResearch();
        }

        public bool Append(ResearchNode node)
        {
            if (_queue.Contains(node) || CantResearch(node))
            {
                return false;
            }

            UnsafeAppend(node);
            return true;
        }

        public bool Prepend(ResearchNode node)
        {
            if (CantResearch(node))
            {
                return false;
            }

            UnsafeConcatFront(node.MissingPrerequisitesInc());
            UpdateCurrentResearch();
            return true;
        }

        // S means "static"
        static public bool AppendS(ResearchNode node)
        {
            var res = _instance.Append(node);
            NewUndoState();
            return res;
        }

        static public bool PrependS(ResearchNode node)
        {
            var b = _instance.Prepend(node);
            NewUndoState();
            return b;
        }

        public void Clear()
        {
            _queue.Clear();
            UpdateCurrentResearch();
        }

        static public void ClearS()
        {
            _instance.Clear();
            NewUndoState();
        }

        private void MarkShouldRemove(int index, List<ResearchNode> shouldRemove)
        {
            var node = _queue[index];
            shouldRemove.Add(node);
            for (int i = index + 1; i < _queue.Count(); ++i)
            {
                if (shouldRemove.Contains(_queue[i]))
                {
                    continue;
                }

                if (_queue[i].MissingPrerequisites().Contains(node))
                {
                    MarkShouldRemove(i, shouldRemove);
                }
            }
        }

        private ResearchProjectDef CurrentBasicResearch()
        {
            return _queue.FirstOrDefault(x => x.Research?.knowledgeCategory == KnowledgeCategoryDefOf.Basic)?.Research;
        }

        private ResearchProjectDef CurrentAdvancedResearch()
        {
            return _queue.FirstOrDefault(x => x.Research?.knowledgeCategory == KnowledgeCategoryDefOf.Advanced)
                ?.Research;
        }

        private void UpdateCurrentResearch()
        {
            var currentBasic = CurrentBasicResearch();
            if (currentBasic == null)
            {
                if (GetCurrentBasicProject() != null)
                    Find.ResearchManager.StopProject(GetCurrentBasicProject());
            }
            else
            {
                Find.ResearchManager.SetCurrentProject(currentBasic);
            }

            var currentAdvanced = CurrentAdvancedResearch();
            if (currentAdvanced == null)
            {
                if (GetCurrentAdvancedProject() != null)
                    Find.ResearchManager.StopProject(GetCurrentAdvancedProject());
            }
            else
            {
                Find.ResearchManager.SetCurrentProject(currentAdvanced);
            }
        }

        private static ResearchProjectDef GetCurrentBasicProject()
        {
            return Find.ResearchManager.GetProject(KnowledgeCategoryDefOf.Basic);
        }

        private static ResearchProjectDef GetCurrentAdvancedProject()
        {
            return Find.ResearchManager.GetProject(KnowledgeCategoryDefOf.Advanced);
        }

        static private void UpdateCurrentResearchS()
        {
            _instance.UpdateCurrentResearch();
        }

        public void SanityCheck()
        {
            List<ResearchNode> finished = new List<ResearchNode>();
            List<ResearchNode> unavailable = new List<ResearchNode>();

            foreach (var n in _queue)
            {
                if (n.Research.IsFinished)
                {
                    finished.Add(n);
                }
                else if (!n.GetAvailable())
                {
                    unavailable.Add(n);
                }
            }

            finished.ForEach(n => _queue.Remove(n));
            unavailable.ForEach(n => Remove(n));

            var curBasic = GetCurrentBasicProject();
            var curAdvanced = GetCurrentAdvancedProject();
            if (curBasic != null && curBasic != CurrentBasicResearch())
            {
                Replace(curBasic.ResearchNode());
            }

            if (curAdvanced != null && curAdvanced != CurrentAdvancedResearch())
            {
                Replace(curAdvanced.ResearchNode());
            }

            UpdateCurrentResearch();
        }

        static public void SanityCheckS()
        {
            _instance.SanityCheck();
        }

        public bool Remove(ResearchNode node)
        {
            if (node.Completed())
            {
                return _queue.Remove(node);
            }

            List<ResearchNode> shouldRemove = new List<ResearchNode>();
            var idx = _queue.IndexOf(node);
            if (idx == -1)
            {
                return false;
            }

            MarkShouldRemove(idx, shouldRemove);
            foreach (var n in shouldRemove)
            {
                _queue.Remove(n);
            }

            if (idx == 0)
            {
                UpdateCurrentResearch();
            }

            return true;
        }

        public void Replace(ResearchNode node)
        {
            _queue.Clear();
            Append(node);
        }

        public void ReplaceMore(IEnumerable<ResearchNode> nodes)
        {
            _queue.Clear();
            foreach (var node in nodes)
            {
                Append(node);
            }
        }


        static public void ReplaceS(ResearchNode node)
        {
            _instance.Replace(node);
            NewUndoState();
        }

        static public void ReplaceMoreS(IEnumerable<ResearchNode> nodes)
        {
            _instance.ReplaceMore(nodes);
        }

        static public bool RemoveS(ResearchNode node)
        {
            var b = _instance.Remove(node);
            NewUndoState();
            return b;
        }

        public void Finish(ResearchNode node)
        {
            foreach (var n in node.MissingPrerequisitesInc())
            {
                _queue.Remove(n);
                Find.ResearchManager.FinishProject(n.Research);
            }
        }

        static public void FinishS(ResearchNode node)
        {
            _instance.Finish(node);
        }

        public bool Contains(ResearchNode node)
        {
            return _queue.Contains(node);
        }

        public static bool ContainsS(ResearchNode node)
        {
            return _instance._queue.Contains(node);
        }

        public int Count()
        {
            return _queue.Count();
        }

        public static int CountS()
        {
            return _instance.Count();
        }

        public static String DebugQueueSerialize(IEnumerable<ResearchNode> nodes)
        {
            if (Settings.verboseDebug)
            {
                return string.Join(", ", nodes.Select(n => n.Research.label));
            }

            return "";
        }

        public static bool Undo()
        {
            var oldState = undoState.Undo();
            if (oldState == null)
            {
                return false;
            }

            Log.Debug("Undo to {0}", DebugQueueSerialize(oldState));
            // avoid recording new undo state
            _instance.ReplaceMore(oldState);
            return true;
        }

        public static bool Redo()
        {
            var s = undoState.Redo();
            if (s == null)
            {
                return false;
            }

            Log.Debug("Redo to {0}", DebugQueueSerialize(s));
            // avoid recording new undo state
            _instance.ReplaceMore(s);
            return true;
        }

        static public void NewUndoState()
        {
            var q = AnomalyQueue._instance;
            var s = q._queue.ToArray();
            Log.Debug("Undo state recorded: {0}", DebugQueueSerialize(s));
            undoState.NewState(s);
        }

        static public void HandleUndo()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.control)
            {
                if (Event.current.keyCode == KeyCode.Z)
                {
                    Undo();
                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.R)
                {
                    Redo();
                    Event.current.Use();
                }
            }
        }

        public ResearchNode this[int n]
        {
            get { return _queue[n]; }
        }

        public static ResearchNode AtS(int n)
        {
            return _instance[n];
        }

        public ResearchNode Current()
        {
            return _queue.FirstOrDefault();
        }

        public static ResearchNode CurrentS()
        {
            return _instance.Current();
        }

        public static void TryStartNext(ResearchProjectDef finished)
        {
            var current = CurrentS();

            var finishedNode = _instance._queue.Find(n => n.Research == finished);
            if (finishedNode == null)
            {
                return;
            }

            RemoveS(finishedNode);
            if (finishedNode != current)
            {
                return;
            }

            var next = CurrentS()?.Research;
            if (next != null)
                Find.ResearchManager.SetCurrentProject(next);
            if (!Settings.useVanillaResearchFinishedMessage)
            {
                Log.Debug(
                    "Send completion letter for {0}, next is {1}",
                    current.Research.label, next?.label ?? "NONE");
                DoCompletionLetter(current.Research, next);
            }
        }

        private static void DoCompletionLetter(ResearchProjectDef current, ResearchProjectDef next)
        {
            // message
            string label = "ResearchFinished".Translate(current.LabelCap);
            string text = current.LabelCap + "\n\n" + current.description;

            if (next != null)
            {
                text += "\n\n" + ResourceBank.String.NextInQueue(next.LabelCap);
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent);
            }
            else
            {
                text += "\n\n" + ResourceBank.String.NextInQueue("none");
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NeutralEvent);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // store research defs as these are the defining elements
            if (Scribe.mode == LoadSaveMode.Saving)
                _saveableQueue = _queue.Select(node => node.Research).ToList();

            Scribe_Collections.Look(ref _saveableQueue, "AnomalyQueue", LookMode.Def);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Settings.asyncLoadingOnStartup)
                {
                    Tree.WaitForResearchNodes();
                }

                foreach (var research in _saveableQueue)
                {
                    // find a node that matches the research - or null if none found
                    var node = research.ResearchNode();

                    if (node != null)
                    {
                        UnsafeAppend(node);
                    }
                }

                undoState.Clear();
                NewUndoState();
                UpdateCurrentResearch();
            }
        }

        private void DoMove(ResearchNode node, int from, int to)
        {
            List<ResearchNode> movingNodes = new List<ResearchNode>();
            to = Math.Max(0, Math.Min(Count(), to));
            if (to > from)
            {
                movingNodes.Add(node);
                int dest = --to;
                for (int i = from + 1; i <= to; ++i)
                {
                    if (_queue[i].MissingPrerequisites().Contains(node))
                    {
                        movingNodes.Add(_queue[i]);
                        --dest;
                    }
                }

                movingNodes.ForEach(n => _queue.Remove(n));
                _queue.InsertRange(dest, movingNodes);
            }
            else if (to < from)
            {
                var prerequisites = node.MissingPrerequisites().ToList();
                for (int i = to; i < from; ++i)
                {
                    if (prerequisites.Contains(_queue[i]))
                    {
                        movingNodes.Add(_queue[i]);
                    }
                }

                movingNodes.Add(node);
                UnsafeInsert(movingNodes, to);
            }
        }

        public void Insert(ResearchNode node, int pos)
        {
            if (CantResearch(node))
            {
                return;
            }

            pos = Math.Max(0, Math.Min(Count(), pos));
            var idx = _queue.IndexOf(node);
            if (idx == pos) return;
            if (idx != -1)
            {
                DoMove(node, idx, pos);
            }
            else
            {
                UnsafeInsert(node.MissingPrerequisitesInc(), pos);
            }

            UpdateCurrentResearch();
        }

        public static void InsertS(ResearchNode node, int pos)
        {
            _instance.Insert(node, pos);
            NewUndoState();
        }

        static private Vector2 _scroll_pos = new Vector2(0, 0);

        static private float Width()
        {
            var original = DisplayQueueLength() * (NodeSize.x + Margin) - Margin;
            if (Settings.showIndexOnQueue)
            {
                return original + Constants.QueueLabelSize * 0.5f;
            }

            return original;
        }

        static private Rect ViewRect(Rect canvas)
        {
            return new Rect(0, 0, Mathf.Max(Width(), canvas.width), canvas.height);
        }

        static private Vector2 NodePos(int i)
        {
            return new Vector2(i * (Margin + NodeSize.x), 0);
        }

        static private Rect VisibleRect(Rect canvas)
        {
            return new Rect(_scroll_pos, canvas.size);
        }

        private static void ReleaseNodeAt(ResearchNode node, int dropIdx)
        {
            if (dropIdx == -1)
            {
                RemoveS(node);
            }

            var tab = MainTabWindow_ResearchTree.Instance;
            if (_instance._queue.IndexOf(node) == dropIdx)
            {
                if (tab.DraggingTime() < 0.2f)
                {
                    node.LeftClick();
                }
            }
            else
            {
                if (DraggingFromQueue() && dropIdx > _instance._queue.IndexOf(node))
                {
                    ++dropIdx;
                }

                InsertS(node, dropIdx);
            }
        }

        static private bool ReleaseEvent()
        {
            return DraggingNode()
                   && Event.current.type == EventType.MouseUp
                   && Event.current.button == 0;
        }

        static private void StopDragging()
        {
            MainTabWindow_ResearchTree.Instance.StopDragging();
        }

        static private int DropIndex(Rect visibleRect, Vector2 dropPos)
        {
            Rect relaxedRect = visibleRect;
            relaxedRect.yMin -= NodeSize.y * 0.3f;
            relaxedRect.height += NodeSize.y;
            if (!visibleRect.Contains(dropPos))
            {
                return -1;
            }

            return VerticalPosToIdx(dropPos.x);
        }

        private static void HandleDragReleaseInside(Rect visibleRect)
        {
            if (ReleaseEvent())
            {
                ReleaseNodeAt(
                    DraggedNode(),
                    DropIndex(visibleRect, Event.current.mousePosition));
                StopDragging();
                ResetNodePositions();
                Event.current.Use();
            }
        }

        private static int VerticalPosToIdx(float pos)
        {
            return (int)(pos / (Margin + NodeSize.x));
        }

        static private List<int> NormalPositions(int n)
        {
            List<int> poss = new List<int>();
            for (int i = 0; i < n; ++i)
            {
                poss.Add(i);
            }

            return poss;
        }

        private static List<int> DraggingNodePositions(Rect visibleRect)
        {
            List<int> poss = new List<int>();
            if (!DraggingNode())
            {
                return NormalPositions(CountS());
            }

            int draggedIdx = DropIndex(
                visibleRect, Event.current.mousePosition);
            for (int p = 0, i = 0; i < CountS();)
            {
                var node = _instance[i];
                // The dragged node should disappear
                if (DraggingFromQueue() && node == DraggedNode())
                {
                    poss.Add(-1);
                    ++i;
                    // The space of the queue is occupied
                }
                else if (draggedIdx == p)
                {
                    ++p;
                    continue;
                    // usual situation
                }
                else
                {
                    poss.Add(p);
                    ++p;
                    ++i;
                }
            }

            return poss;
        }

        private static void ResetNodePositions()
        {
            currentPositions = NormalPositions(CountS());
        }

        static private List<int> currentPositions = new List<int>();

        static private bool nodeDragged = false;

        public void NotifyNodeDragged()
        {
            nodeDragged = true;
        }

        static public void NotifyNodeDraggedS()
        {
            _instance.NotifyNodeDragged();
        }

        static private bool DraggingNode()
        {
            return MainTabWindow_ResearchTree.Instance.DraggingNode();
        }

        static private bool DraggingFromQueue()
        {
            return MainTabWindow_ResearchTree.Instance.DraggingSource() == Painter.Queue;
        }

        static private ResearchNode DraggedNode()
        {
            return MainTabWindow_ResearchTree.Instance.DraggedNode();
        }

        static private int DisplayQueueLength()
        {
            if (DraggingNode() && !DraggingFromQueue())
            {
                return CountS() + 1;
            }

            return CountS();
        }

        public static void UpdateCurrentPosition(Rect visibleRect)
        {
            if (!DraggingNode())
            {
                if (nodeDragged)
                {
                    ResetNodePositions();
                }
                else
                {
                    TryRefillPositions();
                }

                return;
            }
            else if (nodeDragged)
            {
                currentPositions = DraggingNodePositions(visibleRect);
            }

            nodeDragged = false;
        }

        private static void TryRefillPositions()
        {
            if (currentPositions.Count() != CountS())
            {
                ResetNodePositions();
            }
        }

        static private Pair<float, float> TolerantVerticalRange(float ymin, float ymax)
        {
            return new Pair<float, float>(ymin - NodeSize.x * 0.3f, ymax + NodeSize.y * 0.7f);
        }

        static private void HandleReleaseOutside(Rect canvas)
        {
            var mouse = Event.current.mousePosition;
            if (ReleaseEvent() && !canvas.Contains(mouse))
            {
                var vrange = TolerantVerticalRange(canvas.yMin, canvas.yMax);
                if (mouse.y >= vrange.First && mouse.y <= vrange.Second)
                {
                    if (mouse.x <= canvas.xMin)
                    {
                        ReleaseNodeAt(DraggedNode(), 0);
                    }
                    else if (mouse.x >= canvas.xMax)
                    {
                        ReleaseNodeAt(DraggedNode(), CountS());
                    }

                    ResetNodePositions();
                    StopDragging();
                    Event.current.Use();
                }
                else if (DraggingFromQueue())
                {
                    RemoveS(DraggedNode());
                    ResetNodePositions();
                    StopDragging();
                    Event.current.Use();
                }
            }
        }

        static private void HandleScroll(Rect canvas)
        {
            if (Event.current.isScrollWheel && Mouse.IsOver(canvas))
            {
                _scroll_pos.x += Event.current.delta.y * 20;
                Event.current.Use();
            }
            else if (DraggingNode())
            {
                var tab = MainTabWindow_ResearchTree.Instance;
                var nodePos = tab.DraggedNodePos();
                if (nodePos.y <= canvas.yMin - NodeSize.y
                    || nodePos.y >= canvas.yMax
                    || (nodePos.x >= canvas.xMin
                        && nodePos.x <= canvas.xMax - NodeSize.x))
                {
                    return;
                }

                float baseScroll = 20;
                if (nodePos.x < canvas.xMin)
                {
                    _scroll_pos.x -= baseScroll * (canvas.xMin - nodePos.x) / NodeSize.x;
                }
                else if (nodePos.x > canvas.xMax - NodeSize.x)
                {
                    _scroll_pos.x += baseScroll * (nodePos.x + NodeSize.x - canvas.xMax) / NodeSize.x;
                }
            }
        }

        static private void DrawBackground(Rect baseCanvas)
        {
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            GUI.DrawTexture(baseCanvas, Assets.RedBackground); //BaseContent.GreyTex);
        }

        static private Rect CanvasFromBaseCanvas(Rect baseCanvas)
        {
            var r = baseCanvas.ContractedBy(Constants.Margin);
            r.xMin += Margin;
            r.xMax -= Margin;
            return r;
        }

        static List<ResearchNode> temporaryQueue = new List<ResearchNode>();

        static private void DrawNodes(Rect visibleRect)
        {
            temporaryQueue.Clear();
            // when handling event in nodes, the queue itself may change
            // so using a temporary queue to avoid the unmatching DrawAt and SetRect
            foreach (var node in _instance._queue)
            {
                temporaryQueue.Add(node);
            }

            for (int i = 0; i < temporaryQueue.Count(); ++i)
            {
                if (currentPositions[i] == -1)
                {
                    continue;
                }

                var pos = NodePos(currentPositions[i]);
                var node = temporaryQueue[i];
                node.DrawAt(pos, visibleRect, Painter.Queue, true);
            }

            if (Settings.showIndexOnQueue)
            {
                DrawLabels(visibleRect);
            }

            foreach (var node in temporaryQueue)
            {
                node.SetRects();
            }

            if (temporaryQueue.Count() != CountS())
            {
                ResetNodePositions();
            }
        }

        static public void DrawS(Rect baseCanvas, bool interactible)
        {
            DrawBackground(baseCanvas);
            HandleUndo();
            var canvas = CanvasFromBaseCanvas(baseCanvas);

            if (CountS() == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(canvas, ResourceBank.String.NothingQueued);
                Text.Anchor = TextAnchor.UpperLeft;
            }

            HandleReleaseOutside(canvas);
            HandleScroll(canvas);


            _scroll_pos = GUI.BeginScrollView(
                canvas, _scroll_pos, ViewRect(canvas), GUIStyle.none, GUIStyle.none);
            Profiler.Start("AnomalyQueue.DrawQueue");


            var visibleRect = VisibleRect(canvas);
            HandleDragReleaseInside(visibleRect);
            UpdateCurrentPosition(visibleRect);

            DrawNodes(visibleRect);

            Profiler.End();
            GUI.EndScrollView(false);
        }

        public static void Notify_InstantFinished()
        {
            foreach (var node in new List<ResearchNode>(_instance._queue))
                if (node.Research.IsFinished)
                    _instance._queue.Remove(node);
            if (_instance._queue.FirstOrDefault()?.Research != null)
                Find.ResearchManager.SetCurrentProject(_instance._queue.FirstOrDefault()?.Research);
        }
    }
}