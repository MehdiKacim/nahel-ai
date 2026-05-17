namespace Nahel.Engine.OVGenAI;

public sealed class OVGenAIOptions
{
    public string EngineId { get; set; } = "ovgenai";
    public string DisplayName { get; set; } = "OVGenAI";
    public string Engine { get; set; } = "ov_genai"; // ov_genai | optimum | openvino
    public string ModelName { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public string Device { get; set; } = "CPU";
    public int Port { get; set; } = 8100;
}
