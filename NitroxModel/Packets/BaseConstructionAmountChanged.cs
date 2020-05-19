using System;
using NitroxModel.DataStructures;

namespace NitroxModel.Packets
{
    [Serializable]
    public class BaseConstructionAmountChanged : Packet
    {
        public NitroxId Id { get; }
        public float ConstructionAmount { get; }
        public bool Construct { get; }

        public BaseConstructionAmountChanged(NitroxId id, float constructionAmount, bool construct)
        {
            Id = id;
            ConstructionAmount = constructionAmount;
            Construct = construct;
        }

        public override string ToString()
        {
            return "[BaseConstructionAmountChanged Id:" + Id + " ConstructionAmount: " + ConstructionAmount + " Construct: " + Construct + "]";
        }
    }
}
