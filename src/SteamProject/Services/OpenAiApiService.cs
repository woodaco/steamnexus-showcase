using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;

namespace SteamProject.Services
{
    public class OpenAiApiService : IOpenAiApiService
    {
        private readonly OpenAIService _openAiService;

        // gpt-4o-mini is the cheapest and most efficient model
        private const string Model = "gpt-4o-mini";

        public OpenAiApiService(string token)
        {
            _openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = token  
            });
        }

        public async Task<string> SummarizeNewsShortAsync(string text)
        {
            var completionResult = await _openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>
                {
                    ChatMessage.FromSystem(
                        "You are a helpful assistant meant to summarize mostly video game news and patch notes in english in a max of two sentences," +
                        "if for some reason the news doesn't make any sense or there is no news for this specific game, " +
                        "explain that it didn't make sense something creative to impress a user like telling a joke"),
                    ChatMessage.FromUser(text),
                },
                Model = Model
            });
            if (completionResult.Successful)
            {
                return completionResult.Choices.First().Message.Content;
            }

            return null;
        }

        public async Task<string> SummarizeNewsLongAsync(string text)
        {
            var completionResult = await _openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>
                {
                    ChatMessage.FromSystem(
                        "You are a helpful assistant meant to summarize inputs in english from the user coming off of steam news api in to save users time" +
                        "If you see patch notes try to list it out in a neat table and as graphically as you can, Using some emojis if appropriate" +
                        "and if it doesn't make sense try to impress the reader with something to do with video games"),
                    ChatMessage.FromUser(text),
                },
                Model = Model
            });
            if (completionResult.Successful)
            {
                return completionResult.Choices.First().Message.Content;
            }

            return null;
        }
    }
}
