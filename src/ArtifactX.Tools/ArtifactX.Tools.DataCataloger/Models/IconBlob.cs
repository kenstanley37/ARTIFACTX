namespace ArtifactX.Tools.DataCataloger.Models;

/// <summary>
/// One row per UNIQUE source texture (deduplicated by SourceDdsPath). Many CatalogItems
/// commonly share the same icon (generic resource icons, category placeholders, etc.), so
/// this is normalized out rather than storing the same PNG bytes on every IconAsset row -
/// keeps the database from re-storing identical bytes thousands of times over.
/// </summary>
public class IconBlob
{
    public int Id { get; set; }

    /// <summary>Original in-game texture path, e.g. "TEXTURES/UI/FRONTEND/ICONS/U4PRODUCTS/PRODUCT.CASING.DDS".</summary>
    public string SourceDdsPath { get; set; } = "";

    /// <summary>Converted, resized PNG bytes - ready to hand directly to a BitmapImage/Image control.</summary>
    public byte[] PngData { get; set; } = Array.Empty<byte>();

    public List<IconAsset> Assets { get; set; } = new();
}