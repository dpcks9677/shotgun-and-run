using System.Collections.Generic;

namespace Consumable
{
    public interface IConsumableCraftingRecipeProvider
    {
        public IReadOnlyList<ConsumableCraftingCost> GetAllRecipes();
        public bool TryGetRecipe(ConsumableData data, out ConsumableCraftingCost recipe);
        public bool TryGetRecipe(ConsumableType type, out ConsumableCraftingCost recipe);
    }
}
