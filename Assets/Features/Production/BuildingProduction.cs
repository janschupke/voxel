using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    public enum ProductionState
    {
        Idle,
        Producing,
        Blocked
    }

    /// <summary>
    /// Per-building production state machine. Runs selected recipe when inputs and output space available.
    /// </summary>
    public class BuildingProduction : MonoBehaviour
    {
        [SerializeField] private BuildingInventory inventory;
        [SerializeField] private RecipeListConfig recipeList;

        private ProductionState _state = ProductionState.Idle;
        private int _currentRecipeIndex = -1;
        private float _timerRemaining;
        private float _lastStateChangedTime;

        public int SelectedRecipeIndex { get; set; }
        public ProductionState State => _state;
        public RecipeConfig CurrentRecipe => GetRecipeAt(_currentRecipeIndex);
        public float ProgressNormalized => GetProgressNormalized();
        public float TimerRemaining => _timerRemaining;

        public event Action StateChanged;

        private void Awake()
        {
            if (inventory == null)
                inventory = GetComponent<BuildingInventory>();
        }

        public void Initialize(RecipeListConfig list)
        {
            recipeList = list;
            _state = ProductionState.Idle;
            _currentRecipeIndex = -1;
            _timerRemaining = 0f;
            if (SelectedRecipeIndex < 0 || recipeList == null || SelectedRecipeIndex >= recipeList.Recipes.Length)
                SelectedRecipeIndex = 0;
        }

        public void LoadState(int selectedIndex, int currentIndex, float timer)
        {
            SelectedRecipeIndex = Mathf.Clamp(selectedIndex, 0, recipeList != null ? recipeList.Recipes.Length - 1 : 0);
            _currentRecipeIndex = currentIndex;
            _timerRemaining = Mathf.Max(0f, timer);
            _state = _currentRecipeIndex >= 0 && _timerRemaining > 0f ? ProductionState.Producing : ProductionState.Idle;
        }

        private void Update()
        {
            if (recipeList == null || inventory == null || recipeList.Recipes.Length == 0) return;

            var recipe = GetSelectedRecipe();
            if (recipe == null) return;

            switch (_state)
            {
                case ProductionState.Producing:
                    var producingRecipe = GetRecipeAt(_currentRecipeIndex);
                    if (producingRecipe == null || !ProductionService.CanRun(producingRecipe, inventory))
                    {
                        _state = ProductionState.Blocked;
                        _currentRecipeIndex = -1;
                        _timerRemaining = 0f;
                        StateChanged?.Invoke();
                        break;
                    }
                    _timerRemaining -= Time.deltaTime;
                    if (Time.time - _lastStateChangedTime >= 0.5f)
                    {
                        _lastStateChangedTime = Time.time;
                        StateChanged?.Invoke();
                    }
                    if (_timerRemaining <= 0f)
                        CompleteProduction();
                    break;

                case ProductionState.Blocked:
                    if (ProductionService.CanRun(recipe, inventory))
                    {
                        StartProduction(recipe);
                    }
                    break;

                case ProductionState.Idle:
                    if (ProductionService.CanRun(recipe, inventory))
                    {
                        StartProduction(recipe);
                    }
                    break;
            }
        }

        private RecipeConfig GetSelectedRecipe()
        {
            if (recipeList == null || recipeList.Recipes.Length == 0) return null;
            int idx = Mathf.Clamp(SelectedRecipeIndex, 0, recipeList.Recipes.Length - 1);
            return recipeList.Recipes[idx];
        }

        private RecipeConfig GetRecipeAt(int index)
        {
            if (recipeList == null || index < 0 || index >= recipeList.Recipes.Length)
                return null;
            return recipeList.Recipes[index];
        }

        private float GetProgressNormalized()
        {
            if (_state != ProductionState.Producing || _currentRecipeIndex < 0) return 0f;
            var recipe = GetRecipeAt(_currentRecipeIndex);
            if (recipe == null || recipe.WorkDurationSeconds <= 0f) return 1f;
            float elapsed = recipe.WorkDurationSeconds - _timerRemaining;
            return Mathf.Clamp01(elapsed / recipe.WorkDurationSeconds);
        }

        private void StartProduction(RecipeConfig recipe)
        {
            if (!ProductionService.CanRun(recipe, inventory)) return;

            _currentRecipeIndex = Array.IndexOf(recipeList.Recipes, recipe);
            _timerRemaining = recipe.WorkDurationSeconds;
            _state = ProductionState.Producing;
            _lastStateChangedTime = Time.time;
            StateChanged?.Invoke();
        }

        private void CompleteProduction()
        {
            var recipe = GetRecipeAt(_currentRecipeIndex);
            if (recipe != null && ProductionService.CanRun(recipe, inventory))
            {
                ProductionService.Execute(recipe, inventory);
                _state = ProductionState.Idle;
            }
            else
            {
                _state = ProductionState.Blocked;
            }
            _currentRecipeIndex = -1;
            _timerRemaining = 0f;
            StateChanged?.Invoke();
        }

        /// <summary>Fills output with input items the building needs (currently has 0). Used by collectors.</summary>
        public void GetNeededInputItemsWithZero(HashSet<Item> output)
        {
            output.Clear();
            var recipe = GetSelectedRecipe();
            if (recipe == null || inventory == null) return;
            foreach (var input in recipe.Inputs)
            {
                if (input.Count <= 0) continue;
                if (inventory.GetCount(input.Item) == 0)
                    output.Add(input.Item);
            }
        }

        /// <summary>Call when inputs may have been taken (e.g. by collector).</summary>
        public void RecheckState()
        {
            var recipe = GetSelectedRecipe();
            if (recipe == null) return;

            if (_state == ProductionState.Producing)
            {
                if (!ProductionService.CanRun(recipe, inventory))
                {
                    _state = ProductionState.Blocked;
                    StateChanged?.Invoke();
                }
            }
            else if (_state == ProductionState.Blocked && ProductionService.CanRun(recipe, inventory))
            {
                StartProduction(recipe);
            }
        }
    }
}
