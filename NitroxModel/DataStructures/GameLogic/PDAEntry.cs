﻿using System;
using ProtoBufNet;

namespace NitroxModel.DataStructures.GameLogic
{
    [Serializable]
    [ProtoContract]
    public class PDAEntry
    {
        [ProtoMember(1)]
        public NitroxTechType TechType { get; set; }

        [ProtoMember(2)]
        public float Progress { get; set; }

        [ProtoMember(3)]
        public int Unlocked { get; set; }

        public PDAEntry()
        {
            // Default Constructor for serialization
        }

        public PDAEntry(NitroxTechType techType, float progress, int unlocked)
        {
            TechType = techType;
            Progress = progress;
            Unlocked = unlocked;
        }
    }
}
