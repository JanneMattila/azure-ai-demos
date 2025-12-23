"""
Cooking AI Agent - Interactive console application for recipe search and ingredient extraction.

This app uses Microsoft Agent Framework with Microsoft Foundry models to provide:
- Recipe search based on ingredients or cuisine
- Ingredient extraction from recipe descriptions
- Interactive multi-turn conversations
"""

import asyncio
import os
from typing import Annotated

from agent_framework import ChatAgent
from agent_framework_azure_ai import AzureAIAgentClient
from azure.identity.aio import DefaultAzureCredential


# ============================================================================
# Tools for Recipe Search and Ingredient Extraction
# ============================================================================

def search_recipes(
    query: Annotated[str, "Search query - can be ingredients, cuisine type, or dish name"],
    max_results: Annotated[int, "Maximum number of recipes to return"] = 5
) -> str:
    """Search for recipes based on ingredients, cuisine, or dish name."""
    # In a real app, this would call a recipe API like Spoonacular, Edamam, etc.
    # For demo purposes, we return sample recipes
    sample_recipes = {
        "pasta": [
            {"name": "Spaghetti Carbonara", "cuisine": "Italian", "time": "30 min", "difficulty": "Medium"},
            {"name": "Penne Arrabbiata", "cuisine": "Italian", "time": "25 min", "difficulty": "Easy"},
            {"name": "Fettuccine Alfredo", "cuisine": "Italian", "time": "20 min", "difficulty": "Easy"},
        ],
        "chicken": [
            {"name": "Chicken Tikka Masala", "cuisine": "Indian", "time": "45 min", "difficulty": "Medium"},
            {"name": "Grilled Lemon Herb Chicken", "cuisine": "Mediterranean", "time": "35 min", "difficulty": "Easy"},
            {"name": "Chicken Stir Fry", "cuisine": "Asian", "time": "20 min", "difficulty": "Easy"},
        ],
        "vegetarian": [
            {"name": "Vegetable Curry", "cuisine": "Indian", "time": "40 min", "difficulty": "Easy"},
            {"name": "Mushroom Risotto", "cuisine": "Italian", "time": "45 min", "difficulty": "Medium"},
            {"name": "Caprese Salad", "cuisine": "Italian", "time": "10 min", "difficulty": "Easy"},
        ],
        "dessert": [
            {"name": "Chocolate Lava Cake", "cuisine": "French", "time": "25 min", "difficulty": "Medium"},
            {"name": "Tiramisu", "cuisine": "Italian", "time": "30 min", "difficulty": "Medium"},
            {"name": "Apple Pie", "cuisine": "American", "time": "60 min", "difficulty": "Medium"},
        ],
    }
    
    query_lower = query.lower()
    results = []
    
    # Search through categories
    for category, recipes in sample_recipes.items():
        if category in query_lower or query_lower in category:
            results.extend(recipes[:max_results])
    
    # If no category match, return a mix of recipes
    if not results:
        for recipes in sample_recipes.values():
            results.extend(recipes[:2])
            if len(results) >= max_results:
                break
    
    results = results[:max_results]
    
    if results:
        output = f"Found {len(results)} recipes matching '{query}':\n\n"
        for i, recipe in enumerate(results, 1):
            output += f"{i}. {recipe['name']}\n"
            output += f"   Cuisine: {recipe['cuisine']}\n"
            output += f"   Time: {recipe['time']}\n"
            output += f"   Difficulty: {recipe['difficulty']}\n\n"
        return output
    else:
        return f"No recipes found matching '{query}'. Try searching for: pasta, chicken, vegetarian, or dessert."


