﻿using System;
using System.Collections.Generic;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.Bases;
using NitroxClient.MonoBehaviours;
using NitroxClient.MonoBehaviours.Overrides;
using NitroxClient.Unity.Helper;
using NitroxModel.Core;
using NitroxModel.DataStructures;
using NitroxModel.DataStructures.GameLogic;
using NitroxModel.DataStructures.GameLogic.Buildings.Rotation;
using NitroxModel.DataStructures.Util;
using NitroxModel.Helper;
using NitroxModel.Packets;
using NitroxModel_Subnautica.Helper;
using UnityEngine;

namespace NitroxClient.GameLogic
{
    public class Building
    {
        /* General Info on this class and specially about handling NitroxIds and Events
         * 
         * For understanding the logic of this class, it is first needed to understand the basics of buildable objects 
         * and object positioning in Subnautica. Buildable objects are divided into:
         * 
         * Bases
         * - Bases are all related Base-Piece objects of one Base-Complex. This is represented in Subnautica by a virtual
         *   Base-Object that is generated with the first Base-Hull object and assigned to all related Base-Pieces
         *   
         * Base-Hull objects (e.g. Corridors, Rooms, Moonpool, ..)
         * - These objects define the fundamental layout of a Base and use a cell to be placed in the world. A cell is 
         *   a defined rectangular virutal place in the world grid. A cell always only contains one Base-Hull object. 
         *   Placing the first Base-Hull object in a free space will assign a Base Object to it. Every nearby new Base-Hull
         *   object will be assigned to the Base-Object, even if not physical connected.
         * - Every Base-Hull object has more or fewer surfaces. Some of these surfaces can be replaced by Base-Integrated
         *   objects or be replaced by other surface-types according to objects in adjacent cells. All Surfaces are childs
         *   of the cell that the Base-Hull object represents. 
         * - Rely on ConstructableBase and DeconstructableBase (more on this later)
         *   
         * Base-Integrated objects (e.g. Hatches, Ladders, Reinforcements, Windows, ..)
         * - These objects replace a surface of a Base-Hull object and give it a new appearance. In default they represent 
         *   a single surface in the parent cell and have no further faces that can be build to. 
         *   There are two objects that are special:
         *   Ladders have two surfaces, one in the lower floor and one in the upper. 
         *   Waterparks are the only objects that create additional surfaces for hatches.
         * - These objects can be referenced by the child-index of a cell. (The index can change, see below for more on this.)
         * - Rely on ConstructableBase and DeconstructableBase (more on this later)
         * 
         * Base-Attached objects and outside placable Objects (e.g. Fabricator, Lockers, Solar, ...)
         * - These objects are placed by Position and are automaticaly snapped to the corresponding Base-Object. Some 
         *   functionality of these objects also needs a proper link to a Base (e.g. Power)
         * - These objects are not referenced via cell or faces and do not influence the Base-Layout.
         * - Rely on Constructable (more on this later)
         * 
         * Furniture (e.g. Tables, Chairs, ...)
         * - These objects are simply placed by Position and don't rely on other objects.
         * 
         * Constructing and Deconstructing Base-Hull objects and the Integrated-Objects is the most complex Part of this. 
         * Subnautica uses different viewmodels for objects in construction and the finished objects, which makes it hard
         * to track these objects and keep the reference via the NitroxIds. Additionally to this the models and the base-
         * layouts are pregenerated via ghost-objects for each object in the background, before the viewable object is 
         * updated in the world. A default lifecycle can be described as follows. (example with one hull and one surface)
         * 
         * Construct: create prefabGhost > create GhostBase (or assign to existing) > Clear and ReCalculateGhostGeometry >
         *   destroy prefabGhost > create Base > destroy BaseGhost > Clear and ReCalculateViewableGeometry > spawn HullConstructing > 
         *   (construct to 100%) > CalculateViewableGeometry > spawn HullFinished > (add surface object) > create HullGhost and 
         *   SurfaceGhost > CalculateGhostGeometry > destroy Ghosts and CalculateViewableGeometry > spawn HullFinished > spawn SurfaceConstructing
         * For deconstruction, the steps are nearly the same only with the Models reversed. The recalculationgeometry steps destroy
         * all gameobjects which needs a more complex handling and transferring of the NitroxIds, which are needed to identify the right objects
         * for syncing.
         * 
         * Some special things that need to be considered:
         * - Never assign an Id to a prefabghost object or a baseghost. The ghosts and their gameObjects can only be destroyed and replaced 
         *   by the viewable object when there is no NitroxEntity in GetComponent<>. This will lead to not interactable ghosts in the world 
         *   or hidden remaining ghosts in the world that prohibit multiplayer placement. The same applies for the virtual GhostBases.
         * - Every assigned NitroxId must be removed. 
         * - Objects are placed by position. This position can change according to other objects in the cell or when other objects change.
         * - The index of an object in a cell can also change, when surfaces ar attached or removed to create a new look for the object. 
         * - Keep in memory that abandoned Bases in the world also use the same mechanics for spawning and should not been interfered with
         *   mechanics here (e.g. using UnityEngine.Destroy without caution).
         */

