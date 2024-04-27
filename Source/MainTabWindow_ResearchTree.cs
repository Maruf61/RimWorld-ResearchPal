// MainTabWindow_ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static ResearchPal.Constants;

namespace ResearchPal
{
    public class MainTabWindow_ResearchTree : MainTabWindow
    {
        internal static Vector2 _scrollPosition = Vector2.zero;

        private static Rect _treeRect;

        private Rect _baseViewRect;
        private Rect _baseViewRect_Inner;

        private bool    _dragging;
        private Vector2 _mousePosition = Vector2.zero;
        private Rect   _viewRect;

        private Rect _viewRect_Inner;
        private bool _viewRect_InnerDirty = true;
        private bool _viewRectDirty       = true;

        private float _zoomLevel = 1f;

        private string _prevQuery = "";

        private string _curQuery = "";

        private List<ResearchNode> _searchResults;

        public MainTabWindow_ResearchTree()
        {
            closeOnClickedOutside = false;
            Instance              = this;
        }

        public static MainTabWindow_ResearchTree Instance { get; private set; }

        private bool _searchActive = false;

        public bool SearchActive() {
            return _searchActive;
        }

        public float ScaledMargin => Constants.Margin * ZoomLevel / Prefs.UIScale;

        public float ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                _zoomLevel           = Mathf.Clamp( value, 1f, MaxZoomLevel );
                _viewRectDirty       = true;
                _viewRect_InnerDirty = true;
            }
        }

        public Rect ViewRect
        {
            get
            {
                if ( _viewRectDirty )
                {
                    _viewRect = new Rect(
                        _baseViewRect.xMin   * ZoomLevel,
                        _baseViewRect.yMin   * ZoomLevel,
                        _baseViewRect.width  * ZoomLevel,
                        _baseViewRect.height * ZoomLevel
                    );
                    _viewRectDirty = false;
                }

                return _viewRect;
            }
        }

        public Rect ViewRect_Inner
        {
            get
            {
                if ( _viewRect_InnerDirty )
                {
                    _viewRect_Inner      = _viewRect.ContractedBy( Margin * ZoomLevel );
                    _viewRect_InnerDirty = false;
                }

                return _viewRect_Inner;
            }
        }

        IntVec2 currentTreeSize = new IntVec2(0, 0);

        public Rect TreeRect {
            get {
                if (currentTreeSize != Tree.Size) {
                    ResetTreeRect();
                    currentTreeSize = Tree.Size;
                }
                return _treeRect;
            }
        }

        private void ResetTreeRect() {
            var width  = Tree.Size.x * ( NodeSize.x + NodeMargins.x );
            var height = Tree.Size.z * ( NodeSize.y + NodeMargins.y );
            _treeRect = new Rect( 0f, 0f, width, height );
        }

        public Rect VisibleRect =>
            new Rect(
                _scrollPosition.x,
                _scrollPosition.y,
                ViewRect_Inner.width,
                ViewRect_Inner.height );

        // special rules for tech-level availability
        public static bool AllowedTechlevel(TechLevel level) {
            if (Find.Storyteller.def.defName == "VFEM_Maynard") {
                return level >= TechLevel.Animal && level <= TechLevel.Medieval;
            }
            return true;
        }

        internal float MaxZoomLevel
        {
            get
            {
                // get the minimum zoom level at which the entire tree fits onto the screen, or a static maximum zoom level.
                var fitZoomLevel = Mathf.Max( TreeRect.width  / _baseViewRect_Inner.width,
                                              TreeRect.height / _baseViewRect_Inner.height );
                return Mathf.Min( fitZoomLevel, AbsoluteMaxZoomLevel );
            }
        }

        public override void PreClose() {
            base.PreClose();
        }

        public override void PreOpen() {
            base.PreOpen();

            Tree.WaitForInitialization();

            SetRects();

            forcePause = Settings.shouldPause;

            if (Settings.shouldReset) {
                ResetSearch();
                _scrollPosition = Vector2.zero;
                ZoomLevel = 1f;
            }

            // clear node availability caches
            ResearchNode.ClearCaches();
            Queue.SanityCheckS();

            _dragging             = false;
            closeOnClickedOutside = false;
        }

        private void SetRects()
        {
            var ymin = TopBarHeight + StandardMargin + SideMargin;
            // tree view rects, have to deal with UIScale and ZoomLevel manually.
            _baseViewRect = new Rect(
                StandardMargin / Prefs.UIScale, ymin,
                (Screen.width - StandardMargin) / Prefs.UIScale,
                (Screen.height - MainButtonDef.ButtonHeight - StandardMargin - Constants.Margin) /
                Prefs.UIScale - ymin);
            _baseViewRect_Inner = _baseViewRect.ContractedBy( Constants.Margin / Prefs.UIScale );

            // windowrect, set to topleft (for some reason vanilla alignment overlaps bottom buttons).
            windowRect.x      = 0f;
            windowRect.y      = 0f;
            windowRect.width  = UI.screenWidth;
            windowRect.height = UI.screenHeight - MainButtonDef.ButtonHeight;
        }

        public override void DoWindowContents( Rect canvas )
        {
            GUI.EndClip();
            GUI.EndClip(); // some window black magic by fluffy

            absoluteMousePos = Event.current.mousePosition;
            var topRect = new Rect(
                canvas.xMin + SideMargin,
                canvas.yMin + StandardMargin,
                canvas.width - StandardMargin,
                TopBarHeight );
            DrawTopBar( topRect );
            // HandleNodeDragging();

            ApplyZoomLevel();

            // draw background
            GUI.DrawTexture( ViewRect, Assets.SlightlyDarkBackground );

            // draw the actual tree
            // TODO: stop scrollbars scaling with zoom
            _scrollPosition = GUI.BeginScrollView( ViewRect, _scrollPosition, TreeRect );
            GUI.BeginGroup(
                new Rect(
                    ScaledMargin,
                    ScaledMargin,
                    TreeRect.width  - ScaledMargin * 2f,
                    TreeRect.height - ScaledMargin * 2f
                )
            );

            Tree.Draw( VisibleRect );
            Queue.DrawLabels( VisibleRect );
            HandleLeftoverNodeRelease();
            HandleStopFixedHighlights();

            HandleZoom();

            GUI.EndGroup();
            GUI.EndScrollView(false);

            ResetZoomLevel();
            HandleNodeDragging();
            ApplyZoomLevel();

            HandleDragging();
            HandleDolly();

            // reset zoom level
            ResetClips();



            // // cleanup;
            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void HandleLeftoverNodeRelease() {
            if (  Event.current.type == EventType.MouseUp
               && Event.current.button == 0
               && DraggingNode()) {
                StopDragging();
                Event.current.Use();
            }
        }

        static private void HandleStopFixedHighlights() {
            if (  Event.current.type == EventType.MouseDown
               && (  Event.current.button == 0
                  || Event.current.button == 1 && !Event.current.shift)) {
                if (Tree.StopFixedHighlights()) {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }
        }

        static private bool KeyDefEvent(KeyBindingDef def) {
            KeyBindingData keyBind;
            if (! KeyPrefs.KeyPrefsData.keyPrefs.TryGetValue(def, out keyBind)) {
                return false;
            }
            // var code = Event.current.keyCode;
            // return code == keyBind.keyBindingA || code == keyBind.keyBindingB;
            return Input.GetKey(keyBind.keyBindingA) || Input.GetKey(keyBind.keyBindingB);
        }

        private void HandleDolly()
        {
            var dollySpeed = 10f;
            if ( KeyBindingDefOf.MapDolly_Left.IsDown ) {
                _scrollPosition.x -= dollySpeed;
            }
            if ( KeyBindingDefOf.MapDolly_Right.IsDown ) {
                _scrollPosition.x += dollySpeed;
            }
            if ( KeyBindingDefOf.MapDolly_Up.IsDown ) {
                _scrollPosition.y -= dollySpeed;
            }
            if ( KeyBindingDefOf.MapDolly_Down.IsDown ) {
                _scrollPosition.y += dollySpeed;
            }
        }


        void HandleZoom()
        {
            // handle zoom only with shift
            if (Event.current.isScrollWheel && Event.current.shift)
            {
                // absolute position of mouse on research tree
                var absPos = Event.current.mousePosition;
                // Log.Debug( "Absolute position: {0}", absPos );

                // relative normalized position of mouse on visible tree
                var relPos = ( Event.current.mousePosition - _scrollPosition ) / ZoomLevel;
                // Log.Debug( "Normalized position: {0}", relPos );

                // update zoom level
                ZoomLevel += Event.current.delta.y * ZoomStep * ZoomLevel * Settings.zoomingSpeedMultiplier;

                // we want to keep the _normalized_ relative position the same as before zooming
                _scrollPosition = absPos - relPos * ZoomLevel;

                Event.current.Use();
            }
        }

        private Vector2 absoluteMousePos;

        void HandleDragging()
        {
            // middle mouse or holding down shift for panning
            if (  Event.current.button == 2
               || Event.current.shift && Event.current.button == 0) {
                if (Event.current.type == EventType.MouseDown)
                {
                    _dragging = true;
                    _mousePosition = Event.current.mousePosition;
                    Event.current.Use();
                }
                if (Event.current.type == EventType.MouseUp)
                {
                    _dragging = false;
                    _mousePosition = Vector2.zero;
                }
                if (Event.current.type == EventType.MouseDrag)
                {
                    var _currentMousePosition = Event.current.mousePosition;
                    _scrollPosition += _mousePosition - _currentMousePosition;
                    _mousePosition = _currentMousePosition;
                    Event.current.Use();
                }
            }
            // scroll wheel vertical, switch to horizontal with alt
            if (Event.current.isScrollWheel && !Event.current.shift) {
                float delta = Event.current.delta.y * 15 * Settings.scrollingSpeedMultiplier;
                if (Event.current.alt) {
                    _scrollPosition.x += delta;
                } else {
                    _scrollPosition.y += delta;
                }
                Event.current.Use();
            }
        }

        private Matrix4x4 originalMatrix;

        private void ApplyZoomLevel()
        {
            // GUI.EndClip(); // window contents
            // GUI.EndClip(); // window itself?
            originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3( 0f, 0f, 0f ),
                Quaternion.identity,
                new Vector3(Prefs.UIScale / ZoomLevel, Prefs.UIScale / ZoomLevel, 1f));
        }

        private void ResetZoomLevel() {
            GUI.matrix = originalMatrix;
        }

        private void ResetClips()
        {
            // dummies to maintain correct stack size
            // TODO; figure out how to get actual clipping rects in ApplyZoomLevel();
            UI.ApplyUIScale();
            GUI.BeginClip( windowRect );
            GUI.BeginClip( new Rect( 0f, 0f, UI.screenWidth, UI.screenHeight ) );
        }

        private ResearchNode draggedNode = null;
        private Vector2 draggedPosition;

        private Painter draggingSource;
        private float startDragging;

        public bool DraggingNode() {
            return ! (draggedNode == null);
        }

        public ResearchNode DraggedNode() {
            return draggedNode;
        }

        public void StartDragging(ResearchNode node, Painter painter) {
            Log.Debug("Start dragging node: {0}", node.Research.label);
            draggedNode = node;
            draggingSource = painter;
            draggedPosition = UI.GUIToScreenPoint(node.Rect.position);
            startDragging = Time.time;
        }
        public void StopDragging() {
            draggedNode?.NotifyDraggingRelease();
            draggedNode = null;
        }

        public Painter DraggingSource() {
            return draggingSource;
        }

        public Vector2 DraggedNodePos() {
            return draggedPosition;
        }

        public void HandleNodeDragging() {
            if (!DraggingNode()) {
                return;
            }
            var evt = Event.current;
            if (evt.type == EventType.MouseDrag && evt.button == 0) {
                draggedPosition += evt.delta;
                Queue.NotifyNodeDraggedS();
                evt.Use();
            }
            if (DraggingSource() == Painter.Tree) {
                if (DraggingTime() > Settings.draggingDisplayDelay) {
                    var pos = absoluteMousePos;
                    pos.x -= NodeSize.x * 0.5f;
                    pos.y -= NodeSize.y * 0.5f;
                    draggedNode.DrawAt(pos, windowRect, Painter.Drag);
                }
            } else {
                draggedNode.DrawAt(
                    draggedPosition, windowRect, Painter.Drag); 
            }
        }

        public float DraggingTime() {
            if (!DraggingNode()) {
                return 0;
            }
            return Time.time - startDragging;
        }

        private void DrawTopBar( Rect canvas )
        {
            var searchRect = canvas;
            var queueRect  = canvas;
            searchRect.width =  200f;
            queueRect.xMin   += 200f;
            queueRect.xMax   -= 130f;

            GUI.DrawTexture( searchRect, Assets.SlightlyDarkBackground );
            // GUI.DrawTexture( queueRect, Assets.SlightlyDarkBackground );

            DrawSearchBar( searchRect.ContractedBy( Constants.Margin ) );
            Queue.DrawS( queueRect, !_dragging );
        }

        private bool CancelSearchButton(Rect canvas) {
            var iconRect = new Rect(
                    canvas.xMax - Constants.Margin - 12f,
                    0f,
                    12f,
                    12f )
               .CenteredOnYIn( canvas.TopHalf() );

            var texture = ContentFinder<Texture2D>.Get("UI/Widgets/CloseXSmall");
            return Widgets.ButtonImage(iconRect, texture, false);
        }

        private void DrawSearchBar( Rect canvas )
        {
            
            
            Profiler.Start( "DrawSearchBar" );
            // var iconrect = new Rect(
            //         canvas.xMax - Constants.Margin - 16f,
            //         0f,
            //         16f,
            //         16f )
            //    .CenteredOnYIn( canvas );
            var searchRect = new Rect(
                    canvas.xMin,
                    0f,
                    canvas.width,
                    Constants.QueueLabelSize )
               .CenteredOnYIn( canvas.TopHalf() );
            
            if (ModLister.AnomalyInstalled &&
                Widgets.ButtonText(
                    new Rect(canvas.xMin, Constants.QueueLabelSize + Constants.Margin, canvas.width, Constants.QueueLabelSize).CenteredOnYIn(canvas.BottomHalf()),
                    "Show Anomaly"))
            {
                Find.MainTabsRoot.ToggleTab(Assets.MainButtonDefOf.ResearchHidden);
                ((MainTabWindow_Research)Assets.MainButtonDefOf.ResearchHidden.TabWindow).CurTab =
                    ResearchTabDefOf.Anomaly;
                return;
            }
            
            // GUI.DrawTexture( iconRect, Assets.Search );
            if (CancelSearchButton(canvas)) {
                ResetSearch();
            }

            UpdateTextField(searchRect);
            OnSearchFieldChanged(searchRect);

            Profiler.End();
        }

        public static float SearchResponseDelay = 0.3f;
        private float lastSearchChangeTime = 0;

        private void UpdateTextField(Rect searchRect) {
            var curQuery = Widgets.TextField(searchRect, _curQuery);
            if (curQuery != _curQuery) {
                lastSearchChangeTime = Time.realtimeSinceStartup;
                _curQuery = curQuery;
            }
        }

        private void OnSearchFieldChanged(Rect searchRect) {
            if ( _curQuery == _prevQuery
               || Time.realtimeSinceStartup - lastSearchChangeTime
                  < SearchResponseDelay) {
                return;
            }

            _prevQuery = _curQuery;
            ClearPreviousSearch();

            if (_curQuery.Length <= 2) {
                return;
            }

            _searchActive = true;
            // open float menu with search results, if any.
            var options = new List<FloatMenuOption>();

            _searchResults = Tree.ResearchNodes()
                .Select( n => new {node = n, match = n.Matches( _curQuery )} )
                .Where( result => result.match > 0 )
                .OrderBy( result => result.match).Select(p => p.node).ToList();

            Log.Debug("Search activate: {0}", _curQuery);
            Log.Debug("Search result: {0}", Queue.DebugQueueSerialize(_searchResults));

            foreach (var result in _searchResults) {
                result.isMatched = true;
                options.Add(new FloatMenuOption(
                    result.Label, () => CenterOn(result),
                    MenuOptionPriority.Default, rect => CenterOn(result)));
            }

            if ( !options.Any() )
                options.Add(new FloatMenuOption(ResourceBank.String.NoResearchFound, null));

            Find.WindowStack.Add(new FloatMenu_Fixed(
                options, UI.GUIToScreenPoint(
                    new Vector2(searchRect.xMin, searchRect.yMax))));
        }

        private void ResetSearch() {
            _curQuery = "";
            _prevQuery = "";
            ClearPreviousSearch();
        }

        private void ClearPreviousSearch() {
            Find.WindowStack.FloatMenu?.Close(false);
            _searchActive = false;
            if (_searchResults != null) {
                _searchResults.ForEach(n => n.isMatched = false);
                _searchResults = null;
            }
        }

        public void CenterOn( ResearchNode node )
        {
            var position = new Vector2(
                ( NodeSize.x + NodeMargins.x ) * ( node.X - .5f ),
                ( NodeSize.y + NodeMargins.y ) * ( node.Y - .5f ) );

            position -= new Vector2( UI.screenWidth, UI.screenHeight ) / 2f;

            position.x      = Mathf.Clamp( position.x, 0f, TreeRect.width  - ViewRect.width );
            position.y      = Mathf.Clamp( position.y, 0f, TreeRect.height - ViewRect.height );
            _scrollPosition = position;
        }
    }
}