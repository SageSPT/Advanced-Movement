using System;

namespace AdvancedMovement.Sync
{
    internal enum BodyOpType : byte
    {
        Play,
        Then,
        Release,
        Stop,
    }

    internal struct BodyOp
    {
        public BodyOpType Type;
        public string Clip;
        public bool Loop;
        public float Value;
    }

    internal static class BodySync
    {
        public static Action<BodyOp> Send;

        public static void Broadcast(BodyOpType type, string clip = "", bool loop = false, float value = 0f)
        {
            Action<BodyOp> send = Send;
            if (send == null)
            {
                return;
            }
            try
            {
                send(new BodyOp { Type = type, Clip = clip ?? "", Loop = loop, Value = value });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not send the body animation to other players: " + ex.Message);
            }
        }
    }
}
