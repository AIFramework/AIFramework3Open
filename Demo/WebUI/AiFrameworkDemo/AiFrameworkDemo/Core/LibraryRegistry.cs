namespace AiFrameworkDemo.Core;

public static class LibraryRegistry
{
    private static readonly List<ILibraryModule> _modules = [];

    public static IReadOnlyList<ILibraryModule> Modules => _modules;

    public static void Register(ILibraryModule module) => _modules.Add(module);

    public static ILibraryModule? Get(string libId) =>
        _modules.FirstOrDefault(m => m.Id == libId);

    public static AlgoDef? FindAlgo(string libId, string algoKey)
    {
        var mod = Get(libId);
        if (mod is null) return null;
        foreach (var cat in mod.Categories)
            foreach (var algo in cat.Algorithms)
                if (algo.Key == algoKey) return algo;
        return null;
    }

    public static CategoryDef? FindCategory(string libId, string catId)
    {
        var mod = Get(libId);
        return mod?.Categories.FirstOrDefault(c => c.Id == catId);
    }

    static LibraryRegistry()
    {
        Register(new AiFrameworkDemo.Modules.Ai.AiModule());              // AI (ядро)
        Register(new AiFrameworkDemo.Modules.Algorithms.AlgorithmsModule()); // AI.Algorithms
        Register(new AiFrameworkDemo.Modules.Charts.ChartsModule());         // AI.Charts
        Register(new AiFrameworkDemo.Modules.ClassicMath.ClassicMathModule()); // AI.ClassicMath
        Register(new AiFrameworkDemo.Modules.ComputerVision.ComputerVisionModule()); // AI.ComputerVision
        Register(new AiFrameworkDemo.Modules.ControlSystems.ControlSystemsModule()); // AI.ControlSystems
        Register(new AiFrameworkDemo.Modules.DataPrepaire.DataPrepModule()); // AI.DataPrepaire
        Register(new AiFrameworkDemo.Modules.DSP.DspModule());               // AI.DSP
        Register(new AiFrameworkDemo.Modules.Economics.EconomicsModule());    // AI.Economics
        Register(new AiFrameworkDemo.Modules.Faiss.FaissModule());           // AI.Faiss
        Register(new AiFrameworkDemo.Modules.Fuzzy.FuzzyModule());           // AI.Fuzzy
        Register(new AiFrameworkDemo.Modules.Geometry.GeometryModule());     // AI.Geometry
        Register(new AiFrameworkDemo.Modules.LLM.LlmModule());              // AI.LLM
        Register(new AiFrameworkDemo.Modules.Microwave.MicrowaveModule()); // AI.Microwave
        Register(new AiFrameworkDemo.Modules.ML.MlModule());                 // AI.ML
        Register(new AiFrameworkDemo.Modules.NeuralNetworks.NeuralNetworksModule()); // AI.NeuralNetworks
        Register(new AiFrameworkDemo.Modules.NLP.NlpModule());               // AI.NLP
        Register(new AiFrameworkDemo.Modules.ONNX.OnnxModule());             // AI.ONNX
        Register(new AiFrameworkDemo.Modules.SignalLabs.SignalLabsModule());  // AI.SignalLabs
        Register(new AiFrameworkDemo.Modules.Agents.AgentsModule());           // AI.LLM.Agents
    }
}
