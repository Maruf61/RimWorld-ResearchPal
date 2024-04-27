// Assets.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPal
{
    [StaticConstructorOnStartup]
    public static class Assets
    {
        public static            Texture2D Button       = ContentFinder<Texture2D>.Get( "Buttons/button" );
        public static            Texture2D ButtonActive = ContentFinder<Texture2D>.Get( "Buttons/button-active" );
        public static            Texture2D ResearchIcon = ContentFinder<Texture2D>.Get( "Icons/Research" );
        public static            Texture2D MoreIcon     = ContentFinder<Texture2D>.Get( "Icons/more" );
        public static            Texture2D Lock         = ContentFinder<Texture2D>.Get( "Icons/padlock" );
        internal static readonly Texture2D CircleFill   = ContentFinder<Texture2D>.Get( "Icons/circle-fill" );

        public static Color NormalHighlightColor = GenUI.MouseoverColor;

        public static Color                        NegativeMouseoverColor = new Color( .4f, .1f, .1f );
        public static Color HoverPrimaryColor = new Color(0.6f, 0.55f, 0.9f);
        public static Color FixedPrimaryColor = new Color(0.55f, 0.9f, 0.95f);
        // public static Color HeavyMouseoverColor = Color.white;
        public static Dictionary<TechLevel, Color> ColorCompleted         = new Dictionary<TechLevel, Color>();
        public static Dictionary<TechLevel, Color> ColorEdgeCompleted         = new Dictionary<TechLevel, Color>();
        public static Dictionary<TechLevel, Color> ColorAvailable         = new Dictionary<TechLevel, Color>();
        public static Dictionary<TechLevel, Color> ColorUnavailable       = new Dictionary<TechLevel, Color>();
        public static Dictionary<TechLevel, Color> ColorUnmatched         = new Dictionary<TechLevel, Color>();
        public static Dictionary<TechLevel, Color> ColorMatched         = new Dictionary<TechLevel, Color>();
        public static Color                        TechLevelColor         = new Color( 1f, 1f, 1f, .2f );

        public static Texture2D SlightlyDarkBackground =
            SolidColorMaterials.NewSolidColorTexture( 0f, 0f, 0f, .1f );
        
        public static Texture2D RedBackground =
            SolidColorMaterials.NewSolidColorTexture( 1f, 0f, 0f, .9f );

        public static Texture2D Search =
            ContentFinder<Texture2D>.Get( "Icons/magnifying-glass" );

        static Assets()
        {
            var techlevels = Tree.RelevantTechLevels;
            var n          = techlevels.Count;
            for ( var i = 0; i < n; i++ )
            {
                ColorCompleted[techlevels[i]]     = Color.HSVToRGB( 1f / n * i, .75f, .75f );
                ColorEdgeCompleted[techlevels[i]] = Color.HSVToRGB( 1f / n * i, .5f, .6f );
                ColorMatched[techlevels[i]]     = Color.HSVToRGB( 1f / n * i, .4f, .45f );
                ColorAvailable[techlevels[i]]   = Color.HSVToRGB( 1f / n * i, .33f, .33f );
                ColorUnavailable[techlevels[i]] = Color.HSVToRGB( 1f / n * i, .125f, .33f);
                ColorUnmatched[techlevels[i]]   = Color.HSVToRGB( 1f / n * i, .17f,  .17f);
            }
        }

        [StaticConstructorOnStartup]
        public static class Lines
        {
            public static Texture2D Circle = ContentFinder<Texture2D>.Get( "Lines/Outline/circle" );
            public static Texture2D End    = ContentFinder<Texture2D>.Get( "Lines/Outline/end" );
            public static Texture2D EW     = ContentFinder<Texture2D>.Get( "Lines/Outline/ew" );
            public static Texture2D NS     = ContentFinder<Texture2D>.Get( "Lines/Outline/ns" );
        }
        
        [DefOf]
        public static class MainButtonDefOf
        {
            public static MainButtonDef ResearchHidden;
        }
    }
}