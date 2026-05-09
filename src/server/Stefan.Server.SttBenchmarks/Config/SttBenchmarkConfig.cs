using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;

namespace Stefan.Server.SttBenchmarks.Config;

public class SttBenchmarkConfig : ManualConfig
{
    public SttBenchmarkConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
    }
}
