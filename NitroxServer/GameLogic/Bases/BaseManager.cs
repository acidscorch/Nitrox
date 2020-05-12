﻿using System.Collections.Generic;
using NitroxModel.DataStructures.GameLogic;
using NitroxModel.DataStructures.GameLogic.Buildings.Metadata;
using NitroxModel.DataStructures;
using System.Linq;
using NitroxModel.DataStructures.Util;

namespace NitroxServer.GameLogic.Bases
{
    public class BaseManager
    {
        // List of all BasePieces
        // Separation in finished and unfinished Pieces is not needed on server, because the information is on every single object and
        // the building layout is handled by the client on initial loading.  
        private List<BasePiece> allBasePieces;

        public BaseManager(List<BasePiece> allBasePieces)
        {
            this.allBasePieces = allBasePieces;
        }

        public List<BasePiece> GetAllBasePieces()
        {
            lock(allBasePieces)
            {
                return new List<BasePiece>(allBasePieces);
            }
        }
      
        public void BasePieceConstructionBegin(BasePiece basePiece)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("BasePieceConstructionBegin - id: " + basePiece.Id + " - basePiece: " + basePiece);
#endif

            lock (allBasePieces)
            {
                // Subnautica changes the order of basepieces if there are Layout-Changes on an existing base, e.g. by adding pieces or splitting a base apart
                // in two bases by removing connector-tubes. This changes the order in which BasePieces are need to be generated by Nitrox on client side loading.
                if (allBasePieces.Count > 0)
                {
                    int nextBuildIndex = allBasePieces.Max(piece => piece.BuildIndex) + 1;
                    basePiece.BuildIndex = nextBuildIndex;
                }
                
                allBasePieces.Add(basePiece);
            }
        }

        public void BasePieceConstructionAmountChanged(NitroxId id, float constructionAmount)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("BasePieceConstructionAmount - id: " + id + " - constructionAmount: " + constructionAmount);
#endif

            BasePiece basePiece;
                        
            lock (allBasePieces)
            {
                basePiece = allBasePieces.Find(piece => piece.Id == id);

                if (basePiece != null)
                {
                    basePiece.ConstructionAmount = constructionAmount;
                }
                else
                {
                    NitroxModel.Logger.Log.Error("BasePieceConstructionAmountChanged - Received ConstructionAmountChange for unknown NitroxID: " + id);
                }
            }
        }

        public void BasePieceConstructionCompleted(NitroxId id, NitroxId baseId)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("BasePieceConstructionCompleted - id: " + id + " - baseId: " + baseId);
#endif

            BasePiece basePiece;

            lock (allBasePieces)
            {
                basePiece = allBasePieces.Find(piece => piece.Id == id);

                if (basePiece != null)
                {
                    basePiece.ConstructionAmount = 1.0f;
                    basePiece.ConstructionCompleted = true;

                    if (!basePiece.IsFurniture)
                    {
                        // For standard base pieces, the baseId is may not be finialized until construction 
                        // completes because Subnautica uses a GhostBase in the world if there hasn't yet been
                        // a fully constructed piece.  Therefor, we always update this attribute to make sure it
                        // is the latest.
                        basePiece.BaseId = baseId;
                        basePiece.ParentId = Optional.OfNullable(baseId);
                    }
                }
                else
                {
                    NitroxModel.Logger.Log.Error("BasePieceConstructionCompleted - Received ConstructionCompleted for unknown NitroxID: " + id);
                }
            }
        }

        public void BasePieceDeconstructionBegin(NitroxId id)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("BasePieceDeconstructionBegin - id: " + id);
#endif

            BasePiece basePiece;

            lock (allBasePieces)
            {
                basePiece = allBasePieces.Find(piece => piece.Id == id);

                if (basePiece != null)
                {
                    basePiece.ConstructionAmount = 0.95f;
                    basePiece.ConstructionCompleted = false;                   
                }
                else
                {
                    NitroxModel.Logger.Log.Error("BasePieceDeconstructionBegin - Received DeconstructionBegin for unknown NitroxID: " + id);
                }
            }
        }

        public void BasePieceDeconstructionCompleted(NitroxId id)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("BasePieceDeconstructionCompleted - id: " + id);
#endif

            BasePiece basePiece;

            lock (allBasePieces)
            {
                basePiece = allBasePieces.Find(piece => piece.Id == id);

                if (basePiece != null)
                {
                    allBasePieces.Remove(basePiece);
                }
                else
                {
                    NitroxModel.Logger.Log.Error("BasePieceDeconstructionCompleted - Received DeconstructionCompleted for unknown NitroxID: " + id);
                }
            }
        }

        public void UpdateBasePieceMetadata(NitroxId id, BasePieceMetadata metadata)
        {
            BasePiece basePiece;

            lock (allBasePieces)
            {
                basePiece = allBasePieces.Find(piece => piece.Id == id);

                if (basePiece != null)
                {
                    basePiece.Metadata = Optional.OfNullable(metadata);
                }
                else
                {
                    NitroxModel.Logger.Log.Error("UpdateBasePieceMetadata - Received UpdateBasePieceMetadata for unknown NitroxID: " + id);
                }
            }
        }

        public List<BasePiece> GetBasePiecesForNewlyConnectedPlayer()
        {
            List<BasePiece> basePieces;

            lock (allBasePieces)
            {
                basePieces = new List<BasePiece>(allBasePieces);
            }

            return basePieces;
        }
    }
}
