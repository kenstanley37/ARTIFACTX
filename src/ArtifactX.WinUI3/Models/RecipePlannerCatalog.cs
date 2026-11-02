using ArtifactX.Core.Recipes;
using ArtifactX.WinUI3.Services;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.Models;

internal sealed record RecipePlannerCatalog(IReadOnlyList<RecipeInfo> Recipes,
    IReadOnlyDictionary<string, string> Names, IReadOnlyDictionary<string, string> Sources,
    FoodIngestorTables? Food, ExtractionWorldTables? Extraction = null);
