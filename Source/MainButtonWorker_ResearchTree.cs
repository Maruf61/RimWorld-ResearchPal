// MainButtonWorker_ResearchTree.cs
// Copyright Karel Kroeze, 2018-2020

using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPal
{
    public class MainButtonWorker_ResearchTree : MainButtonWorker_ToggleResearchTab
    {
        public override void DoButton(Rect rect)
        {
            base.DoButton(rect);

            float maxi = rect.xMax;
            if (Queue.NumQueued > 0)
            {
                var queueRect = new Rect(
                    rect.xMax - Constants.SmallQueueLabelSize - Constants.Margin,
                    0f,
                    Constants.SmallQueueLabelSize,
                    Constants.SmallQueueLabelSize).CenteredOnYIn(rect);
                maxi = queueRect.xMin;
                Queue.DrawLabel(queueRect, Color.white, Color.grey, Queue.NumQueued);
            }

            if (AnomalyQueue.NumQueued > 0)
            {
                var queueRect = new Rect(
                    maxi - Constants.SmallQueueLabelSize - Constants.Margin,
                    0f,
                    Constants.SmallQueueLabelSize,
                    Constants.SmallQueueLabelSize).CenteredOnYIn(rect);
                AnomalyQueue.DrawLabel(queueRect, Color.white, Color.red, AnomalyQueue.NumQueued);
            }
        }

        public override void Activate()
        {
            // Find.MainTabsRoot.ToggleTab(Event.current.shift ? Assets.MainButtonDefOf.ResearchHidden : def);
            Find.MainTabsRoot.ToggleTab(def);
        }
    }
}