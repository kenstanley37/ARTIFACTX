using libMBIN.NMS.GameComponents;
public static class Diff
{
    public static void Run()
    {
        var c = (GcDifficultyConfig)Paks.Decode("METADATA/GAMESTATE/DIFFICULTYCONFIG.MBIN");
        var names = Enum.GetNames<GcItemShopAvailabilityDifficultyOption.ItemShopAvailabilityDifficultyEnum>();
        for (int i = 0; i < c.ItemShopAvailabilityOptionData.Length; i++)
            Console.WriteLine($"{names[i]}: {string.Join(",", c.ItemShopAvailabilityOptionData[i].NeverSoldItems.Select(x => x.Value))}");
    }
}
