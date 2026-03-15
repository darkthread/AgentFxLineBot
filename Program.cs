using Azure.AI.OpenAI;
using System.ClientModel;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using isRock.LineBot;
using System.Collections.Concurrent;
using AgentFxLineBot;
using OpenAI.Chat;
using MSChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AngleSharp;

var builder = WebApplication.CreateBuilder(args);

const string modelName = "gpt-5.2-chat";
SessionState.ModelName = modelName;
SessionState.ModelInTokenRate = 54.84m / 1_000_000;
SessionState.ModelOutTokenRate = 438.66m / 1_000_000;

var chanAccToken = builder.Configuration["LineBotToken"] ?? throw new Exception("No LineBotToken setting");

var azureChatClient = new AzureOpenAIClient(
        new Uri(builder.Configuration["AoaiEndPoint"] ?? throw new Exception("No AoaiEndPoint setting")),
        new ApiKeyCredential(builder.Configuration["AoaiApiKey"] ?? throw new Exception("No AoaiApiKey setting"))
    ).GetChatClient(modelName);

var agent = azureChatClient
    .AsAIAgent(
        instructions:
            """
            你是 LINE 聊天助理，負責完成以下任務：

            - 若使用者提供 URL，依其指示處理該網頁內容
            - 若使用者提供圖片，依其指示分析圖片內容

            ## 執行說明

            - 使用者除 URL 及圖片外需說明需求，請先確認需求，再進行分析
            - 使用 ReadWebPage 工具取得網頁內容
            - 不要使用 Emoji 與 Markdown
            """,
        tools: [
            AIFunctionFactory.Create(ReadWebPageContent)
        ]
    );

string helpMsg =
"""
MS Agent Framework 版 LINE 聊天機器人 PoC
====
使用 gpt-5.2-chat，支援讀取網頁與圖片解析

## 指令
/help 顯示此說明
/clear 清除對話記憶
""";

var app = builder.Build();

var bot = new isRock.LineBot.Bot(chanAccToken);

app.MapGet("/", () => "Hello World!");

var sessionPool = new ConcurrentDictionary<string, SessionState>();

app.MapPost("/MsgCallback", async (HttpContext context) =>
{
    var baseUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
    var body = context.Request.Body;
    using var reader = new StreamReader(body);
    var json = await reader.ReadToEndAsync();
    var msg = Utility.Parsing(json);
    foreach (var evt in msg.events)
    {
        if (evt.type == "message")
        {
            if (evt.source.type != "user")
            {
                // 不支援群組聊天室，僅支援與單人對話
                continue;
            }
            var lineUserId = evt.source.userId;
            var sessionState = sessionPool.GetOrAdd(lineUserId, _ => new SessionState(lineUserId, agent.CreateSessionAsync().Result));
            if (evt.message.type == "image")
            {
                var image = await AgentFxLineBot.LineBotUtils.GetUserUploadedContentAsync(evt.message.id, chanAccToken);
                var mediaType = image.MimeType;
                var data = new DataContent(image.Content, mediaType);
                data.AdditionalProperties = new() { ["name"] = (object?)image.Id + "." + mediaType.Split('/').Last() };
                sessionState.Images.Add(data);
            }
            else if (evt.message.type == "text")
            {
                var text = evt.message.text;
                switch (text)
                {
                    case "/help":
                        bot.ReplyMessage(evt.replyToken, helpMsg);
                        break;
                    case "/clear":
                        sessionState.Reset(agent.CreateSessionAsync().Result);
                        bot.ReplyMessage(evt.replyToken, "已重設交談階段");
                        break;
                    default:
                        sessionState.LogInput(text);
                        string outMsg;
                        try
                        {
                            var chatMsg = new MSChatMessage(ChatRole.User, text);
                            if (sessionState.Images.Any())
                            {
                                chatMsg = new MSChatMessage(ChatRole.User, new List<AIContent>() { new TextContent(text) }.Concat(sessionState.Images).ToList());
                                sessionState.Images.Clear();
                            }
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            var res = await agent.RunAsync(chatMsg, sessionState.Session);
                            sw.Stop();
                            outMsg = res.Text;
                            var inTokens = res.Usage?.InputTokenCount ?? 0;
                            var outTokens = res.Usage?.OutputTokenCount ?? 0;
                            sessionState.LogOutput(outMsg);
                            sessionState.AddTokens(inTokens, outTokens);
                            outMsg += $"\n # 耗時 {sw.Elapsed.TotalSeconds:n1}s, IN {inTokens:n0} / OUT {outTokens:n0} Tokens, {sessionState.TokenUsage}";
                            var resonTokens = res.Usage?.ReasoningTokenCount ?? 0;
                            // outMsg += resonTokens > 0 ? $"\n # 推理：{resonTokens:n0} Tokens" : "";
                        }
                        catch (Exception ex)
                        {
                            sessionState.LogOutput("Error: " + ex.Message);
                            outMsg = $"發生錯誤：{ex.Message}";
                        }
                        bot.ReplyMessage(evt.replyToken, outMsg);
                        break;
                }
            }
        }
    }
    await context.Response.WriteAsync("OK");
});
app.UsePathBase("/line-bot");
app.Run();

[Description("輸入 URL 讀取網頁 HTML 內容")]
static async Task<string> ReadWebPageContent([Description("網頁 URL")] string url)
{
    Console.WriteLine($"讀取網頁內容: {url}");
    try
    {
        var config = Configuration.Default.WithDefaultLoader();
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(url);
        return document?.Body?.TextContent ?? string.Empty;
    }
    catch (Exception ex)
    {
        return $"無法讀取網頁內容: {ex.Message}";
    }
}