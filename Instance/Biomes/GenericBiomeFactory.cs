using System.Collections.Generic;
using System.Linq;

public class GenericBiomeFactory : IBiomeFactory
{
    private DensityMapOptions baseOptions;
    private Dictionary<string, BiomeOptions> biomeOptions;

    public GenericBiomeFactory(DensityMapOptions baseOptions, List<BiomeOptions> biomeOptions)
    {
        this.baseOptions = baseOptions;
        this.biomeOptions = biomeOptions.ToDictionary(r => r.Name);
    }

    public IBiome CreateInstance(string biomeName)
    {
        switch (biomeName)
        {
            case "DeepOcean":
                return new DeepOceanBiome(biomeOptions["DeepOcean"], baseOptions);
            case "Ocean":
                return new OceanBiome(biomeOptions["Ocean"], baseOptions);
            case "Beach":
                return new BeachBiome(biomeOptions["Beach"], baseOptions);
            case "Plains":
                return new PlainsBiome(biomeOptions["Plains"], baseOptions);
            case "SmallHills":
                return new SmallHillsBiome(biomeOptions["SmallHills"], baseOptions);
        }

        throw new System.NotSupportedException(biomeName);
    }
}