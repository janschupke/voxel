namespace Voxel
{
    /// <summary>Serializable production state for world persistence.</summary>
    public struct ProductionSaveData
    {
        public string EntryName;
        public int BlockX;
        public int BlockY;
        public int BlockZ;
        public int SelectedRecipeIndex;
        public int CurrentRecipeIndex;
        public float TimerRemaining;

        public ProductionSaveData(string entryName, int blockX, int blockY, int blockZ,
            int selectedRecipeIndex, int currentRecipeIndex, float timerRemaining)
        {
            EntryName = entryName ?? "";
            BlockX = blockX;
            BlockY = blockY;
            BlockZ = blockZ;
            SelectedRecipeIndex = selectedRecipeIndex;
            CurrentRecipeIndex = currentRecipeIndex;
            TimerRemaining = timerRemaining;
        }
    }
}