def get_recipe_details(
    recipe_name: Annotated[str, "Name of the recipe to get details for"]
) -> str:
    """Get detailed recipe information including ingredients and instructions."""
    # Sample detailed recipes
    recipes = {
        "spaghetti carbonara": {
            "name": "Spaghetti Carbonara",
            "servings": 4,
            "prep_time": "10 min",
            "cook_time": "20 min",
            "ingredients": [
                "400g spaghetti",
                "200g guanciale or pancetta, diced",
                "4 large egg yolks",
                "1 whole egg",
                "100g Pecorino Romano, grated",
                "50g Parmesan, grated",
                "Freshly ground black pepper",
                "Salt for pasta water"
            ],
            "instructions": [
                "Bring a large pot of salted water to boil. Cook spaghetti until al dente.",
                "While pasta cooks, fry guanciale in a large pan until crispy.",
                "In a bowl, whisk egg yolks, whole egg, and grated cheeses.",
                "Reserve 1 cup pasta water, then drain pasta.",
                "Add hot pasta to the pan with guanciale (off heat).",
                "Quickly toss with egg mixture, adding pasta water as needed.",
                "Season with black pepper and serve immediately."
            ]
        },
        "chicken tikka masala": {
            "name": "Chicken Tikka Masala",
            "servings": 4,
            "prep_time": "20 min",
            "cook_time": "25 min",
            "ingredients": [
                "600g chicken breast, cubed",
                "1 cup yogurt",
                "2 tbsp tikka masala spice",
                "1 large onion, diced",
                "4 cloves garlic, minced",
                "1 inch ginger, grated",
                "400g canned tomatoes",
                "1 cup heavy cream",
                "Fresh cilantro for garnish",
                "Salt to taste"
            ],
            "instructions": [
                "Marinate chicken in yogurt and half the spices for at least 30 minutes.",
                "Grill or pan-fry chicken until charred and cooked through.",
                "Sauté onion, garlic, and ginger until fragrant.",
                "Add remaining spices and cook for 1 minute.",
                "Add tomatoes and simmer for 10 minutes.",
                "Stir in cream and cooked chicken.",
                "Garnish with cilantro and serve with rice or naan."
            ]
        },
        "chocolate lava cake": {
            "name": "Chocolate Lava Cake",
            "servings": 4,
            "prep_time": "15 min",
            "cook_time": "12 min",
            "ingredients": [
                "200g dark chocolate",
                "100g butter",
                "2 whole eggs",
                "2 egg yolks",
                "50g sugar",
                "2 tbsp flour",
                "Butter and cocoa for ramekins",
                "Vanilla ice cream for serving"
            ],
            "instructions": [
                "Preheat oven to 220°C (425°F). Butter and dust ramekins with cocoa.",
                "Melt chocolate and butter together over a water bath.",
                "Whisk eggs, yolks, and sugar until light and fluffy.",
                "Fold chocolate mixture into eggs, then fold in flour.",
                "Divide batter among ramekins.",
                "Bake for 10-12 minutes until edges are set but center is soft.",
                "Let cool 1 minute, invert onto plates, serve with ice cream."
            ]
        }
    }
    
    recipe_key = recipe_name.lower()
    
    # Find matching recipe
    for key, recipe in recipes.items():
        if key in recipe_key or recipe_key in key:
            output = f"📖 {recipe['name']}\n"
            output += f"{'='*50}\n\n"
            output += f"👥 Servings: {recipe['servings']}\n"
            output += f"⏱️ Prep Time: {recipe['prep_time']}\n"
            output += f"🍳 Cook Time: {recipe['cook_time']}\n\n"
            
            output += "📝 Ingredients:\n"
            for ingredient in recipe['ingredients']:
                output += f"  • {ingredient}\n"
            
            output += "\n👨‍🍳 Instructions:\n"
            for i, step in enumerate(recipe['instructions'], 1):
                output += f"  {i}. {step}\n"
            
            return output
    
    return f"Recipe '{recipe_name}' not found. Try: Spaghetti Carbonara, Chicken Tikka Masala, or Chocolate Lava Cake."


def extract_ingredients(
    recipe_text: Annotated[str, "Recipe description or text to extract ingredients from"]
) -> str:
    """Extract and list ingredients from a recipe description."""
    # Common ingredients to look for
    common_ingredients = [
        "chicken", "beef", "pork", "fish", "salmon", "shrimp", "tofu",
        "pasta", "rice", "bread", "flour", "noodles",
        "tomato", "onion", "garlic", "ginger", "carrot", "potato", "spinach",
        "mushroom", "pepper", "broccoli", "zucchini", "eggplant",
        "milk", "cream", "butter", "cheese", "yogurt", "egg",
        "olive oil", "vegetable oil", "sesame oil",
        "salt", "pepper", "sugar", "honey", "soy sauce", "vinegar",
        "basil", "oregano", "thyme", "rosemary", "cilantro", "parsley",
        "cumin", "paprika", "cinnamon", "nutmeg", "curry",
        "lemon", "lime", "orange", "apple", "banana",
        "chocolate", "vanilla", "cocoa"
    ]
    
    text_lower = recipe_text.lower()
    found_ingredients = []
    
    for ingredient in common_ingredients:
        if ingredient in text_lower:
            found_ingredients.append(ingredient.title())
    
    if found_ingredients:
        output = f"🥘 Extracted Ingredients ({len(found_ingredients)} found):\n\n"
        for ingredient in found_ingredients:
            output += f"  ✓ {ingredient}\n"
        return output
    else:
        return "No common ingredients found in the text. Please provide a recipe description."


