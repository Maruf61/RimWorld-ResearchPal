using RimWorld;
using Verse;

namespace ResearchPal;

public class Utils
{
    //Using Reflection, since current project is private
    public static ResearchProjectDef GetCurrentProject(ResearchManager resMan)
    {
        return (ResearchProjectDef)typeof(ResearchManager)
            .GetField("currentProj",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(resMan);
        
    }
}