        /*Reminder: ##TODO BUILDING## 
         * - suppress hull calculation during initialsync
         * - suppress item consumtion/granting during remote events
         * - block simultanious constructing/deconstructing of same object by local and remote player
         * - sync bulkhead door state
         * minor:
         * - sync hull integrity
         * 
         */
         
            
        private readonly IPacketSender packetSender;
        private readonly RotationMetadataFactory rotationMetadataFactory;

        public bool IsInitialSyncing = false;

        // Contains the last hovered constructable object hovered by the current player. Is needed to ensure fired events for the correct item.
        private Constructable lastHoveredConstructable = null;
        
        // State of currently using BuilderTool or not
        private bool currentlyHandlingBuilderTool = false;
        
        // State if a construction Event is raised by Initialsync or a current remote player.
        private bool remoteEventActive = false;

        // For the base objects themself as master objects of a base-complex we can't assign Ids to the ghosts, 
        private Dictionary<GameObject, NitroxId> baseGhostsIDCache = new Dictionary<GameObject, NitroxId>();


        public Building(IPacketSender packetSender, RotationMetadataFactory rotationMetadataFactory)
        {
            this.packetSender = packetSender;
            this.rotationMetadataFactory = rotationMetadataFactory;
        }

        

        // For Base objects we also need to transfer the ids
        public void Base_CopyFrom_Pre(Base targetBase, Base sourceBase)
        {
            NitroxId sourceBaseId = NitroxEntity.GetIdNullable(sourceBase.gameObject);
            NitroxId targetBaseId = NitroxEntity.GetIdNullable(targetBase.gameObject);

#if TRACE && BUILDING
            BaseRoot sourceBaseRoot = sourceBase.GetComponent<BaseRoot>();
            BaseRoot targetBaseRoot = targetBase.GetComponent<BaseRoot>();
            NitroxModel.Logger.Log.Debug("Base_CopyFrom_Pre - Base copy - sourceBase: " + sourceBase + " targetBase: " + targetBase + " targetBaseIsGhost: " + targetBase.isGhost + " sourceBaseId: " + sourceBaseId + " targetBaseId: " + targetBaseId + " sourceBaseRoot: " + sourceBaseRoot + " targetBaseRoot: " + targetBaseRoot);
#endif

            // Transferring from a ghost base to a real base
            if (baseGhostsIDCache.ContainsKey(sourceBase.gameObject) && targetBaseId == null && !targetBase.isGhost)
            {

                NitroxEntity.SetNewId(targetBase.gameObject, baseGhostsIDCache[sourceBase.gameObject]);

#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("Base_CopyFrom_Pre - assigning cached Base Id from remote event or initial loading: " + baseGhostsIDCache[sourceBase.gameObject]);
#endif
            }
            // Transferring from a real base to a ghost base in case of beginning deconstruction of the last basepiece. Need this if player does not completely destroy 
            // last piece instead chooses to reconstruct this last piece.
            else if(sourceBaseId != null && !sourceBase.isGhost && !baseGhostsIDCache.ContainsKey(targetBase.gameObject))
            {
                baseGhostsIDCache[targetBase.gameObject] = sourceBaseId;

#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("Base_CopyFrom_Pre - caching Base Id from deconstructing object: " + sourceBaseId);
#endif
            }

            /*else
            {
                if (sourceBaseId != null && targetBaseId == null && !instance.isGhost)
                {

#if TRACE && BUILDING
                    NitroxModel.Logger.Log.Debug("Base_CopyFrom_Pre - assining id from local constructing of a new BaseComplex: " + sourceBaseId);
#endif

                    NitroxEntity.SetNewId(instance.gameObject, sourceBaseId);
                }
            }*/
        }

