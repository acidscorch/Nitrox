using System;
using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic.PlayerModel.Abstract;
using NitroxClient.MonoBehaviours;
using NitroxClient.Unity.Helper;
using NitroxModel.DataStructures;
using NitroxModel.DataStructures.GameLogic;
using NitroxModel.DataStructures.Util;
using NitroxModel.MultiplayerSession;
using NitroxModel.Packets;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NitroxClient.GameLogic
{
    public class LocalPlayer : ILocalNitroxPlayer
    {
        private readonly IMultiplayerSession multiplayerSession;
        private readonly IPacketSender packetSender;
        private readonly Lazy<GameObject> body;
        private readonly Lazy<GameObject> playerModel;
        private readonly Lazy<GameObject> bodyPrototype;

        private Vector3 lastMovementLocation = Vector3.zero;
        private Vector3 lastMovementVelocity = Vector3.zero;
        private Quaternion lastMovementBodyRotation = new Quaternion();
        private Quaternion lastMovementAimingRotation = new Quaternion();

        public GameObject Body => body.Value;

        public GameObject PlayerModel => playerModel.Value;

        public GameObject BodyPrototype => bodyPrototype.Value;

        public string PlayerName => multiplayerSession.AuthenticationContext.Username;
        public PlayerSettings PlayerSettings => multiplayerSession.PlayerSettings;

        public LocalPlayer(IMultiplayerSession multiplayerSession, IPacketSender packetSender)
        {
            this.multiplayerSession = multiplayerSession;
            this.packetSender = packetSender;
            body = new Lazy<GameObject>(() => Player.main.RequireGameObject("body"));
            playerModel = new Lazy<GameObject>(() => Body.RequireGameObject("player_view"));
            bodyPrototype = new Lazy<GameObject>(CreateBodyPrototype);
        }

        public void BroadcastStats(float oxygen, float maxOxygen, float health, float food, float water, float infectionAmount)
        {
            PlayerStats playerStats = new PlayerStats(multiplayerSession.Reservation.PlayerId, oxygen, maxOxygen, health, food, water, infectionAmount);
            packetSender.Send(playerStats);
        }

        public void UpdateLocation(Vector3 location, Vector3 velocity, Quaternion bodyRotation, Quaternion aimingRotation, Optional<VehicleMovementData> vehicle)
        {
            Movement movement;
            if (vehicle.HasValue)
            {
                movement = new VehicleMovement(multiplayerSession.Reservation.PlayerId, vehicle.Value);
                packetSender.Send(movement);
            }
            else
            {
                //reduce unneeded net traffic and server package floating if no movement has happened, because this method is called every frame 
                
                bool _needSend = false;

                //use .ToString compare to skip minor changes
                if (!(lastMovementLocation.ToString().Equals(location.ToString()) && lastMovementBodyRotation.ToString().Equals(bodyRotation.ToString()) && lastMovementVelocity.ToString().Equals(velocity.ToString()) && lastMovementAimingRotation.ToString().Equals(aimingRotation.ToString())))
                {
                    _needSend = true;
                }

                if (_needSend)
                {
                    lastMovementLocation = location;
                    lastMovementVelocity = velocity;
                    lastMovementBodyRotation = bodyRotation;
                    lastMovementAimingRotation = aimingRotation;

                    movement = new Movement(multiplayerSession.Reservation.PlayerId, location, velocity, bodyRotation, aimingRotation);
                    packetSender.Send(movement);

                }

            }
           
        }

        public void AnimationChange(AnimChangeType type, AnimChangeState state)
        {
            AnimationChangeEvent animEvent = new AnimationChangeEvent(multiplayerSession.Reservation.PlayerId, (int)type, (int)state);
            packetSender.Send(animEvent);
        }

        public void BroadcastDeath(Vector3 deathPosition)
        {
            PlayerDeathEvent playerDeath = new PlayerDeathEvent(multiplayerSession.AuthenticationContext.Username, deathPosition);
            packetSender.Send(playerDeath);
        }

        public void BroadcastSubrootChange(Optional<NitroxId> subrootId)
        {
            SubRootChanged packet = new SubRootChanged(multiplayerSession.Reservation.PlayerId, subrootId);
            packetSender.Send(packet);
        }

        private GameObject CreateBodyPrototype()
        {
            GameObject prototype = Body;

            // Cheap fix for showing head, much easier since male_geo contains many different heads
            prototype.GetComponentInParent<Player>().head.shadowCastingMode = ShadowCastingMode.On;
            GameObject clone = Object.Instantiate(prototype);
            prototype.GetComponentInParent<Player>().head.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            clone.SetActive(false);

            return clone;
        }
    }
}
