using Slack.NetStandard;
using Slack.NetStandard.Messages;
using Slack.NetStandard.WebApi.Conversations;
using SlackMessageSaver.Helpers;
using System.Configuration;
using System.Globalization;
using System.IO.Compression;
using System.Text;

class Program
{
    static string apiToken;
    static string basePath;
    static string dumpPath;
    static async Task Main(string[] args)
    {
        #region Settings
        apiToken = ConfigurationManager.AppSettings.Get("apiToken");
        basePath = ConfigurationManager.AppSettings.Get("resultPath");
        dumpPath = Path.Combine(basePath, FileUtil.GenerateFolderName());
        if (!Directory.Exists(ConfigurationManager.AppSettings.Get("resultPath")))
            throw new DirectoryNotFoundException($"Current path does not exist: '{dumpPath}'");
        #endregion
        #region infrastructure
        var client = new SlackWebApiClient(apiToken);
        client.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
        Console.WriteLine("Get a list of users...");
        var users1 = await client.Usergroups.List();
        var users = await client.Users.List();
        if (users.Error != null)
        {
            Console.WriteLine("Error retrieving user list: " + users.Error);
            return;
        }

        Console.WriteLine("Get a list of private chats...");
        var imChannels = await client.Conversations.List(new ConversationListRequest { Types = "im" });
        if (imChannels.Error != null)
        {
            Console.WriteLine("Error retrieving the list of private chats: " + imChannels.Error);
            return;
        }

        foreach (var imChannel in imChannels.Channels)
        {
            var userId = imChannel.OtherFields["user"].ToString();
            var user = users.Members.FirstOrDefault(u => u.ID == userId);
            if (user == null)
            {
                Console.WriteLine($"I couldn't find a user with an ID {userId}");
                continue;
            }

            var userName = user.Name;
            var realName = user.RealName;
            Console.WriteLine($"Download messages and files for user {userName}...");

            var messages = await GetAllMessagesFromChannel(client, imChannel.ID);
            var messagesHtml = new StringBuilder();
            string messageFolderPath = null;
            foreach (var message in messages)
            {

                string attachedFiles = "";
                var messageDate = DateTimeOffset.FromUnixTimeSeconds((long)double.Parse(message.Timestamp.RawValue, CultureInfo.InvariantCulture)).DateTime;

                messageFolderPath = Path.Combine(dumpPath, $"{realName}(@{userName})");
                Directory.CreateDirectory(messageFolderPath);

                if (message.Files != null)
                {
                    foreach (var file in message.Files)
                    {
                        if (file.UrlPrivate is null)
                            continue;


                        var fileFolderPath = Path.Combine(messageFolderPath, "files");
                        var fileName = FileUtil.GetNewFileNameIfExists(Path.Combine(fileFolderPath, file.Name));
                        if (!Directory.Exists(fileFolderPath))
                            Directory.CreateDirectory(fileFolderPath);
                        var filePath = Path.Combine(fileFolderPath, fileName);
                        attachedFiles += $"<a href=\".\\files\\{fileName}\">{fileName}</a>";
                        await DownloadFile(client, file.UrlPrivateDownload, filePath);
                    }
                }

                string messageHtml = $@"
                <div class=""message"">
                    <span class=""timestamp"">[{messageDate}]</span>
                    <span class=""username"">{message.User}:</span>
                    <span class=""text"">{(String.IsNullOrEmpty(message.Text) ? null : $"{message.Text.Replace("<", "&lt;").Replace(">", "&gt;")} ")}</span>
                    <span class=""files"">{attachedFiles}</span>
                </div>";
                users.Members.ToList().ForEach(u => messageHtml = messageHtml.Replace(u.ID, u.RealName));
                messagesHtml.Insert(0, messageHtml);


                //var messageFilePath = Path.Combine(messageFolderPath, "messages.txt");
                //var appendedText = $"[{messageDate}] {message.User}: {(String.IsNullOrEmpty(message.Text) ? null : $"{message.Text} ")}{attachedFiles}\r\n";
                //users.Members.ToList().ForEach(u => appendedText = appendedText.Replace(u.ID, u.RealName));
                //await File.AppendAllTextAsync(messageFilePath, appendedText);
            }
            if (messageFolderPath is null)
                continue;
            string messagesHtmlPath = Path.Combine(messageFolderPath, "messages.html");
            string formattedHtml = $@"
                <!DOCTYPE html>
                <html lang=""en"">
                <head>
                    <meta charset=""UTF-8"">
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                    <style>
                        body {{
                            font-family: Arial, sans-serif;
                            line-height: 1.6;
                        }}
                        .message {{
                            margin-bottom: 10px;
                        }}
                        .timestamp {{
                            font-size: 0.8em;
                            color: #888;
                        }}
                        .username {{
                            font-weight: bold;
                        }}
                        .text {{
                            display: inline;
                        }}
                    </style>
                </head>
                <body>
                {messagesHtml}
                </body>
                </html>
                ";
            await File.WriteAllTextAsync(messagesHtmlPath, formattedHtml);
        }
        #endregion
        #region saving
        Console.WriteLine("Data saving...");
        ZipFile.CreateFromDirectory(dumpPath, dumpPath + ".zip", CompressionLevel.Optimal, includeBaseDirectory: false);
        Directory.Delete(dumpPath, true);
        #endregion
        Console.WriteLine("Program finished.");
    }
    static async Task DownloadFile(SlackWebApiClient client, string fileUrl, string localPath)
    {
        var response = await client.Client.GetAsync(fileUrl);
        response.EnsureSuccessStatusCode();
        await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fileStream);
    }
    static async Task<Message[]> GetAllMessagesFromChannel(SlackWebApiClient client, string channelId)
    {
        var messages = new List<Message>();
        string cursor = null;

        do
        {

            var response = await client.Conversations.History(new ConversationHistoryRequest { Channel = channelId, Cursor = cursor });
            //Console.WriteLine("Полученный ответ: " + JsonConvert.SerializeObject(response));

            if (response.Error != null)
            {
                Console.WriteLine("Error receiving messages: " + response.Error);
                break;
            }

            messages.AddRange(response.Messages);
            cursor = response.ResponseMetadata?.NextCursor;

        } while (!string.IsNullOrEmpty(cursor));

        return messages.ToArray();
    }
}
