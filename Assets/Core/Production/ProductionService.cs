namespace Voxel
{
    /// <summary>
    /// Pure domain logic for production: can run check (inputs + output space) and execute (consume + produce).
    /// No Unity lifecycle dependencies; uses interfaces and config types from Core.
    /// </summary>
    public static class ProductionService
    {
        /// <summary>Returns true if inventory has all inputs and space for all outputs.</summary>
        public static bool CanRun(RecipeConfig recipe, IBuildingInventory inventory)
        {
            if (recipe == null || inventory == null) return false;

            foreach (var input in recipe.Inputs)
            {
                if (input.Count <= 0) continue;
                if (inventory.GetCount(input.Item) < input.Count)
                    return false;
            }

            int outputSlots = 0;
            foreach (var output in recipe.Outputs)
            {
                if (output.Count > 0)
                    outputSlots += output.Count;
            }
            if (outputSlots > 0 && !inventory.HasSpaceFor(outputSlots))
                return false;

            return true;
        }

        /// <summary>Consumes inputs and adds outputs. Assumes CanRun was true.</summary>
        public static void Execute(RecipeConfig recipe, IBuildingInventory inventory)
        {
            if (recipe == null || inventory == null) return;

            foreach (var input in recipe.Inputs)
            {
                if (input.Count <= 0) continue;
                inventory.TryTake(input.Item, input.Count);
            }

            foreach (var output in recipe.Outputs)
            {
                if (output.Count <= 0) continue;
                inventory.AddItem(output.Item, output.Count, emitUnitProduced: true);
            }
        }
    }
}
