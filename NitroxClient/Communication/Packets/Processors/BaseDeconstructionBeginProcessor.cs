using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors.Abstract;
using NitroxClient.GameLogic.Bases;
using NitroxClient.GameLogic.Helper;
using NitroxClient.MonoBehaviours;
using NitroxModel.Logger;
using NitroxModel.Packets;
using UnityEngine;
using static NitroxClient.GameLogic.Helper.TransientLocalObjectManager;

namespace NitroxClient.Communication.Packets.Processors
{
    public class BaseDeconstructionBeginProcessor : ClientPacketProcessor<BaseDeconstructionBegin>
    {
        private BuildThrottlingQueue buildEventQueue;

        public BaseDeconstructionBeginProcessor(BuildThrottlingQueue buildEventQueue)
        {
            this.buildEventQueue = buildEventQueue;
        }

        public override void Process(BaseDeconstructionBegin packet)
        {
            buildEventQueue.EnqueueDeconstructionBegin(packet.Id);

            /*Log.Info("Received deconstruction packet for id: " + packet.Id);

            GameObject deconstructing = NitroxEntity.RequireObjectFrom(packet.Id);
            
            Constructable constructable = deconstructing.GetComponent<Constructable>();
            BaseDeconstructable baseDeconstructable = deconstructing.GetComponent<BaseDeconstructable>();

            using (packetSender.Suppress<BaseDeconstructionBegin>())
            {
                if (baseDeconstructable != null)
                {
                    TransientLocalObjectManager.Add(TransientObjectType.LATEST_DECONSTRUCTED_BASE_PIECE_GUID, packet.Id);

                    baseDeconstructable.Deconstruct();
                }
                else if (constructable != null)
                {
                    constructable.SetState(false, false);
                }
            }*/
        }
    }
}
