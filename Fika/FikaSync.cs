using System;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Players;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using AdvancedMovement.Body;
using AdvancedMovement.Sync;

namespace AdvancedMovement.Coop
{
    internal struct BodyPacket : INetSerializable
    {
        public int NetId;
        public byte Type;
        public string Clip;
        public bool Loop;
        public float Value;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(NetId);
            writer.Put(Type);
            writer.Put(Clip ?? "");
            writer.Put(Loop);
            writer.Put(Value);
        }

        public void Deserialize(NetDataReader reader)
        {
            NetId = reader.GetInt();
            Type = reader.GetByte();
            Clip = reader.GetString();
            Loop = reader.GetBool();
            Value = reader.GetFloat();
        }
    }

    internal static class FikaSync
    {
        public static void Init(Harmony harmony)
        {
            FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnManagerCreated);
            harmony.Patch(AccessTools.Method(typeof(ObservedPlayer), "ObservedVisualPass"),
                prefix: new HarmonyMethod(typeof(FikaSync), nameof(ObservedVisualPassPrefix)));
            BodySync.Send = Send;
            AdvancedMovement.Plugin.Log.LogInfo("Fika found: slide, jump and dive animations are shared with other players");
        }

        private static void OnManagerCreated(FikaNetworkManagerCreatedEvent e)
        {
            e.Manager.RegisterPacket<BodyPacket, NetPeer>(OnPacket);
        }

        private static void Send(BodyOp op)
        {
            if (!Singleton<IFikaNetworkManager>.Instantiated || !Singleton<GameWorld>.Instantiated)
            {
                return;
            }
            if (!(Singleton<GameWorld>.Instance.MainPlayer is FikaPlayer local))
            {
                return;
            }
            var packet = new BodyPacket
            {
                NetId = local.NetId,
                Type = (byte)op.Type,
                Clip = op.Clip,
                Loop = op.Loop,
                Value = op.Value,
            };
            Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, DeliveryMethod.ReliableOrdered, broadcast: true);
        }

        private static void OnPacket(BodyPacket packet, NetPeer peer)
        {
            try
            {
                if (!Singleton<IFikaNetworkManager>.Instantiated)
                {
                    return;
                }
                var players = Singleton<IFikaNetworkManager>.Instance.CoopHandler?.Players;
                if (players == null || !players.TryGetValue(packet.NetId, out FikaPlayer player) || player == null || player.IsYourPlayer)
                {
                    return;
                }
                BodyPlayer.Receive(player, new BodyOp
                {
                    Type = (BodyOpType)packet.Type,
                    Clip = packet.Clip,
                    Loop = packet.Loop,
                    Value = packet.Value,
                });
            }
            catch (Exception ex)
            {
                AdvancedMovement.Plugin.Log.LogWarning("Could not play another player's body animation: " + ex.Message);
            }
        }

        private static void ObservedVisualPassPrefix(ObservedPlayer __instance)
        {
            try
            {
                BodyPlayer.ApplyRemote(__instance);
            }
            catch (Exception ex)
            {
                AdvancedMovement.Plugin.Log.LogWarning("Another player's body animation failed and was stopped: " + ex.Message);
                BodyPlayer.Receive(__instance, new BodyOp { Type = BodyOpType.Stop });
            }
        }
    }
}