        // Suppress item consumption and recalculation of construction amount at construction
        public bool Constructable_Construct_Pre(Constructable instance, ref bool result)
        {
            if(remoteEventActive)
            {
                if(instance.constructed)
                {
                    result = false;
                }
                else
                {
                    System.Reflection.MethodInfo updateMaterial = typeof(Constructable).GetMethod("UpdateMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Validate.NotNull(updateMaterial);
                    updateMaterial.Invoke(instance, new object[] { });
                    if (instance.constructedAmount >= 1f)
                    {
                        instance.SetState(true, true);
                    }
                    result = true;
                }
                return false;
            }
            return true;
        }

        // Suppress item granting and recalculation of construction amount at construction  and remove NitroxId from 
        public bool Constructable_Deconstruct_Pre(Constructable instance, ref bool result)
        {
            if (remoteEventActive)
            {
                if (instance.constructed)
                {
                    result = false;
                }
                else
                {
                    System.Reflection.MethodInfo updateMaterial = typeof(Constructable).GetMethod("UpdateMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Validate.NotNull(updateMaterial);
                    updateMaterial.Invoke(instance, new object[] { });
                    if (instance.constructedAmount <= 0f)
                    {
                        UnityEngine.Object.Destroy(instance.gameObject);
                    }
                    result = true;
                }
                return false;
            }

            return true;
        }


        // Section: BuilderTool

        public void BuilderTool_OnHoverConstructable_Post(GameObject gameObject, Constructable constructable)
        {

#if TRACE && BUILDING && HOVERCONSTRUCTABLE
            NitroxId id = NitroxEntity.GetIdNullable(constructable.gameObject);
            NitroxModel.Logger.Log.Debug("BuilderTool_OnHoverConstructable_Post - instance: " + constructable.gameObject.name + " id: " + id);
#endif

            lastHoveredConstructable = constructable;
        }

        public void BuilderTool_OnHoverDeconstructable_Post(GameObject gameObject, BaseDeconstructable deconstructable)
        {

#if TRACE && BUILDING && HOVERDECONSTRUCTABLE
            NitroxId id = NitroxEntity.GetIdNullable(deconstructable.gameObject);
            NitroxId baseId = null;
            Base abase = deconstructable.gameObject.GetComponentInParent<Base>();
            if (abase)
            {
                baseId = NitroxEntity.GetIdNullable(abase.gameObject);
            }
            NitroxModel.Logger.Log.Debug("BuilderTool_OnHoverDeconstructable_Post - instance: " + deconstructable.gameObject.name + " id: " + id + " baseId: " + baseId + " position: " + deconstructable.gameObject.transform.position + " rotation: " + deconstructable.gameObject.transform.rotation + " cellPosition: " + deconstructable.gameObject.transform.parent.position + " cellIndex: " + deconstructable.gameObject.transform.GetSiblingIndex());
#endif

        }
        
        public bool BuilderTool_HandleInput_Pre(GameObject gameObject)
        {

#if TRACE && BUILDING && HOVER
            NitroxModel.Logger.Log.Debug("BuilderTool_Pre_HandleInput");
#endif

            currentlyHandlingBuilderTool = true;
            return true;
            
            /*
             * #TODO BUILDING# #ISSUE# Lock objects that are currently targeted by a player to be not constructed/deconstructed by others. 
             * 
            if (lastHoveredConstructable != null)
            {
                string _crafterGuid = GuidHelper.GetGuid(lastHoveredConstructable.gameObject);
                ushort _remotePlayerId;
                if (NitroxServiceLocator.LocateService<SimulationOwnership>().HasExclusiveLockByRemotePlayer(_crafterGuid, out _remotePlayerId))
                {
                    //if Object is in use by remote Player, supress deconstruction
                    if (GameInput.GetButtonHeld(GameInput.Button.LeftHand) || GameInput.GetButtonDown(GameInput.Button.LeftHand) || GameInput.GetButtonHeld(GameInput.Button.Deconstruct) || GameInput.GetButtonDown(GameInput.Button.Deconstruct))
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }*/
        }

        public void BuilderTool_HandleInput_Post(BuilderTool instance)
        {
            currentlyHandlingBuilderTool = false;
        }


        // SECTION: Local Events

        public void Constructable_Construct_Post(Constructable instance, bool result)
        {

#if TRACE && BUILDING
            NitroxId tempId = NitroxEntity.GetIdNullable(instance.gameObject);            
            NitroxModel.Logger.Log.Debug("Constructable_Construct_Post - instance: " + instance + " tempId: " + tempId + " construced: " + instance._constructed + " amount: " + instance.constructedAmount + " remoteEventActive: " + remoteEventActive);
#endif

            //Check if we raised the event by using our own BuilderTool or if it came as post Event of a Remote-Action or Init-Action
            if (lastHoveredConstructable != null && lastHoveredConstructable == instance && currentlyHandlingBuilderTool && !remoteEventActive)
            {
                if (result && instance.constructedAmount < 1f)
                {
                    NitroxId id = NitroxEntity.GetIdNullable(instance.gameObject);
                    if (id == null)
                    {
                        NitroxModel.Logger.Log.Error("Constructable_Construct_Post - no id on local object - object: " + instance.gameObject + " amount: " + instance.constructedAmount);
                    }
                    else
                    {
#if TRACE && BUILDING
                        NitroxModel.Logger.Log.Debug("Constructable_Construct_Post - sending notify for self constructing object - id: " + id + " amount: " + instance.constructedAmount);
#endif
                        BaseConstructionAmountChanged amountChanged = new BaseConstructionAmountChanged(id, instance.constructedAmount, true);
                        packetSender.Send(amountChanged);
                    }
                }
            }
            
            if (result && instance.constructedAmount == 1f && remoteEventActive) 
            {

#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("Constructable_Construct_Post - finished construct remote");
#endif

                /*
                if (instance.gameObject.name.Contains("Solar") || instance.gameObject.name.Contains("Reactor"))
                {


#if TRACE && BUILDING
                    NitroxModel.Logger.Log.Debug("Constructable_Construct_Post - Energy");
#endif

                    if (instance.gameObject.name.Contains("Solar") )
                    {
                        PowerSource _powersource = instance.gameObject.GetComponent<PowerSource>();

                        if (_powersource)
                        {
#if TRACE && BUILDING
                            NitroxModel.Logger.Log.Debug("Constructable_Construct_Post - Energy _powersource: " + _powersource);
#endif
                            //TODO: Remind for later
                            //initialize with 50% after RemoteConstruction >> ToDo: use additional flag for only setting at initalsync or update to explicit value when energy is synced 
                            _powersource.SetPower(_powersource.maxPower / 2);
                        }
                    }
                }*/
            }
        }

        public void Constructable_Deconstruct_Post(Constructable instance, bool result)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("Constructable_Deconstruct_Post - _construced: " + instance._constructed + " amount: " + instance.constructedAmount);
#endif

            //Check if we raised the event by using our own BuilderTool or if it came as post Event of a Remote-Action or Init-Action
            if (lastHoveredConstructable != null && lastHoveredConstructable == instance && currentlyHandlingBuilderTool && !remoteEventActive)
            {
                NitroxId id = NitroxEntity.GetIdNullable(instance.gameObject);
                if (id == null)
                {
                    NitroxModel.Logger.Log.Error("Constructable_Deconstruct_Post - Trying to deconstruct an Object that has no NitroxId - gameObject: " + instance.gameObject);
                }
                else
                {
                    if (result && instance.constructedAmount <= 0f)
                    {

#if TRACE && BUILDING
                        NitroxModel.Logger.Log.Debug("Constructable_Deconstruct_Post - sending notify for self deconstructed object - id: " + id);
#endif

                        BaseDeconstructionCompleted deconstructionCompleted = new BaseDeconstructionCompleted(id);
                        packetSender.Send(deconstructionCompleted);
                    }
                    else if (result && instance.constructedAmount > 0f)
                    {

#if TRACE && BUILDING
                        NitroxModel.Logger.Log.Debug("Constructable_Deconstruct_Post - sending notify for self deconstructing object  - id: " + id + " amount: " + instance.constructedAmount);
#endif

                        BaseConstructionAmountChanged amountChanged = new BaseConstructionAmountChanged(id, instance.constructedAmount, false);
                        packetSender.Send(amountChanged);
                    }
                }
            }

            if(result && instance.constructedAmount <= 0f)
            {
                if (instance.gameObject)
                {
                    NitroxEntity.RemoveId(instance.gameObject);
                    UnityEngine.Object.Destroy(instance.gameObject);
                }
            }
        }

        public void Constructable_NotifyConstructedChanged_Post(Constructable instance)
        {
#if TRACE && BUILDING
            NitroxId tempId = NitroxEntity.GetIdNullable(instance.gameObject);
            NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - instance: " + instance + " id: " + tempId + " _construced: " + instance._constructed + " amount: " + instance.constructedAmount);
#endif

            if (!remoteEventActive)
            {

#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - no remoteAction");
#endif

                // Case: A new base piece has been build by player
                if (!instance._constructed && instance.constructedAmount == 0f)
                {

#if TRACE && BUILDING
                    NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - case new instance");
#endif

                    if (!(instance is ConstructableBase))
                    {

                        NitroxId id = NitroxEntity.GetId(instance.gameObject);
                        NitroxId parentId = null;
                        SubRoot sub = Player.main.currentSub;

                        if (sub != null)
                        {
                            parentId = NitroxEntity.GetId(sub.gameObject);
                        }
                        else
                        {
                            Base playerBase = instance.gameObject.GetComponentInParent<Base>();
                            if (playerBase != null)
                            {
                                parentId = NitroxEntity.GetId(playerBase.gameObject);
                            }
                        }

                        Transform camera = Camera.main.transform;
                        BasePiece basePiece = new BasePiece(id, instance.gameObject.transform.position, instance.gameObject.transform.rotation, camera.position, camera.rotation, instance.techType.Model(), Optional.OfNullable(parentId), true, Optional.Empty);

#if TRACE && BUILDING
                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - sending notify for self begin constructing object - basePiece: " + basePiece );
#endif

                        BaseConstructionBegin constructionBegin = new BaseConstructionBegin(basePiece);
                        packetSender.Send(constructionBegin);
                    }
                    else
                    {
                        if (instance is ConstructableBase)
                        {
                            NitroxId parentBaseId = null;
                            BaseGhost ghost = instance.GetComponentInChildren<BaseGhost>();
                            if(ghost != null)
                            {

#if TRACE && BUILDING
                                NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - creating constructable base with ghost: " + ghost);
#endif

                                if (ghost.TargetBase != null)
                                {
                                    // Case: a constructableBase is build in range of 3 cells to an existing base structure
#if TRACE && BUILDING
                                    NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost has target base: " + ghost.TargetBase);
#endif

                                    parentBaseId = NitroxEntity.GetIdNullable(ghost.TargetBase.gameObject);
                                    if (parentBaseId != null)
                                    {
#if TRACE && BUILDING
                                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - target base id: " + parentBaseId);
#endif
                                    }
                                    else
                                    {
                                        parentBaseId = NitroxEntity.GetId(ghost.TargetBase.gameObject);
#if TRACE && BUILDING
                                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - target base had no id, assigned new one: " + parentBaseId);
#endif
                                    }
                                }
                                else
                                {
#if TRACE && BUILDING
                                    NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost has no target base");
#endif
                                    if (ghost.GhostBase != null)
                                    {
                                        // Case: a constructableBase is build out of range of 3 cells of an existing base structure and is creating a new base complex

#if TRACE && BUILDING
                                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost has ghost base: " + ghost.GhostBase);
#endif

                                        parentBaseId = NitroxEntity.GetIdNullable(ghost.GhostBase.gameObject);
                                        if (parentBaseId != null)
                                        {
#if TRACE && BUILDING
                                            NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost base id: " + parentBaseId);
#endif
                                        }
                                        else
                                        {
                                            //test
                                            parentBaseId = new NitroxId();
                                            baseGhostsIDCache[ghost.GhostBase.gameObject] = parentBaseId;
                                            
#if TRACE && BUILDING
                                            NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost base had no id, cached new one: " + baseGhostsIDCache[ghost.GhostBase.gameObject]);
#endif

                                            //orig
                                            /*
                                            parentBaseId = NitroxEntity.GetId(ghost.GhostBase.gameObject);
#if TRACE && BUILDING
                                            NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost base had no id, assigned new one: " + parentBaseId);
#endif
                                            */
                                        }
                                    }
                                    else
                                    {
#if TRACE && BUILDING
                                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost has no ghostbase and no targetbase");
#endif

                                        // Trying to find a Base in the parents of the ghost
                                        Base aBase = ghost.gameObject.GetComponentInParent<Base>();
                                        if(aBase != null)
                                        {
#if TRACE && BUILDING
                                            NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost has base in parentComponents: " + aBase);
#endif
                                            parentBaseId = NitroxEntity.GetIdNullable(aBase.gameObject);
                                            if (parentBaseId != null)
                                            {
#if TRACE && BUILDING
                                                NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost parentComponents base id: " + parentBaseId);
#endif
                                            }
                                            else
                                            {
                                                parentBaseId = NitroxEntity.GetId(aBase.gameObject);
#if TRACE && BUILDING
                                                NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - ghost parentComponentsbase had no id, assigned new one: " + parentBaseId);
#endif

                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Case: the constructableBase doesn't use a ghostModel to be build, instead using its final objectModel to be build

#if TRACE && BUILDING
                                NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - creating constructablebase without a ghost");
#endif

                                // Trying to find a Base in the parents of the gameobject itself
                                Base aBase = instance.gameObject.GetComponentInParent<Base>();
                                if (aBase != null)
                                {
#if TRACE && BUILDING
                                    NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - constructableBase has base in parentComponents: " + aBase);
#endif
                                    parentBaseId = NitroxEntity.GetIdNullable(aBase.gameObject);
                                    if (parentBaseId != null)
                                    {
#if TRACE && BUILDING
                                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - constructableBase parentComponents base id: " + parentBaseId);
#endif
                                    }
                                    else
                                    {
                                        parentBaseId = NitroxEntity.GetId(aBase.gameObject);
#if TRACE && BUILDING
                                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - constructableBase parentComponentsbase had no id, assigned new one: " + parentBaseId);
#endif
                                    }
                                }
                            }

                            Vector3 placedPosition = instance.gameObject.transform.position;

                            NitroxId id = NitroxEntity.GetIdNullable(instance.gameObject);
                            if(id == null)
                            {

                                id = NitroxEntity.GetId(instance.gameObject);
#if TRACE && BUILDING
                                NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - constructableBase gameobject had no id, assigned new one: " + id);
#endif
                            }

                            Transform camera = Camera.main.transform;
                            Optional<RotationMetadata> rotationMetadata = rotationMetadataFactory.From(ghost);

#if TRACE && BUILDING
                            NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - techType: " + instance.techType + " techType.Model(): " + instance.techType.Model());
#endif

                            //fix for wrong techType
                            TechType origTechType = instance.techType;
                            if (origTechType == TechType.BaseCorridor)
                            {
                                origTechType = TechType.BaseConnector;
                            }
                            NitroxModel.DataStructures.TechType techType = origTechType.Model();

#if TRACE && BUILDING
                            NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - techType: " + techType);
#endif

                            BasePiece basePiece = new BasePiece(id, placedPosition, instance.gameObject.transform.rotation, camera.position, camera.rotation, techType, Optional.OfNullable(parentBaseId), false, rotationMetadata);

#if TRACE && BUILDING
                            NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - sending notify for self begin constructing object - basePiece: " + basePiece);
#endif

                            BaseConstructionBegin constructionBegin = new BaseConstructionBegin(basePiece);
                            packetSender.Send(constructionBegin);
                        }
                    }
                }
                // Case: A local constructed item has been finished
                else if (instance._constructed && instance.constructedAmount == 1f)
                {

#if TRACE && BUILDING
                    NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - case item finished - lastHoveredConstructable: " + lastHoveredConstructable + " instance: " + instance + " currentlyHandlingBuilderTool: " + currentlyHandlingBuilderTool);
#endif
                    if (lastHoveredConstructable != null && lastHoveredConstructable == instance && currentlyHandlingBuilderTool)
                    {

                        NitroxId id = NitroxEntity.GetId(instance.gameObject);
                        Base parentBase = instance.gameObject.GetComponentInParent<Base>();
                        NitroxId parentBaseId = null;
                        if (parentBase != null)
                        {
                            parentBaseId = NitroxEntity.GetId(parentBase.gameObject);
                        }
                        
#if TRACE && BUILDING
                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - sending notify for self end constructed object - id: " + id + " parentbaseId: " + parentBaseId);
#endif
                        BaseConstructionCompleted constructionCompleted = new BaseConstructionCompleted(id);
                        packetSender.Send(constructionCompleted);
                    }
                    else
                    {

#if TRACE && BUILDING
                        NitroxId id = NitroxEntity.GetIdNullable(instance.gameObject);
                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - end of construction of - gameobject: " + instance.gameObject + " id: " + id);
#endif
                    }
                }
                //case: A finished item was started to be deconstructed by the local player
                else if (!instance._constructed && instance.constructedAmount == 1f)
                {
#if TRACE && BUILDING
                    NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - case item deconstruct");
#endif
                    
                    NitroxId id = NitroxEntity.GetIdNullable(instance.gameObject);
                    if (id == null)
                    {
                        NitroxModel.Logger.Log.Error("Constructable_NotifyConstructedChanged_Post - no id on local object - object: " + instance.gameObject);
                    }
                    else
                    {

#if TRACE && BUILDING
                        NitroxModel.Logger.Log.Debug("Constructable_NotifyConstructedChanged_Post - sending notify for self begin deconstructing object - id: " + id);
#endif

                        BaseDeconstructionBegin deconstructionBegin = new BaseDeconstructionBegin(id);
                        packetSender.Send(deconstructionBegin);
                    }
                }

                lastHoveredConstructable = null;
            }
        }

        // SECTION: Remote events from Initial-Sync or remote players

        internal void Constructable_ConstructionBegin_Remote(BasePiece basePiece)
        {
#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("Constructable_ConstructionBegin_Remote - id: " + basePiece.Id + " parentbaseId: " + basePiece.ParentId  + " techType: " + basePiece.TechType + " basePiece: " + basePiece);
#endif

            remoteEventActive = true;
            try
            {

#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("Constructable_ConstructionBegin_Remote - techTypeEnum: " + basePiece.TechType.Enum());
#endif

                GameObject buildPrefab = CraftData.GetBuildPrefab(basePiece.TechType.Enum());

#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("Constructable_ConstructionBegin_Remote - buildPrefab: " + buildPrefab);
#endif

                MultiplayerBuilder.overridePosition = basePiece.ItemPosition;
                MultiplayerBuilder.overrideQuaternion = basePiece.Rotation;
                MultiplayerBuilder.overrideTransform = new GameObject().transform;
                MultiplayerBuilder.overrideTransform.position = basePiece.CameraPosition;
                MultiplayerBuilder.overrideTransform.rotation = basePiece.CameraRotation;
                MultiplayerBuilder.placePosition = basePiece.ItemPosition;
                MultiplayerBuilder.placeRotation = basePiece.Rotation;
                MultiplayerBuilder.rotationMetadata = basePiece.RotationMetadata;

                MultiplayerBuilder.IsInitialSyncing = IsInitialSyncing;

                if (!MultiplayerBuilder.Begin(buildPrefab))
                {
                    NitroxModel.Logger.Log.Error("Constructable_ConstructionBegin_Remote - Cannot build Objekt: " + buildPrefab );

                    MultiplayerBuilder.End();
                    return;
                }

                GameObject parentBase = null;

                if (basePiece.ParentId.HasValue)
                {
                    parentBase = NitroxEntity.GetObjectFrom(basePiece.ParentId.Value).OrElse(null);
                    // In case of the first piece of a newly constructed Base from a remote Player or at InitialSync
                    // the ParentId has a Value, but the Id belongs to the BaseGhost instead of any known NitroxEntity.
                    // ParentBase will be null, let this untouched to let the Multiplayer-Builder generate a ghost and
                    // assign the Id afterwards. 
                }

                Constructable constructable;
                GameObject gameObject;

                if (basePiece.IsFurniture)
                {
                    SubRoot subRoot = (parentBase != null) ? parentBase.GetComponent<SubRoot>() : null;
                    gameObject = MultiplayerBuilder.TryPlaceFurniture(subRoot);
                    constructable = gameObject.RequireComponentInParent<Constructable>();
                    NitroxEntity.SetNewId(gameObject, basePiece.Id);
                }
                else
                {
                    NitroxServiceLocator.LocateService<GeometryLayoutChangeHandler>().ClearPreservedIdForConstructing(); //clear it, in case, the last constructed object wasn't finished and a former id is still cached
                    
                    constructable = MultiplayerBuilder.TryPlaceBase(parentBase);
                    gameObject = constructable.gameObject;
                    NitroxEntity.SetNewId(gameObject, basePiece.Id);

                    BaseGhost ghost = constructable.GetComponentInChildren<BaseGhost>();

#if TRACE && BUILDING
                    NitroxModel.Logger.Log.Debug("Constructable_ConstructionBegin_Remote - ghost: " + ghost + " parentBaseID: " + basePiece.ParentId + " parentBase: " + parentBase);
                    if(ghost!=null)
                    {
                        NitroxModel.Logger.Log.Debug("Constructable_ConstructionBegin_Remote - ghost.TargetBase: " + ghost.TargetBase + " ghost.GhostBase: " + ghost.GhostBase + " ghost.GhostBase.GameObject: " + ghost.GhostBase.gameObject);
                    }
#endif 

                    if (parentBase == null && basePiece.ParentId.HasValue && ghost != null && ghost.GhostBase != null && ghost.TargetBase == null)
                    {
                        // A new Base is created, transfer the Id to the ghost. 
                        // It will be reused to the finished base by the Base_CopyFrom_Patch.

#if TRACE && BUILDING
                        NitroxModel.Logger.Log.Debug("Constructable_ConstructionBegin_Remote - setting new Base Id to ghostBase: " + ghost.GhostBase.gameObject + " parentBaseID: " + basePiece.ParentId.Value);
#endif 
                        baseGhostsIDCache[ghost.GhostBase.gameObject] = basePiece.ParentId.Value;
                    }
                }

                /**
                 * Manually call start to initialize the object as we may need to interact with it within the same frame.
                 */


                //test
                System.Reflection.MethodInfo initResourceMap = typeof(Constructable).GetMethod("InitResourceMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Validate.NotNull(initResourceMap);
                initResourceMap.Invoke(constructable, new object[] { });

                // orig
                /*
                System.Reflection.MethodInfo startCrafting = typeof(Constructable).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Validate.NotNull(startCrafting);
                startCrafting.Invoke(constructable, new object[] { });*/
                
            }
            finally
            {
                remoteEventActive = false;
            }
        }

        internal void Constructable_AmountChanged_Remote(NitroxId id, float constructionAmount)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("Constructable_AmountChanged_Remote - id: " + id + " amount: " + constructionAmount);
#endif

            remoteEventActive = true;
            try
            {
                GameObject constructingGameObject = NitroxEntity.GetObjectFrom(id).OrElse(null);

                if(constructingGameObject == null)
                {
                    NitroxModel.Logger.Log.Error("Constructable_AmountChanged_Remote - received AmountChange for unknown id: " + id + " amount: " + constructionAmount);
                    remoteEventActive = false;
                    return;
                }

                if (constructionAmount > 0f && constructionAmount < 1f)
                {
                    Constructable constructable = constructingGameObject.GetComponentInChildren<Constructable>();
                    if (constructable.constructedAmount < constructionAmount)
                    {
                        constructable.constructedAmount = constructionAmount;
                        constructable.Construct();
                    }
                    else
                    {
                        constructable.constructedAmount = constructionAmount;
                        constructable.Deconstruct();
                    }
                }
            }
            finally
            {
                remoteEventActive = false;
            }
        }

        internal void Constructable_ConstructionCompleted_Remote(NitroxId id)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("Constructable_ConstructionCompleted_Remote - id: " + id);
#endif

            remoteEventActive = true;
            try
            {
                GameObject constructingGameObject = NitroxEntity.GetObjectFrom(id).OrElse(null);

                if (constructingGameObject == null)
                {
                    NitroxModel.Logger.Log.Error("Constructable_ConstructionCompleted_Remote - received ConstructionComplete for unknown id: " + id  );
                    remoteEventActive = false;
                    return;
                }

                ConstructableBase constructableBase = constructingGameObject.GetComponent<ConstructableBase>();
                if (constructableBase)
                {
                    constructableBase.constructedAmount = 1f;
                    constructableBase.Construct();
                }
                else
                {
                    Constructable constructable = constructingGameObject.GetComponent<Constructable>();
                    if (constructable)
                    {
                        constructable.constructedAmount = 1f;
                        constructable.Construct();
                        //orig
                        //constructable.SetState(true, true);
                    }
                }
            }
            finally
            {
                remoteEventActive = false;
            }
        }

        internal void Constructable_DeconstructionBegin_Remote(NitroxId id)
        {
            remoteEventActive = true;
            
            try
            {

#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("Constructable_DeconstructionBegin_Remote - id: " + id);
#endif

                GameObject deconstructing = NitroxEntity.GetObjectFrom(id).OrElse(null);

                if (deconstructing == null)
                {
                    NitroxModel.Logger.Log.Error("Constructable_ConstructionCompleted_Remote - received DeconstructionBegin for unknown id: " + id );
                    remoteEventActive = false;
                    return;
                }

                BaseDeconstructable baseDeconstructable = deconstructing.GetComponent<BaseDeconstructable>();
                if (baseDeconstructable)
                {
                    baseDeconstructable.Deconstruct();
                }
                else
                {
                    Constructable constructable = deconstructing.RequireComponent<Constructable>();
                    constructable.SetState(false, false);
                    constructable.Deconstruct();
                }
            }
            finally
            {
                remoteEventActive = false;
            }
        }

        internal void Constructable_DeconstructionComplete_Remote(NitroxId id)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("Constructable_DeconstructionComplete_Remote - id: " + id);
#endif

            remoteEventActive = true;
            try
            {
                GameObject deconstructing = NitroxEntity.GetObjectFrom(id).OrElse(null);

                if (deconstructing == null)
                {
                    NitroxModel.Logger.Log.Error("Constructable_DeconstructionComplete_Remote - received DeconstructionComplete for unknown id: " + id);
                    remoteEventActive = false;
                    return;
                }

                ConstructableBase constructableBase = deconstructing.GetComponent<ConstructableBase>();
                if (constructableBase)
                {
                    constructableBase.constructedAmount = 0f;
                    constructableBase.Deconstruct();
                }
                else
                {
                    Constructable constructable = deconstructing.GetComponent<Constructable>();
                    constructable.constructedAmount = 0f;
                    constructable.Deconstruct();
                }
            }
            finally
            {
                remoteEventActive = false;
            }
        }

        public bool GameModeUtils_RequiresReinforcements_Pre(ref bool result)
        {

#if TRACE && BUILDING
            NitroxModel.Logger.Log.Debug("GameModeUtils_RequiresReinforcements_Post - remoteEventActive: " + remoteEventActive + " IsInitialSyncing: " + IsInitialSyncing + " original result: " + result);
#endif

            if (remoteEventActive && IsInitialSyncing)
            {
                result = false;
                return false;
            }
            return true;
        }


        // SECTION: For tracing purposes. Will be removed when functionality is fully verified. 

        public void BaseRoot_Constructor_Post(BaseRoot instance)
        {
            if(instance.isBase)
            {
                NitroxId id = NitroxEntity.GetIdNullable(instance.gameObject);
                
#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("BaseRoot_Constructor_Post - New BaseRoot Instance - instance: " + instance + " instance.gameObject: " + instance.gameObject + " gameObjectId: " + id );
#endif

            }
        }

        public bool CellManager_RegisterEntity_Pre(GameObject baseEntity)
        {

#if TRACE && BUILDING
            if (remoteEventActive)
            {
                NitroxModel.Logger.Log.Debug("CellManager_RegisterEntity_Pre - instance: " + baseEntity + " instance.name: " + baseEntity.name);
            }
#endif

            return true;
        }

        public bool Builder_ShowRotationControlsHint_Pre()
        {

#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("Builder_ShowRotationControlsHint_Pre - isInitialSyncing: " + IsInitialSyncing + " remoteEventActive: " + remoteEventActive);
#endif

            if (IsInitialSyncing || remoteEventActive)
            {
#if TRACE && BUILDING
                NitroxModel.Logger.Log.Debug("Builder_ShowRotationControlsHint_Pre - returning false");
#endif
                return false;
            }
            return true;
        }
    }
}
