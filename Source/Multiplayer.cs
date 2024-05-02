using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using Verse;

namespace ResearchPal
{
    // Can be run anywhere really. Multiplayer runs its API init code on Mod()
    // and since it runs always after Core, it's certain it will be ready.
    [StaticConstructorOnStartup]
    public static class Multiplayer
    {
        static Dictionary<ResearchProjectDef, ResearchNode> _cache;

        static Multiplayer()
        {
            if (!MP.enabled) return;

            // Let's sync all Queue operations that involve the GUI
            // Don't worry about EnqueueRange calling Enqueue, MP mod handles it
            MP.RegisterSyncMethod(typeof(Queue), nameof(Queue.AppendS));
            MP.RegisterSyncMethod(typeof(Queue), nameof(Queue.PrependS));
            MP.RegisterSyncMethod(typeof(Queue), nameof(Queue.ReplaceS));
            MP.RegisterSyncMethod(typeof(Queue), nameof(Queue.RemoveS));
            MP.RegisterSyncMethod(typeof(Queue), nameof(Queue.Insert));
            MP.RegisterSyncMethod(typeof(Queue), nameof(Queue.FinishS));
            MP.RegisterSyncMethod(typeof(Queue), nameof(Queue.ReplaceMoreS));
            MP.RegisterSyncMethod(typeof(Queue), nameof(Queue.Undo));
            MP.RegisterSyncMethod(typeof(Queue), nameof(Queue.Redo));

            MP.RegisterSyncMethod(typeof(AnomalyQueue), nameof(AnomalyQueue.AppendS));
            MP.RegisterSyncMethod(typeof(AnomalyQueue), nameof(AnomalyQueue.PrependS));
            MP.RegisterSyncMethod(typeof(AnomalyQueue), nameof(AnomalyQueue.ReplaceS));
            MP.RegisterSyncMethod(typeof(AnomalyQueue), nameof(AnomalyQueue.RemoveS));
            MP.RegisterSyncMethod(typeof(AnomalyQueue), nameof(AnomalyQueue.Insert));
            MP.RegisterSyncMethod(typeof(AnomalyQueue), nameof(AnomalyQueue.FinishS));
            MP.RegisterSyncMethod(typeof(AnomalyQueue), nameof(AnomalyQueue.ReplaceMoreS));
            MP.RegisterSyncMethod(typeof(AnomalyQueue), nameof(AnomalyQueue.Undo));
            MP.RegisterSyncMethod(typeof(AnomalyQueue), nameof(AnomalyQueue.Redo));
            MP.RegisterSyncWorker<ResearchNode>(HandleResearchNode);

            Log.Message("Initialized compatibility for Multiplayer");
        }

        static ResearchNode Lookup(ResearchProjectDef projectDef)
        {
            if (_cache == null)
                _cache = Tree.ResearchNodes().ToDictionary(r => r.Research);

            return _cache[projectDef];
        }

        // We only care about the research, no new nodes will be created or moved.
        static void HandleResearchNode(SyncWorker sw, ref ResearchNode node)
        {
            // Bind commands are in the order they are placed
            // So if you write a Def first, you must read it first and so on
            if (sw.isWriting)
            {
                // We are writing, node is the outgoing object
                sw.Bind(ref node.Research);
            }
            else
            {
                ResearchProjectDef research = null;

                sw.Bind(ref research);

                // We are reading, node is null, we must set it. So we look it up
                // research is unique in the Tree
                node = Lookup(research);
            }
        }
    }
}