def suggest_substitutes(
    ingredient: Annotated[str, "Ingredient to find substitutes for"]
) -> str:
    """Suggest ingredient substitutes for dietary restrictions or availability."""
    substitutes = {
        "butter": ["coconut oil", "olive oil", "applesauce (for baking)", "avocado"],
        "milk": ["almond milk", "oat milk", "soy milk", "coconut milk"],
        "egg": ["flax egg (1 tbsp flaxseed + 3 tbsp water)", "chia egg", "applesauce", "mashed banana"],
        "flour": ["almond flour", "coconut flour", "oat flour", "gluten-free flour blend"],
        "sugar": ["honey", "maple syrup", "stevia", "coconut sugar"],
        "cream": ["coconut cream", "cashew cream", "silken tofu blended"],
        "cheese": ["nutritional yeast", "vegan cheese", "cashew cheese"],
        "soy sauce": ["coconut aminos", "tamari", "liquid aminos"],
        "chicken": ["tofu", "tempeh", "seitan", "jackfruit"],
        "beef": ["mushrooms", "lentils", "black beans", "textured vegetable protein"],
    }
    
    ingredient_lower = ingredient.lower()
    
    for key, subs in substitutes.items():
        if key in ingredient_lower or ingredient_lower in key:
            output = f"🔄 Substitutes for {ingredient.title()}:\n\n"
            for sub in subs:
                output += f"  → {sub}\n"
            return output
    
    return f"No substitutes found for '{ingredient}'. Try: butter, milk, egg, flour, sugar, cream, cheese, or meat alternatives."


# ============================================================================
# Main Application
# ============================================================================

async def main():
    """Main entry point for the Cooking AI Agent."""
    
    # Get configuration from environment variables
    project_endpoint = os.getenv(
        "AZURE_AI_FOUNDRY_PROJECT_ENDPOINT",
        "https://<your-endpoint>.services.ai.azure.com/api/projects/<your-project>"
    )
    model_deployment = os.getenv("MODEL_DEPLOYMENT_NAME", "gpt-4o")
    
    print("=" * 60)
    print("🍳 Welcome to the Cooking AI Agent!")
    print("=" * 60)
    print()
    print("I can help you with:")
    print("  • 🔍 Searching for recipes by ingredients or cuisine")
    print("  • 📖 Getting detailed recipe instructions")
    print("  • 🥘 Extracting ingredients from recipe descriptions")
    print("  • 🔄 Finding ingredient substitutes")
    print()
    print("Type 'quit' or 'exit' to end the conversation.")
    print("-" * 60)
    print()
    
    # Define the agent's instructions
    agent_instructions = """You are a friendly and knowledgeable cooking assistant AI.
    
Your capabilities include:
1. Searching for recipes based on ingredients, cuisine type, or dish names
2. Providing detailed recipe information with ingredients and step-by-step instructions
3. Extracting ingredients from recipe descriptions
4. Suggesting ingredient substitutes for dietary restrictions or availability

When users ask about cooking or recipes:
- Use the search_recipes tool to find recipes
- Use get_recipe_details to provide full recipe information
- Use extract_ingredients when users provide recipe text
- Use suggest_substitutes when users need alternatives

Be helpful, encouraging, and provide cooking tips when appropriate.
Format your responses clearly and use emojis to make them engaging.
"""
    
    try:
        async with ChatAgent(
            chat_client=AzureAIAgentClient(
                project_endpoint=project_endpoint,
                model_deployment_name=model_deployment,
                async_credential=DefaultAzureCredential(),
                agent_name="CookingAgent",
            ),
            instructions=agent_instructions,
            tools=[
                search_recipes,
                get_recipe_details,
                extract_ingredients,
                suggest_substitutes,
            ],
        ) as agent:
            # Create a thread for multi-turn conversation
            thread = agent.get_new_thread()
            
            while True:
                try:
                    user_input = input("You: ").strip()
                except EOFError:
                    break
                
                if not user_input:
                    continue
                
                if user_input.lower() in ["quit", "exit", "bye", "goodbye"]:
                    print("\n👨‍🍳 Happy cooking! Goodbye!\n")
                    break
                
                # Stream the response
                print("\n🤖 Chef AI: ", end="", flush=True)
                async for chunk in agent.run_stream(user_input, thread=thread):
                    if chunk.text:
                        print(chunk.text, end="", flush=True)
                print("\n")
                
    except Exception as e:
        print(f"\n❌ Error: {e}")
        print("\nMake sure you have:")
        print("  1. Set AZURE_AI_FOUNDRY_PROJECT_ENDPOINT environment variable")
        print("  2. Set MODEL_DEPLOYMENT_NAME environment variable (default: gpt-4o)")
        print("  3. Logged in with Azure CLI: az login")
        print("  4. Installed dependencies: pip install agent-framework-azure-ai --pre")
        raise


if __name__ == "__main__":
    asyncio.run(main())
