using NitroxClient.Communication.Packets.Processors.Abstract;
using NitroxClient.GameLogic.Bases;
using NitroxClient.MonoBehaviours;
using NitroxModel.Packets;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors
{
    public class BaseDeconstructionCompletedProcessor : ClientPacketProcessor<BaseDeconstructionCompleted>
    {
        private BuildThrottlingQueue buildEventQueue;

        public BaseDeconstructionCompletedProcessor(BuildThrottlingQueue buildEventQueue)
        {
            this.buildEventQueue = buildEventQueue;
        }

        public override void Process(BaseDeconstructionCompleted packet)
        {
            buildEventQueue.EnqueueDeconstructionCompleted(packet.Id);

            /*GameObject deconstructing = NitroxEntity.RequireObjectFrom(packet.Id);
            UnityEngine.Object.Destroy(deconstructing);
            */
        }
    }
}
