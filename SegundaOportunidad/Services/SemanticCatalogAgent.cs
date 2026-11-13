using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;

namespace SegundaOportunidad.Services;

// Solo se expone una función de lectura. Se conserva el transporte seguro de Gemini.
public sealed class SemanticCatalogAgent
{
    private readonly Kernel kernel;

    public SemanticCatalogAgent(ChatCatalogService catalog, AiSearchService gemini)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(new GeminiCompletionAdapter(gemini));
        kernel = builder.Build();
        kernel.Plugins.AddFromFunctions("Catalogo", [
            KernelFunctionFactory.CreateFromMethod(
                async (string message, bool useIndex, CancellationToken cancellationToken) =>
                    await catalog.SearchAsync(message, cancellationToken, useIndex),
                "Buscar", "Consulta publicaciones autorizadas con filtros de categoría y presupuesto.")
        ]);
    }

    internal Task<CatalogResult?> SearchAsync(string message, CancellationToken ct, bool useIndex = true) =>
        kernel.InvokeAsync<CatalogResult>("Catalogo", "Buscar",
            new KernelArguments { ["message"] = message, ["useIndex"] = useIndex }, ct);

    public async Task<string> CompleteAsync(string systemPrompt, string message, CancellationToken ct)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(message);
        var completion = kernel.GetRequiredService<IChatCompletionService>();
        var answer = await completion.GetChatMessageContentAsync(history, kernel: kernel, cancellationToken: ct);
        return answer.Content ?? "No se obtuvo una respuesta del asistente.";
    }

    private sealed class GeminiCompletionAdapter(AiSearchService gemini) : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; } =
            new Dictionary<string, object?> { ["Provider"] = "Gemini", ["Transport"] = "AiSearchService" };

        public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            var system = string.Join("\n", chatHistory.Where(m => m.Role == AuthorRole.System).Select(m => m.Content));
            var message = string.Join("\n", chatHistory.Where(m => m.Role != AuthorRole.System).Select(m => m.Content));
            var response = await gemini.GetCompletionAsync(system, message, cancellationToken);
            return [new ChatMessageContent(AuthorRole.Assistant, response)];
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var messages = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, messages[0].Content);
        }
    }
}
