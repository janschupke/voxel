namespace Voxel
{
    /// <summary>Serializable actor state for world persistence.</summary>
    public struct ActorSaveData
    {
        public string ActorTypeName;
        public string HomeEntryName;
        public int HomeBlockX;
        public int HomeBlockY;
        public int HomeBlockZ;
        public float PosX;
        public float PosY;
        public float PosZ;
        public int StateId;
        public string CarriedItemId;

        public ActorSaveData(string actorTypeName, string homeEntryName, int homeBlockX, int homeBlockY, int homeBlockZ,
            float posX, float posY, float posZ, int stateId, string carriedItemId = null)
        {
            ActorTypeName = actorTypeName ?? "";
            HomeEntryName = homeEntryName ?? "";
            HomeBlockX = homeBlockX;
            HomeBlockY = homeBlockY;
            HomeBlockZ = homeBlockZ;
            PosX = posX;
            PosY = posY;
            PosZ = posZ;
            StateId = stateId;
            CarriedItemId = carriedItemId ?? "";
        }
    }
}
